using Emberwake.Backend;
using Emberwake.Core;
using Microsoft.Extensions.Logging.Abstractions;

static class WorkerChecks
{
    public static async Task Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var keys=new[]{"EMBERWAKE_REMOTE_WORKER","EMBERWAKE_MATCH_DATA","EMBERWAKE_MATCH_REGION","EMBERWAKE_AI_WAIT_SECONDS","EMBERWAKE_AI_ENABLED"};
        var previous=keys.Select(Environment.GetEnvironmentVariable).ToArray();
        string root=Path.Combine(Path.GetTempPath(),"emberwake-worker-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable(keys[0],"1");Environment.SetEnvironmentVariable(keys[1],Path.Combine(root,"matches"));Environment.SetEnvironmentVariable(keys[2],"us");
        Environment.SetEnvironmentVariable(keys[3],"1");Environment.SetEnvironmentVariable(keys[4],"1");
        var store=new Store(Path.Combine(root,"test.db"));
        using var matches=new FusionMatches(store,NullLogger<FusionMatches>.Instance);
        try
        {
            check(!matches.Enabled,"Remote matchmaking unavailable without worker heartbeat");
            reject(()=>matches.WorkerPoll(new WorkerPoll{worker="../invalid"}),"Malformed worker ID rejected");
            matches.WorkerPoll(new WorkerPoll{worker="test-worker"});
            check(matches.Enabled,"Worker heartbeat enables matchmaking");
            reject(()=>matches.WorkerPoll(new WorkerPoll{worker="second-worker"}),"Second worker cannot steal live registration");
            var a=store.Authenticate(new AuthRequest{name="Worker A"});var b=store.Authenticate(new AuthRequest{name="Worker B"});
            matches.Join(a.profile.id);matches.Join(b.profile.id);await matches.StartAsync(CancellationToken.None);
            WorkerJobs jobs=null;
            for(int i=0;i<20;i++){await Task.Delay(150);jobs=matches.WorkerPoll(new WorkerPoll{worker="test-worker"});if(jobs.assignments.Length==1)break;}
            check(jobs.assignments.Length==1,"Coordinator allocates one remote assignment for two captains");
            var assignment=jobs.assignments.Single();
            check(!assignment.aiOpponent,"Queued humans take priority over AI fallback");
            check(matches.Poll(a.profile.id).ticket==null,"Admission ticket withheld until worker is ready");
            reject(()=>matches.WorkerReport(new WorkerReport{worker="wrong-worker",matchId=assignment.matchId,status="ready"}),"Wrong worker cannot report readiness");
            matches.WorkerReport(new WorkerReport{worker="test-worker",matchId=assignment.matchId,status="ready"});
            check(matches.Poll(a.profile.id).ticket==assignment.tickets[0]&&matches.Poll(b.profile.id).ticket==assignment.tickets[1],"Ready clients receive their own admission tickets");
            reject(()=>matches.WorkerReport(new WorkerReport{worker="test-worker",matchId=assignment.matchId,status="complete",outcome=new FusionOutcome{matchId=assignment.matchId,winner=9}}),"Invalid remote outcome rejected");
            var result=new WorkerReport{worker="test-worker",matchId=assignment.matchId,status="complete",outcome=new FusionOutcome{matchId=assignment.matchId,winner=0,tick=1800}};
            matches.WorkerReport(result);matches.WorkerReport(result);
            check(store.Profile(a.profile.id).battles==1&&store.Profile(b.profile.id).battles==1,"Repeated remote result settles both players exactly once");
            check(matches.WorkerPoll(new WorkerPoll{worker="test-worker"}).assignments.Length==0,"Completed match removed from worker jobs");
            var solo=store.Authenticate(new AuthRequest{name="Solo Captain"});
            store.Mutate(solo.profile.id,p=>{p.trophies=75;p.cards[0].level=3;});
            var identities=store.AICaptains().Select(p=>p.id).ToArray();store.EnsureAICaptains();
            check(identities.Length==10&&identities.SequenceEqual(store.AICaptains().Select(p=>p.id)),"Ten AI accounts retain their identities across initialization");
            matches.Join(solo.profile.id);
            check(matches.Poll(solo.profile.id).status=="waiting","Solo player waits for a human before AI fallback");
            for(int i=0;i<30;i++){await Task.Delay(150);jobs=matches.WorkerPoll(new WorkerPoll{worker="test-worker"});if(jobs.assignments.Length==1)break;}
            var bot=jobs.assignments.Single();
            check(bot.aiOpponent&&bot.players[0]==solo.profile.id&&identities.Contains(bot.players[1]),"Lone queued captain receives a persistent AI captain");
            var ai=store.Profile(bot.players[1]);
            check(ai.trophies==75&&ai.cards[0].level==3,"AI captain mirrors player trophies and card levels");
            check(store.MatchAICaptain(store.Profile(solo.profile.id),identities)==null,"Busy AI captains cannot be reused in simultaneous matches");
            check(bot.tickets[1]==""&&bot.deckB.SequenceEqual(bot.deckA),"AI uses a legal mirrored fleet and has no client admission ticket");
            matches.WorkerReport(new WorkerReport{worker="test-worker",matchId=bot.matchId,status="ready"});
            check(matches.Poll(solo.profile.id).status=="matched","Single human can join ready AI battle");
            var botResult=new WorkerReport{worker="test-worker",matchId=bot.matchId,status="complete",outcome=new FusionOutcome{matchId=bot.matchId,winner=0,tick=1801}};
            matches.WorkerReport(botResult);matches.WorkerReport(botResult);
            var profile=store.Profile(solo.profile.id);
            check(profile.battles==1&&profile.wins==1&&profile.history[0].opponent==bot.names[1]+" · AI","AI match settles the real captain exactly once with labelled history");
            check(store.Leaderboard().Length==3,"AI accounts do not occupy the human leaderboard");
        }
        finally{await matches.StopAsync(CancellationToken.None);for(int i=0;i<keys.Length;i++)Environment.SetEnvironmentVariable(keys[i],previous[i]);}
    }
}
