using Emberwake.Core;

namespace Emberwake.Backend;

public sealed class WorkerPoll { public string worker; }
public sealed class WorkerJobs { public FusionAssignment[] assignments; }
public sealed class WorkerReport { public string worker, matchId, status; public FusionOutcome outcome; }

public sealed partial class FusionMatches
{
    readonly bool Remote=Environment.GetEnvironmentVariable("EMBERWAKE_REMOTE_WORKER")=="1";
    DateTime workerSeen;
    string workerId;
    // One trusted worker per coordinator. Do not reassign a running match: that could
    // produce two authorities. A missed lease aborts the match without rewards.
    public WorkerJobs WorkerPoll(WorkerPoll request)
    {
        lock(gate)
        {
            if(!Remote)throw new ApiError("Remote workers are disabled",409);
            if(string.IsNullOrEmpty(request.worker)||request.worker.Length>64||request.worker.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ApiError("Invalid worker ID");
            if(workerId!=null&&workerId!=request.worker&&DateTime.UtcNow-workerSeen<TimeSpan.FromSeconds(45))throw new ApiError("Another worker is registered",409);
            workerId=request.worker;workerSeen=DateTime.UtcNow;
            var jobs=rooms.Values.Where(r=>r.State=="allocating"||r.State=="matched").Where(r=>r.Worker==null||r.Worker==workerId).ToArray();
            foreach(var r in jobs){r.Worker=workerId;r.WorkerSeen=workerSeen;}
            return new WorkerJobs{assignments=jobs.Select(r=>r.Assignment).ToArray()};
        }
    }
    public void WorkerReport(WorkerReport report)
    {
        lock(gate)
        {
            if(!Remote||!rooms.TryGetValue(report.matchId??"",out var r)||r.Worker!=report.worker||string.IsNullOrEmpty(report.worker))throw new ApiError("Unknown worker assignment",409);
            if(r.State=="complete"&&report.status=="complete")return;
            if(r.State!="allocating"&&r.State!="matched")throw new ApiError("Assignment has ended",409);
            r.WorkerSeen=DateTime.UtcNow;
            if(report.status=="ready")r.State="matched";
            else if(report.status=="failed")Abort(r,"Battle worker stopped; no trophies were changed.");
            else if(report.status=="complete")
            {
                var o=report.outcome;
                if(o==null||o.matchId!=r.Assignment.matchId||o.winner< -1||o.winner>1||o.tick<0||o.tick>20000)throw new ApiError("Invalid battle result");
                var path=Path.Combine(r.Directory,"result.json");
                File.WriteAllText(path+".tmp",Wire.Encode(o));File.Move(path+".tmp",path,true);
                Settle(r.Directory,r.Assignment);r.State="complete";r.Finished=DateTime.UtcNow;
            }
            else throw new ApiError("Unknown worker status");
        }
    }
}
