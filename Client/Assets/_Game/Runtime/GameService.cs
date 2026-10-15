using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Emberwake.Core;

namespace Emberwake.Client
{
    [Serializable] public class ClientConfig { public string apiUrl="http://localhost:5080"; public int clientVersion=1; }
    [Serializable] public class SocketMessage { public string kind; public BattleSnapshot snapshot; public CommandReply reply; }
    public sealed class GameService : MonoBehaviour
    {
        public Catalog Catalog=Catalog.Default(); public Profile Profile=new Profile(); public Bootstrap Bootstrap=new Bootstrap();
        public string Origin,Token,Error; public bool Online; public BattleSnapshot Snapshot;
        public string CredentialKey=>"credential:"+Origin;
        public const string ProductionOrigin="https://royale.inmomentservices.com";
        static string TestCredential(string fallback)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return FusionBattleSession.Arg("-test-credential",fallback);
#else
            return fallback;
#endif
        }
        public void AcceptAccount(AuthResponse a){Token=a.token;Profile=a.profile;PlayerPrefs.SetString(CredentialKey,a.credential);PlayerPrefs.Save();Online=true;}
        public readonly ConcurrentQueue<SocketMessage> Messages=new ConcurrentQueue<SocketMessage>();
        ClientWebSocket socket; CancellationTokenSource stop; readonly SemaphoreSlim sendLock=new SemaphoreSlim(1,1);
        public string CachePath => Path.Combine(Application.persistentDataPath,"catalog.json");
        public IEnumerator Connect(Action done)
        {
            Origin=ProductionOrigin;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Origin=FusionBattleSession.Arg("-api",ProductionOrigin).TrimEnd('/');
#endif
            // Retire old developer URL preferences; release clients always use production.
            PlayerPrefs.DeleteKey("api");
            Token=null;Online=false;
            // Preserve captains from the original localhost preview, without sending its credential to a new host.
            if((Origin=="http://localhost:5080"||Origin=="http://127.0.0.1:5080")&&!PlayerPrefs.HasKey(CredentialKey)&&PlayerPrefs.HasKey("credential"))
            {PlayerPrefs.SetString(CredentialKey,PlayerPrefs.GetString("credential"));PlayerPrefs.DeleteKey("credential");PlayerPrefs.Save();}
            if(File.Exists(CachePath))try {var cached=JsonUtility.FromJson<Catalog>(File.ReadAllText(CachePath));if(Core.Catalog.Validate(cached)==null)Catalog=cached;}catch(Exception e){Debug.LogWarning(e.Message);}
            yield return Request<Bootstrap>("GET","/v1/bootstrap",null,b=>Bootstrap=b);
            if(Error==null && Bootstrap.minClientVersion<=1){
                yield return Request<Catalog>("GET","/v1/catalog",null,c=>{if(Core.Catalog.Validate(c)!=null || c.minClientVersion>1)throw new Exception("A newer game version is required.");Catalog=c;try{File.WriteAllText(CachePath,JsonUtility.ToJson(c));}catch(IOException e){Debug.LogWarning("Catalog cache unavailable: "+e.Message);}});
                if(Error==null)yield return Request<AuthResponse>("POST","/v1/auth/guest",new AuthRequest{credential=TestCredential(PlayerPrefs.GetString(CredentialKey,"")),name="Captain"},AcceptAccount);
            }else if(Bootstrap.minClientVersion>1)Error="A newer game version is required.";
            if(!Online)Profile.Unlock(Catalog);
            done();
        }
        public IEnumerator Request<T>(string method,string path,object body,Action<T> success)
        {
            Error=null;
            using(var req=new UnityWebRequest(Origin+path,method)){
                req.downloadHandler=new DownloadHandlerBuffer();req.timeout=12;
                if(body!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));req.SetRequestHeader("Content-Type","application/json");}
                if(!string.IsNullOrEmpty(Token))req.SetRequestHeader("Authorization","Bearer "+Token);
                yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success){Error="Cannot reach the harbor service. Check connection and retry.";try{var err=JsonUtility.FromJson<ErrorResponse>(req.downloadHandler.text);if(!string.IsNullOrEmpty(err.error))Error=err.error;}catch{}yield break;}
                try{success?.Invoke(JsonUtility.FromJson<T>(req.downloadHandler.text));}catch(Exception e){Error=e.Message;}
            }
        }
        public async void OpenMatch(string id)
        {
            CloseMatch(); stop=new CancellationTokenSource();var cancellation=stop.Token;
            var current=new ClientWebSocket();socket=current;current.Options.SetRequestHeader("Authorization","Bearer "+Token);
            try{
                await current.ConnectAsync(new Uri(Origin.Replace("https://","wss://").Replace("http://","ws://")+"/v1/matches/"+id+"/stream"),cancellation);
                byte[] buffer=new byte[262144];
                while(current.State==WebSocketState.Open){int count=0;WebSocketReceiveResult r;do{r=await current.ReceiveAsync(new ArraySegment<byte>(buffer,count,buffer.Length-count),cancellation);if(r.MessageType==WebSocketMessageType.Close)throw new Exception("Connection closed. Reconnect to resume.");count+=r.Count;if(count==buffer.Length&&!r.EndOfMessage)throw new Exception("Invalid server message.");}while(!r.EndOfMessage);
                    Messages.Enqueue(JsonUtility.FromJson<SocketMessage>(Encoding.UTF8.GetString(buffer,0,count)));
                }
            }catch(OperationCanceledException){}catch(Exception e){Messages.Enqueue(new SocketMessage{kind="error",reply=new CommandReply{error=e.Message}});}
        }
        public async void Deploy(DeployRequest cmd)
        {
            var current=socket;if(current==null||current.State!=WebSocketState.Open){Messages.Enqueue(new SocketMessage{kind="error",reply=new CommandReply{error="Disconnected. Reconnect to resume."}});return;}
            try{await sendLock.WaitAsync();try{var data=Encoding.UTF8.GetBytes(JsonUtility.ToJson(cmd));await current.SendAsync(new ArraySegment<byte>(data),WebSocketMessageType.Text,true,stop.Token);}finally{sendLock.Release();}}catch(Exception e){Messages.Enqueue(new SocketMessage{kind="error",reply=new CommandReply{error=e.Message}});}
        }
        public void CloseMatch(){stop?.Cancel();socket?.Dispose();socket=null;Snapshot=null;while(Messages.TryDequeue(out _)){} }
        void OnDestroy(){CloseMatch();}
    }
}

