using System;
using System.Collections.Generic;
using System.Linq;

namespace Emberwake.Core
{
    [Serializable] public class UnitState
    {
        public int id,team; public string card,kind; public float x,z,hp,maxHp,cooldown,spawnAt,travel,expires; public bool structure,hq,active=true;
    }
    [Serializable] public class ShotState { public int id,team,target; public float x,z,damage,splash,speed; }
    [Serializable] public class CombatEvent { public int id,tick,kind,source,target,team; public float x,z,toX,toZ,amount; }
    [Serializable] public class ZoneState { public int id,team; public string kind; public float x,z,radius,expires; }
    [Serializable] public class BattleSnapshot
    {
        public string matchId,phase="battle",opponent; public float time,supply; public int team,winner=-2,tick,publicHash; public int[] score;
        public string[] hand; public string next; public UnitState[] units; public ShotState[] shots; public ZoneState[] zones; public CombatEvent[] events; public long acknowledged;
    }
    public sealed class MatchSimulation
    {
        public readonly Catalog Catalog;
        public readonly List<UnitState> Units = new List<UnitState>();
        public readonly List<ShotState> Shots = new List<ShotState>();
        public readonly List<ZoneState> Zones = new List<ZoneState>();
        // Bounded event history preserves short-lived attacks between network snapshots.
        public readonly List<CombatEvent> Events = new List<CombatEvent>();
        int nextEvent;
        public void Emit(int kind,int source,int target,int team,float x,float z,float toX,float toZ,float amount=0)
        {
            Events.Add(new CombatEvent{id=++nextEvent,tick=Tick,kind=kind,source=source,target=target,team=team,x=x,z=z,toX=toX,toZ=toZ,amount=amount});
            if(Events.Count>256)Events.RemoveAt(0);
        }
        public float Time {get;private set;} public int Tick {get;private set;} public int Winner {get;private set;}=-2;
        public readonly float[] Supply = {5,5};
        public readonly string[][] Hands = {new string[4],new string[4]};
        readonly Queue<string>[] queues = {new Queue<string>(),new Queue<string>()};
        readonly long[] lastSequence = {-1,-1}; readonly CommandReply[] lastReply = new CommandReply[2];
        readonly List<Damage> damage = new List<Damage>(); readonly List<PendingOrder> orders = new List<PendingOrder>();
        int nextId = 1; readonly Random random;
        public const float StepSeconds = 1f/30f;
        struct Damage { public int id; public float amount; }
        class PendingOrder { public int team; public CardDef card; public float x,z,at; }
        public MatchSimulation(Catalog catalog,string[] a,string[] b,int seed=1)
        {
            if(Catalog.Validate(catalog)!=null) throw new ArgumentException("Invalid catalog");
            Catalog=catalog; random=new Random(seed);
            for(int t=0;t<2;t++) {
                var deck=t==0?a:b;
                if(deck==null || deck.Length!=8 || deck.Distinct().Count()!=8 || deck.Any(id=>catalog.Get(id)==null || !catalog.Get(id).enabled)) throw new ArgumentException("Invalid deck");
                // Shuffle the full opening deck reproducibly; no card gets a privileged slot.
                var shuffled=(string[])deck.Clone(); for(int i=7;i>0;i--){int j=random.Next(i+1);var temp=shuffled[i];shuffled[i]=shuffled[j];shuffled[j]=temp;}
                Array.Copy(shuffled,Hands[t],4); for(int i=4;i<8;i++) queues[t].Enqueue(shuffled[i]);
                float sign=t==0?-1:1;
                AddBase(t,-7,10*sign,false); AddBase(t,7,10*sign,false); AddBase(t,0,15*sign,true);
            }
        }
        void AddBase(int team,float x,float z,bool hq) {Units.Add(new UnitState{id=nextId++,team=team,card=hq?"hq":"outpost",kind=hq?"hq":"outpost",x=x,z=z,hp=hq?2800:1800,maxHp=hq?2800:1800,structure=true,hq=hq,active=!hq});}
        public int[] Score(){return new[]{Units.Count(u=>u.team==1 && u.structure && !u.hq && u.hp<=0),Units.Count(u=>u.team==0 && u.structure && !u.hq && u.hp<=0)};}
        static bool Finite(float x){return !float.IsNaN(x)&&!float.IsInfinity(x);}
        public bool Legal(int team,CardDef card,float x,float z)
        { return LegalPlacement(team,card,x,z,Units); }
        public static bool LegalPlacement(int team,CardDef card,float x,float z,IEnumerable<UnitState> units)
        {
            if(team<0||team>1||!Finite(x)||!Finite(z)||Math.Abs(x)>11||Math.Abs(z)>17)return false;
            if(IsOrder(card))return true;
            float relative=z*(team==0?1:-1);
            if(relative> -1.6f) {
                if(relative>7 || Math.Abs(x)<3.8f || Math.Abs(x)>10)return false;
                if(!units.Any(u=>u.structure&&!u.hq&&u.team!=team&&u.hp<=0&&Math.Sign(u.x)==Math.Sign(x)))return false;
            }
            if(Math.Abs(z)<1.3f && (Math.Abs(x)<5||Math.Abs(x)>9))return false;
            if(units.Any(u=>u.structure && Dist(x,z,u.x,u.z)<2.1f))return false;
            return true;
        }
        static bool IsOrder(CardDef c){return c.behavior=="grapeshot"||c.behavior=="smoke"||c.behavior=="tailwind";}
        public CommandReply Deploy(int team,DeployRequest cmd)
        {
            if(team<0||team>1||cmd==null)return new CommandReply{error="Invalid command"};
            if(cmd.sequence==lastSequence[team] && lastReply[team]!=null)return lastReply[team];
            var reply=new CommandReply{sequence=cmd.sequence};
            if(cmd.sequence<=lastSequence[team]){reply.error="Stale command";return reply;}
            lastSequence[team]=cmd.sequence;lastReply[team]=reply;
            if(Winner!=-2){reply.error="Match finished";return reply;}
            if(cmd.slot<0||cmd.slot>3){reply.error="Invalid slot";return reply;}
            var card=Catalog.Get(Hands[team][cmd.slot]);
            if(!string.IsNullOrEmpty(cmd.expectedCard)&&cmd.expectedCard!=card.id){reply.error="Hand changed. Select the card again.";return reply;}
            if(Supply[team]+.0001f<card.cost){reply.error="Not enough Supply";return reply;}
            if(!Legal(team,card,cmd.x,cmd.z)){reply.error="Choose clear water in your deployment area";return reply;}
            if(!IsOrder(card)&&Units.Count(u=>!u.structure&&u.hp>0)+card.count>96){reply.error="Fleet capacity reached";return reply;}
            Supply[team]-=card.cost;queues[team].Enqueue(card.id);Hands[team][cmd.slot]=queues[team].Dequeue();reply.accepted=true;
            if(IsOrder(card))orders.Add(new PendingOrder{team=team,card=card,x=cmd.x,z=cmd.z,at=Time+.35f});
            else for(int i=0;i<card.count;i++)Units.Add(new UnitState{id=nextId++,team=team,card=card.id,kind=card.behavior,x=cmd.x+(i-(card.count-1)*.5f)*.65f,z=cmd.z,hp=card.hp,maxHp=card.hp,spawnAt=Time+.6f,expires=card.behavior=="battery"?Time+25:0});
            // Replicated visual cue survives several network snapshots, including impact.
            if(card.behavior=="grapeshot")Zones.Add(new ZoneState{id=nextId++,team=team,kind="grapeshot",x=cmd.x,z=cmd.z,radius=card.range,expires=Time+.95f});
            return reply;
        }
        public void Surrender(int team){if(Winner==-2 && (team==0||team==1))Winner=1-team;}
        public void Step()
        {
            if(Winner!=-2)return;
            Tick++;Time=Tick*StepSeconds; float dt=StepSeconds;damage.Clear();
            float rate=Time>=240?3:Time>=120?2:1;
            for(int t=0;t<2;t++)Supply[t]=Math.Min(10,Supply[t]+dt*rate/2.8f);
            foreach(var o in orders.Where(x=>x.at<=Time).ToArray()){
                if(o.card.behavior=="grapeshot")foreach(var u in Units)if(u.hp>0&&u.team!=o.team&&Dist(o.x,o.z,u.x,u.z)<=o.card.range)damage.Add(new Damage{id=u.id,amount=u.structure?o.card.damage*.3f:o.card.damage});
                else {} // handled below; explicit block avoids ambiguous nested branches
                if(o.card.behavior!="grapeshot")Zones.Add(new ZoneState{id=nextId++,team=o.team,kind=o.card.behavior,x=o.x,z=o.z,radius=o.card.range,expires=Time+4});
                orders.Remove(o);
            }
            Zones.RemoveAll(z=>z.expires<=Time);
            foreach(var u in Units.ToArray()){
                if(u.hp<=0||Time<u.spawnAt)continue;
                if(u.expires>0&&Time>=u.expires){u.hp=0;continue;}
                var c=u.structure?null:Catalog.Get(u.card);
                if(u.hq&&!u.active)u.active=u.hp<u.maxHp||Units.Any(v=>v.team==u.team&&v.structure&&!v.hq&&v.hp<=0);
                if(!u.active)continue;
                bool repair=c!=null&&c.behavior=="repair";
                float range=u.structure?6:c.range;
                var candidates=Units.Where(v=>v.id!=u.id&&v.hp>0&&Time>=v.spawnAt&&(repair?(v.team==u.team&&!v.structure&&v.kind!="repair"&&v.hp<v.maxHp):(v.team!=u.team)));
                if(c!=null&&(c.behavior=="tank"||c.behavior=="ram"))candidates=candidates.Where(v=>v.structure);
                var target=repair?candidates.Where(v=>Dist(u.x,u.z,v.x,v.z)<8).OrderBy(v=>v.hp/v.maxHp).ThenBy(v=>v.id).FirstOrDefault():candidates.OrderBy(v=>Dist(u.x,u.z,v.x,v.z)).ThenBy(v=>v.id).FirstOrDefault();
                float reload=Zones.Any(v=>v.kind=="smoke"&&v.team!=u.team&&Dist(u.x,u.z,v.x,v.z)<v.radius)?.75f:1;
                u.cooldown-=dt*reload;
                if(target!=null){
                    float distance=Dist(u.x,u.z,target.x,target.z);
                    bool inRange=distance<=range && (c==null||distance>=c.minRange) && ClearShot(u.x,u.z,target.x,target.z);
                    if(inRange && u.cooldown<=0){
                        u.cooldown=u.structure?1:c.interval;
                        float amount=u.structure?(u.hq?60:45):c.damage;
                        Emit(repair?3:c!=null&&c.behavior=="ram"?4:1,u.id,target.id,u.team,u.x,u.z,target.x,target.z,amount);
                        if(repair)damage.Add(new Damage{id=target.id,amount=-amount});
                        else if(c!=null&&c.behavior=="ram"){damage.Add(new Damage{id=target.id,amount=u.travel>=3?300:amount});u.travel=0;}
                        else Shots.Add(new ShotState{id=nextId++,team=u.team,target=target.id,x=u.x,z=u.z,damage=amount,splash=c==null?0:c.splash,speed=15});
                    }
                    if(!u.structure&&!inRange && c.speed>0)Move(u,target.x,target.z,c.speed,dt);
                }else if(!u.structure && c.speed>0)Move(u,u.x, u.team==0?10:-10,c.speed,dt);
            }
            foreach(var s in Shots.ToArray()){
                var target=Units.Find(u=>u.id==s.target&&u.hp>0);
                if(target==null){Shots.Remove(s);continue;}
                float d=Dist(s.x,s.z,target.x,target.z),step=s.speed*dt;
                if(d<=step){Emit(2,s.id,target.id,s.team,s.x,s.z,target.x,target.z,s.damage);foreach(var u in Units)if(u.hp>0&&u.team!=s.team&&(u.id==s.target||(s.splash>0&&Dist(target.x,target.z,u.x,u.z)<=s.splash)))damage.Add(new Damage{id=u.id,amount=s.damage});Shots.Remove(s);}
                else{s.x+=(target.x-s.x)/d*step;s.z+=(target.z-s.z)/d*step;}
            }
            foreach(var group in damage.GroupBy(d=>d.id)){var u=Units.Find(v=>v.id==group.Key);if(u!=null){float amount=group.Sum(d=>d.amount);u.hp=Math.Max(0,Math.Min(u.maxHp,u.hp-amount));Emit(5,0,u.id,u.team,u.x,u.z,u.x,u.z,amount);}}
            Units.RemoveAll(u=>!u.structure&&u.hp<=0);
            bool dead0=Units.Any(u=>u.hq&&u.team==0&&u.hp<=0),dead1=Units.Any(u=>u.hq&&u.team==1&&u.hp<=0);
            if(dead0||dead1){Winner=dead0&&dead1?-1:dead0?1:0;return;}
            if(Tick>=5400){var score=Score();if(score[0]!=score[1]){Winner=score[0]>score[1]?0:1;return;}}
            if(Tick>=9000){float a=Units.Where(u=>u.team==0&&u.structure&&u.hp>0).Min(u=>u.hp),b=Units.Where(u=>u.team==1&&u.structure&&u.hp>0).Min(u=>u.hp);Winner=Math.Abs(a-b)<.001f?-1:a>b?0:1;}
        }
        void Move(UnitState u,float tx,float tz,float speed,float dt)
        {
            if(Math.Sign(u.z)!=Math.Sign(tz)&&Math.Abs(u.z)>1.1f){float lane=u.x<0?-7:7;if(Math.Abs(u.x-lane)>1.1f){tx=lane;tz=Math.Sign(u.z)*2;}}
            if(Math.Abs(u.z)<1.5f){tx=u.x<0?-7:7;tz=u.team==0?2:-2;}
            float dx=tx-u.x,dz=tz-u.z,d=(float)Math.Sqrt(dx*dx+dz*dz);if(d<.1f)return;
            if(Zones.Any(v=>v.kind=="tailwind"&&v.team==u.team&&Dist(u.x,u.z,v.x,v.z)<v.radius))speed*=1.4f;
            float amount=Math.Min(speed*dt,d);u.x+=dx/d*amount;u.z+=dz/d*amount;u.travel+=amount;
            // Lightweight local separation, no Unity physics dependency on the authority.
            foreach(var other in Units)if(other.id!=u.id&&!other.structure&&other.hp>0){float gap=Dist(u.x,u.z,other.x,other.z);if(gap>.001f&&gap<.55f){u.x+=(u.x-other.x)/gap*.18f*dt;u.z+=(u.z-other.z)/gap*.18f*dt;}}
            u.x=Math.Max(-10.8f,Math.Min(10.8f,u.x));u.z=Math.Max(-17,Math.Min(17,u.z));
        }
        public static float Dist(float x,float z,float a,float b){float dx=x-a,dz=z-b;return(float)Math.Sqrt(dx*dx+dz*dz);}
        static bool ClearShot(float x,float z,float a,float b){if(z*b>=0)return true;float f=-z/(b-z),cross=x+(a-x)*f;return Math.Abs(cross)>=4.6f&&Math.Abs(cross)<=9.4f;}
        public void Bot(int team,long sequence)
        {
            if(Winner!=-2)return;
            var affordable=Enumerable.Range(0,4).Where(i=>Catalog.Get(Hands[team][i]).cost<=Supply[team]).ToArray();if(affordable.Length==0)return;
            int slot=affordable[random.Next(affordable.Length)];var c=Catalog.Get(Hands[team][slot]);float x=random.Next(2)==0?-7:7,z=(team==0?-1:1)*(3+random.Next(5));
            if(IsOrder(c)){var target=Units.Where(u=>u.team!=team&&!u.structure).OrderBy(u=>Math.Abs(u.z)).FirstOrDefault();if(target==null)return;x=target.x;z=target.z;}
            Deploy(team,new DeployRequest{sequence=sequence,slot=slot,x=x,z=z});
        }
        public BattleSnapshot Snapshot(int team,string matchId="practice",string opponent="Training Captain")
        {
            var snapshot=new BattleSnapshot{matchId=matchId,opponent=opponent,team=team,time=Time,tick=Tick,winner=Winner,phase=Winner!=-2?"finished":Time>=180?"overtime":"battle",supply=Supply[team],hand=(string[])Hands[team].Clone(),next=queues[team].Peek(),score=Score(),units=Units.Select(Clone).ToArray(),shots=Shots.Select(s=>new ShotState{id=s.id,team=s.team,target=s.target,x=s.x,z=s.z,damage=s.damage,splash=s.splash,speed=s.speed}).ToArray(),zones=Zones.Select(z=>new ZoneState{id=z.id,team=z.team,kind=z.kind,x=z.x,z=z.z,radius=z.radius,expires=z.expires}).ToArray(),events=Events.ToArray(),acknowledged=lastSequence[team]};
            snapshot.publicHash=BattleStateHash.Compute(snapshot);return snapshot;
        }
        static UnitState Clone(UnitState u){return new UnitState{id=u.id,team=u.team,card=u.card,kind=u.kind,x=u.x,z=u.z,hp=u.hp,maxHp=u.maxHp,spawnAt=u.spawnAt,structure=u.structure,hq=u.hq,active=u.active};}
    }
}
