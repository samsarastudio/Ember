using System;
using System.Collections.Generic;
using System.Linq;
using Emberwake.Core;
using Fusion;
using UnityEngine;

namespace Emberwake.Client
{
    public struct FleetUnit : INetworkStruct
    {
        public int Id, Team, Card, Flags;
        public Vector2 Position;
        public float Hp, MaxHp, SpawnAt;
    }
    public struct FleetShot : INetworkStruct
    {
        public int Id, Team, Target;
        public Vector2 Position;
        public float Damage, Splash, Speed;
    }
    public struct FleetZone : INetworkStruct
    {
        public int Id, Team, Kind;
        public Vector2 Position;
        public float Radius, Expires;
    }
    public struct FleetEvent : INetworkStruct
    {
        public int Id, Tick, Kind, Source, Target, Team;
        public Vector2 From, To;
        public float Amount;
    }

    // One replicated world. Only StateAuthority runs the pure 30 Hz combat simulation.
    // Fusion delta-compresses these bounded arrays; cosmetic objects exist only in ArenaView.
    public sealed class FusionBattleWorld : NetworkBehaviour
    {
        [Networked] public PlayerRef CaptainA { get; set; }
        [Networked] public PlayerRef CaptainB { get; set; }
        [Networked] public int CombatTick { get; set; }
        [Networked] public int Winner { get; set; }
        [Networked] public NetworkBool Started { get; set; }
        [Networked] public int UnitCount { get; set; }
        [Networked] public int ShotCount { get; set; }
        [Networked] public int ZoneCount { get; set; }
        [Networked] public int EventCount { get; set; }
        [Networked] public int LastEventId { get; set; }
        [Networked] public int PublicHash { get; set; }
        [Networked, Capacity(102)] public NetworkArray<FleetUnit> Units => default;
        [Networked, Capacity(256)] public NetworkArray<FleetShot> Shots => default;
        [Networked, Capacity(32)] public NetworkArray<FleetZone> Zones => default;
        [Networked, Capacity(256)] public NetworkArray<FleetEvent> Events => default;

        Catalog catalog = Catalog.Default();
        readonly string[][] decks = new string[2][];
        readonly Queue<Action> commands = new Queue<Action>();
        readonly long[] receivedSequence = { -1, -1 };
        MatchSimulation simulation;
        FusionBattleSession session;
        BattleSnapshot privateState;
        float accumulator;
        int lastRendered = -1;
        int lastWinner = -3;
        public int LocalTeam => Team(Runner.LocalPlayer);
        public int Team(PlayerRef player) => player == PlayerRef.None ? -1 : player == CaptainA ? 0 : player == CaptainB ? 1 : -1;

        public override void Spawned()
        {
            session = Runner.GetComponent<FusionBattleSession>();
            session.World = this;
            catalog=session.Catalog;
            if(HasStateAuthority&&session.Assignment?.aiOpponent==true)decks[1]=session.Assignment.deckB;
            // A host also owns a captain, even though its runner is the server.
            if (LocalTeam >= 0) session.SendCommand(new FleetMessage { kind = "ready", text = string.Join(",", session.Deck) });
        }

        
        public void Ready(string csv, PlayerRef source)
        {
            int team = Team(source);
            if(team>=0&&session.Assignment!=null){decks[team]=team==0?session.Assignment.deckA:session.Assignment.deckB;return;}
            if (team < 0 || Started || csv == null || csv.Length > 400) return;
            var deck = csv.Split(',');
            // Private test duels accept only starter cards; no account ownership is trusted here.
            if (deck.Length != 8 || deck.Distinct().Count() != 8 || deck.Any(id => catalog.Get(id) == null || !catalog.Get(id).enabled || catalog.Get(id).unlockTrophies != 0))
            {
                Reply(source, false, "This test duel requires eight unique starter cards.");
                return;
            }
            decks[team] = deck;
        }

        
        public void DeployCommand(long sequence, int slot, float x, float z, string expectedCard, PlayerRef source)
        {
            int team = Team(source);
            if (team < 0 || simulation == null || commands.Count >= 32) return;
            if (sequence <= receivedSequence[team]) return;
            receivedSequence[team] = sequence;
            var player = source;
            commands.Enqueue(() =>
            {
                var reply = simulation.Deploy(team, new DeployRequest { sequence = sequence, slot = slot, x = x, z = z, expectedCard = expectedCard });
                Reply(player, reply.accepted, reply.error ?? "Fleet deployed.", sequence);
            });
        }

        
        public void SurrenderCommand(PlayerRef source)
        {
            int team = Team(source);
            if (team >= 0 && simulation != null && commands.Count < 32) commands.Enqueue(() => simulation.Surrender(team));
        }

        
        void Reply(PlayerRef target, bool accepted, string message, long sequence=0)
        {
            session.SendTo(target, new FleetMessage { kind = "reply", text = message, sequence=sequence });
        }

