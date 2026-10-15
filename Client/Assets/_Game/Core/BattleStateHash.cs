using System;
namespace Emberwake.Core
{
    // Audit of public combat only; private hand/supply differ by player.
    public static class BattleStateHash
    {
        public static int Compute(BattleSnapshot s)
        {
            unchecked
            {
                int hash=17;
                void Add(int n){hash=hash*31+n;}
                void Float(float n){Add((int)Math.Round((double)n*1000));}
                Add(s.tick);Add(s.winner);
                Add(s.units.Length);foreach(var u in s.units){Add(u.id);Add(u.team);Add(u.structure?1:0);Add(u.hq?1:0);Add(u.active?1:0);Float(u.x);Float(u.z);Float(u.hp);Float(u.maxHp);Float(u.spawnAt);}
                Add(s.shots.Length);foreach(var p in s.shots){Add(p.id);Add(p.team);Add(p.target);Float(p.x);Float(p.z);Float(p.damage);Float(p.splash);Float(p.speed);}
                Add(s.zones.Length);foreach(var z in s.zones){Add(z.id);Add(z.team);Float(z.x);Float(z.z);Float(z.radius);Float(z.expires);}
                return hash;
            }
        }
    }
}
