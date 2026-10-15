using System.Collections.Concurrent;
using Emberwake.Core;

namespace Emberwake.Backend;

public sealed class MatchRoom
{
    public readonly object Gate=new(); public string Id=Guid.NewGuid().ToString("N"); public Profile[] Players;
    public MatchSimulation Simulation; public DateTime Created=DateTime.UtcNow, Finished; public bool Settled;
    public string PrivateCode; public int Team(string id)=>Array.FindIndex(Players,p=>p.id==id);
    public bool Started, Aborted;
    public readonly CancellationTokenSource[] Connections=new CancellationTokenSource[2];
    public readonly DateTime[] Disconnected={DateTime.UtcNow,DateTime.UtcNow};
}
public sealed class Matches : BackgroundService
{
    sealed class Entry { public DateTime Queued,Seen;public string Code;public int Trophies; }
    readonly Store store; readonly object queueGate=new(); readonly Dictionary<string,Entry> queue=new();
    readonly int capacity=int.TryParse(Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_CAPACITY"),out var limit)?Math.Clamp(limit,1,100):4;
    public readonly ConcurrentDictionary<string,MatchRoom> Rooms=new(); readonly ILogger<Matches> log;
    public Matches(Store store,ILogger<Matches> log){this.store=store;this.log=log;}
    public QueueReply Join(string id,string code="")
    {
        code=(code??"").Trim().ToUpperInvariant();if(code.Length>12||code.Any(c=>!char.IsLetterOrDigit(c)))throw new ApiError("Room code must use up to 12 letters or numbers");
        lock(queueGate){var existing=Rooms.Values.FirstOrDefault(r=>r.Team(id)>=0&&r.Simulation.Winner==-2);if(existing!=null)return Reply(existing,id);
            foreach(var stale in queue.Where(x=>DateTime.UtcNow-x.Value.Seen>TimeSpan.FromSeconds(30)).Select(x=>x.Key).ToArray())queue.Remove(stale);
            var player=store.Profile(id);ValidateDeck(player,store.Catalog());
            if(!queue.TryGetValue(id,out var entry)||entry.Code!=code)queue[id]=entry=new Entry{Queued=DateTime.UtcNow,Seen=DateTime.UtcNow,Code=code,Trophies=player.trophies};
            entry.Seen=DateTime.UtcNow;
            var opponent=queue.Where(x=>x.Key!=id&&x.Value.Code==code&&(code!=""||TrophyMatching.Compatible(entry.Trophies,x.Value.Trophies,(DateTime.UtcNow-entry.Queued).TotalSeconds,(DateTime.UtcNow-x.Value.Queued).TotalSeconds)))
                .OrderBy(x=>Math.Abs((long)entry.Trophies-x.Value.Trophies)).ThenBy(x=>x.Value.Queued).FirstOrDefault();
            if(opponent.Key==null||Rooms.Values.Count(r=>r.Simulation.Winner==-2)>=capacity)return new QueueReply{status="waiting",transport="websocket"};
            queue.Remove(opponent.Key);queue.Remove(id);var a=store.Profile(opponent.Key);var b=store.Profile(id);var c=store.Catalog();
            ValidateDeck(a,c);ValidateDeck(b,c);var room=new MatchRoom{Players=new[]{a,b},PrivateCode=code,Simulation=new MatchSimulation(c,a.deck,b.deck,Random.Shared.Next())};Rooms[room.Id]=room;return Reply(room,id);
        }
    }
    static void ValidateDeck(Profile p,Catalog c){if(p.deck==null||p.deck.Length!=8||p.deck.Distinct().Count()!=8||p.deck.Any(x=>p.Card(x)==null||c.Get(x)==null||!c.Get(x).enabled))throw new ApiError("Fleet contains unavailable cards; edit your deck");}
    public void Cancel(string id){lock(queueGate){queue.Remove(id);}}
    static QueueReply Reply(MatchRoom r,string id){int team=r.Team(id);return new QueueReply{status="matched",matchId=r.Id,team=team,opponent=r.Players[1-team].name,transport="websocket",catalog=r.Simulation.Catalog};}
    public QueueReply Poll(string id){lock(queueGate){var r=Rooms.Values.FirstOrDefault(x=>x.Team(id)>=0&&x.Simulation.Winner==-2);if(r!=null)return Reply(r,id);if(queue.TryGetValue(id,out var q))return Join(id,q.Code);return new QueueReply{status="idle"};}}
    public MatchRoom Get(string match,string player){if(!Rooms.TryGetValue(match,out var room)||room.Team(player)<0)throw new ApiError("Match not found",404);return room;}
    public void RemovePlayer(string id){Cancel(id);foreach(var r in Rooms.Values.Where(r=>r.Team(id)>=0)){lock(r.Gate){if(!r.Settled){int deleted=r.Team(id);r.Simulation.Surrender(deleted);if(r.Started&&string.IsNullOrEmpty(r.PrivateCode))store.Record(r.Id,r.Players[1-deleted].id,r.Players[deleted].name,r.Simulation.Winner==-1?0:r.Simulation.Winner==1-deleted?1:-1);r.Settled=true;r.Finished=DateTime.UtcNow;}}}}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1.0/30));
        var elapsed=System.Diagnostics.Stopwatch.StartNew();double last=0,accumulated=0;
        while(await timer.WaitForNextTickAsync(stoppingToken)){
        double now=elapsed.Elapsed.TotalSeconds;accumulated+=Math.Min(.25,now-last);last=now;int steps=Math.Min(8,(int)(accumulated/MatchSimulation.StepSeconds));accumulated-=steps*MatchSimulation.StepSeconds;
        foreach(var room in Rooms.Values){
            lock(room.Gate){
                if(room.Simulation.Winner==-2){
                    if(!room.Started&&DateTime.UtcNow-room.Created>TimeSpan.FromSeconds(90)){room.Simulation.Surrender(0);room.Aborted=true;room.Settled=true;room.Finished=DateTime.UtcNow;}
                    if(room.Started){
                        for(int team=0;team<2;team++)if(room.Connections[team]==null&&room.Disconnected[team]!=default&&DateTime.UtcNow-room.Disconnected[team]>TimeSpan.FromSeconds(30))room.Simulation.Surrender(team);
                        for(int i=0;i<steps;i++)room.Simulation.Step();
                    }
                }
                else if(!room.Settled){
                    try{if(string.IsNullOrEmpty(room.PrivateCode))store.RecordMatch(room.Id,room.Players.Select(p=>p.id).ToArray(),room.Players.Select(p=>p.name).ToArray(),room.Simulation.Winner);room.Settled=true;room.Finished=DateTime.UtcNow;}
                    catch(Exception e){log.LogError(e,"Could not settle match {MatchId}",room.Id);}
                }
                if(room.Settled&&room.Finished!=default&&DateTime.UtcNow-room.Finished>TimeSpan.FromMinutes(10))Rooms.TryRemove(room.Id,out _);
            }
        }
        }
    }
}