        // Private fields are sent only to their owner, never put on the public world object.
        
        public void ReceivePrivate(int tick, float supply, string hand, string next, long acknowledged)
        {
            if (privateState != null && privateState.tick > tick) return;
            privateState = new BattleSnapshot { tick = tick, supply = supply, hand = hand.Split(','), next = next, acknowledged = acknowledged };
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (simulation == null)
            {
                if (decks[0] == null || decks[1] == null) return;
                simulation = new MatchSimulation(catalog, decks[0], decks[1], session.Assignment?.seed??1729);
                Winner = -2;
                Started = true;
                Publish();
            }
            accumulator += Runner.DeltaTime;
            while (accumulator + .000001f >= MatchSimulation.StepSeconds)
            {
                accumulator -= MatchSimulation.StepSeconds;
                while (commands.Count > 0) commands.Dequeue()();
                if(session.Assignment?.aiOpponent==true&&simulation.Tick%42==0)simulation.Bot(1,simulation.Tick+1);
                simulation.Step();
            }
            if (CombatTick != simulation.Tick || Winner != simulation.Winner) Publish();
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority) return;
            int team = Team(player);
            if (team >= 0 && simulation != null) simulation.Surrender(team);
        }
        public void Forfeit(int team){if(HasStateAuthority&&simulation!=null)simulation.Surrender(team);}

        // Local operator view only. Never include either captain's private hand or supply.
        public BattleSnapshot ObserverSnapshot()
        {
            if(!HasStateAuthority||simulation==null)return null;
            var snapshot=simulation.Snapshot(0,session.Assignment?.matchId??Runner.SessionInfo.Name);
            snapshot.hand=null;snapshot.next=null;snapshot.supply=0;snapshot.acknowledged=0;
            return snapshot;
        }

        void Publish()
        {
            // A bound violation must fail explicitly, never silently omit authoritative entities.
            if (simulation.Units.Count > Units.Length || simulation.Shots.Count > Shots.Length || simulation.Zones.Count > Zones.Length)
                throw new InvalidOperationException("Fleet replication capacity exceeded.");
            CombatTick = simulation.Tick;
            Winner = simulation.Winner;
            UnitCount = simulation.Units.Count;
            ShotCount = simulation.Shots.Count;
            ZoneCount = simulation.Zones.Count;
            EventCount = simulation.Events.Count;
            for (int i = 0; i < UnitCount; i++)
            {
                var u = simulation.Units[i];
                Units.Set(i, new FleetUnit { Id = u.id, Team = u.team, Card = Array.FindIndex(catalog.cards, c => c.id == u.card), Flags = (u.structure ? 1 : 0) | (u.hq ? 2 : 0) | (u.active ? 4 : 0), Position = new Vector2(u.x, u.z), Hp = u.hp, MaxHp = u.maxHp, SpawnAt = u.spawnAt });
            }
            for (int i = 0; i < ShotCount; i++)
            {
                var s = simulation.Shots[i];
                Shots.Set(i, new FleetShot { Id = s.id, Team = s.team, Target = s.target, Position = new Vector2(s.x, s.z), Damage = s.damage, Splash = s.splash, Speed = s.speed });
            }
            for (int i = 0; i < ZoneCount; i++)
            {
                var z = simulation.Zones[i];
                Zones.Set(i, new FleetZone { Id = z.id, Team = z.team, Kind = z.kind == "grapeshot" ? 0 : z.kind == "smoke" ? 1 : 2, Position = new Vector2(z.x, z.z), Radius = z.radius, Expires = z.expires });
            }
            int previousEvent = LastEventId;
            for (int i = 0; i < EventCount; i++)
            {
                var e = simulation.Events[i];
                if(e.id<=previousEvent)continue;
                Events.Set(e.id % Events.Length, new FleetEvent { Id = e.id, Tick = e.tick, Kind = e.kind, Source = e.source, Target = e.target, Team = e.team, From = new Vector2(e.x, e.z), To = new Vector2(e.toX, e.toZ), Amount = e.amount });
                LastEventId=e.id;
            }
            PublicHash = CalculateHash();
            if (CombatTick % 6 == 0 || Winner != -2)
                for (int team = 0; team < 2; team++)
                {
                    var player = team == 0 ? CaptainA : CaptainB;
                    if (!Runner.ActivePlayers.Contains(player)) continue;
                    var s = simulation.Snapshot(team);
                    session.SendTo(player, new FleetMessage { kind = "private", tick = s.tick, supply = s.supply, text = string.Join(",", s.hand), next = s.next, sequence = s.acknowledged });
                }
        }

