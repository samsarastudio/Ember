using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Threading;
using Emberwake.Core;
using Fusion;
using Fusion.Sockets;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace Emberwake.Client
{
    [Serializable] public sealed class FleetMessage
    {
        public string kind, text, next;
        public long sequence;
        public int slot, tick;
        public float x, z, supply;
    }
    public sealed class FusionBattleSession : MonoBehaviour, INetworkRunnerCallbacks
    {
        public NetworkRunner Runner { get; private set; }
        public FusionBattleWorld World;
        public string[] Deck = new Profile().deck;
        public Catalog Catalog = Catalog.Default();
        public FusionAssignment Assignment;
        public string Ticket, Opponent;
        readonly Dictionary<PlayerRef,int> assignedTeams=new Dictionary<PlayerRef,int>();
        readonly Dictionary<int,float> reservations=new Dictionary<int,float>();
        readonly float[] disconnectedAt={-1,-1};
        public Action<BattleSnapshot> Snapshot;
        public Action<string> Message;
        public string Error { get; private set; }
        public string Status { get; private set; } = "Connecting…";
        bool closing, spawned;
        int messageId; readonly Dictionary<long,float> deploymentTimes=new Dictionary<long,float>();
        readonly CancellationTokenSource connecting=new CancellationTokenSource();
        public static string Arg(string name, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        public static bool Flag(string name) => Environment.GetCommandLineArgs().Contains(name);

        public async Task<bool> Connect(GameMode mode, string room, string region)
        {
            if (Runner != null) throw new InvalidOperationException("Use a fresh session for each connection.");
            if (string.IsNullOrWhiteSpace(room) || room.Length > 40 || room.Any(c => !char.IsLetterOrDigit(c) && c != '-')) { Fail("Use a room code of 1–40 letters, numbers or hyphens."); return false; }
            if (region != "us" && region != "usw" && region != "cae") { Fail("Choose US East, US West or Canada East."); return false; }
            Application.runInBackground = true;
            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.AddCallbacks(this);
            var settings = PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.AppVersion = "emberwake-combat-1";
            settings.FixedRegion = region;
            // Use the loaded config so its runtime prefab table remains attached.
            // Each player process owns one runner; test conditions are process-local.
            var config=NetworkProjectConfig.Global;
            if(Flag("-fusion-impaired"))
            {
                config.NetworkConditions.Enabled=true;
                config.NetworkConditions.DelayMin=config.NetworkConditions.DelayMax=.08f;
                config.NetworkConditions.AdditionalJitter=.02f;
                config.NetworkConditions.LossChanceMin=config.NetworkConditions.LossChanceMax=.02f;
                config.NetworkConditions.AdditionalLoss=0;
                Debug.Log("FUSION_IMPAIRED delay=80ms jitter=0-20ms loss=2% (configured packet simulation, not measured RTT)");
            }
            var result = await Runner.StartGame(new StartGameArgs
            {
                // One transient reconnect seat; admission still binds exactly two account teams.
                GameMode = mode, SessionName = room.ToUpperInvariant(), PlayerCount = Assignment!=null?3:2,
                IsVisible = false, EnableClientSessionCreation = false, CustomPhotonAppSettings = settings,
                Config=config, StartGameCancellationToken=connecting.Token,
                ConnectionToken=string.IsNullOrEmpty(Ticket)?null:Encoding.UTF8.GetBytes(Ticket)
            });
            if (!result.Ok) { Fail("Cannot join duel: " + result.ShutdownReason); return false; }
            Status = "Connected • waiting for both captains";
            Debug.Log($"FUSION_BATTLE_CONNECTED role={mode} region={region} room={room}");
            return true;
        }
        void Update()
        {
            if (Runner == null || !Runner.IsRunning || !Runner.IsServer) return;
            if(spawned)
            {
                for(int t=0;t<2;t++)if(disconnectedAt[t]>=0&&Time.realtimeSinceStartup-disconnectedAt[t]>30)
                { World.Forfeit(t);disconnectedAt[t]=-1; }
                return;
            }
            var players = Runner.ActivePlayers.Where(p=>Assignment==null||assignedTeams.ContainsKey(p)).OrderBy(p => p.RawEncoded).ToArray();
            if (players.Length != (Assignment?.aiOpponent==true?1:2)) return;
            var prefab = Resources.Load<NetworkObject>("FusionBattleWorld");
            if (!prefab) { Fail("Combat prefab missing. Rebuild the game."); return; }
            spawned = true;
            Runner.SessionInfo.IsOpen = Assignment!=null;
            Runner.Spawn(prefab, onBeforeSpawned: (runner, obj) =>
            {
                var world = obj.GetComponent<FusionBattleWorld>();
                world.CaptainA = Assignment==null?players[0]:players.First(p=>assignedTeams[p]==0);
                world.CaptainB = Assignment?.aiOpponent==true?PlayerRef.None:Assignment==null?players[1]:players.First(p=>assignedTeams[p]==1); world.Winner = -2;
            });
        }
        public void Deliver(BattleSnapshot snapshot) { Status = "Live duel"; Snapshot?.Invoke(snapshot); }
        public void Notify(string message) { Message?.Invoke(message); }
        public void Fail(string error) { Error = error; Status = error; Debug.LogError("FUSION_BATTLE_ERROR " + error); Notify(error); }
        public void Deploy(DeployRequest command)
        {
            if (!World || !World.Started || World.Winner != -2) { Notify("Waiting for the match server."); return; }
            if(deploymentTimes.Count>64)deploymentTimes.Clear();deploymentTimes[command.sequence]=Time.realtimeSinceStartup;
            SendCommand(new FleetMessage { kind="deploy", sequence=command.sequence, slot=command.slot, x=command.x, z=command.z, text=command.expectedCard });
        }
        public void Surrender() { if (World) SendCommand(new FleetMessage { kind="surrender" }); }
        // Small reliable command/private-state messages avoid the SDK 2.1.3 Mono RPC
        // CheckInvokeRpc access failure. Public combat uses normal Networked delta state.
        public void SendCommand(FleetMessage message)
        {
            if (Runner.IsServer) ReceiveCommand(Runner.LocalPlayer,message);
            else Runner.SendReliableDataToServer(ReliableKey.FromInts(++messageId,0,0,0),Encoding.UTF8.GetBytes(JsonUtility.ToJson(message)));
        }
        public void SendTo(PlayerRef player,FleetMessage message)
        {
            if(!Runner.IsServer)throw new InvalidOperationException("Only the match authority sends private state.");
            if(player==Runner.LocalPlayer)ReceiveState(message);
            else Runner.SendReliableDataToPlayer(player,ReliableKey.FromInts(++messageId,0,0,0),Encoding.UTF8.GetBytes(JsonUtility.ToJson(message)));
        }
        void ReceiveCommand(PlayerRef source,FleetMessage message)
        {
            if(!World||!Runner.IsServer||World.Team(source)<0)return;
            if(message.kind=="ready")World.Ready(message.text,source);
            else if(message.kind=="deploy")World.DeployCommand(message.sequence,message.slot,message.x,message.z,message.text,source);
            else if(message.kind=="surrender")World.SurrenderCommand(source);
        }
        void ReceiveState(FleetMessage message)
        {
            if(message.kind=="reply"){if(deploymentTimes.TryGetValue(message.sequence,out var sent)){Debug.Log("FUSION_DEPLOY_ACK_MS "+Mathf.RoundToInt((Time.realtimeSinceStartup-sent)*1000));deploymentTimes.Remove(message.sequence);}Notify(message.text);}
            else if(message.kind=="private"&&World)World.ReceivePrivate(message.tick,message.supply,message.text,message.next,message.sequence);
        }
        public async Task Close()
        {
            closing = true;
            connecting.Cancel();
            if (Runner != null && Runner.IsRunning) await Runner.Shutdown();
            Destroy(gameObject);
        }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if(Assignment!=null)
            {
                if(assignedTeams.TryGetValue(player,out int team)){assignedTeams.Remove(player);reservations.Remove(team);disconnectedAt[team]=Time.realtimeSinceStartup;}
                return;
            }
            if (World) World.PlayerLeft(player);
        }
        int TicketTeam(byte[] token)
        {
            if(Assignment==null||token==null||token.Length!=64)return -1;
            // Fixed work for both tickets; no client supplied player/team identifiers are trusted.
            int found=-1;
            for(int t=0;t<2;t++){if(Assignment.aiOpponent&&t==1)continue;var expected=Encoding.UTF8.GetBytes(Assignment.tickets[t]);if(expected.Length!=64)continue;int diff=0;for(int i=0;i<64;i++)diff|=token[i]^expected[i];if(diff==0)found=t;}
            return found;
        }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if(Assignment==null||!runner.IsServer)return;
            int team=TicketTeam(runner.GetPlayerConnectionToken(player));
            if(team<0){runner.Disconnect(player);return;}
            assignedTeams[player]=team;disconnectedAt[team]=-1;reservations.Remove(team);
            if(World){if(team==0)World.CaptainA=player;else World.CaptainB=player;}
        }
        public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { if (!closing) Fail("Duel connection closed: " + reason + ". Return to harbor to join a new duel."); }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { if (!closing) Fail(string.IsNullOrEmpty(Ticket)?"Server disconnected. This test duel cannot resume.":"Connection lost. Return to harbor and tap Battle within 30 seconds to resume."); }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if(Assignment==null){if(!spawned&&runner.ActivePlayers.Count()<2)request.Accept();else request.Refuse();return;}
            int team=TicketTeam(token);
            if(team<0||(World&&World.Winner!=-2)||(reservations.TryGetValue(team,out float until)&&Time.realtimeSinceStartup<until)){request.Refuse();return;}
            foreach(var old in assignedTeams.Where(p=>p.Value==team).Select(p=>p.Key).ToArray())
            {assignedTeams.Remove(old);runner.Disconnect(old);disconnectedAt[team]=Time.realtimeSinceStartup;}
            reservations[team]=Time.realtimeSinceStartup+10;request.Accept();
        }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { Fail("Connection failed: " + reason); }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> list) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { Fail("Host disconnected. Start a new test duel."); }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if(data.Length>2048)return;
            try
            {
                var message=JsonUtility.FromJson<FleetMessage>(Encoding.UTF8.GetString(data.ToArray()));
                if(message==null)return;
                if(runner.IsServer)ReceiveCommand(player,message);else ReceiveState(message);
            }
            catch(ArgumentException){Notify("Malformed network message rejected.");}
        }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
    }
}
