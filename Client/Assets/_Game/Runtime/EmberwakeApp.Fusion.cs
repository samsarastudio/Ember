using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Emberwake.Core;
using Fusion;
using UnityEngine;

namespace Emberwake.Client
{
    public sealed partial class EmberwakeApp
    {
        FusionBattleSession fusionSession;
        bool fusionMatch;
        bool rankedMatch;
        string fusionRegion = "us";
        long testSequence;
        int testNextDeploy, testSnapshots, testAttacks, testImpacts, testDamage;
        int testLastEvent;
        readonly HashSet<int> testTeams = new HashSet<int>();
        bool testFinished, testCaptured;

        void FusionLobby()
        {
            Clear("fusion-lobby");
            Header("Duel with a captain.", "Private multiplayer • No rewards or trophies");
            Label(page,"Both captains must use the same region and room code.",45,285,810,90,28);
            string[] codes = { "us", "usw", "cae" }, names = { "US East", "US West", "Canada East" };
            for(int i=0;i<3;i++)
            {
                string code=codes[i];
                Button(page,names[i]+(fusionRegion==code?" ✓":""),45+i*275,395,260,75,()=>{fusionRegion=code;FusionLobby();},fusionRegion==code);
            }
            var room = Field(page, "", "Room code", 45, 530, 810);
            Button(page,"Join duel",45,635,810,90,()=>JoinFusion(false,room.text,fusionRegion),true);
            Button(page,"Host a test duel",45,750,810,85,()=>JoinFusion(true,room.text,fusionRegion));
            Label(page,"Hosting uses this device for the match. Keep it open.\nFor dedicated-server tests, launch the server first and have both captains choose Join duel.",45,870,810,175,27);
            Button(page,"Back to harbor",45,1200,810,85,Home);
        }

        void RankedBattle()
        {
            if(!service.Online){Toast("Connect to your account in Settings first.");return;}
            if(service.Bootstrap.battleTransport!="fusion"){Queue("");return;}
            Clear("ranked-queue");Header("Finding your rival.","Matching captains by trophies");
            Label(page,"READY YOUR FLEET\nSearching for a captain near your trophy count…",45,550,810,220,35,gold,TextAnchor.MiddleCenter);
            Button(page,"Cancel search",45,1190,810,90,()=>StartCoroutine(CancelRanked()));
            queueRoutine=StartCoroutine(WaitRanked());
        }
        IEnumerator CancelRanked()
        {
            if(queueRoutine!=null)StopCoroutine(queueRoutine);
            yield return service.Request<FusionQueueReply>("DELETE","/v1/fusion/queue",null,_=>{});Home();
        }
        IEnumerator WaitRanked()
        {
            FusionQueueReply q=null;
            yield return service.Request<FusionQueueReply>("POST","/v1/fusion/queue",null,r=>q=r);
            while(screen=="ranked-queue")
            {
                if(service.Error!=null){Toast(service.Error);yield break;}
                if(q?.status=="matched"){JoinFusion(false,q.room,q.region,q);yield break;}
                if(q?.status=="failed"||q?.status=="idle"||q?.status=="complete"){Toast(q.error??"Search ended. Please search again.");yield break;}
                yield return new WaitForSecondsRealtime(2);
                yield return service.Request<FusionQueueReply>("GET","/v1/fusion/queue",null,r=>q=r);
            }
        }
        IEnumerator RefreshRankedResult()
        {
            for(int i=0;i<15;i++)
            {
                FusionQueueReply q=null;yield return service.Request<FusionQueueReply>("GET","/v1/fusion/queue",null,r=>q=r);
                if(q?.status=="complete"){yield return RefreshResult();yield break;}
                if(q?.status=="failed"){Toast(q.error);yield break;}
                yield return new WaitForSecondsRealtime(1);
            }
            Toast("Result is still being saved. Your progress will refresh when you reconnect.");
        }

