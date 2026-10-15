using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Emberwake.Core;

namespace Emberwake.Backend;

public sealed class ApiError : Exception { public int Status; public ApiError(string message,int status=400):base(message){Status=status;} }
public static class Wire
{
    public static readonly JsonSerializerOptions Json = new() { IncludeFields=true,PropertyNameCaseInsensitive=true };
    public static string Encode<T>(T value)=>JsonSerializer.Serialize(value,Json);
    public static T Decode<T>(string value)=>JsonSerializer.Deserialize<T>(value,Json);
    public static string Secret()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value??"")));
}

public sealed partial class Store
{
    readonly object gate=new(); readonly string connectionString;
    public Store(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        connectionString=new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadWriteCreate}.ToString();
        using var db=Open(); using var q=db.CreateCommand();q.CommandText="""
        PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;
        CREATE TABLE IF NOT EXISTS players(id TEXT PRIMARY KEY, credential TEXT UNIQUE NOT NULL, profile TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS ai_captains(slot INTEGER PRIMARY KEY,player TEXT UNIQUE NOT NULL);
        CREATE TABLE IF NOT EXISTS sessions(hash TEXT PRIMARY KEY, player TEXT NOT NULL, expires INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS catalogs(version INTEGER PRIMARY KEY, payload TEXT NOT NULL, created TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY,value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS ledger(id TEXT PRIMARY KEY, player TEXT NOT NULL, kind TEXT NOT NULL, created TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS friends(owner TEXT NOT NULL, other TEXT NOT NULL, PRIMARY KEY(owner,other));
        CREATE TABLE IF NOT EXISTS audit(id INTEGER PRIMARY KEY AUTOINCREMENT, action TEXT NOT NULL, created TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS accounts(email TEXT PRIMARY KEY,player TEXT UNIQUE NOT NULL);
        CREATE TABLE IF NOT EXISTS devices(hash TEXT PRIMARY KEY,player TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS email_codes(email TEXT PRIMARY KEY,challenge TEXT NOT NULL,hash TEXT NOT NULL,player TEXT NOT NULL,expires INTEGER NOT NULL,sent INTEGER NOT NULL,attempts INTEGER NOT NULL);
        """;q.ExecuteNonQuery();
        using var seed=db.CreateCommand();seed.CommandText="INSERT OR IGNORE INTO catalogs VALUES(1,$json,$date); INSERT OR IGNORE INTO settings VALUES('active','1');";seed.Parameters.AddWithValue("$json",Wire.Encode(Emberwake.Core.Catalog.Default()));seed.Parameters.AddWithValue("$date",DateTime.UtcNow.ToString("O"));seed.ExecuteNonQuery();
    }
    SqliteConnection Open(){var db=new SqliteConnection(connectionString);db.Open();return db;}
    static object Scalar(SqliteConnection db,string sql,params (string,object)[] args){using var q=db.CreateCommand();q.CommandText=sql;foreach(var a in args)q.Parameters.AddWithValue(a.Item1,a.Item2);return q.ExecuteScalar();}
    static void Exec(SqliteConnection db,string sql,params (string,object)[] args){using var q=db.CreateCommand();q.CommandText=sql;foreach(var a in args)q.Parameters.AddWithValue(a.Item1,a.Item2);q.ExecuteNonQuery();}
    public Catalog Catalog(){lock(gate){using var db=Open();return Wire.Decode<Catalog>((string)Scalar(db,"SELECT payload FROM catalogs WHERE version=CAST((SELECT value FROM settings WHERE key='active') AS INTEGER)"));}}
    public AuthResponse Authenticate(AuthRequest request)
    {
        if(request==null)throw new ApiError("Request required");
        lock(gate){using var db=Open();string credential=request.credential;
            Profile profile;
            if(string.IsNullOrEmpty(credential)){
                credential=Wire.Secret();profile=new Profile{id=Guid.NewGuid().ToString("N"),name=CleanName(request.name)};profile.Unlock(Catalog());foreach(var c in profile.cards)c.blueprints=20;
                Exec(db,"INSERT INTO players VALUES($id,$credential,$profile)",("$id",profile.id),("$credential",Wire.Hash(credential)),("$profile",Wire.Encode(profile)));
            }else{
                if(credential.Length!=64)throw new ApiError("Invalid device credential",401);
                var json=Scalar(db,"SELECT profile FROM players WHERE credential=$c OR id=(SELECT player FROM devices WHERE hash=$c)",("$c",Wire.Hash(credential))) as string;
                if(json==null)throw new ApiError("Device credential is no longer valid",401);profile=Wire.Decode<Profile>(json);var available=Catalog();profile.Unlock(available);profile.deck=(profile.deck??Array.Empty<string>()).Where(x=>profile.Card(x)!=null&&available.Get(x)?.enabled==true).Concat(profile.cards.Select(x=>x.id).Where(x=>available.Get(x)?.enabled==true)).Distinct().Take(8).ToArray();Exec(db,"UPDATE players SET profile=$p WHERE id=$id",("$p",Wire.Encode(profile)),("$id",profile.id));
            }
            var token=Wire.Secret();Exec(db,"DELETE FROM sessions WHERE expires<$now; INSERT INTO sessions VALUES($hash,$player,$expires)",("$now",DateTimeOffset.UtcNow.ToUnixTimeSeconds()),("$hash",Wire.Hash(token)),("$player",profile.id),("$expires",DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds()));
            return new AuthResponse{token=token,credential=credential,profile=profile};
        }
    }
    public static string CleanName(string name){name=(name??"Captain").Trim();if(name.Length<2||name.Length>20||name.Any(c=>!char.IsLetterOrDigit(c)&&c!=' '&&c!='_'&&c!='-'))throw new ApiError("Name must be 2–20 letters, numbers, spaces or dashes");return name;}
    public string Resolve(string token){if(string.IsNullOrEmpty(token)||token.Length!=64)throw new ApiError("Sign in required",401);lock(gate){using var db=Open();var id=Scalar(db,"SELECT player FROM sessions WHERE hash=$h AND expires>$now",("$h",Wire.Hash(token)),("$now",DateTimeOffset.UtcNow.ToUnixTimeSeconds())) as string;if(id==null)throw new ApiError("Session expired",401);return id;}}
    public Profile Profile(string id)
    {
        lock(gate){using var db=Open();var profile=Read(db,id);int before=profile.cards.Count;profile.Unlock(Catalog());
            if(profile.cards.Count!=before)Exec(db,"UPDATE players SET profile=$p WHERE id=$id",("$p",Wire.Encode(profile)),("$id",id));
            return profile;}
    }
    static Profile Read(SqliteConnection db,string id){var s=Scalar(db,"SELECT profile FROM players WHERE id=$id",("$id",id)) as string;if(s==null)throw new ApiError("Player not found",404);return Wire.Decode<Profile>(s);}
    public Profile Mutate(string id,Action<Profile> action)
    {
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();var p=Read(db,id);action(p);Exec(db,"UPDATE players SET profile=$p WHERE id=$id",("$p",Wire.Encode(p)),("$id",id));tx.Commit();return p;}
    }
    public Profile SetDeck(string id,string[] deck){var c=Catalog();return Mutate(id,p=>{if(deck==null||deck.Length!=8||deck.Distinct().Count()!=8||deck.Any(x=>p.Card(x)==null||c.Get(x)==null||!c.Get(x).enabled))throw new ApiError("Choose eight distinct unlocked active cards");p.deck=deck;});}
    public Profile Upgrade(string id,string card){return Mutate(id,p=>{var c=p.Card(card);if(c==null)throw new ApiError("Card locked");if(c.level>=5)throw new ApiError("Maximum level");int gold=c.level*100,prints=c.level*10;if(p.gold<gold||c.blueprints<prints)throw new ApiError("Not enough gold or blueprints");p.gold-=gold;c.blueprints-=prints;c.level++;});}
    public Profile Buy(string id,string offer){var o=Catalog().offers.FirstOrDefault(x=>x.id==offer);if(o==null)throw new ApiError("Offer unavailable");return Mutate(id,p=>{if(p.cosmetics.Contains(o.cosmetic))throw new ApiError("Already owned");if(p.gold<o.goldCost)throw new ApiError("Not enough gold");p.gold-=o.goldCost;p.cosmetics.Add(o.cosmetic);p.equipped=o.cosmetic;});}
    public Profile Claim(string id,int tier){return Mutate(id,p=>{if(tier<1||tier>20||p.seasonXp<tier*50)throw new ApiError("Tier not reached");if(p.claimedSeason.Contains(tier))throw new ApiError("Already claimed",409);p.claimedSeason.Add(tier);p.gold+=100+tier*10;});}
    public void Record(string matchId,string id,string opponent,int result)
    {
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();RecordOne(db,matchId,id,opponent,result);tx.Commit();}
    }
    public void RecordMatch(string matchId,string[] players,string[] names,int winner)
    {
        if(players?.Length!=2||names?.Length!=2||winner< -1||winner>1)throw new ApiError("Invalid match outcome");
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();for(int t=0;t<2;t++)RecordOne(db,matchId,players[t],names[1-t],winner==-1?0:winner==t?1:-1);tx.Commit();}
    }
    void RecordOne(SqliteConnection db,string matchId,string id,string opponent,int result)
    {
            var ledgerId=matchId+":"+id;
            if(Scalar(db,"SELECT id FROM ledger WHERE id=$id",("$id",ledgerId))!=null)return;
            var p=Read(db,id);int gold=result==1?60:result==0?35:20,delta=result==1?30:result==0?0:-15;
            p.gold+=gold;p.trophies=Math.Max(0,p.trophies+delta);p.battles++;if(result==1)p.wins++;p.seasonXp+=result==1?25:10;
            p.Unlock(Catalog());foreach(var c in p.cards)c.blueprints+=result==1?3:1;
            p.history.Insert(0,new MatchRecord{id=matchId,opponent=opponent,result=result==1?"Victory":result==0?"Draw":"Defeat",gold=gold,trophies=delta,date=DateTime.UtcNow.ToString("O")});if(p.history.Count>50)p.history.RemoveRange(50,p.history.Count-50);
            Exec(db,"UPDATE players SET profile=$p WHERE id=$id",("$p",Wire.Encode(p)),("$id",id));Exec(db,"INSERT INTO ledger VALUES($id,$player,'match',$date)",("$id",ledgerId),("$player",id),("$date",DateTime.UtcNow.ToString("O")));
    }
    public int Publish(Catalog c,int expected)
    {
        var error=Core.Catalog.Validate(c);if(error!=null)throw new ApiError(error);
        lock(gate){using var db=Open();using var tx=db.BeginTransaction();int active=int.Parse((string)Scalar(db,"SELECT value FROM settings WHERE key='active'"));if(active!=expected)throw new ApiError("Catalog changed; reload before publishing",409);
            if(c.version<=Convert.ToInt32(Scalar(db,"SELECT MAX(version) FROM catalogs")))throw new ApiError("Version must exceed all previous releases");
            Exec(db,"INSERT INTO catalogs VALUES($v,$p,$d); UPDATE settings SET value=$v WHERE key='active'; INSERT INTO audit(action,created) VALUES($a,$d)",("$v",c.version),("$p",Wire.Encode(c)),("$d",DateTime.UtcNow.ToString("O")),("$a","publish:"+c.version));tx.Commit();return c.version;}
    }
    public void Rollback(int version){lock(gate){using var db=Open();if(Scalar(db,"SELECT version FROM catalogs WHERE version=$v",("$v",version))==null)throw new ApiError("Unknown release");Exec(db,"UPDATE settings SET value=$v WHERE key='active'; INSERT INTO audit(action,created) VALUES($a,$d)",("$v",version),("$a","rollback:"+version),("$d",DateTime.UtcNow.ToString("O")));}}
    public object[] Versions(){lock(gate){using var db=Open();using var q=db.CreateCommand();q.CommandText="SELECT version,created FROM catalogs ORDER BY version DESC";using var r=q.ExecuteReader();var rows=new List<object>();while(r.Read())rows.Add(new{version=r.GetInt32(0),created=r.GetString(1)});return rows.ToArray();}}
    public object[] Leaderboard(){lock(gate){using var db=Open();using var q=db.CreateCommand();q.CommandText="SELECT profile FROM players WHERE id NOT IN (SELECT player FROM ai_captains) ORDER BY CAST(json_extract(profile,'$.trophies') AS INTEGER) DESC LIMIT 50";using var r=q.ExecuteReader();var list=new List<object>();while(r.Read()){var p=Wire.Decode<Profile>(r.GetString(0));list.Add(new{id=p.id,name=p.name,trophies=p.trophies});}return list.ToArray();}}
    public object[] Friends(string id){lock(gate){using var db=Open();using var q=db.CreateCommand();q.CommandText="SELECT p.profile FROM friends f JOIN players p ON p.id=f.other WHERE f.owner=$id";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();var list=new List<object>();while(r.Read()){var p=Wire.Decode<Profile>(r.GetString(0));list.Add(new{id=p.id,name=p.name,trophies=p.trophies});}return list.ToArray();}}
    public void AddFriend(string id,string other){if(id==other)throw new ApiError("This is your Captain ID");lock(gate){using var db=Open();Read(db,other);Exec(db,"INSERT OR IGNORE INTO friends VALUES($a,$b)",("$a",id),("$b",other));}}
    public void Delete(string id){lock(gate){using var db=Open();using var tx=db.BeginTransaction();Exec(db,"DELETE FROM email_codes WHERE player=$id OR email IN (SELECT email FROM accounts WHERE player=$id); DELETE FROM accounts WHERE player=$id; DELETE FROM devices WHERE player=$id; DELETE FROM sessions WHERE player=$id; DELETE FROM friends WHERE owner=$id OR other=$id; DELETE FROM ledger WHERE player=$id; DELETE FROM players WHERE id=$id",("$id",id));tx.Commit();}}
}
