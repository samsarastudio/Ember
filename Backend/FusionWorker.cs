using System.Diagnostics;
using System.Text;
using Emberwake.Core;

namespace Emberwake.Backend;

// Outbound HTTPS only; no administrative listener is exposed on the battle host.
public static class FusionWorker
{
    sealed class Job { public Process Process; public string Directory; public DateTime Completed; public DateTime Started=DateTime.UtcNow; }
    public static async Task Run()
    {
        var origin=new Uri(Environment.GetEnvironmentVariable("EMBERWAKE_COORDINATOR")??throw new Exception("Set EMBERWAKE_COORDINATOR"));
        if(origin.Scheme!="https"&&!(origin.Scheme=="http"&&origin.IsLoopback))throw new Exception("Coordinator requires HTTPS except localhost tests");
        string key=Environment.GetEnvironmentVariable("EMBERWAKE_WORKER_KEY")??"";
        if(key.Length<32)throw new Exception("Worker key must be at least 32 characters");
        string executable=Path.GetFullPath(Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_EXECUTABLE")??throw new Exception("Set match executable"));
        if(!File.Exists(executable))throw new Exception("Match executable not found");
        string root=Path.GetFullPath(Environment.GetEnvironmentVariable("EMBERWAKE_MATCH_DATA")??"worker-data");Directory.CreateDirectory(root);
        // An exclusive lock prevents two launchers sharing the same work directory.
        using var instance=new FileStream(Path.Combine(root,"worker.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string idPath=Path.Combine(root,"worker-id");if(!File.Exists(idPath))File.WriteAllText(idPath,Guid.NewGuid().ToString("N"));string id=File.ReadAllText(idPath).Trim();
        // Never forward the privileged worker header to a redirect destination.
        using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=origin,Timeout=TimeSpan.FromSeconds(8)};http.DefaultRequestHeaders.Add("X-Worker-Key",key);
        var jobs=new Dictionary<string,Job>();var seen=new HashSet<string>();DateTime connected=DateTime.UtcNow;
        using var stop=new CancellationTokenSource();Console.CancelKeyPress+=(s,e)=>{e.Cancel=true;stop.Cancel();};
        async Task<string> Post(string route,object body){using var response=await http.PostAsync(route,new StringContent(Wire.Encode(body),Encoding.UTF8,"application/json"),stop.Token);response.EnsureSuccessStatusCode();return await response.Content.ReadAsStringAsync(stop.Token);}
        try
        {
            while(!stop.IsCancellationRequested)
            {
                try
                {
                    var reply=Wire.Decode<WorkerJobs>(await Post("/internal/fusion/poll",new WorkerPoll{worker=id}));connected=DateTime.UtcNow;
                    var active=new HashSet<string>(reply.assignments.Select(a=>a.matchId));
                    foreach(var old in jobs.Keys.Where(k=>!active.Contains(k)).ToArray()){var j=jobs[old];if(j.Completed!=default&&DateTime.UtcNow-j.Completed<TimeSpan.FromSeconds(12))continue;if(!j.Process.HasExited)j.Process.Kill(true);j.Process.Dispose();jobs.Remove(old);}
                    foreach(var a in reply.assignments)
                    {
                        if(!Guid.TryParseExact(a.matchId,"N",out _))throw new Exception("Invalid assignment ID");
                        if(!jobs.TryGetValue(a.matchId,out var job))
                        {
                            var dir=Path.Combine(root,a.matchId);
                            // Never restart an interrupted simulation from tick zero.
                            if(seen.Contains(a.matchId)||Directory.Exists(dir)){await Post("/internal/fusion/report",new WorkerReport{worker=id,matchId=a.matchId,status="failed"});continue;}
                            seen.Add(a.matchId);Directory.CreateDirectory(dir);File.WriteAllText(Path.Combine(dir,"assignment.json"),Wire.Encode(a));
                            var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(executable)};
                            foreach(var arg in new[]{"-batchmode","-nographics","-fusion-server","-fusion-room",a.room,"-fusion-region",a.region,"-logFile",Path.Combine(dir,"server.log")})start.ArgumentList.Add(arg);
                            start.Environment["EMBERWAKE_ASSIGNMENT"]=Path.Combine(dir,"assignment.json");
                            try{job=new Job{Process=Process.Start(start),Directory=dir};jobs[a.matchId]=job;Console.WriteLine("Started match "+a.matchId);}
                            catch{await Post("/internal/fusion/report",new WorkerReport{worker=id,matchId=a.matchId,status="failed"});continue;}
                        }
                        var result=Path.Combine(job.Directory,"result.json");
                        string status=File.Exists(result)?"complete":job.Process.HasExited||DateTime.UtcNow-job.Started>TimeSpan.FromMinutes(8)?"failed":File.Exists(Path.Combine(job.Directory,"ready"))?"ready":null;
                        if(status=="complete"&&job.Completed==default)job.Completed=DateTime.UtcNow;
                        if(status!=null)await Post("/internal/fusion/report",new WorkerReport{worker=id,matchId=a.matchId,status=status,outcome=status=="complete"?Wire.Decode<FusionOutcome>(File.ReadAllText(result)):null});
                    }
                }
                catch(OperationCanceledException) when(stop.IsCancellationRequested){break;}
                catch(Exception e){Console.WriteLine("Worker connection/update failed: "+e.GetType().Name);}
                if(DateTime.UtcNow-connected>TimeSpan.FromSeconds(30))foreach(var j in jobs.Values)if(!j.Process.HasExited)j.Process.Kill(true);
                await Task.Delay(2000,stop.Token);
            }
        }
        catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        finally{foreach(var j in jobs.Values){if(!j.Process.HasExited)j.Process.Kill(true);j.Process.Dispose();}}
    }
}
