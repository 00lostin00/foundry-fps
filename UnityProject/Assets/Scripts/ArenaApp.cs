using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;

namespace Foundry
{
    [Serializable] public sealed class UserSettings { public float Sensitivity=2,Fov=85,Volume=.65f; }
    [Serializable] public sealed class RoomView { public string id,name,map;public int players,capacity;public bool locked; }
    [Serializable] public sealed class RoomList { public RoomView[] rooms; }
    [Serializable] public sealed class RoomCreated { public string id,hostKey,address;public int port; }
    [Serializable] public sealed class JoinReply { public string address,ticket;public int port; }
    [Serializable] public sealed class AdmissionReply { public string playerName; }
    [Serializable] public sealed class ConnectionData { public string playerName,ticket; }
    [Serializable] public sealed class CreateRequest { public string name,password;public int capacity=8,port; }
    [Serializable] public sealed class JoinRequest { public string playerName,password; }
    [Serializable] public sealed class TicketRequest { public string ticket; }
    [Serializable] public sealed class HeartbeatRequest { public int players; }

    public sealed partial class ArenaApp : MonoBehaviour
    {
        public static ArenaApp Instance;
        public static UserSettings Settings=new UserSettings();
        static readonly Dictionary<ulong,string> Names=new Dictionary<ulong,string>();
        public Camera MenuCamera;
        public bool Paused,SmokeClient,DebugInput;
        public bool MatchFinished;
        public double MatchEnds;
        public string Winner="";
        NetworkManager network;
        UnityTransport transport;
        string lobby="http://127.0.0.1:5080",playerName="Operator",roomName="Foundry local",password="",address="127.0.0.1",portText="7777";
        string roomId="",hostKey="",status="Ready. Start the local lobby service to find rooms.";
        RoomView[] rooms=Array.Empty<RoomView>();
        readonly List<(string text,float until)> killFeed=new List<(string,float)>();
        bool busy,settingsOpen,wasConnected;
        int peakPlayers,observedKills,observedDeaths;
        float nextHeartbeat,nextPoll;
        public static string NameFor(ulong id)=>Names.TryGetValue(id,out var n)?n:"Operator "+id;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(FindFirstObjectByType<ArenaApp>()==null)new GameObject("Foundry runtime").AddComponent<ArenaApp>();
        }
        void Awake()
        {
            Instance=this;Application.runInBackground=true;Application.targetFrameRate=144;
            QualitySettings.vSyncCount=0;QualitySettings.antiAliasing=4;Time.fixedDeltaTime=1f/60;
            var saved=PlayerPrefs.GetString("foundry.settings","");
            if(saved.Length>0)try{Settings=JsonUtility.FromJson<UserSettings>(saved)??new UserSettings();}catch{Settings=new UserSettings();}
            playerName=PlayerPrefs.GetString("foundry.name","Operator");
            ArenaWorld.Build();
            MenuCamera=new GameObject("Overview camera").AddComponent<Camera>();
            MenuCamera.transform.position=new Vector3(25,18,-24);MenuCamera.transform.LookAt(new Vector3(0,1,0));
            MenuCamera.backgroundColor=new Color(.37f,.47f,.53f);MenuCamera.clearFlags=CameraClearFlags.Skybox;MenuCamera.gameObject.AddComponent<ArenaPostFX>();
            var n=new GameObject("Netcode / Unity Transport");network=n.AddComponent<NetworkManager>();transport=n.AddComponent<UnityTransport>();
            network.NetworkConfig=new NetworkConfig();
            network.NetworkConfig.NetworkTransport=transport;
            network.NetworkConfig.PlayerPrefab=Resources.Load<GameObject>("Operator");
            if(network.NetworkConfig.PlayerPrefab==null) {status="Player prefab missing. Run Foundry > Prepare Project in Unity.";enabled=false;return;}
            network.NetworkConfig.TickRate=30;network.NetworkConfig.ConnectionApproval=true;
            network.NetworkConfig.ClientConnectionBufferTimeout=10;
            network.ConnectionApprovalCallback=Approve;
            network.OnClientConnectedCallback+=id=> {wasConnected=true;status="Connected";Debug.Log("FOUNDRY_CONNECTED "+id);};
            network.OnClientDisconnectCallback+=id=> {
                if(id==network.LocalClientId && !network.IsServer) {status="Disconnected: "+network.DisconnectReason;ReturnToMenu();}
                Names.Remove(id);
            };
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();string Get(string key,string fallback)=>Array.IndexOf(args,key) is var i && i>=0 && i+1<args.Length?args[i+1]:fallback;
            lobby=Get("--lobby",lobby);address=Get("--connect",address);portText=Get("--port",portText);
            playerName=Get("--name",playerName);SmokeClient=args.Contains("--smoke");DebugInput=args.Contains("--debug-input");
            if(args.Contains("--server"))StartDirect(true);
            else if(args.Contains("--host"))StartDirect(false);
            else if(args.Contains("--connect"))ConnectDirect();
            if(float.TryParse(Get("--quit-after","0"),out var timeout)&&timeout>0) {
                yield return new WaitForSecondsRealtime(timeout);
                Debug.Log("FOUNDRY_SMOKE_RESULT "+JsonUtility.ToJson(new SmokeResult { connected=wasConnected,players=ArenaPlayer.Players.Count,
                    kills=observedKills,deaths=observedDeaths,peakPlayers=peakPlayers }));
                Application.Quit(wasConnected||network.IsServer?0:2);
            }
        }
        [Serializable] sealed class SmokeResult {public bool connected;public int players,kills,deaths,peakPlayers;}
        void Approve(NetworkManager.ConnectionApprovalRequest request,NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject=true;response.Pending=false;
            if(network.ConnectedClients.Count>=16){response.Approved=false;response.Reason="Server full";return;}
            ConnectionData data;
            try{if(request.Payload.Length>512)throw new Exception();data=JsonUtility.FromJson<ConnectionData>(Encoding.UTF8.GetString(request.Payload));}
            catch{response.Approved=false;response.Reason="Invalid connection data";return;}
            if(data==null || string.IsNullOrWhiteSpace(data.playerName) || data.playerName.Length>24 || Encoding.UTF8.GetByteCount(data.playerName)>60) {response.Reason="Invalid name";return;}
            if(roomId.Length==0 || request.ClientNetworkId==network.LocalClientId) {
                Names[request.ClientNetworkId]=data.playerName;response.Approved=true;return;
            }
            response.Pending=true;StartCoroutine(Admit(request.ClientNetworkId,data,response));
        }
        IEnumerator Admit(ulong clientId,ConnectionData data,NetworkManager.ConnectionApprovalResponse response)
        {
            bool accepted=false;string name="";
            yield return Request("POST","/api/rooms/"+roomId+"/admit",JsonUtility.ToJson(new TicketRequest{ticket=data.ticket}),
                body=>{name=JsonUtility.FromJson<AdmissionReply>(body).playerName;accepted=true;},true);
            if(accepted)Names[clientId]=name;
            response.Approved=accepted;response.Reason=accepted?"":"Room admission rejected; refresh rooms and retry";response.Pending=false;
        }
        bool Configure()
        {
            if(!ushort.TryParse(portText,out var port)||port<1024) {status="Port must be 1024–65535";return false;}
            if(string.IsNullOrWhiteSpace(playerName)||playerName.Length>24||Encoding.UTF8.GetByteCount(playerName)>60){status="Name must be 1–24 characters and at most 60 UTF-8 bytes";return false;}
            transport.SetConnectionData(address,port,"127.0.0.1");
            network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ConnectionData{playerName=playerName,ticket=""}));
            PlayerPrefs.SetString("foundry.name",playerName);PlayerPrefs.Save();return true;
        }
        void StartDirect(bool dedicated)
        {
            if(!Configure())return;MatchFinished=false;
            if(!(dedicated?network.StartServer():network.StartHost()))status="Unable to bind game port. Choose a different port.";
            else {MatchEnds=network.ServerTime.Time+480;status=dedicated?"Dedicated server running on loopback":"Direct local session";}
        }
        void ConnectDirect()
        {if(Configure() && !network.StartClient())status="Unable to start client";}
        IEnumerator CreateRoom()
        {
            if(!Configure())yield break;busy=true;RoomCreated created=null;
            yield return Request("POST","/api/rooms",JsonUtility.ToJson(new CreateRequest{name=roomName,password=password,port=int.Parse(portText)}),
                body=>created=JsonUtility.FromJson<RoomCreated>(body));
            if(created!=null) {
                roomId=created.id;hostKey=created.hostKey;
                if(network.StartHost()){MatchEnds=network.ServerTime.Time+480;MatchFinished=false;nextHeartbeat=0;}
                else {yield return Request("DELETE","/api/rooms/"+roomId,null,_=>{},true);roomId="";hostKey="";status="Game port occupied";}
            }
            busy=false;
        }
        IEnumerator Join(RoomView room)
        {
            if(!Configure())yield break;busy=true;JoinReply reply=null;
            yield return Request("POST","/api/rooms/"+room.id+"/join",JsonUtility.ToJson(new JoinRequest{playerName=playerName,password=password}),
                body=>reply=JsonUtility.FromJson<JoinReply>(body));
            if(reply!=null) {
                transport.SetConnectionData(reply.address,(ushort)reply.port);
                network.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ConnectionData{playerName=playerName,ticket=reply.ticket}));
                if(!network.StartClient())status="Unable to start client";
            }
            busy=false;
        }
        IEnumerator RefreshRooms()
        {yield return Request("GET","/api/rooms",null,body=>{rooms=JsonUtility.FromJson<RoomList>(body).rooms??Array.Empty<RoomView>();status=rooms.Length+" rooms available";});}
        IEnumerator Request(string method,string path,string json,Action<string> success,bool host=false)
        {
            using(var request=new UnityWebRequest(lobby.TrimEnd('/')+path,method)) {
                request.downloadHandler=new DownloadHandlerBuffer();request.timeout=5;
                if(json!=null){request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));request.SetRequestHeader("Content-Type","application/json");}
                if(host)request.SetRequestHeader("X-Host-Key",hostKey);
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success)success(request.downloadHandler.text);
                else status=request.responseCode==401?"Wrong room password or expired admission ticket":request.responseCode==409?"Room is full":
                    "Lobby request failed ("+request.responseCode+"). Start tools/start.ps1; check the lobby address.";
            }
        }
        void Update()
        {
            killFeed.RemoveAll(item=>Time.time>item.until);
            if(network==null)return;
            peakPlayers=Math.Max(peakPlayers,ArenaPlayer.Players.Count);
            observedKills=Math.Max(observedKills,ArenaPlayer.Players.Sum(p=>p.Kills.Value));
            observedDeaths=Math.Max(observedDeaths,ArenaPlayer.Players.Sum(p=>p.Deaths.Value));
            if(network.IsServer) {
                if(!MatchFinished && (network.ServerTime.Time>=MatchEnds || ArenaPlayer.Players.Any(p=>p.Kills.Value>=20))) {
                    MatchFinished=true;Winner=ArenaPlayer.Players.OrderByDescending(p=>p.Kills.Value).FirstOrDefault()?.PlayerName.Value.ToString()??"No winner";
                    Debug.Log("FOUNDRY_MATCH_FINISHED "+Winner);
                }
                if(roomId.Length>0 && Time.unscaledTime>=nextHeartbeat) {
                    nextHeartbeat=Time.unscaledTime+5;
                    StartCoroutine(Request("POST","/api/rooms/"+roomId+"/heartbeat",JsonUtility.ToJson(new HeartbeatRequest{players=network.ConnectedClients.Count}),_=>{},true));
                }
            } else if(ArenaPlayer.Local!=null) {
                MatchFinished=ArenaPlayer.Local.RoundOver.Value;MatchEnds=ArenaPlayer.Local.RoundEnds.Value;
                Winner=ArenaPlayer.Local.RoundWinner.Value.ToString();
            } else if(!network.IsListening&&!busy&&Time.unscaledTime>=nextPoll) {
                nextPoll=Time.unscaledTime+10;StartCoroutine(RefreshRooms());
            }
        }
        public void TogglePause()
        {Paused=!Paused;Cursor.lockState=Paused?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=Paused;}
        public void AddKill(string killer,string victim)
        {killFeed.Add((killer+"   /   "+victim,Time.time+5));if(killFeed.Count>5)killFeed.RemoveAt(0);}
        void ReturnToMenu()
        {
            Paused=false;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;MenuCamera.gameObject.SetActive(true);
        }
        IEnumerator Leave()
        {
            if(roomId.Length>0)yield return Request("DELETE","/api/rooms/"+roomId,null,_=>{},true);
            roomId="";hostKey="";network.Shutdown();Names.Clear();ReturnToMenu();
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
    }
}
