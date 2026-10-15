using System;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace Emberwake.Client
{
    // Isolated connectivity probe: no account data, combat or rewards.
    public sealed class FusionConnectionProbe : MonoBehaviour
    {
        static string Arg(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        async void Start()
        {
            try
            {
                Application.runInBackground = true;
                var region = Arg("-fusionRegion", "us");
                if (region != "us" && region != "usw" && region != "cae") throw new ArgumentException("Use us, usw or cae for this North America pilot.");
                var server = Arg("-fusionRole", "client") == "server";
                var runner = gameObject.AddComponent<NetworkRunner>();
                var settings = new FusionAppSettings
                {
                    AppIdFusion = "0e92780c-3f6a-4aae-8179-74f2064bd14a",
                    AppVersion = "emberwake-connectivity-1",
                    UseNameServer = true,
                    FixedRegion = region
                };
                var result = await runner.StartGame(new StartGameArgs
                {
                    GameMode = server ? GameMode.Server : GameMode.Client,
                    SessionName = Arg("-fusionSession", "emberwake-dev-probe"),
                    PlayerCount = 2,
                    IsVisible = false,
                    EnableClientSessionCreation = false,
                    CustomPhotonAppSettings = settings
                });
                if (!result.Ok) throw new Exception(result.ToString());
                Debug.Log($"FUSION_PROBE_CONNECTED role={(server ? "server" : "client")} region={runner.SessionInfo.Region}");
                var deadline = Time.realtimeSinceStartup + 90;
                while (runner.IsRunning && Time.realtimeSinceStartup < deadline)
                {
                    var count = runner.ActivePlayers.Count();
                    if (count == 2)
                    {
                        Debug.Log("FUSION_PROBE_TWO_PLAYERS_OK");
                        await Task.Delay(server ? 10000 : 3000);
                        await runner.Shutdown();
                        Application.Quit(0);
                        return;
                    }
                    await Task.Delay(500);
                }
                throw new Exception("Two players did not remain connected within the probe timeout.");
            }
            catch (Exception exception)
            {
                Debug.LogError("FUSION_PROBE_FAILED " + exception);
                Application.Quit(1);
            }
        }
    }
}