        async void JoinFusion(bool host, string room, string region, FusionQueueReply assignment=null, int retries=15)
        {
            if (fusionSession != null) return;
            fusionMatch=true;practice=false;sequence=0;
            rankedMatch=assignment!=null;
            // The pilot uses the pinned built-in balance catalog on every peer.
            service.Catalog=assignment?.catalog??Catalog.Default();art.Reload(service.Catalog);
            fusionSession=new GameObject("Fusion duel session").AddComponent<FusionBattleSession>();
            var current=fusionSession;
            current.Deck=service.Profile.deck;
            current.Catalog=service.Catalog;current.Ticket=assignment?.ticket;current.Opponent=assignment?.opponent;
            current.Message=Toast;
            Clear("fusion-wait");Header("Preparing the duel.","Room: "+room+"  •  "+region.ToUpperInvariant());
            Label(page,"Waiting for two captains…",45,600,810,110,38,gold);
            Button(page,"Cancel",45,1190,810,90,()=>{LeaveFusion();Home();});
            current.Snapshot=s=>
            {
                if(fusionSession!=current)return;
                if(screen=="fusion-wait"){BeginBattle();}
                if(screen!="battle")return;
                snap=s;sequence=Math.Max(sequence,s.acknowledged);lastSnapshotAt=Time.unscaledTime;ApplySnapshot();
                if(FusionBattleSession.Flag("-fusion-autotest"))AuditFusion(s);
            };
            bool connected=await current.Connect(host?GameMode.Host:GameMode.Client,room,region);
            if(fusionSession!=current){await current.Close();return;}
            // Photon may still count the old connection for a few seconds after an abrupt drop.
            // Retry the existing authenticated allocation rather than creating a second battle.
            if(!connected&&assignment!=null&&retries>0)
            {
                await current.Close();
                if(fusionSession!=current)return;
                fusionSession=null;
                await System.Threading.Tasks.Task.Delay(1000);
                if(screen=="fusion-wait")JoinFusion(false,room,region,assignment,retries-1);
                return;
            }
            if(!connected)Toast(current.Error);
            if(FusionBattleSession.Flag("-fusion-autotest"))StartCoroutine(FusionTestTimeout());
        }

        async void LeaveFusion()
        {
            var old=fusionSession;fusionSession=null;
            if(old!=null)await old.Close();
        }

