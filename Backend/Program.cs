using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Emberwake.Backend;
using Emberwake.Core;

if(args.Contains("--worker")){await FusionWorker.Run();return;}
if(args.Contains("--benchmark")){ServerBenchmark.Run();return;}
var builder=WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o=>{o.SerializerOptions.IncludeFields=true;o.SerializerOptions.PropertyNameCaseInsensitive=true;});
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=512*1024);
builder.Services.AddSingleton(new Store(Environment.GetEnvironmentVariable("EMBERWAKE_DB")??"data/emberwake.db"));
builder.Services.AddSingleton<Matches>();builder.Services.AddHostedService(s=>s.GetRequiredService<Matches>());
builder.Services.AddSingleton<FusionMatches>();builder.Services.AddHostedService(s=>s.GetRequiredService<FusionMatches>());
builder.Services.AddSingleton<AccountMail>();
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=300,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddPolicy("auth",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=10,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
var app=builder.Build();
// Only the default loopback proxies are trusted; the public listener is Caddy.
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions{ForwardedHeaders=Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor|Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto});
app.Use(async(ctx,next)=>{try{ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Referrer-Policy"]="no-referrer";await next();}catch(ApiError e){if(!ctx.Response.HasStarted){ctx.Response.StatusCode=e.Status;await ctx.Response.WriteAsJsonAsync(new ErrorResponse{error=e.Message});}}catch(Exception e){app.Logger.LogError(e,"Request failed");if(!ctx.Response.HasStarted){ctx.Response.StatusCode=500;await ctx.Response.WriteAsJsonAsync(new ErrorResponse{error="Service error. Please retry."});}}});
app.UseRateLimiter();app.UseWebSockets(new WebSocketOptions{KeepAliveInterval=TimeSpan.FromSeconds(20)});app.UseDefaultFiles();app.UseStaticFiles(new StaticFileOptions{OnPrepareResponse=c=>{if(c.Context.Request.Path.StartsWithSegments("/content"))c.Context.Response.Headers.CacheControl="public,max-age=300";}});
string Player(HttpContext ctx,Store store){var header=ctx.Request.Headers.Authorization.ToString();return store.Resolve(header.StartsWith("Bearer ")?header.Substring(7):"");}
void Admin(HttpContext ctx){string expected=Environment.GetEnvironmentVariable("EMBERWAKE_ADMIN_TOKEN"),actual=ctx.Request.Headers["X-Admin-Token"].ToString();if(string.IsNullOrEmpty(expected)||expected.Length<32)throw new ApiError("Admin access is not configured",503);if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Wire.Hash(actual)),Encoding.UTF8.GetBytes(Wire.Hash(expected))))throw new ApiError("Admin authentication required",401);}
string BattleTransport()=>Environment.GetEnvironmentVariable("EMBERWAKE_BATTLE_TRANSPORT")=="websocket"?"websocket":"fusion";
void WorkerAuth(HttpContext ctx){var expected=Environment.GetEnvironmentVariable("EMBERWAKE_WORKER_KEY")??"";var actual=ctx.Request.Headers["X-Worker-Key"].ToString();if(expected.Length<32||!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Wire.Hash(actual)),Encoding.UTF8.GetBytes(Wire.Hash(expected))))throw new ApiError("Worker authentication required",401);}
app.MapPost("/internal/fusion/poll",(HttpContext c,FusionMatches m,WorkerPoll req)=>{WorkerAuth(c);return m.WorkerPoll(req);});
app.MapPost("/internal/fusion/report",(HttpContext c,FusionMatches m,WorkerReport req)=>{WorkerAuth(c);m.WorkerReport(req);return Results.Ok(new{accepted=true});});
app.MapGet("/health",(FusionMatches m)=>Results.Ok(new{status="ok",version="0.2.0",transport=BattleTransport(),photonIntegrated=true,rankedAvailable=BattleTransport()=="websocket"||m.Enabled,region=BattleTransport()=="fusion"?m.Region:"self-hosted"}));
app.MapGet("/v1/bootstrap",()=>new Bootstrap{battleTransport=BattleTransport(),privacyUrl=Environment.GetEnvironmentVariable("EMBERWAKE_PRIVACY_URL")??"",termsUrl=Environment.GetEnvironmentVariable("EMBERWAKE_TERMS_URL")??""});
app.MapGet("/v1/catalog",(HttpContext ctx,Store store)=>{var c=store.Catalog();var tag="\"catalog-"+c.version+"\"";ctx.Response.Headers.ETag=tag;if(ctx.Request.Headers.IfNoneMatch==tag)return Results.StatusCode(304);return Results.Json(c,Wire.Json);});
app.MapPost("/v1/auth/guest",(AuthRequest req,Store store)=>store.Authenticate(req)).RequireRateLimiting("auth");
app.MapPost("/v1/auth/email/code",async(HttpContext ctx,Store s,AccountMail mail,EmailCodeRequest req)=>{
    if(mail.Key.Length<32)throw new ApiError("Email sign-in is not configured yet.",503);
    var email=Store.CleanEmail(req.email);string player=req.link?Player(ctx,s):null;
    var code=RandomNumberGenerator.GetInt32(1000000).ToString("D6");var challenge=s.CreateEmailCode(email,player,code,mail.Key);
    try{await mail.Send(email,code);}catch(Exception e){s.RevokeEmailCode(challenge);app.Logger.LogWarning("Verification email delivery failed: {Type}",e.GetType().Name);throw new ApiError("Cannot send a verification email right now. Please try again later.",503);}
    return new EmailCodeReply{challenge=challenge};
}).RequireRateLimiting("auth");
app.MapPost("/v1/auth/email/verify",(Store s,AccountMail mail,EmailVerifyRequest req)=>{if(mail.Key.Length<32)throw new ApiError("Email sign-in unavailable",503);return s.VerifyEmailCode(req,mail.Key);}).RequireRateLimiting("auth");
app.MapGet("/v1/account",(HttpContext ctx,Store s)=>s.Account(Player(ctx,s)));
app.MapPost("/v1/auth/logout",(HttpContext ctx,Store s,FusionMatches f,Matches m,AuthRequest req)=>{var id=Player(ctx,s);if(f.HasActive(id)||m.Rooms.Values.Any(r=>r.Team(id)>=0&&r.Simulation.Winner==-2))throw new ApiError("Finish your battle before signing out.",409);f.Cancel(id);m.Cancel(id);s.Logout(id,req.credential,ctx.Request.Headers.Authorization.ToString().Substring(7));return Results.Ok(new{signedOut=true});});
app.MapGet("/v1/profile",(HttpContext ctx,Store s)=>s.Profile(Player(ctx,s)));
app.MapPut("/v1/profile/name",(HttpContext ctx,Store s,AuthRequest req)=>s.Mutate(Player(ctx,s),p=>p.name=Store.CleanName(req.name)));
app.MapPut("/v1/profile/deck",(HttpContext ctx,Store s,DeckRequest req)=>s.SetDeck(Player(ctx,s),req.deck));
app.MapPost("/v1/profile/tutorial",(HttpContext ctx,Store s)=>s.Mutate(Player(ctx,s),p=>p.tutorialDone=true));
app.MapPost("/v1/cards/upgrade",(HttpContext ctx,Store s,IdRequest req)=>s.Upgrade(Player(ctx,s),req.id));
app.MapPost("/v1/shop/buy",(HttpContext ctx,Store s,IdRequest req)=>s.Buy(Player(ctx,s),req.id));
app.MapPost("/v1/cosmetics/equip",(HttpContext ctx,Store s,IdRequest req)=>s.Mutate(Player(ctx,s),p=>{if(!p.cosmetics.Contains(req.id))throw new ApiError("Cosmetic not owned");p.equipped=req.id;}));
app.MapPost("/v1/season/claim",(HttpContext ctx,Store s,IdRequest req)=>s.Claim(Player(ctx,s),req.tier));
app.MapGet("/v1/profile/export",(HttpContext ctx,Store s)=>Results.File(Encoding.UTF8.GetBytes(Wire.Encode(s.Profile(Player(ctx,s)))),"application/json","emberwake-profile.json"));
app.MapDelete("/v1/profile",(HttpContext ctx,Store s,Matches m,FusionMatches f)=>{string id=Player(ctx,s);if(f.HasActive(id))throw new ApiError("Finish your battle before deleting this account.",409);f.Cancel(id);m.RemovePlayer(id);s.Delete(id);return Results.Ok(new{deleted=true});});
app.MapGet("/v1/leaderboard",(Store s)=>new{players=s.Leaderboard()});
app.MapGet("/v1/friends",(HttpContext ctx,Store s)=>new{players=s.Friends(Player(ctx,s))});
app.MapPost("/v1/friends",(HttpContext ctx,Store s,IdRequest req)=>{s.AddFriend(Player(ctx,s),req.id);return Results.Ok(new{added=true});});
app.MapPost("/v1/queue",(HttpContext ctx,Store s,Matches m,FusionMatches f,IdRequest req)=>{var id=Player(ctx,s);if(f.HasActive(id))throw new ApiError("You already have an active ranked battle.",409);f.Cancel(id);return m.Join(id,req.id);});
app.MapPost("/v1/fusion/queue",(HttpContext ctx,Store s,Matches m,FusionMatches f)=>{var id=Player(ctx,s);if(m.Rooms.Values.Any(r=>r.Team(id)>=0&&r.Simulation.Winner==-2))throw new ApiError("Finish your current battle first.",409);m.Cancel(id);return f.Join(id);});
app.MapGet("/v1/fusion/queue",(HttpContext ctx,Store s,FusionMatches f)=>f.Poll(Player(ctx,s)));
app.MapDelete("/v1/fusion/queue",(HttpContext ctx,Store s,FusionMatches f)=>{f.Cancel(Player(ctx,s));return Results.Ok(new{cancelled=true});});
app.MapGet("/v1/queue",(HttpContext ctx,Store s,Matches m)=>m.Poll(Player(ctx,s)));
app.MapDelete("/v1/queue",(HttpContext ctx,Store s,Matches m)=>{m.Cancel(Player(ctx,s));return Results.Ok(new{cancelled=true});});
app.MapGet("/v1/matches/{id}",(string id,HttpContext ctx,Store s,Matches m)=>{string p=Player(ctx,s);var r=m.Get(id,p);lock(r.Gate)return r.Simulation.Snapshot(r.Team(p),r.Id,r.Players[1-r.Team(p)].name);});
app.MapPost("/v1/matches/{id}/surrender",(string id,HttpContext ctx,Store s,Matches m)=>{string p=Player(ctx,s);var r=m.Get(id,p);lock(r.Gate){if(!r.Started)throw new ApiError("Waiting for both captains.",409);r.Simulation.Surrender(r.Team(p));}return Results.Ok(new{accepted=true});});
app.Map("/v1/matches/{id}/stream",async(string id,HttpContext ctx,Store s,Matches m)=>{
    string p=Player(ctx,s);var room=m.Get(id,p);int team=room.Team(p);
    if(!ctx.WebSockets.IsWebSocketRequest){ctx.Response.StatusCode=400;return;}
    using var socket=await ctx.WebSockets.AcceptWebSocketAsync();using var stop=CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);using var sendLock=new SemaphoreSlim(1,1);
    lock(room.Gate){room.Connections[team]?.Cancel();room.Connections[team]=stop;room.Disconnected[team]=default;if(room.Connections.All(c=>c!=null))room.Started=true;}
    async Task Send(object payload){var bytes=Encoding.UTF8.GetBytes(Wire.Encode(payload));await sendLock.WaitAsync(stop.Token);try{await socket.SendAsync(bytes,WebSocketMessageType.Text,true,stop.Token);}finally{sendLock.Release();}}
    var sendTask=Task.Run(async()=>{int lastEvent=0;using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(100));while(await timer.WaitForNextTickAsync(stop.Token)){BattleSnapshot snap;lock(room.Gate)snap=room.Simulation.Snapshot(team,id,room.Players[1-team].name);snap.events=snap.events.Where(e=>e.id>lastEvent&&e.tick>=snap.tick-30).ToArray();await Send(new{kind="snapshot",snapshot=snap});if(snap.events.Length>0)lastEvent=snap.events[^1].id;}},stop.Token);
    try{
        byte[] buffer=new byte[4096];DateTime window=DateTime.UtcNow;int commands=0;
        while(socket.State==WebSocketState.Open){int used=0;WebSocketReceiveResult result;do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer,used,buffer.Length-used),stop.Token);used+=result.Count;if(result.MessageType==WebSocketMessageType.Close)return;if(used==buffer.Length&&!result.EndOfMessage)throw new ApiError("Message too large");}while(!result.EndOfMessage);
            if(DateTime.UtcNow-window>TimeSpan.FromSeconds(1)){window=DateTime.UtcNow;commands=0;}if(++commands>20)throw new ApiError("Too many commands",429);
            var cmd=Wire.Decode<DeployRequest>(Encoding.UTF8.GetString(buffer,0,used));CommandReply reply;lock(room.Gate){if(!ReferenceEquals(room.Connections[team],stop))return;reply=room.Started?room.Simulation.Deploy(team,cmd):new CommandReply{error="Waiting for the other captain."};}await Send(new{kind="reply",reply});
        }
    }catch(OperationCanceledException){}catch(WebSocketException){}finally{lock(room.Gate){if(ReferenceEquals(room.Connections[team],stop)){room.Connections[team]=null;room.Disconnected[team]=DateTime.UtcNow;}}stop.Cancel();try{await sendTask;}catch(OperationCanceledException){}catch(WebSocketException){}if(socket.State==WebSocketState.Open||socket.State==WebSocketState.CloseReceived){using var closeTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));try{await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"Disconnected",closeTimeout.Token);}catch(OperationCanceledException){}}}
});
app.MapGet("/admin/api/catalog",(HttpContext c,Store s)=>{Admin(c);return s.Catalog();});
app.MapGet("/admin/api/releases",(HttpContext c,Store s)=>{Admin(c);return new{releases=s.Versions()};});
app.MapPost("/admin/api/catalog/validate",(HttpContext c,Catalog payload)=>{Admin(c);var error=Catalog.Validate(payload);return Results.Ok(new{valid=error==null,error});});
app.MapPost("/admin/api/catalog/publish",(HttpContext c,Store s,Catalog payload)=>{Admin(c);if(!int.TryParse(c.Request.Headers["If-Match-Version"],out int expected))throw new ApiError("If-Match-Version is required",428);return new{version=s.Publish(payload,expected)};});
app.MapPost("/admin/api/catalog/rollback",(HttpContext c,Store s,IdRequest req)=>{Admin(c);s.Rollback(req.tier);return new{active=s.Catalog().version};});
app.Run();

