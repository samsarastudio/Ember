using System.Diagnostics;
using System.Text.Json;
using Emberwake.Core;

namespace Emberwake.Backend;

public static class ServerBenchmark
{
    public static void Run()
    {
        const int matches=4,ticks=900;
        var catalog=Catalog.Default();var deck=new Profile().deck;
        var rooms=Enumerable.Range(0,matches).Select(i=>new MatchSimulation(catalog,deck,deck,i+1)).ToArray();
        foreach(var room in rooms)
            for(int team=0;team<2;team++)for(int n=0;n<48;n++)
                room.Units.Add(new UnitState{id=1000+team*100+n,team=team,card="skiffs",kind="swarm",x=(n%2==0?-7:7)+(n%3)*.15f,z=(team==0?-1:1)*(1.7f+n/12f),hp=10000,maxHp=10000});
        var durations=new List<double>();long bytes=0;int samples=0;
        for(int tick=0;tick<ticks;tick++)
        {
            long begin=Stopwatch.GetTimestamp();
            foreach(var room in rooms)
            {
                room.Step();
                if(tick%3==0)for(int team=0;team<2;team++)
                {
                    var snapshot=room.Snapshot(team);
                    snapshot.events=snapshot.events.Where(e=>e.tick>=snapshot.tick-3).ToArray();
                    // Include JSON encoding cost at the actual 10 Hz snapshot cadence.
                    bytes+=JsonSerializer.SerializeToUtf8Bytes(new{kind="snapshot",snapshot},Wire.Json).Length;samples++;
                }
            }
            if(tick>=30)durations.Add(Stopwatch.GetElapsedTime(begin).TotalMilliseconds);
        }
        durations.Sort();
        Console.WriteLine(JsonSerializer.Serialize(new{mode="simulation-and-serialization-only",matches,shipsPerMatch=96,
            meanTickMs=durations.Average(),p95TickMs=durations[(int)(durations.Count*.95)],maxTickMs=durations[^1],
            tickBudgetMs=1000.0/30,meanSnapshotBytes=bytes/Math.Max(1,samples),
            note="Run on the Pi itself. Excludes TLS, network, SQLite, SMTP and thermal endurance; this is not a verified concurrent-player capacity."},new JsonSerializerOptions{WriteIndented=true}));
    }
}
