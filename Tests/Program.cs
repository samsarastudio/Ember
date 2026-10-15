using Emberwake.Core;
using Emberwake.Backend;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;

int passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
void Reject(Action action,string name){try{action();}catch(ApiError){Check(true,name);return;}throw new Exception("Expected rejection: "+name);}
var catalog=Catalog.Default();var deck=new Profile().deck;
Check(TrophyMatching.Compatible(500,600,0,0)&&!TrophyMatching.Compatible(500,601,0,0),"Initial trophy band is 100");
Check(TrophyMatching.Compatible(500,700,10,10)&&!TrophyMatching.Compatible(500,700,10,0),"Widening respects both captains' waiting time");
Check(TrophyMatching.Range(999999)==600,"Trophy widening is capped");
Check(Catalog.Validate(catalog)==null,"Default catalog valid");
var bad=Wire.Decode<Catalog>(Wire.Encode(catalog));bad.cards[0].damage=float.NaN;Check(Catalog.Validate(bad)!=null,"Reject non-finite balance data");
bad=Wire.Decode<Catalog>(Wire.Encode(catalog));bad.cards[0].bundleUrl="https://example.com/model";Check(Catalog.Validate(bad)!=null,"Bundles require integrity hash and prefab");
bad=Wire.Decode<Catalog>(Wire.Encode(catalog));bad.cards[0].modelBundles=new[]{new ModelBundle{platform="Android",url="https://example.com/ship.bundle",sha256=new string('a',64),asset="Ship"}};Check(Catalog.Validate(bad)==null,"Platform-specific model manifest accepted");bad.cards[0].modelBundles[0].platform="unknown";Check(Catalog.Validate(bad)!=null,"Unsupported model platform rejected");
var sim=new MatchSimulation(catalog,deck,deck,3);
int slot=Array.FindIndex(sim.Hands[0],x=>catalog.Get(x).cost<=5);var id=sim.Hands[0][slot];float before=sim.Supply[0];
var cmd=new DeployRequest{sequence=1,slot=slot,x=-7,z=-5};Check(sim.Deploy(0,cmd).accepted,"Legal deployment accepted");float after=sim.Supply[0];
Check(Math.Abs(before-after-catalog.Get(id).cost)<.01f,"Supply charged exactly");
Check(sim.Deploy(0,cmd).accepted&&sim.Supply[0]==after,"Retry does not double-charge or cycle");
Check(!sim.Deploy(0,new DeployRequest{sequence=2,slot=0,x=float.NaN,z=-5}).accepted,"Reject NaN placement");
Check(sim.Supply[0]==after,"Rejected deployment does not spend");
Check(!sim.Deploy(0,new DeployRequest{sequence=3,slot=0,x=-7,z=-5,expectedCard="not_the_current_card"}).accepted&&sim.Supply[0]==after,"Delayed deployment cannot play a replacement card from a changed hand");
var snap=sim.Snapshot(0);Check(snap.hand.SequenceEqual(sim.Hands[0])&&snap.supply==sim.Supply[0],"Snapshot contains own hand only");
var clock=new MatchSimulation(catalog,deck,deck);for(int i=0;i<5400;i++)clock.Step();Check(clock.Winner==-2&&clock.Snapshot(0).phase=="overtime","Tied regulation enters overtime");for(int i=5400;i<9000;i++)clock.Step();Check(clock.Winner==-1,"Equal untouched objectives draw at hard limit");
var equal=new MatchSimulation(catalog,deck,deck);foreach(var u in equal.Units.Where(x=>x.hq))u.hp=0;equal.Step();Check(equal.Winner==-1,"Same-tick HQ destruction draws");
var catalog2=Wire.Decode<Catalog>(Wire.Encode(catalog));catalog2.cards[0].enabled=false;Check(Catalog.Validate(catalog2)!=null,"Cannot disable starter roster below eight");
var battle=new MatchSimulation(catalog,deck,deck,123);long sequence=1;for(int i=0;i<9000&&battle.Winner==-2;i++){if(i%35==0){battle.Bot(0,sequence++);battle.Bot(1,sequence++);}battle.Step();}
Check(battle.Winner!=-2,"Bot duel completes within time limit");Check(battle.Supply.All(x=>x>=0&&x<=10),"Supply remains bounded during full battle");Check(battle.Units.All(x=>x.hp>=0&&!float.IsNaN(x.x)&&!float.IsNaN(x.z)),"Combat state remains finite");
var feedback=new MatchSimulation(catalog,deck,deck,9);
Check(!MatchSimulation.LegalPlacement(0,catalog.Get("cutter"),7,5,feedback.Units),"Enemy half blocks ships");
Check(MatchSimulation.LegalPlacement(0,catalog.Get("grapeshot"),7,5,feedback.Units),"Orders can target enemy half");
Check(MatchSimulation.LegalPlacement(1,catalog.Get("cutter"),7,5,feedback.Units),"Team one placement is mirrored");
feedback.Units.First(u=>u.team==1&&!u.hq&&u.x>0).hp=0;
Check(MatchSimulation.LegalPlacement(0,catalog.Get("cutter"),7,5,feedback.Units),"Destroyed outpost opens its lane");
Check(!MatchSimulation.LegalPlacement(0,catalog.Get("cutter"),-7,-10,feedback.Units),"Harbor foundation blocks placement");
feedback.Hands[0][0]="grapeshot";var victim=feedback.Units.First(u=>u.team==1&&u.hq);float hull=victim.hp;
Check(feedback.Deploy(0,new DeployRequest{sequence=1,slot=0,x=victim.x,z=victim.z}).accepted,"Grapeshot accepted");
Check(feedback.Snapshot(1).zones.Any(z=>z.kind=="grapeshot"),"Opponent receives grapeshot flight cue");
for(int i=0;i<10;i++)feedback.Step();Check(victim.hp==hull,"Grapeshot cannot damage before flight completes");
feedback.Step();Check(victim.hp==hull-30,"Grapeshot impact preserves reduced structure damage");
for(int i=0;i<20;i++)feedback.Step();Check(!feedback.Zones.Any(z=>z.kind=="grapeshot"),"Grapeshot cue expires after impact");
string path=Path.Combine(Path.GetTempPath(),"emberwake-tests-"+Guid.NewGuid()+".db");
var fastShot=new MatchSimulation(catalog,deck,deck);
fastShot.Units.Add(new UnitState{id=100,team=0,card="cutter",kind="cutter",x=-7,z=-3,hp=520,maxHp=520});
fastShot.Units.Add(new UnitState{id=101,team=1,card="skiffs",kind="swarm",x=-7,z=-2.9f,hp=95,maxHp=95,cooldown=10});
fastShot.Step();
Check(fastShot.Shots.Count==0&&fastShot.Events.Any(e=>e.kind==1)&&fastShot.Events.Any(e=>e.kind==2),"Same-tick projectile fire and impact remain visible in event history");
Check(fastShot.Units.First(u=>u.id==101).hp==23,"Projectile damage applies once on authoritative impact");
Check(Wire.Encode(fastShot.Snapshot(0).events)==Wire.Encode(fastShot.Snapshot(1).events),"Both perspectives receive identical attack and impact events");
var healing=new MatchSimulation(catalog,deck,deck);
healing.Units.Add(new UnitState{id=100,team=0,card="repair",kind="repair",x=-7,z=-4,hp=320,maxHp=320});
healing.Units.Add(new UnitState{id=101,team=0,card="cutter",kind="cutter",x=-7,z=-3,hp=200,maxHp=520});
healing.Step();
Check(healing.Units.First(u=>u.id==101).hp==235&&healing.Events.Any(e=>e.kind==3),"Repair healing and its shared visual event agree");
var ramTest=new MatchSimulation(catalog,deck,deck);
ramTest.Units.Add(new UnitState{id=100,team=0,card="ram",kind="ram",x=-7,z=9,hp=540,maxHp=540,travel=4});
ramTest.Step();
Check(ramTest.Units.First(u=>u.team==1&&!u.hq&&u.x==-7).hp==1500&&ramTest.Events.Any(e=>e.kind==4),"Charged ram damage and shared impact event agree");
for(int n=0;n<400;n++)fastShot.Emit(1,100,101,0,-7,-3,-7,-2.9f);
Check(fastShot.Events.Count==256&&fastShot.Events.Zip(fastShot.Events.Skip(1),(a,b)=>b.id>a.id).All(v=>v),"Combat event history is bounded with monotonically increasing IDs");
var crowded=new MatchSimulation(catalog,deck,deck);
for(int team=0;team<2;team++)for(int n=0;n<48;n++)crowded.Units.Add(new UnitState{id=1000+team*100+n,team=team,card="skiffs",kind="swarm",x=(n%2==0?-7:7)+(n%3)*.15f,z=(team==0?-1:1)*(1.7f+n/12f),hp=95,maxHp=95});
int peakShots=0;for(int n=0;n<900;n++){crowded.Step();peakShots=Math.Max(peakShots,crowded.Shots.Count);}
Check(crowded.Units.Count<=102&&peakShots<=256&&crowded.Events.Count<=256,"96-ship combat remains inside replicated unit, projectile and event capacities");
var store=new Store(path);var auth=store.Authenticate(new AuthRequest{name="Test Captain"});Check(store.Resolve(auth.token)==auth.profile.id,"Guest bearer resolves");Reject(()=>store.Resolve("bad"),"Unauthenticated access rejected");
var qa=store.Authenticate(new AuthRequest{name="Queue A"});var qb=store.Authenticate(new AuthRequest{name="Queue B"});
store.Mutate(qb.profile.id,p=>p.trophies=1000);
using(var matching=new Matches(store,Microsoft.Extensions.Logging.Abstractions.NullLogger<Matches>.Instance))
{
    Check(matching.Join(qa.profile.id).status=="waiting"&&matching.Join(qb.profile.id).status=="waiting","Server queue separates distant trophy counts");
    matching.Cancel(qb.profile.id);store.Mutate(qb.profile.id,p=>p.trophies=50);
    var paired=matching.Join(qb.profile.id);Check(paired.status=="matched"&&matching.Poll(qa.profile.id).matchId==paired.matchId,"Nearby captains receive the same authoritative match");
    Check(paired.catalog.version==store.Catalog().version,"Matched client receives pinned server catalog");
}
store.RecordMatch("pair-reward",new[]{qa.profile.id,qb.profile.id},new[]{"Queue A","Queue B"},0);
store.RecordMatch("pair-reward",new[]{qa.profile.id,qb.profile.id},new[]{"Queue A","Queue B"},0);
Check(store.Profile(qa.profile.id).battles==1&&store.Profile(qb.profile.id).battles==1&&store.Profile(qa.profile.id).wins==1,"Both accounts settle once in the same transaction");
store.Delete(qa.profile.id);store.Delete(qb.profile.id);
var p=store.Profile(auth.profile.id);Check(p.cards.Count==8,"Eight starter cards unlocked");Reject(()=>store.SetDeck(p.id,new[]{"cutter"}),"Invalid deck rejected");
int oldGold=p.gold;store.Record("test-match",p.id,"Opponent",1);store.Record("test-match",p.id,"Opponent",1);p=store.Profile(p.id);Check(p.gold==oldGold+60&&p.battles==1,"Match grant is transactionally idempotent");
Reject(()=>store.RecordMatch("rollback-match",new[]{p.id,"missing-player"},new[]{"A","B"},0),"Two-player settlement rejects missing account");
Check(store.Profile(p.id).gold==p.gold&&store.Profile(p.id).battles==1,"Failed settlement rolls back both account rewards");
store.Upgrade(p.id,"cutter");p=store.Profile(p.id);Check(p.Card("cutter").level==2&&p.gold==oldGold-40,"Upgrade debits gold and prints");Reject(()=>store.Claim(p.id,20),"Unreached season reward rejected");
catalog2=store.Catalog();catalog2.version=2;catalog2.news="Published test";store.Publish(catalog2,1);Check(store.Catalog().version==2,"Catalog publication is persistent");Reject(()=>store.Publish(catalog2,1),"Stale publish rejected");store.Rollback(1);Check(store.Catalog().version==1,"Catalog rollback works");
var resumed=store.Authenticate(new AuthRequest{credential=auth.credential});Check(resumed.profile.id==p.id,"Guest credential resumes same profile");
var update=store.Catalog();update.version=3;var added=Wire.Decode<CardDef>(Wire.Encode(update.cards[1]));added.id="new_cutter";update.cards=update.cards.Append(added).ToArray();update.Get("skiffs").enabled=false;store.Publish(update,1);resumed=store.Authenticate(new AuthRequest{credential=auth.credential});Check(resumed.profile.Card("new_cutter")!=null,"New backend starter card unlocks on sign-in");Check(resumed.profile.deck.Length==8&&!resumed.profile.deck.Contains("skiffs"),"Retired cards are replaced in deck on sign-in");
string accountKey=new string('x',32);
var challenge=store.CreateEmailCode("captain@example.com",p.id,"123456",accountKey);
Reject(()=>store.CreateEmailCode("captain@example.com",p.id,"123456",accountKey),"Email resend cooldown enforced");
Reject(()=>store.VerifyEmailCode(new EmailVerifyRequest{challenge=challenge,code="000000"},accountKey),"Wrong verification code rejected");
var linked=store.VerifyEmailCode(new EmailVerifyRequest{challenge=challenge,code="123456"},accountKey);
Check(linked.profile.id==p.id&&linked.profile.battles==1&&store.Account(p.id).linked,"Email registration preserves guest progression");
Reject(()=>store.Authenticate(new AuthRequest{credential=auth.credential}),"Linking retires the old guest credential");
Reject(()=>store.Resolve(auth.token),"Linking retires the old guest session");
Reject(()=>store.VerifyEmailCode(new EmailVerifyRequest{challenge=challenge,code="123456"},accountKey),"Verification code cannot be replayed");
var loginChallenge=store.CreateEmailCode("captain@example.com",null,"654321",accountKey);
var recovered=store.VerifyEmailCode(new EmailVerifyRequest{challenge=loginChallenge,code="654321"},accountKey);
Check(recovered.profile.id==p.id&&recovered.credential!=linked.credential,"New-device email login recovers the same fleet with a separate credential");
Check(store.Authenticate(new AuthRequest{credential=recovered.credential}).profile.id==p.id,"Remembered email account reconnects");
store.Logout(p.id,recovered.credential,recovered.token);
Reject(()=>store.Resolve(recovered.token),"Sign-out revokes current bearer");
Reject(()=>store.Authenticate(new AuthRequest{credential=recovered.credential}),"Sign-out revokes current device credential");
Check(store.Authenticate(new AuthRequest{credential=linked.credential}).profile.id==p.id,"Signing out one device preserves another device");
var brute=store.CreateEmailCode("brute@example.com",null,"111111",accountKey);
for(int attempt=0;attempt<5;attempt++)Reject(()=>store.VerifyEmailCode(new EmailVerifyRequest{challenge=brute,code="222222"},accountKey),"Bad code attempt rejected");
Reject(()=>store.VerifyEmailCode(new EmailVerifyRequest{challenge=brute,code="111111"},accountKey),"Correct code rejected after attempt limit");
store.Delete(p.id);Reject(()=>store.Resolve(resumed.token),"Deletion invalidates sessions");
Check(!store.Account(p.id).linked,"Deletion removes email registration");
if(args.Length>0){
    using var http=new HttpClient{BaseAddress=new Uri(args[0])};
    async Task<AuthResponse> Login(string name){using var r=await http.PostAsJsonAsync("/v1/auth/guest",new AuthRequest{name=name},Wire.Json);r.EnsureSuccessStatusCode();return Wire.Decode<AuthResponse>(await r.Content.ReadAsStringAsync());}
    var a=await Login("Captain A");var b=await Login("Captain B");
    async Task<QueueReply> Queue(AuthResponse user){using var req=new HttpRequestMessage(HttpMethod.Post,"/v1/queue"){Content=JsonContent.Create(new IdRequest{id=""},options:Wire.Json)};req.Headers.Authorization=new("Bearer",user.token);using var res=await http.SendAsync(req);res.EnsureSuccessStatusCode();return Wire.Decode<QueueReply>(await res.Content.ReadAsStringAsync());}
    Check((await Queue(a)).status=="waiting","First captain queues");var match=await Queue(b);Check(match.status=="matched","Second captain creates authoritative duel");
    using var ws=new ClientWebSocket();ws.Options.SetRequestHeader("Authorization","Bearer "+b.token);var url=new UriBuilder(args[0]){Scheme="ws",Path="/v1/matches/"+match.matchId+"/stream"};await ws.ConnectAsync(url.Uri,CancellationToken.None);
    using var timeout=new CancellationTokenSource(5000);byte[] buffer=new byte[65536];var result=await ws.ReceiveAsync(buffer,timeout.Token);var json=Encoding.UTF8.GetString(buffer,0,result.Count);Check(json.Contains("snapshot")&&json.Contains(match.matchId),"WebSocket streams battle state");
    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure,"Test complete",CancellationToken.None);
    Check(ws.State==WebSocketState.Closed,"WebSocket close handshake completes");
    using var stranger=await http.GetAsync("/v1/matches/"+match.matchId);Check(stranger.StatusCode==System.Net.HttpStatusCode.Unauthorized,"Battle state requires authentication");
    using var captainA=new ClientWebSocket();captainA.Options.SetRequestHeader("Authorization","Bearer "+a.token);await captainA.ConnectAsync(url.Uri,CancellationToken.None);
    using var socket=new ClientWebSocket();socket.Options.SetRequestHeader("Authorization","Bearer "+b.token);await socket.ConnectAsync(url.Uri,CancellationToken.None);
    async Task<string> Receive(){using var deadline=new CancellationTokenSource(5000);using var data=new MemoryStream();WebSocketReceiveResult part;do{part=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),deadline.Token);data.Write(buffer,0,part.Count);}while(!part.EndOfMessage);return Encoding.UTF8.GetString(data.ToArray());}
    var initial=Wire.Decode<Envelope>(await Receive()).snapshot;int affordable=Array.FindIndex(initial.hand,x=>catalog.Get(x).cost<=initial.supply);
    var deployment=new DeployRequest{sequence=1,slot=affordable,x=7,z=5};var bytes=Encoding.UTF8.GetBytes(Wire.Encode(deployment));await socket.SendAsync(bytes,WebSocketMessageType.Text,true,CancellationToken.None);
    Envelope response;do{response=Wire.Decode<Envelope>(await Receive());}while(response.kind!="reply");Check(response.reply.accepted,"Online deployment passes authoritative validation");
    await socket.SendAsync(bytes,WebSocketMessageType.Text,true,CancellationToken.None);do{response=Wire.Decode<Envelope>(await Receive());}while(response.kind!="reply");Check(response.reply.accepted,"Online retry returns cached acceptance");
    using var surrender=new HttpRequestMessage(HttpMethod.Post,"/v1/matches/"+match.matchId+"/surrender");surrender.Headers.Authorization=new("Bearer",b.token);using var surrendered=await http.SendAsync(surrender);Check(surrendered.IsSuccessStatusCode,"Authenticated surrender accepted");
    await Task.Delay(150);using var profileRequest=new HttpRequestMessage(HttpMethod.Get,"/v1/profile");profileRequest.Headers.Authorization=new("Bearer",b.token);using var profileResponse=await http.SendAsync(profileRequest);var finished=Wire.Decode<Profile>(await profileResponse.Content.ReadAsStringAsync());Check(finished.battles==1&&finished.history[0].result=="Defeat","Server settles surrender and persists reward once");
    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"Test complete",CancellationToken.None);
    var adminToken=Environment.GetEnvironmentVariable("EMBERWAKE_TEST_ADMIN_TOKEN");
    if(!string.IsNullOrEmpty(adminToken)){
        using var denied=await http.GetAsync("/admin/api/catalog");Check(denied.StatusCode==System.Net.HttpStatusCode.Unauthorized,"Admin console rejects missing token");
        async Task<HttpResponseMessage> Admin(string path,object payload=null,int? expected=null){var request=new HttpRequestMessage(payload==null?HttpMethod.Get:HttpMethod.Post,path);request.Headers.Add("X-Admin-Token",adminToken);if(expected!=null)request.Headers.Add("If-Match-Version",expected.ToString());if(payload!=null)request.Content=JsonContent.Create(payload,options:Wire.Json);return await http.SendAsync(request);}
        using var currentResponse=await Admin("/admin/api/catalog");var current=Wire.Decode<Catalog>(await currentResponse.Content.ReadAsStringAsync());int original=current.version;
        using var versionsResponse=await Admin("/admin/api/releases");using var versions=System.Text.Json.JsonDocument.Parse(await versionsResponse.Content.ReadAsStringAsync());current.version=versions.RootElement.GetProperty("releases").EnumerateArray().Max(x=>x.GetProperty("version").GetInt32())+1;
        using var publication=await Admin("/admin/api/catalog/publish",current,original);Check(publication.IsSuccessStatusCode,"Admin publishes validated catalog over HTTP");
        using var stale=await Admin("/admin/api/catalog/publish",current,original);Check(stale.StatusCode==System.Net.HttpStatusCode.Conflict,"Admin HTTP publication rejects stale version");
        using var restore=await Admin("/admin/api/catalog/rollback",new IdRequest{tier=original});Check(restore.IsSuccessStatusCode,"Admin HTTP rollback succeeds");
        var restored=await http.GetFromJsonAsync<Catalog>("/v1/catalog",Wire.Json);Check(restored.version==original,"Public catalog reflects rollback");
    }
}
await WorkerChecks.Run(Check,Reject);
var unlockCaptain=store.Authenticate(new AuthRequest{name="Unlock Test"});
var victoryUnlock=store.Authenticate(new AuthRequest{name="Victory Unlock"});
store.Mutate(victoryUnlock.profile.id,p=>p.trophies=30);
store.Record("threshold-victory",victoryUnlock.profile.id,"Opponent",1);
Check(store.Profile(victoryUnlock.profile.id).Card("interceptor")!=null,"Winning from 30 to 60 trophies grants Interceptor");
store.Record("threshold-victory",victoryUnlock.profile.id,"Opponent",1);
Check(store.Profile(victoryUnlock.profile.id).cards.Count(c=>c.id=="interceptor")==1,"Repeated result never duplicates the unlocked card");
foreach(var card in store.Catalog().cards.Where(c=>c.enabled&&c.unlockTrophies>0).OrderBy(c=>c.unlockTrophies))
{
    store.Mutate(unlockCaptain.profile.id,p=>p.trophies=card.unlockTrophies-1);
    Check(store.Profile(unlockCaptain.profile.id).Card(card.id)==null,"Card stays locked below threshold: "+card.id);
    store.Mutate(unlockCaptain.profile.id,p=>p.trophies=card.unlockTrophies);
    Check(store.Profile(unlockCaptain.profile.id).Card(card.id)!=null,"Profile grants card exactly at threshold: "+card.id);
}
store.Mutate(unlockCaptain.profile.id,p=>p.trophies=0);
Check(store.Authenticate(new AuthRequest{credential=unlockCaptain.credential}).profile.cards.Count(c=>store.Catalog().Get(c.id)?.unlockTrophies>0)==4,"Unlocked cards persist after trophy loss and reconnect");
Console.WriteLine($"ALL {passed} CHECKS PASSED");
class Envelope {public string kind=null;public BattleSnapshot snapshot=null;public CommandReply reply=null;}


