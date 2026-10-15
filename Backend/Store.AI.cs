using Emberwake.Core;

namespace Emberwake.Backend;

public sealed partial class Store
{
    static readonly string[] AINames={"Mara Tidewalker","Rowan Blackwater","Kael Stormwatch","Iris Silverwake","Finn Ironshore","Nova Reefrunner","Theo Ashsail","Lyra Wavecrest","Orion Driftwood","Skye Saltwind"};
    public void EnsureAICaptains()
    {
        lock(gate)
        {
            using var db=Open();using var tx=db.BeginTransaction();
            Exec(db,"CREATE TABLE IF NOT EXISTS ai_captains(slot INTEGER PRIMARY KEY,player TEXT UNIQUE NOT NULL)");
            for(int i=0;i<AINames.Length;i++)
            {
                if(Scalar(db,"SELECT player FROM ai_captains WHERE slot=$s",("$s",i))!=null)continue;
                var profile=new Profile{id=Guid.NewGuid().ToString("N"),name=AINames[i],tutorialDone=true};profile.Unlock(Catalog());
                // No usable login credential, device, email or session is issued for AI accounts.
                Exec(db,"INSERT INTO players VALUES($id,$credential,$profile); INSERT INTO ai_captains VALUES($s,$id)",
                    ("$id",profile.id),("$credential","ai-disabled:"+profile.id),("$profile",Wire.Encode(profile)),("$s",i));
            }
            tx.Commit();
        }
    }
    public Profile[] AICaptains()
    {
        lock(gate){using var db=Open();using var q=db.CreateCommand();q.CommandText="SELECT p.profile FROM ai_captains a JOIN players p ON p.id=a.player ORDER BY a.slot";
            using var r=q.ExecuteReader();var profiles=new List<Profile>();while(r.Read())profiles.Add(Wire.Decode<Profile>(r.GetString(0)));return profiles.ToArray();}
    }
    public Profile MatchAICaptain(Profile human,IEnumerable<string> busy)
    {
        lock(gate)
        {
            var available=AICaptains().Where(p=>!busy.Contains(p.id)).ToArray();if(available.Length==0)return null;
            var chosen=available[Random.Shared.Next(available.Length)];
            return Mutate(chosen.id,p=>{p.trophies=human.trophies;p.deck=(string[])human.deck.Clone();
                p.cards=human.cards.Select(c=>new CardProgress{id=c.id,level=c.level,blueprints=c.blueprints}).ToList();});
        }
    }
}