        IEnumerator StartFusionServer()
        {
            Application.targetFrameRate=60;
            fusionSession=new GameObject("Dedicated match server").AddComponent<FusionBattleSession>();
            string assignmentPath=Environment.GetEnvironmentVariable("EMBERWAKE_ASSIGNMENT");
            string directory=null;
            if(!string.IsNullOrEmpty(assignmentPath))
            {
                var assignment=JsonUtility.FromJson<FusionAssignment>(System.IO.File.ReadAllText(assignmentPath));
                fusionSession.Assignment=assignment;fusionSession.Catalog=assignment.catalog;
                directory=System.IO.Path.GetDirectoryName(assignmentPath);
            }
            var task=fusionSession.Connect(GameMode.Server,FusionBattleSession.Arg("-fusion-room","EMBERWAKE"),FusionBattleSession.Arg("-fusion-region","us"));
            while(!task.IsCompleted)yield return null;
            if(task.IsFaulted||!task.Result){Debug.LogError("FUSION_SERVER_START_FAILED");Application.Quit(1);yield break;}
            if(directory!=null)System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"ready"),"ready");
            float deadline=Time.realtimeSinceStartup+180;
            float nextObserver=0;
            while(Time.realtimeSinceStartup<deadline||!FusionBattleSession.Flag("-fusion-autotest"))
            {
                var world=fusionSession.World;
                if(directory!=null&&world&&world.Started&&(Time.realtimeSinceStartup>=nextObserver||world.Winner!=-2))
                {
                    nextObserver=Time.realtimeSinceStartup+.2f;
                    try
                    {
                        string observer=System.IO.Path.Combine(directory,"observer.json");
                        System.IO.File.WriteAllText(observer+".tmp",JsonUtility.ToJson(world.ObserverSnapshot()));
                        if(System.IO.File.Exists(observer))System.IO.File.Replace(observer+".tmp",observer,null);
                        else System.IO.File.Move(observer+".tmp",observer);
                    }
                    catch(System.IO.IOException){ /* Operator viewer must never interrupt combat. */ }
                }
                if(directory!=null&&Time.realtimeSinceStartup>120&&(!world||!world.Started)){Debug.LogError("MATCH_ADMISSION_TIMEOUT");Application.Quit(1);yield break;}
                if(world&&world.Started&&world.Winner!=-2)
                {
                    Debug.Log($"FUSION_SERVER_RESULT tick={world.CombatTick} winner={world.Winner} hash={world.PublicHash}");
                    if(directory!=null)
                    {
                        string path=System.IO.Path.Combine(directory,"result.json");
                        System.IO.File.WriteAllText(path+".tmp",JsonUtility.ToJson(new FusionOutcome{matchId=fusionSession.Assignment.matchId,tick=world.CombatTick,winner=world.Winner,hash=world.PublicHash}));
                        System.IO.File.Move(path+".tmp",path);
                    }
                    yield return new WaitForSecondsRealtime(10);
                    var close=fusionSession.Close();while(!close.IsCompleted)yield return null;
                    Application.Quit(0);yield break;
                }
                yield return null;
            }
            Debug.LogError("FUSION_SERVER_TEST_TIMEOUT");Application.Quit(1);
        }

        void AuditFusion(BattleSnapshot s)
        {
            testSequence=Math.Max(testSequence,s.acknowledged);
            testSnapshots++;
            foreach(var u in s.units.Where(u=>!u.structure))testTeams.Add(u.team);
            foreach(var e in s.events??Array.Empty<CombatEvent>())
            {
                if(e.id<=testLastEvent)continue;
                testLastEvent=e.id;
                if(e.kind==1)testAttacks++;
                if(e.kind==2)testImpacts++;
                if(e.kind==5&&e.amount>0)testDamage++;
            }
            if(s.tick>=testNextDeploy&&s.winner==-2)
            {
                testNextDeploy=s.tick+30;
                int slot=Array.FindIndex(s.hand,id=>service.Catalog.Get(id).cost<=s.supply && service.Catalog.Get(id).hp>0);
                if(slot<0)slot=Array.FindIndex(s.hand,id=>service.Catalog.Get(id).cost<=s.supply);
                if(slot>=0)
                {
                    var card=service.Catalog.Get(s.hand[slot]);
                    float z=(s.team==0?-1:1)*3;
                    if(card.hp==0)z=-z;
                    var command=new DeployRequest{sequence=++testSequence,slot=slot,x=-7,z=z,expectedCard=card.id};
                    if(fusionMatch)fusionSession.Deploy(command);else service.Deploy(command);
                }
            }
            if(s.tick>=900&&!testCaptured&&!FusionBattleSession.Flag("-nographics"))
            {
                testCaptured=true;
                string path=FusionBattleSession.Arg("-fusion-capture");
                if(!string.IsNullOrEmpty(path)){System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));CaptureFrame(path);}
            }
            if(s.tick>=1800&&(s.team==1||FusionBattleSession.Flag("-ai-autotest"))&&s.winner==-2){if(fusionMatch)fusionSession.Surrender();else StartCoroutine(service.Request<ErrorResponse>("POST","/v1/matches/"+matchId+"/surrender",null,_=>{}));}
            if(s.winner!=-2&&!testFinished)
            {
                testFinished=true;
                bool ok=testSnapshots>100&&testTeams.Count==2&&testAttacks>0&&testImpacts>0&&testDamage>0&&(fusionMatch?fusionSession.Error==null:BattleStateHash.Compute(s)==s.publicHash);
                Debug.Log($"{(fusionMatch?"FUSION":"PI")}_CLIENT_RESULT team={s.team} tick={s.tick} winner={s.winner} hash={(fusionMatch?fusionSession.World.PublicHash:s.publicHash)} snapshots={testSnapshots} attacks={testAttacks} impacts={testImpacts} damage={testDamage} bothFleets={testTeams.Count==2} pass={ok}");
                if(fusionMatch&&fusionSession.Runner.IsServer)Debug.Log($"FUSION_SERVER_RESULT tick={s.tick} winner={s.winner} hash={fusionSession.World.PublicHash}");
                StartCoroutine(ExitFusionTest(ok?0:1));
            }
        }
        IEnumerator ExitFusionTest(int code){yield return new WaitForSecondsRealtime(3);LeaveFusion();Application.Quit(code);}
        IEnumerator FusionTestTimeout(){yield return new WaitForSecondsRealtime(150);if(!testFinished){Debug.LogError("FUSION_CLIENT_TEST_TIMEOUT "+fusionSession?.Error);Application.Quit(1);}}
    }
}
