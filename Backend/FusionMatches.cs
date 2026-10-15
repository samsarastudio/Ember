using System.Diagnostics;
using Emberwake.Core;

namespace Emberwake.Backend;

// One bounded allocator per API instance. Run exactly one replica against this database.
// Battle authority lives in headless Unity processes, never in either player's client.
public sealed partial class FusionMatches : BackgroundService
{
    sealed class Entry { public string Id; public int Trophies; public DateTime Queued, Seen; }
    sealed class Room
    {
        public FusionAssignment Assignment; public string Directory; public Process Process;
        public string Worker; public DateTime WorkerSeen; public DateTime Created=DateTime.UtcNow, Finished; public string State="allocating", Error;
    }
    readonly object gate=new();
    readonly Dictionary<string,Entry> queue=new();
    readonly Dictionary<string,Room> rooms=new();
    readonly Store store;
    readonly ILogger<FusionMatches> log;
    readonly string executable=Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_EXECUTABLE");
    readonly string root=Path.GetFullPath(Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_DATA")??"data/matches");
    public string Region { get; }=Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_REGION")??"us";
    readonly int capacity=int.TryParse(Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_CAPACITY"),out var n)?Math.Clamp(n,1,100):4;
    readonly bool aiEnabled=Environment.GetEnvironmentVariable("EMBERWAKE_AI_ENABLED")!="0";
    readonly int aiWait=int.TryParse(Environment.GetEnvironmentVariable("EMBERWAKE_AI_WAIT_SECONDS"),out var wait)?Math.Clamp(wait,1,120):15;
    public bool Enabled=>Remote ? DateTime.UtcNow-workerSeen<TimeSpan.FromSeconds(20) : !string.IsNullOrEmpty(executable)&&File.Exists(executable);
    public FusionMatches(Store store,ILogger<FusionMatches> log)
    {
        this.store=store;this.log=log;
        store.EnsureAICaptains();
        if(Region!="us"&&Region!="usw"&&Region!="cae")throw new InvalidOperationException("Invalid EMBERWAKE_MATCH_REGION");
        Directory.CreateDirectory(root);
    }
    static void Validate(Profile p,Catalog c)
    {
        if(p.deck==null||p.deck.Length!=8||p.deck.Distinct().Count()!=8||p.deck.Any(id=>p.Card(id)==null||c.Get(id)==null||!c.Get(id).enabled))
            throw new ApiError("Fleet contains unavailable cards; edit your deck");
    }
    public FusionQueueReply Join(string id)
    {
        lock(gate)
        {
            if(!Enabled)throw new ApiError("Ranked battle servers are not configured yet.",503);
            var existing=Active(id);if(existing!=null)return Reply(existing,id);
            var p=store.Profile(id);Validate(p,store.Catalog());
            if(!queue.TryGetValue(id,out var entry))queue[id]=new Entry{Id=id,Trophies=p.trophies,Queued=DateTime.UtcNow,Seen=DateTime.UtcNow};
            else entry.Seen=DateTime.UtcNow;
            return new FusionQueueReply{status="waiting",region=Region};
        }
    }
    Room Active(string id)=>rooms.Values.FirstOrDefault(r=>r.Assignment.players.Contains(id)&&(r.State=="allocating"||r.State=="matched"));
    public FusionQueueReply Poll(string id)
    {
        lock(gate)
        {
            var r=Active(id)??rooms.Values.Where(x=>x.Assignment.players.Contains(id)).OrderByDescending(x=>x.Created).FirstOrDefault();
            if(Active(id)!=null)return Reply(r,id);
            if(queue.TryGetValue(id,out var q)){q.Seen=DateTime.UtcNow;return new FusionQueueReply{status="waiting",region=Region};}
            return r!=null?Reply(r,id):new FusionQueueReply{status="idle"};
        }
    }
    public void Cancel(string id){lock(gate){queue.Remove(id);var r=Active(id);if(r?.State=="allocating")Abort(r,"Search cancelled before the battle server was ready.");}}
    public bool HasActive(string id){lock(gate)return Active(id)!=null;}
    static FusionQueueReply Reply(Room r,string id)
    {
        int team=Array.IndexOf(r.Assignment.players,id);
        return new FusionQueueReply{status=r.State,matchId=r.Assignment.matchId,region=r.Assignment.region,
            room=r.State=="matched"?r.Assignment.room:null,ticket=r.State=="matched"?r.Assignment.tickets[team]:null,
            team=team,opponent=r.Assignment.names[1-team]+(r.Assignment.aiOpponent?" · AI":""),catalog=r.State=="matched"?r.Assignment.catalog:null,error=r.Error};
    }
    void Allocate(Entry a,Entry b)
    {
        var pa=store.Profile(a.Id);var pb=b==null?store.MatchAICaptain(pa,rooms.Values.Where(r=>r.State=="allocating"||r.State=="matched").SelectMany(r=>r.Assignment.players)):store.Profile(b.Id);
        if(pb==null)return;
        queue.Remove(a.Id);if(b!=null)queue.Remove(b.Id);
        var catalog=store.Catalog();Validate(pa,catalog);Validate(pb,catalog);
        string id=Guid.NewGuid().ToString("N"),dir=Path.Combine(root,id);Directory.CreateDirectory(dir);
        var assignment=new FusionAssignment{matchId=id,room=id,region=Region,aiOpponent=b==null,
            players=new[]{a.Id,pb.id},names=new[]{pa.name,pb.name},
            tickets=new[]{Wire.Secret(),b==null?"":Wire.Secret()},deckA=pa.deck,deckB=pb.deck,catalog=catalog,seed=Random.Shared.Next()};
        var room=new Room{Assignment=assignment,Directory=dir};rooms[id]=room;
        File.WriteAllText(Path.Combine(dir,"assignment.json"),Wire.Encode(assignment)); if(Remote)return;
        try
        {
            var start=new ProcessStartInfo(Path.GetFullPath(executable)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(Path.GetFullPath(executable))};
            foreach(var arg in new[]{"-batchmode","-nographics","-fusion-server","-fusion-room",id,"-fusion-region",Region,"-logFile",Path.Combine(dir,"server.log")})start.ArgumentList.Add(arg);
            start.Environment["EMBERWAKE_ASSIGNMENT"]=Path.Combine(dir,"assignment.json");
            room.Process=Process.Start(start)??throw new InvalidOperationException("Match process did not start");
        }
        catch(Exception e){log.LogError(e,"Cannot allocate match {MatchId}",id);Abort(room,"Battle server unavailable. Please search again.");}
    }
    void Abort(Room r,string error)
    {
        if(r.Process!=null&&!r.Process.HasExited)r.Process.Kill(true);
        r.State="failed";r.Error=error;r.Finished=DateTime.UtcNow;
    }
    void Settle(string dir,FusionAssignment assignment)
    {
        var outcome=Wire.Decode<FusionOutcome>(File.ReadAllText(Path.Combine(dir,"result.json")));
        if(outcome.matchId!=assignment.matchId||outcome.winner< -1||outcome.winner>1||outcome.tick<0||outcome.tick>20000)throw new InvalidDataException("Invalid authoritative outcome");
        if(assignment.aiOpponent)store.Record(assignment.matchId,assignment.players[0],assignment.names[1]+" · AI",outcome.winner==-1?0:outcome.winner==0?1:-1);
        else store.RecordMatch(assignment.matchId,assignment.players,assignment.names,outcome.winner);
        File.WriteAllText(Path.Combine(dir,"settled"),DateTime.UtcNow.ToString("O"));
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Recover completed outcomes after an API crash. Store.Record's ledger makes retries idempotent.
        foreach(var dir in Directory.GetDirectories(root))
            if(File.Exists(Path.Combine(dir,"result.json"))&&!File.Exists(Path.Combine(dir,"settled")))
                try{Settle(dir,Wire.Decode<FusionAssignment>(File.ReadAllText(Path.Combine(dir,"assignment.json"))));}
                catch(Exception e){log.LogError(e,"Could not recover match {Directory}",dir);}
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while(await timer.WaitForNextTickAsync(stoppingToken))lock(gate)
            {
                foreach(var r in rooms.Values.ToArray())
                {
                    try
                    {
                        if(r.State=="allocating"||r.State=="matched")
                        {
                            if(File.Exists(Path.Combine(r.Directory,"result.json"))){Settle(r.Directory,r.Assignment);r.State="complete";r.Finished=DateTime.UtcNow;}
                            else if(Remote && r.Worker!=null && DateTime.UtcNow-r.WorkerSeen>TimeSpan.FromSeconds(45))Abort(r,"Battle worker disconnected; no trophies were changed."); else if(!Remote && (r.Process==null||r.Process.HasExited))Abort(r,"Battle server stopped; no trophies were changed.");
                            else if(DateTime.UtcNow-r.Created>TimeSpan.FromMinutes(8))Abort(r,"Battle timed out; no trophies were changed.");
                            else if(r.State=="allocating"&&File.Exists(Path.Combine(r.Directory,"ready")))r.State="matched";
                            else if(r.State=="allocating"&&DateTime.UtcNow-r.Created>TimeSpan.FromSeconds(60))Abort(r,"Battle server did not become ready.");
                        }
                        if(r.Finished!=default&&DateTime.UtcNow-r.Finished>TimeSpan.FromMinutes(10)){r.Process?.Dispose();rooms.Remove(r.Assignment.matchId);}
                    }
                    catch(Exception e){log.LogError(e,"Match update failed {MatchId}",r.Assignment.matchId);}
                }
                foreach(var stale in queue.Values.Where(q=>DateTime.UtcNow-q.Seen>TimeSpan.FromSeconds(30)).ToArray())queue.Remove(stale.Id);
                if(!Enabled)continue;
                while(rooms.Values.Count(r=>Remote ? r.State=="allocating"||r.State=="matched" : r.Process!=null&&!r.Process.HasExited)<capacity)
                {
                    var ordered=queue.Values.OrderBy(q=>q.Queued).ToArray();Entry first=null,second=null;
                    foreach(var a in ordered)
                    {
                        var b=ordered.Where(b=>b.Id!=a.Id&&TrophyMatching.Compatible(a.Trophies,b.Trophies,(DateTime.UtcNow-a.Queued).TotalSeconds,(DateTime.UtcNow-b.Queued).TotalSeconds))
                            .OrderBy(b=>Math.Abs((long)a.Trophies-b.Trophies)).ThenBy(b=>b.Queued).FirstOrDefault();
                        if(b!=null){first=a;second=b;break;}
                    }
                    // Exhaust eligible human pairs before offering a server-controlled opponent.
                    if(first==null&&aiEnabled)first=ordered.FirstOrDefault(q=>(DateTime.UtcNow-q.Queued).TotalSeconds>=aiWait);
                    if(first==null)break;
                    int before=queue.Count;
                    try{Allocate(first,second);}catch(Exception e){log.LogWarning(e,"Invalid queued fleet removed");queue.Remove(first.Id);if(second!=null)queue.Remove(second.Id);}
                    if(queue.Count==before)break; // All ten AI captains are already in battles.
                }
            }
        }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){}
        finally{lock(gate)foreach(var r in rooms.Values)if(r.Process!=null&&!r.Process.HasExited)r.Process.Kill(true);}
    }
}