        public int CalculateHash()
        {
            // Integer quantization makes the audit independent of float string formatting.
            unchecked
            {
                int hash = 17;
                void Add(int value) { hash = hash * 31 + value; }
                void Float(float value) { Add(Mathf.RoundToInt(value * 1000)); }
                Add(CombatTick); Add(Winner); Add(UnitCount); Add(ShotCount); Add(ZoneCount); Add(EventCount);
                for (int i = 0; i < UnitCount; i++) { var u = Units[i]; Add(u.Id); Add(u.Team); Add(u.Card); Add(u.Flags); Float(u.Position.x); Float(u.Position.y); Float(u.Hp); Float(u.MaxHp); Float(u.SpawnAt); }
                for (int i = 0; i < ShotCount; i++) { var s = Shots[i]; Add(s.Id); Add(s.Team); Add(s.Target); Float(s.Position.x); Float(s.Position.y); Float(s.Damage); Float(s.Splash); Float(s.Speed); }
                for (int i = 0; i < ZoneCount; i++) { var z = Zones[i]; Add(z.Id); Add(z.Team); Add(z.Kind); Float(z.Position.x); Float(z.Position.y); Float(z.Radius); Float(z.Expires); }
                for (int i = 0; i < EventCount; i++) { var e = Events[(LastEventId - EventCount + 1 + i) % Events.Length]; Add(e.Id); Add(e.Tick); Add(e.Kind); Add(e.Source); Add(e.Target); Add(e.Team); Float(e.From.x); Float(e.From.y); Float(e.To.x); Float(e.To.y); Float(e.Amount); }
                return hash;
            }
        }

        public override void Render()
        {
            if (!Started || privateState == null || LocalTeam < 0 || (lastRendered == CombatTick && lastWinner == Winner)) return;
            lastRendered = CombatTick;
            lastWinner = Winner;
            if (CalculateHash() != PublicHash) { session.Fail("Replicated combat state checksum mismatch."); return; }
            var s = new BattleSnapshot
            {
                matchId = Runner.SessionInfo.Name, opponent = session.Opponent??"Captain " + (2 - LocalTeam), team = LocalTeam,
                tick = CombatTick, time = CombatTick * MatchSimulation.StepSeconds, winner = Winner,
                phase = Winner != -2 ? "finished" : CombatTick >= 5400 ? "overtime" : "battle",
                supply = privateState.supply, hand = privateState.hand, next = privateState.next, acknowledged = privateState.acknowledged,
                units = new UnitState[UnitCount], shots = new ShotState[ShotCount], zones = new ZoneState[ZoneCount], events = new CombatEvent[EventCount]
            };
            for (int i = 0; i < UnitCount; i++)
            {
                var u = Units[i]; bool structure = (u.Flags & 1) != 0, hq = (u.Flags & 2) != 0;
                var card = structure ? null : catalog.cards[u.Card];
                s.units[i] = new UnitState { id = u.Id, team = u.Team, card = structure ? hq ? "hq" : "outpost" : card.id, kind = structure ? hq ? "hq" : "outpost" : card.behavior, x = u.Position.x, z = u.Position.y, hp = u.Hp, maxHp = u.MaxHp, spawnAt = u.SpawnAt, structure = structure, hq = hq, active = (u.Flags & 4) != 0 };
            }
            for (int i = 0; i < ShotCount; i++) { var p = Shots[i]; s.shots[i] = new ShotState { id = p.Id, team = p.Team, target = p.Target, x = p.Position.x, z = p.Position.y, damage = p.Damage, splash = p.Splash, speed = p.Speed }; }
            for (int i = 0; i < ZoneCount; i++) { var z = Zones[i]; s.zones[i] = new ZoneState { id = z.Id, team = z.Team, kind = z.Kind == 0 ? "grapeshot" : z.Kind == 1 ? "smoke" : "tailwind", x = z.Position.x, z = z.Position.y, radius = z.Radius, expires = z.Expires }; }
            for (int i = 0; i < EventCount; i++) { var e = Events[(LastEventId - EventCount + 1 + i) % Events.Length]; s.events[i] = new CombatEvent { id = e.Id, tick = e.Tick, kind = e.Kind, source = e.Source, target = e.Target, team = e.Team, x = e.From.x, z = e.From.y, toX = e.To.x, toZ = e.To.y, amount = e.Amount }; }
            s.score = new[] { s.units.Count(u => u.team == 1 && u.structure && !u.hq && u.hp <= 0), s.units.Count(u => u.team == 0 && u.structure && !u.hq && u.hp <= 0) };
            session.Deliver(s);
        }
    }
}
