

namespace NDG.UnityNet
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEngine;
    using NDG.Realtime;
    using Debug = UnityEngine.Debug;
    using System.Diagnostics;
    using UnityEngine.SceneManagement;

#if UNITY_EDITOR
    using UnityEditor;
    using System.IO;

#endif
    public struct InstantiateParameters
    {
        public int[] viewIDs;
        public byte objLevelPrefix;
        public object[] data;
        public byte @group;
        public Quaternion rotation;
        public Vector3 position;
        public string prefabName;
        public Player creator;
        public int timestamp;

        public InstantiateParameters(string prefabName, Vector3 position, Quaternion rotation, byte @group, object[] data, byte objLevelPrefix, int[] viewIDs, Player creator, int timestamp)
        {
            this.prefabName = prefabName;
            this.position = position;
            this.rotation = rotation;
            this.@group = @group;
            this.data = data;
            this.objLevelPrefix = objLevelPrefix;
            this.viewIDs = viewIDs;
            this.creator = creator;
            this.timestamp = timestamp;
        }
    }


    public static partial class NDG_Network
    {
        public const string NDG_Version = "1.0";
        public static string GameVersion
        {
            get
            {
                return gameVersion;
            }
            set
            {
                gameVersion = value;
                NetworkingClient.AppVersion = string.Format("{0}_{1}", value, NDG_Network.NDG_Version);
            }
        }

        private static string gameVersion;

        public static string AppVersion
        {
            get
            {
                return NetworkingClient.AppVersion;
            }
        }

        public static LoadBalancingClient NetworkingClient;

        public static readonly int MAX_VIEW_IDS = 1000;

        public const string ServerSettingsFileName = "NDG_ServerSettings";

        public static AppSettings appSettings;



        

        private static ServerSettings serverSetting;
        public static ServerSettings ServerSetting
        {
            get
            {
                if(serverSetting == null)
                {
                    serverSetting = (ServerSettings)Resources.Load(NDG_Network.ServerSettingsFileName, typeof(ServerSettings));
                }
                return serverSetting;
            }
            private set
            {
                serverSetting = value;
            }
        }

        private const string PlayerPrefsKey = "BestRegion";
        public static string BestRegionSummaryInPreferences
        {
            get
            {
                return PlayerPrefs.GetString(PlayerPrefsKey, null);
            }
            internal set
            {
                if (String.IsNullOrEmpty(value))
                {
                    PlayerPrefs.DeleteKey(PlayerPrefsKey);
                }
                else
                {
                    PlayerPrefs.SetString(PlayerPrefsKey, value.ToString());
                }
            }
        }

        /// <summary>
        /// 네트워크에 처음 연결하기 전까지는 False입니다.
        /// </summary>
        public static bool IsConnected
        {
            get
            {

                if(OfflineMode)
                {
                    return true;
                }

                if (NetworkingClient == null)
                    return false;

                return NetworkingClient.IsConnected;
            }
        }

        /// <summary>
        /// 네트워크에 연결될수 있는 상태에서 TRUE가 반환됩니다.
        /// </summary>
        public static bool IsConnectedAndReady
        {
            get
            {
                if(OfflineMode)
                {
                    return true;
                }

                if (NetworkingClient == null)
                    return false;

                return NetworkingClient.IsConnectedAndReady;
            }

        }

        /// <summary>
        /// 오프라인 모드가 아닌 경우 네트워크에서의 클라이언트 상태를 제공합니다.
        /// </summary>
        public static ClientState NetworkClientState
        {
            get
            {
                if (OfflineMode)
                {
                    return (offlineModeRoom != null) ? ClientState.Joined : ClientState.ConnectedToMasterServer;
                }

                if (NetworkingClient == null)
                {
                    return ClientState.Disconnected;
                }

                return NetworkingClient.State;
            }
        }

        public static ConnectMethod ConnectMethod = ConnectMethod.NotCalled;

        /// <summary>
        /// 현재 접속되어있는 서버를 나타냅니다.
        /// </summary>
        public static ServerConnection Server 
        { 
            get
            { 
                return (NDG_Network.NetworkingClient != null) ? NDG_Network.NetworkingClient.Server : ServerConnection.NameServer; 
            } 
        }

        /// <summary>
        /// 연결 중에 사용되는 사용자의 인증 값입니다.
        /// </summary>
        public static AuthenticationValues AuthValues
        {
            get { return (NetworkingClient != null) ? NetworkingClient.AuthValues : null; }
            set { if (NetworkingClient != null) NetworkingClient.AuthValues = value; }
        }

        /// <summary>
        /// 현재 로비의 타입을 나타냅니다.
        /// 로비에 참여하거나 룸을 만들때 정의됩니다.
        /// </summary>
        public static TypedLobby CurrentLobby
        {
            get { return NetworkingClient.CurrentLobby; }
        }

        /// <summary>
        /// 현재 있는 룸을 가져옵니다.
        /// 방에 있지않으면 Null을 반환합니다.
        /// </summary>
        public static Room CurrentRoom
        {
            get
            {
                if (offlineMode)
                {
                    return offlineModeRoom;
                }

                return NetworkingClient == null ? null : NetworkingClient.CurrentRoom;
            }
        }

        public static NetLogLevel LogLevel = NetLogLevel.ErrorsOnly;

        /// <summary>
        /// 로컬 플레이어를 가져옵니다.
        /// </summary>
        public static Player LocalPlayer
        {
            get
            {
                if (NetworkingClient == null)
                {
                    return null;
                }

                return NetworkingClient.LocalPlayer;
            }
        }

        /// <summary>
        ///  룸에 있는 모든 사용자와 동기화되는 닉네임입니다.
        /// </summary>
        public static string NickName
        {
            get
            {
                return NetworkingClient.NickName;
            }

            set
            {
                NetworkingClient.NickName = value;
            }
        }

        /// <summary>
        ///  현재 룸의 플레이어 목록을 정렬한 복사본입니다.
        ///  플레이어가 룸에 참여하거나 나갈때 업데이트됩니다.
        /// </summary>
        public static Player[] PlayerList
        {
            get
            {
                Room room = CurrentRoom;
                if (room != null)
                {
                    return room.Players.Values.OrderBy((x) => x.ActorNumber).ToArray();
                }
                return new Player[0];
            }
        }

        /// <summary>
        /// 로컬 플레이어 이외에 룸 안에있는 다른 정렬된 플레이어 목록입니다.
        /// 플레이어가 참여하거나 나갈때 업데이트 됩니다.
        /// </summary>
        public static Player[] PlayerListOthers
        {
            get
            {
                Room room = CurrentRoom;
                if (room != null)
                {
                    return room.Players.Values.OrderBy((x) => x.ActorNumber).Where(x => !x.IsLocal).ToArray();
                }
                return new Player[0];
            }
        }


        /// <summary>
        /// OnSerialize / Observing 컴포넌트를 통해 전송할때 전송되기 위해 과거 값과 차이나야하는 최소  거리 값
        /// sqrMagnitude (두 점간의 거리의 제곱에 루트값). 두 점간의 거리차이
        /// </summary>
        public static float PrecisionForVectorSynchronization = 0.000099f;

        /// <summary>
        /// 과거 값과 차이나야하는 최소 회전 값
        /// </summary>
        public static float PrecisionForQuaternionSynchronization = 1.0f;

        /// <summary>
        /// 과거 값과 차이나야 하는 최소 부동소수점 차이
        /// </summary>
        public static float PrecisionForFloatSynchronization = 0.01f;

        /// <summary>
        /// 오프라인 모드는 싱글 플레이어 게임 모드에서 멀티 플레이어 코드를 재사용할수 있게 설정할 수 있습니다.
        /// </summary>
        public static bool OfflineMode
        {
            get
            {
                return offlineMode;
            }

            set
            {
                if (value == offlineMode)
                {
                    return;
                }

                if (value && IsConnected)
                {
                    Debug.LogError("연결되어 있는 동안에는 오프라인 모드를 사용할 수 없습니다.");
                    return;
                }

                if (NetworkingClient.IsConnected)
                {
                    NetworkingClient.Disconnect(); 
                }

                offlineMode = value;

                if (offlineMode)
                {
                    NetworkingClient.ChangeLocalID(-1);
                    NetworkingClient.ConnectionCallbackTargets.OnConnectedToMaster();
                }
                else
                {
                    bool wasInOfflineRoom = offlineModeRoom != null;

                    if (wasInOfflineRoom)
                    {
                        LeftRoomCleanup();
                    }
                    offlineModeRoom = null;
                    NDG_Network.NetworkingClient.CurrentRoom = null;
                    NetworkingClient.ChangeLocalID(-1);
                    if (wasInOfflineRoom)
                    {
                        NetworkingClient.MatchMakingCallbackTargets.OnLeftRoom();
                    }
                }
            }
        }

        private static bool offlineMode = false;
        private static Room offlineModeRoom = null;

        /// <summary>
        /// 룸의 모든 클라이언트가 마스터 클라이언트와 동일한 레벨로 자동으로 로드해야 하는지 여부를 정합니다.
        /// </summary>
        public static bool AutomaticallySyncScene
        {
            get
            {
                return automaticallySyncScene;
            }
            set
            {
                automaticallySyncScene = value;
                if (automaticallySyncScene && CurrentRoom != null)
                {
                    LoadLevelIfSynced();
                }
            }
        }

        private static bool automaticallySyncScene = false;


        /// <summary>
        /// 로컬 클라이언트가 로비에 있는동안에는 true가 반환됩니다.
        /// </summary>
        public static bool InLobby
        {
            get
            {
                return NetworkingClient.InLobby;
            }
        }

        /// <summary>
        /// 초당 package를 전송할 횟수를 정의합니다.
        /// 변경할경우 SerializationRate도 변경해야 합니다.
        /// </summary>
        public static int SendRate
        {
            get
            {
                return 1000 / sendFrequency;
            }

            set
            {
                sendFrequency = 1000 / value;
                if (NetworkHandler.Instance != null)
                {
                    NetworkHandler.Instance.UpdateInterval = sendFrequency;
                }

                if (value < SerializationRate)
                {
                    SerializationRate = value;
                }
            }
        }

        private static int sendFrequency = 50;

        /// <summary>
        /// NetworkView에서 OnNetSerialize를 초당 몇번 호출해야 하는지 정의합니다.
        /// </summary>
        public static int SerializationRate
        {
            get
            {
                return 1000 / serializationFrequency;
            }

            set
            {
                if (value > SendRate)
                {
                    Debug.LogError("Network Error: Serialize Rate는 전송 레이트보다 클수 없습니다.");
                    value = SendRate;
                }

                serializationFrequency = 1000 / value;
                if (NetworkHandler.Instance != null)
                {
                    NetworkHandler.Instance.UpdateIntervalOnSerialize = serializationFrequency;
                }
            }
        }

        private static int serializationFrequency = 100;

        /// <summary>
        /// 수신 이벤트의 상태를 가져오거나 설정합니다.
        /// </summary>
        public static bool IsMessageQueueRunning
        {
            get
            {
                return isMessageQueueRunning;
            }

            set
            {
                NetworkingClient.LoadBalancingPeer.IsSendingOnlyAcks = !value;
                isMessageQueueRunning = value;
            }
        }

        private static bool isMessageQueueRunning = true;

        
        /// <summary>
        /// 서버와 동기화된 네트워크 시간입니다.
        /// </summary>
        public static double Time
        {
            get
            {
                if(UnityEngine.Time.frameCount == frame)
                {
                    return frametime;
                }

                uint u = (uint)ServerTimestamp;
                double t = u;
                frametime = t / 1000.0d;
                frame = UnityEngine.Time.frameCount;
                return frametime;
            }
        }

        private static double frametime;
        private static int frame;

        /// <summary>
        /// 현재 서버의 밀리초 타임스탬프입니다.
        /// </summary>
        public static int ServerTimestamp
        {
            get
            {
                if (OfflineMode)
                {
                    if (StartupStopwatch != null && StartupStopwatch.IsRunning)
                    {
                        return (int)StartupStopwatch.ElapsedMilliseconds;
                    }
                    return Environment.TickCount;
                }

                return NetworkingClient.LoadBalancingPeer.ServerTimeInMilliSeconds;  
            }
        }

        private static Stopwatch StartupStopwatch;

        /// <summary>
        /// Unity에서 OnApplicationPause(true)가 호출되면 연결을 유지하고 있는 시간을 설정합니다.
        /// 기본 60초
        /// </summary>
        public static float KeepAliveInBackground
        {
            set
            {
                if (NetworkHandler.Instance != null)
                {
                    NetworkHandler.Instance.KeepAliveInBackground = (int)Mathf.Round(value * 1000.0f);
                }
            }

            get { return NetworkHandler.Instance != null ? Mathf.Round(NetworkHandler.Instance.KeepAliveInBackground / 1000.0f) : 60.0f; }
        }

        /// <summary>
        /// NetworkHandler 내의 LateUpdate 에서 수신 메시지를 발송하는 최소 시간을 설정합니다.
        /// </summary>
        public static float MinimalTimeScaleToDispatchInFixedUpdate = -1f;


        /// <summary>
        /// 마스터 클라이언트인지 확인합니다.
        /// </summary>
        public static bool IsMasterClient
        {
            get
            {
                if (OfflineMode)
                {
                    return true;
                }

                return NetworkingClient.CurrentRoom != null && NetworkingClient.CurrentRoom.MasterClientId == LocalPlayer.ActorNumber;
            }
        }


        /// <summary>
        /// 현재 룸의 마스터 클라이언트를 가져옵니다.
        /// 룸 외부일시 NULL
        /// </summary>
        public static Player MasterClient
        {
            get
            {
                if (OfflineMode)
                {
                    return NDG_Network.LocalPlayer;
                }

                if (NetworkingClient == null || NetworkingClient.CurrentRoom == null)
                {
                    return null;
                }

                return NetworkingClient.CurrentRoom.GetPlayer(NetworkingClient.CurrentRoom.masterClientId);
            }
        }

        /// <summary>
        /// 룸 안에 있을 동안은 true입니다.
        /// </summary>
        public static bool InRoom
        {
            get
            {
                return NetworkClientState == ClientState.Joined;
            }
        }

        /// <summary>
        /// 현재 서버에 접속해있는 플레이어의 수 입니다.
        /// </summary>
        public static int CountOfPlayers
        {
            get
            {
                return NetworkingClient.PlayersInRoomsCount + NetworkingClient.PlayersOnMasterCount;
            }
        }

        /// <summary>
        /// 현재 서버에 존재하고있는 방의 갯수입니다.
        /// </summary>
        public static int CountOfRooms
        {
            get
            {
                return NetworkingClient.RoomsCount;
            }
        }

        //각 플레이어는 할당 속도를 높이기 위해 마지막으로 사용한 SubId만 저장합니다.
        private static int lastUsedViewSubId = 0;
        //각 룸에서 마스터클라이언트가 게임오브젝트를 생성하기 위한 고유 SubId입니다. 
        private static int lastUsedViewSubIdStatic = 0;


        /// <summary>
        /// 기본 설정
        /// </summary>

        static NDG_Network()
        {
#if !UNITY_EDITOR || (UNITY_EDITOR && !UNITY_2019_4_OR_NEWER)
            StaticReset();
#endif
        }

#if UNITY_2019_4_OR_NEWER && UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        private static void StaticReset()
        {

#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif

            monoRPCMethodsCache.Clear();

            // 네트워크 프로토콜 및 클라이언트 설정
            ConnectionProtocol protocol = NDG_Network.ServerSetting.AppSettings.Protocol;
            NetworkingClient = new LoadBalancingClient(protocol);
            NetworkingClient.LoadBalancingPeer.QuickResendAttempts = 2;
            NetworkingClient.LoadBalancingPeer.SentCountAllowance = 7;

            NetworkingClient.EventReceived -= OnEvent;
            NetworkingClient.EventReceived += OnEvent;
            NetworkingClient.OpResponseReceived -= OnOperation;
            NetworkingClient.OpResponseReceived += OnOperation;
            NetworkingClient.StateChanged -= OnClientStateChanged;
            NetworkingClient.StateChanged += OnClientStateChanged;

            StartupStopwatch = new Stopwatch();
            StartupStopwatch.Start();
            NetworkingClient.LoadBalancingPeer.LocalMsTimestampDelegate = () => (int)StartupStopwatch.ElapsedMilliseconds;

            NetworkHandler.Instance.Client = NetworkingClient;


            Application.runInBackground = ServerSetting.RunInBackground;
            PrefabPool = new DefaultPool();

            rpcShortcuts = new Dictionary<string, int>(NDG_Network.ServerSetting.RpcList.Count);
            for (int index = 0; index < NDG_Network.ServerSetting.RpcList.Count; index++)
            {
                var name = NDG_Network.ServerSetting.RpcList[index];
                rpcShortcuts[name] = index;
            }

            CustomTypes.Register();
        }



        /// <summary>
        /// ServerSettings파일에서 구성한대로 서버에 연결합니다.
        /// </summary>
        /// <returns></returns>
        public static bool ConnectUsingSettings()
        {
            if (ServerSetting == null)
            {
                Debug.LogError("ServerSettings 파일 로드에 실패했습니다. 'Resources'폴더를 확인해 주세요.");
                return false;
            }

            return ConnectUsingSettings(ServerSetting.AppSettings, ServerSetting.StartInOfflineMode);
        }

        public static bool ConnectUsingSettings(AppSettings appSettings, bool startInOfflineMode = false) 
        {
            if (NetworkingClient.LoadBalancingPeer.PeerState != PeerStateValue.Disconnected)
            {
                Debug.LogWarning("ConnectUsingSettings() 실패.  'Disconnected'상태에서만 연결할 수 있습니다. 현재상태: " + NetworkingClient.LoadBalancingPeer.PeerState);
                return false;
            }
            if (NetworkHandler.AppQuits)
            {
                Debug.LogWarning("Can't connect: Application is closing. Unity called OnApplicationQuit().");
                return false;
            }
            if (ServerSetting == null)
            {
                Debug.LogError("Can't connect: Loading settings failed. ServerSettings asset must be in any 'Resources' folder as: " + ServerSettingsFileName);
                return false;
            }

            SetupLogging();


            NetworkingClient.LoadBalancingPeer.TransportProtocol = appSettings.Protocol;
            NetworkingClient.EnableProtocolFallback = appSettings.EnableProtocolFallback;


            IsMessageQueueRunning = true;
            NetworkingClient.AppId = appSettings.AppIdRealtime;
            GameVersion = appSettings.AppVersion;



            if (startInOfflineMode)
            {
                OfflineMode = true;
                return true;
            }

            if (OfflineMode)
            {
                OfflineMode = false; 
                Debug.LogWarning("ConnectUsingSettings()을 호출하여 오프라인 모드를 비활성화 했습니다.");
            }


            NetworkingClient.EnableLobbyStatistics = appSettings.EnableLobbyStatistics;
            NetworkingClient.ProxyServerAddress = appSettings.ProxyServer;


            if (appSettings.IsMasterServerAddress)
            {
                NetworkingClient.SerializationProtocol = SerializationProtocol.GpBinaryV18;   
                if (AuthValues == null)
                {
                    AuthValues = new AuthenticationValues(Guid.NewGuid().ToString());
                }
                else if (string.IsNullOrEmpty(AuthValues.UserId))
                {
                    AuthValues.UserId = Guid.NewGuid().ToString();
                }
                return ConnectToMaster(appSettings.Server, appSettings.Port, appSettings.AppIdRealtime);
            }


            NetworkingClient.NameServerPortOverride = appSettings.Port;
            if (!appSettings.IsDefaultNameServer)
            {
                NetworkingClient.NameServerHost = appSettings.Server;
            }

            return false;


    
           // return ConnectToRegion(appSettings.FixedRegion);
        }


        /// <summary>
        /// 마스터 서버에 address, port, appID로 연결을 요청합니다.
        /// </summary>
        public static bool ConnectToMaster(string masterServerAddress, int port, string appID)
        {
            if (NetworkingClient.LoadBalancingPeer.PeerState != PeerStateValue.Disconnected)
            {
                Debug.LogWarning("ConnectToMaster() 실패.  'Disconnected' 상태에서만 요청할 수 있습니다. 현재상태: " + NetworkingClient.LoadBalancingPeer.PeerState);
                return false;
            }
            if (NetworkHandler.AppQuits)
            {
                Debug.LogWarning("연결할 수 없음: 프로그램이 종료중입니다. 유니티 OnApplicationQuit()을 호출한 상황입니다.");
                return false;
            }

            if (OfflineMode)
            {
                OfflineMode = false; 
                Debug.LogWarning("ConnectToMaster()가 오프라인 모드를 비활성화 했습니다. 더 이상 오프라인이 아닙니다.");
            }

            if (!IsMessageQueueRunning)
            {
                IsMessageQueueRunning = true;
                Debug.LogWarning("ConnectToMaster()가 IsMessageQueueRunning을 활성화했습니다.  메시지를 발송할 수 있어야합니다.");
            }

            SetupLogging();
            ConnectMethod = ConnectMethod.ConnectToMaster;

            NetworkingClient.IsUsingNameServer = false;
            NetworkingClient.MasterServerAddress = (port == 0) ? masterServerAddress : masterServerAddress + ":" + port;
            NetworkingClient.AppId = appID;

            return NetworkingClient.ConnectToMasterServer();
        }

        /// <summary>
        /// 선택한 지역의 서버에 연결합니다.
        /// </summary>
        public static bool ConnectToRegion(string region)
        {
            if (NetworkingClient.LoadBalancingPeer.PeerState != PeerStateValue.Disconnected && NetworkingClient.Server != ServerConnection.NameServer)
            {
                Debug.LogWarning("ConnectToRegion() 실패. 'Disconnected'상태에서만 연결요청을 할 수 있습니다. 현재 상태: " + NetworkingClient.LoadBalancingPeer.PeerState);
                return false;
            }
            if (NetworkHandler.AppQuits)
            {
                Debug.LogWarning("연결할 수 없음: 프로그램이 종료중입니다. 유니티 OnApplicationQuit()을 호출한 상황입니다.");
                return false;
            }

            SetupLogging();
            ConnectMethod = ConnectMethod.ConnectToRegion;

            if (!string.IsNullOrEmpty(region))
            {
                return NetworkingClient.ConnectToRegionMaster(region);
            }

            return false;
        }


        /// <summary>
        ///  로컬 클라이언트와 서버의 연결을 끊습니다.
        ///  완료시 OnDisconnected를 호출합니다.
        /// </summary>
        public static void Disconnect()
        {
            if (OfflineMode)
            {
                OfflineMode = false;
                offlineModeRoom = null;
                NetworkingClient.State = ClientState.Disconnecting;
                NetworkingClient.OnStatusChanged(StatusCode.Disconnect);
                return;
            }

            if (NetworkingClient == null)
            {
                return; 
            }

            NetworkingClient.Disconnect();
        }

        /// <summary>
        /// 연결 상태를 확인 합니다.
        /// </summary>
        private static bool VerifyCanUseNetwork()
        {
            if (IsConnected)
            {
                return true;
            }

            Debug.LogError("서버에 연결되어 있지 않으면 메시지를 보낼 수 없습니다. ");
            return false;
        }

        /// <summary>
        /// 서버에 대한 현재 왕복 시간입니다.
        /// </summary>
        public static int GetPing()
        {
            return NetworkingClient.LoadBalancingPeer.RoundTripTime;
        }


        public static void SendAllOutgoingCommands()
        {
            if (!VerifyCanUseNetwork())
            {
                return;
            }

            while (NetworkingClient.LoadBalancingPeer.SendOutgoingCommands())
            {
            }
        }

        /// <summary>
        /// 대상 클라이언트에게 연결 해제를 요청합니다. 마스터 클라이언트만 요청할 수 있습니다.
        /// </summary>
        public static bool CloseConnection(Player kickPlayer)
        {
            if (!VerifyCanUseNetwork())
            {
                return false;
            }

            if (!LocalPlayer.IsMasterClient)
            {
                Debug.LogError("CloseConnection: 오직 마스터 플레이어만이 추방할 수 있습니다.");
                return false;
            }

            if (kickPlayer == null)
            {
                Debug.LogError("CloseConnection: 대상 플레이어가 Null상태 입니다.");
                return false;
            }

            RaiseEventOptions options = new RaiseEventOptions() { TargetActors = new int[] { kickPlayer.ActorNumber } };
            return NetworkingClient.OpRaiseEvent(NetEvent.CloseConnection, null, options, SendOptions.SendReliable);
        }


        /// <summary>
        /// 대상 플레이어를 마스터클라이언트로 지정하도록 서버에 요청합니다.
        /// </summary>
        public static bool SetMasterClient(Player masterClientPlayer)
        {
            if (!InRoom || !VerifyCanUseNetwork() || OfflineMode)
            {
                if (LogLevel == NetLogLevel.Informational) Debug.Log("마스터 클라이언트를 설정할 수 없습니다. 현재 룸에있는 것이 아니거나 오프라인 상태입니다.");
                return false;
            }

            return CurrentRoom.SetMasterClient(masterClientPlayer);
        }

        /// <summary>
        /// 필터와 일치하는 임의의 룸에 참가합니다.
        /// 결과로 OnJoinedRoom이나 OnJoinRandomFailed가 호출됩니다.
        /// </summary>
        public static bool JoinRandomRoom()
        {
            return JoinRandomRoom(null, 0, MatchmakingMode.FillRoom, null, null);
        }
        public static bool JoinRandomRoom(Hashtable expectedCustomRoomProperties, byte expectedMaxPlayers)
        {
            return JoinRandomRoom(expectedCustomRoomProperties, expectedMaxPlayers, MatchmakingMode.FillRoom, null, null);
        }

        public static bool JoinRandomRoom(Hashtable expectedCustomRoomProperties, byte expectedMaxPlayers, MatchmakingMode matchingType, TypedLobby typedLobby, string sqlLobbyFilter, string[] expectedUsers = null)
        {
            if (OfflineMode)
            {
                if (offlineModeRoom != null)
                {
                    Debug.LogError("JoinRandomRoom 실패. 오프라인 모드에서는 다른 룸에 들어가려면 현재 룸에서 나와야합니다.");
                    return false;
                }
                EnterOfflineRoom("offline room", null, true);
                return true;
            }
            if (NetworkingClient.Server != ServerConnection.MasterServer || !IsConnectedAndReady)
            {
                Debug.LogError("JoinRandomRoom 실패. 접속되있는 서버: " + NetworkingClient.Server + " (마스터 서버에서 메치메이킹을 시도해야합니다.)" + (IsConnectedAndReady ? " 준비상태" : "  (클라이언트 상태: " + NetworkingClient.State + ")") + ". OnJoinedLobby 나 OnConnectedToMaster  콜백함수를 기다리세요. ");
                return false;
            }

            typedLobby = typedLobby ?? ((NetworkingClient.InLobby) ? NetworkingClient.CurrentLobby : null);  // use given lobby, or active lobby (if any active) or none

            OpJoinRandomRoomParams opParams = new OpJoinRandomRoomParams();
            opParams.ExpectedCustomRoomProperties = expectedCustomRoomProperties;
            opParams.ExpectedMaxPlayers = expectedMaxPlayers;
            opParams.MatchingType = matchingType;
            opParams.TypedLobby = typedLobby;
            opParams.SqlLobbyFilter = sqlLobbyFilter;
            opParams.ExpectedUsers = expectedUsers;

            return NetworkingClient.OpJoinRandomRoom(opParams);
        }


        /// <summary>
        /// 새로운 방을 만듭니다.
        /// OnCreatedRoom 과 OnJoinedRoom이나 OnCreateRoomFailed 콜백함수가 호출됩니다.
        /// </summary>
        /// <param name="roomName"></param>
        /// <param name="roomOptions"></param>
        /// <param name="typedLobby"></param>
        /// <param name="expectedUsers"></param>
        /// <returns></returns>
        public static bool CreateRoom(string roomName, RoomOptions roomOptions = null, TypedLobby typedLobby = null, string[] expectedUsers = null)
        {
            if (OfflineMode)
            {
                if (offlineModeRoom != null)
                {
                    Debug.LogError("CreateRoom 실패. 오프라인 모드에서는 다른 룸에 들어가려면 현재 룸에서 나와야합니다.");
                    return false;
                }
                EnterOfflineRoom(roomName, roomOptions, true);
                return true;
            }
            if (NetworkingClient.Server != ServerConnection.MasterServer || !IsConnectedAndReady)
            {
                Debug.LogError("CreateRoom 실패. 접속되있는 서버: " + NetworkingClient.Server + " (마스터 서버에서 메치메이킹을 시도해야합니다.)" + (IsConnectedAndReady ? " 준비상태" : "  (클라이언트 상태: " + NetworkingClient.State + ")") + ". OnJoinedLobby 나 OnConnectedToMaster  콜백함수를 기다리세요. ");
                return false;
            }

            typedLobby = typedLobby ?? ((NetworkingClient.InLobby) ? NetworkingClient.CurrentLobby : null); 

            EnterRoomParams opParams = new EnterRoomParams();
            opParams.RoomName = roomName;
            opParams.RoomOptions = roomOptions;
            opParams.Lobby = typedLobby;
            opParams.ExpectedUsers = expectedUsers;
            opParams.PlayerProperties = LocalPlayer.CustomProperties;

            return NetworkingClient.OpCreateRoom(opParams);
        }


        /// <summary>
        /// 룸 이름으로 룸에 참가합니다.
        /// OnJoinedRoom 이나 OnJoinRoomFailed가 콜백함수로 호출됩니다.
        /// </summary>
        public static bool JoinRoom(string roomName, string[] expectedUsers = null)
        {
            if (OfflineMode)
            {
                if (offlineModeRoom != null)
                {
                    Debug.LogError("JoinRoom 실패. 다른 룸에 들어가려면 현재 오프라인 룸을 나가야 합니다.");
                    return false;
                }
                EnterOfflineRoom(roomName, null, true);
                return true;
            }
            if (NetworkingClient.Server != ServerConnection.MasterServer || !IsConnectedAndReady)
            {
                Debug.LogError("JoinRoom 접속되있는 서버: " + NetworkingClient.Server + " (마스터 서버에서 메치메이킹을 시도해야합니다.)" + (IsConnectedAndReady ? " 준비상태" : "  (클라이언트 상태: " + NetworkingClient.State + ")") + ". OnJoinedLobby 나 OnConnectedToMaster  콜백함수를 기다리세요. ");
                return false;
            }
            if (string.IsNullOrEmpty(roomName))
            {
                Debug.LogError("JoinRoom 실패. 룸 이름을 하나 이상 적어야 합니다.");
                return false;
            }


            EnterRoomParams opParams = new EnterRoomParams();
            opParams.RoomName = roomName;
            opParams.ExpectedUsers = expectedUsers;

            opParams.Lobby = new TypedLobby("TestGame",0);

            return NetworkingClient.OpJoinRoom(opParams);
        }


        /// <summary>
        /// 현재 룸을 나와 룸에 참여하거나 룸을 생성할 수 있는 마스터 서버로 돌아갑니다.
        /// </summary>
        public static bool LeaveRoom(bool becomeInactive = true)
        {
            if (OfflineMode)
            {
                offlineModeRoom = null;
                NetworkingClient.MatchMakingCallbackTargets.OnLeftRoom();
            }
            else
            {
                if (CurrentRoom == null)
                {
                    Debug.LogWarning("현재 방 상태가 null입니다. State: " + NDG_Network.NetworkClientState);
                }
                else
                {
                    becomeInactive = becomeInactive && CurrentRoom.PlayerTtl != 0; 
                }
                return NetworkingClient.OpLeaveRoom(becomeInactive);
            }

            return true;
        }

        /// <summary>
        /// 오프라인 모드로 룸에 참가합니다.
        /// </summary>
        private static void EnterOfflineRoom(string roomName, RoomOptions roomOptions, bool createdRoom)
        {
            offlineModeRoom = new Room(roomName, roomOptions, true);
            NetworkingClient.ChangeLocalID(1);
            offlineModeRoom.masterClientId = 1;
            offlineModeRoom.AddPlayer(NDG_Network.LocalPlayer);
            offlineModeRoom.LoadBalancingClient = NDG_Network.NetworkingClient;
            NDG_Network.NetworkingClient.CurrentRoom = offlineModeRoom;

            if (createdRoom)
            {
                NetworkingClient.MatchMakingCallbackTargets.OnCreatedRoom();
            }

            NetworkingClient.MatchMakingCallbackTargets.OnJoinedRoom();
        }


        /// <summary>
        /// 마스터 서버에서 현재 룸 리스트를 받아올 수 있는 기본 로비에 참여합니다.
        /// 룸 목록은 ILobbyCalbbacks.OnRoomListUpdate로 받아올 수 있습니다.
        /// </summary>
        public static bool JoinLobby()
        {
            return JoinLobby(null);
        }
        public static bool JoinLobby(TypedLobby typedLobby)
        {
            if (NDG_Network.IsConnected && NDG_Network.Server == ServerConnection.MasterServer)
            {
                return NetworkingClient.OpJoinLobby(typedLobby);
            }

            return false;
        }


        /// <summary>
        /// 로비를 떠나 더 이상 룸 목록을 업데이트 받지 않습니다.
        /// </summary>
        public static bool LeaveLobby()
        {
            if (NDG_Network.IsConnected && NDG_Network.Server == ServerConnection.MasterServer)
            {
                return NetworkingClient.OpLeaveLobby();
            }

            return false;
        }

        /// <summary>
        ///  SQL 'WHERE'과 같이 조건에 매칭되는 커스텀 룸 목록을 서버에서 얻어온 후
        ///  OnReceivedRoomListUpdate 콜백을 호출합니다.
        /// </summary>
        public static bool GetCustomRoomList(TypedLobby typedLobby, string sqlLobbyFilter)
        {
            return NetworkingClient.OpGetGameList(typedLobby, sqlLobbyFilter);
        }

        /// <summary>
        /// 로컬 플레이어의 CustomProperties를 설정하고 다른 플레이어와 동기화 합니다.
        /// </summary>
        public static bool SetPlayerCustomProperties(Hashtable customProperties)
        {
            if (customProperties == null)
            {
                customProperties = new Hashtable();
                foreach (object k in LocalPlayer.CustomProperties.Keys)
                {
                    customProperties[(string)k] = null;
                }
            }

            return LocalPlayer.SetCustomProperties(customProperties);
        }


        /// <summary>
        /// 로컬 플레이어의 특정 CustomProperties를 제거합니다.
        /// </summary>
        public static void RemovePlayerCustomProperties(string[] customPropertiesToDelete)
        {
            if (customPropertiesToDelete == null || customPropertiesToDelete.Length == 0 || LocalPlayer.CustomProperties == null)
            {
                LocalPlayer.CustomProperties = new Hashtable();
                return;
            }

            for (int i = 0; i < customPropertiesToDelete.Length; i++)
            {
                string key = customPropertiesToDelete[i];
                if (LocalPlayer.CustomProperties.ContainsKey(key))
                {
                    LocalPlayer.CustomProperties.Remove(key);
                }
            }
        }


        /// <summary>
        ///  룸에 이벤트들을 전송합니다.
        ///  이벤트를 수신하려면 모든 클래스에서 IOnEventCallback을 구현해야 합니다.
        ///  eventContent는 직렬화가 가능한 데이터타입이어야 합니다.
        /// </summary>
        public static bool RaiseEvent(byte eventCode, object eventContent, RaiseEventOptions raiseEventOptions, SendOptions sendOptions)
        {
            if (offlineMode)
            {
                if (raiseEventOptions.Receivers == ReceiverGroup.Others)
                {
                    return true;
                }

                EventData evData = new EventData { Code = eventCode, Parameters = new Dictionary<byte, object> { { ParameterCode.Data, eventContent }, { ParameterCode.ActorNr, 1 } } };
                NetworkingClient.OnEvent(evData);
                return true;
            }

            if (!InRoom || eventCode >= 200)
            {
                Debug.LogWarning("RaiseEvent(" + eventCode + ") 실패.  룸에 있고 eventCode가 200보다 작아야 합니다. (0..199).");
                return false;
            }

            return NetworkingClient.OpRaiseEvent(eventCode, eventContent, raiseEventOptions, sendOptions);
        }


        /// <summary>
        /// 서버 관련 이벤트를 서버로 보냅니다.
        /// </summary>
        private static bool RaiseEventInternal(byte eventCode, object eventContent, RaiseEventOptions raiseEventOptions, SendOptions sendOptions)
        {
            if (offlineMode)
            {
                return false;
            }

            if (!InRoom)
            {
                Debug.LogWarning("RaiseEvent(" + eventCode + ") 실패. 룸 안에 있는지 확인하세요.");
                return false;
            }

            return NetworkingClient.OpRaiseEvent(eventCode, eventContent, raiseEventOptions, sendOptions);
        }


        /// <summary>
        /// NetworkView를 할당합니다.
        /// </summary>
        public static bool AllocateViewID(NetworkView view)
        {
            if (view.ViewID != 0)
            {
                Debug.LogError("AllocateViewID() 실패. 이미 할당된 View입니다.  현재 View: " + view.ToString());
                return false;
            }

            int manualId = AllocateViewID(LocalPlayer.ActorNumber);
            view.ViewID = manualId;
            return true;
        }

        /// <summary>
        /// 마스터 클라이언트에서 Scene Object view들에 ID를 할당합니다.
        /// </summary>
        public static bool AllocateSceneViewID(NetworkView view)
        {
            if (!NDG_Network.IsMasterClient)
            {
                Debug.LogError("오직 마스터 클라이언트만 AllocateSceneViewID()를 호출할 수 있습니다.");
                return false;
            }

            if (view.ViewID != 0)
            {
                Debug.LogError("AllocateViewID() 실패. 이미 할당된 View입니다.  현재 View: " + view.ToString());
                return false;
            }

            int manualId = AllocateViewID(0);
            view.ViewID = manualId;
            return true;
        }

        /// <summary>
        /// ViewID를 할당합니다.
        /// </summary>
        /// <param name="sceneObject"></param> true일 경우 scene viewID를, false경우 로컬 플레이어 viewID를 할당합니다.
        /// <returns></returns>
        public static int AllocateViewID(bool sceneObject)
        {
            if (sceneObject && !LocalPlayer.IsMasterClient)
            {
                Debug.LogError("오직 마스터 클라이언트만 AllocateSceneViewID()를 호출할 수 있습니다.");
                return 0;
            }

            int ownerActorNumber = sceneObject ? 0 : LocalPlayer.ActorNumber;
            return AllocateViewID(ownerActorNumber);
        }


        /// <summary>
        /// ViewID를 할당합니다
        /// </summary>
        /// <param name="ownerId"></param>
        /// <returns></returns>
        public static int AllocateViewID(int ownerId)
        {
            if (ownerId == 0)
            {
                //새로운 아이디 탐색
                int newSubId = lastUsedViewSubIdStatic;
                int newViewId;
                int ownerIdOffset = ownerId * MAX_VIEW_IDS;
                for (int i = 1; i < MAX_VIEW_IDS; i++)
                {
                    newSubId = (newSubId + 1) % MAX_VIEW_IDS;
                    if (newSubId == 0)
                    {
                        continue;   // 0사용을 피함
                    }

                    newViewId = newSubId + ownerIdOffset;
                    if (!networkViewList.ContainsKey(newViewId))
                    {
                        lastUsedViewSubIdStatic = newSubId;
                        return newViewId;
                    }
                }

                // subID를 찾지 못한경우
                throw new Exception(string.Format("AllocateViewID() 실패. 이용 가능한 ID가 모두 사용중입니다. {0}", ownerId));
            }
            else
            {
                // 새로운 subID를 찾았을때
                int newSubId = lastUsedViewSubId;
                int newViewId;
                int ownerIdOffset = ownerId * MAX_VIEW_IDS;
                for (int i = 1; i <= MAX_VIEW_IDS; i++)
                {
                    newSubId = (newSubId + 1) % MAX_VIEW_IDS;
                    if (newSubId == 0)
                    {
                        continue;   // 0사용 피함
                    }

                    newViewId = newSubId + ownerIdOffset;
                    if (!networkViewList.ContainsKey(newViewId))
                    {
                        lastUsedViewSubId = newSubId;
                        return newViewId;
                    }
                }

                throw new Exception(string.Format("AllocateViewID() 실패. User {0} 은 ViewID범위를 벗어났습니다. 사용가능한 모든 ID가 사용중입니다.", ownerId));
            }
        }

        /// <summary>
        /// 게임 오브젝트를 생성합니다.
        /// </summary>
        /// <param name="prefabName"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="group"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static GameObject Instantiate(string prefabName, Vector3 position, Quaternion rotation, byte group = 0, object[] data = null)
        {
            if (CurrentRoom == null)
            {
                Debug.LogError("클라이언트가 룸에 입장하기 전에는 생성할 수 없습니다.  State: " + NDG_Network.NetworkClientState);
                return null;
            }

            UnityNet.InstantiateParameters netParams = new InstantiateParameters(prefabName, position, rotation, group, data, currentLevelPrefix, null, LocalPlayer, ServerTimestamp);
            return NetworkInstantiate(netParams, false);
        }

        public static GameObject InstantiateRoomObject(string prefabName, Vector3 position, Quaternion rotation, byte group = 0, object[] data = null)
        {
            if (CurrentRoom == null)
            {
                Debug.LogError("클라이언트가 룸에 입장하기 전에는 생성할 수 없습니다.  State: " + NDG_Network.NetworkClientState);
                return null;
            }

            if (LocalPlayer.IsMasterClient)
            {
                UnityNet.InstantiateParameters netParams = new InstantiateParameters(prefabName, position, rotation, group, data, currentLevelPrefix, null, LocalPlayer, ServerTimestamp);
                return NetworkInstantiate(netParams, true);
            }

            return null;
        }

        private static GameObject NetworkInstantiate(Hashtable networkEvent, Player creator)
        {
            string prefabName = (string)networkEvent[(byte)0];
            int serverTime = (int)networkEvent[(byte)6];
            int instantiationId = (int)networkEvent[(byte)7];

            Vector3 position;
            if (networkEvent.ContainsKey((byte)1))
            {
                position = (Vector3)networkEvent[(byte)1];
            }
            else
            {
                position = Vector3.zero;
            }

            Quaternion rotation = Quaternion.identity;
            if (networkEvent.ContainsKey((byte)2))
            {
                rotation = (Quaternion)networkEvent[(byte)2];
            }

            byte group = 0;
            if (networkEvent.ContainsKey((byte)3))
            {
                group = (byte)networkEvent[(byte)3];
            }

            byte objLevelPrefix = 0;
            if (networkEvent.ContainsKey((byte)8))
            {
                objLevelPrefix = (byte)networkEvent[(byte)8];
            }

            int[] viewsIDs;
            if (networkEvent.ContainsKey((byte)4))
            {
                viewsIDs = (int[])networkEvent[(byte)4];
            }
            else
            {
                viewsIDs = new int[1] { instantiationId };
            }

            object[] incomingInstantiationData;
            if (networkEvent.ContainsKey((byte)5))
            {
                incomingInstantiationData = (object[])networkEvent[(byte)5];
            }
            else
            {
                incomingInstantiationData = null;
            }

            // 수신 필터링 설정
            if (group != 0 && !allowedReceivingGroups.Contains(group))
            {
                return null; // Ignore group
            }


            UnityNet.InstantiateParameters netParams = new InstantiateParameters(prefabName, position, rotation, group, incomingInstantiationData, objLevelPrefix, viewsIDs, creator, serverTime);
            return NetworkInstantiate(netParams, false, true);
        }

        private static readonly HashSet<string> PrefabsWithoutMagicCallback = new HashSet<string>();

        private static GameObject NetworkInstantiate(UnityNet.InstantiateParameters parameters, bool sceneObject = false, bool instantiateEvent = false)
        {
            GameObject go = null;
            NetworkView[] networkViews;

            go = prefabPool.Instantiate(parameters.prefabName, parameters.position, parameters.rotation);


            if (go == null)
            {
                Debug.LogError("network-Instantiate 실패: " + parameters.prefabName);
                return null;
            }

            if (go.activeSelf)
            {
                Debug.LogWarning("PrefabPool.Instantiate() 비활성화 된 오브젝트를 생성합니다. " + prefabPool.GetType().Name + " PrefabId: " + parameters.prefabName);
            }


            networkViews = go.GetNetworkViewsInChildren();


            if (networkViews.Length == 0)
            {
                Debug.LogError("NDG_Network.Instantiate()는 오직 NetworkView 컴포넌트가 포함된 객체만 생성할수 있습니다. 현재 Prefab: " + parameters.prefabName);
                return null;
            }

            bool localInstantiate = !instantiateEvent && LocalPlayer.Equals(parameters.creator);
            if (localInstantiate)
            {
                parameters.viewIDs = new int[networkViews.Length];
            }

            for (int i = 0; i < networkViews.Length; i++)
            {
                if (localInstantiate)
                {
                    //로컬 클라이언트가 생성할 경우 ViewID를 할당해 주어야합니다.
                    //Scene오브젝트일 경우 플레이어 Id와 관계없이 0번으로 생성됩니다.
                    parameters.viewIDs[i] = (sceneObject) ? AllocateViewID(0) : AllocateViewID(parameters.creator.ActorNumber);
                }

                var view = networkViews[i];

                view.didAwake = false;
                view.ViewID = 0;
                view.Prefix = parameters.objLevelPrefix;
                view.InstantiationId = parameters.viewIDs[0];
                view.isRuntimeInstantiated = true;
                view.InstantiationData = parameters.data;
                view.ownershipCacheIsValid = NetworkView.OwnershipCacheState.Invalid;
                view.didAwake = true;
                view.ViewID = parameters.viewIDs[i];   

                view.Group = parameters.group;
            }

            //로컬클라이언트가 생성할 경우 생성요청을 보냅니다.
            if (localInstantiate)
            {
                SendInstantiate(parameters, sceneObject);
            }

            go.SetActive(true);

            //  INetInstantiateMagicCallback이 구현되어있는 오브젝트일 경우 바로 콜백함수를 호출합니다
            if (!PrefabsWithoutMagicCallback.Contains(parameters.prefabName))
            {
                var list = go.GetComponents<INetInstantiateMagicCallback>();
                if (list.Length > 0)
                {
                    NetworkMessageInfo pmi = new NetworkMessageInfo(parameters.creator, parameters.timestamp, networkViews[0]);
                    foreach (INetInstantiateMagicCallback callbackComponent in list)
                    {
                        callbackComponent.OnNetInstantiate(pmi);
                    }
                }
                else
                {
                    PrefabsWithoutMagicCallback.Add(parameters.prefabName);
                }
            }
            return go;
        }

        //GC를 줄이기 위해 사용됩니다.
        private static readonly Hashtable SendInstantiateEvHashtable = new Hashtable();                             
        private static readonly RaiseEventOptions SendInstantiateRaiseEventOptions = new RaiseEventOptions();

        internal static bool SendInstantiate(UnityNet.InstantiateParameters parameters, bool sceneObject = false)
        {
            // 게임오브젝트의 InstantiateId는 viewID이기도 합니다.
            int instantiateId = parameters.viewIDs[0];   

            SendInstantiateEvHashtable.Clear();     // SendInstantiateEvHashtable를 다시사용하여 GC를 줄입니다.

            SendInstantiateEvHashtable[keyByteZero] = parameters.prefabName;

            if (parameters.position != Vector3.zero)
            {
                SendInstantiateEvHashtable[keyByteOne] = parameters.position;
            }

            if (parameters.rotation != Quaternion.identity)
            {
                SendInstantiateEvHashtable[keyByteTwo] = parameters.rotation;
            }

            if (parameters.group != 0)
            {
                SendInstantiateEvHashtable[keyByteThree] = parameters.group;
            }

            // ViewIDs 길이가 1보다 클경우입니다. 그렇지 않다면 instantiateId가 viewId가 됩니다.
            if (parameters.viewIDs.Length > 1)
            {
                SendInstantiateEvHashtable[keyByteFour] = parameters.viewIDs; 
            }

            if (parameters.data != null)
            {
                SendInstantiateEvHashtable[keyByteFive] = parameters.data;
            }

            if (currentLevelPrefix > 0)
            {
                SendInstantiateEvHashtable[keyByteEight] = currentLevelPrefix;   
            }

            SendInstantiateEvHashtable[keyByteSix] = NDG_Network.ServerTimestamp;
            SendInstantiateEvHashtable[keyByteSeven] = instantiateId;


            SendInstantiateRaiseEventOptions.CachingOption = (sceneObject) ? EventCaching.AddToRoomCacheGlobal : EventCaching.AddToRoomCache;

            return NDG_Network.RaiseEventInternal(NetEvent.Instantiation, SendInstantiateEvHashtable, SendInstantiateRaiseEventOptions, SendOptions.SendReliable);
        }

        /// <summary>
        /// 타겟 NetworkView를 삭제합니다
        /// </summary>
        ///<remarks>
        ///일반적으로 룸을 나가면 게임오브젝트는 자동으로 파괴가됩니다.
        ///네트워크 객체를 파괴하는 것은 네트워크로 생성된 개체만 적용됩니다.
        ///룸에 없을때 파괴하는 경우는 로컬에서만 실행됩니다.
        ///</remarks>
        public static void Destroy(NetworkView targetView)
        {
            if (targetView != null)
            {
                RemoveInstantiatedGO(targetView.gameObject, !InRoom);
            }
            else
            {
                Debug.LogError("Destroy(targetNetworkView) 실패, 타겟 NetworView가 Null 상태입니다.");
            }
        }

        /// <summary>
        /// 타겟 게임오브젝트를 삭제합니다.
        /// </summary>
        ///<remarks>
        ///일반적으로 룸을 나가면 게임오브젝트는 자동으로 제거됩니다.
        ///네트워크 객체를 파괴하는 것은 네트워크로 생성된 개체만 적용됩니다.
        ///룸에 없을때 파괴하는 경우는 로컬에서만 실행됩니다.
        ///</remarks>
        public static void Destroy(GameObject targetGo)
        {
            RemoveInstantiatedGO(targetGo, !InRoom);
        }


        /// <summary>
        /// 타겟 플레이어의 모든 객체 및 NetworkView,RPC를 제거합니다.
        /// </summary>
        /// <remarks>
        /// 타겟 플레이어가 로컬 플레이어거나 마스터 클라이언트인 경우만 적용됩니다.
        /// </remarks>
        public static void DestroyPlayerObjects(Player targetPlayer)
        {
            if (targetPlayer == null)
            {
                Debug.LogError("DestroyPlayerObjects() 실패, targetPlayer가 Null상태 입니다.");
            }

            DestroyPlayerObjects(targetPlayer.ActorNumber);
        }

        /// <summary>
        /// targetPlayerId의 모든 객체 및 NetworkView,RPC를 제거합니다
        /// </summary>
        /// <remarks>
        /// 타겟 플레이어가 로컬 플레이어거나 마스터 클라이언트인 경우만 적용됩니다.
        /// </remarks>
        public static void DestroyPlayerObjects(int targetPlayerId)
        {
            if (!VerifyCanUseNetwork())
            {
                return;
            }
            if (LocalPlayer.IsMasterClient || targetPlayerId == LocalPlayer.ActorNumber)
            {
                DestroyPlayerObjects(targetPlayerId, false);
            }
            else
            {
                Debug.LogError("DestroyPlayerObjects() 실패, 타겟 오브젝트 주인이거나 마스터 클라이언트이어야 합니다. 현재 MasterClient: " + NDG_Network.IsMasterClient);
            }
        }

        /// <summary>
        /// 현재 룸에 있는 모든 NetworkVIew 및 RPC를 삭제합니다. 마스터 클라이언트에서만 호출할수 있으며
        /// 서버에서 버퍼링된 모든 항목을 삭제합니다.
        /// </summary>
        public static void DestroyAll()
        {
            if (IsMasterClient)
            {
                DestroyAll(false);
            }
            else
            {
                Debug.LogError("마스터 클라이언트만 호출할 수 있습니다.");
            }
        }


        /// <summary>
        /// targetPlayer에서 보낸 모든 버퍼링된 RPC를 서버에서 제거합니다.
        /// 로컬 플레이어 및 마스터클라이언트만 호출할 수 있습니다.
        /// </summary>
        public static void RemoveRPCs(Player targetPlayer)
        {
            if (!VerifyCanUseNetwork())
            {
                return;
            }

            if (!targetPlayer.IsLocal && !IsMasterClient)
            {
                Debug.LogError("Error; 마스터 크라이언트만이 다른 플레이어의 RPC를 제거할 수 있습니다.");
                return;
            }

            OpCleanActorRpcBuffer(targetPlayer.ActorNumber);
        }

        /// <summary>
        /// targetNetworkView로 전송된 모든 RPC를 서버에서 제거합니다.
        /// 로컬 플레이어 및 마스터클라이언트만 호출할 수 있습니다.
        /// </summary>
        public static void RemoveRPCs(NetworkView targetNetworkView)
        {
            if (!VerifyCanUseNetwork())
            {
                return;
            }

            CleanRpcBufferIfMine(targetNetworkView);
        }


        /// <summary>
        /// NetworkView에서 RPC를 전송하기 위한 내부 함수입니다.
        /// 직접 호출하지 말고 NetworkView.RPC로 호출해야 합니다.
        /// </summary>
        internal static void RPC(NetworkView view, string methodName, RpcTarget target, bool encrypt, params object[] parameters)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                Debug.LogError("RPC 함수 이름은 Null이거나 공백일 수 없습니다.");
                return;
            }

            if (!VerifyCanUseNetwork())
            {
                return;
            }

            if (CurrentRoom == null)
            {
                Debug.LogWarning("RPC는 오직 룸 안에서 사용할 수 있습니다. RPC:  \"" + methodName + "\" ");
                return;
            }

            if (NetworkingClient != null)
            {
                RPC(view, methodName, target, null, encrypt, parameters);
            }
            else
            {
                Debug.LogWarning(" RPC를 호출할 수 없습니다. " + methodName  );
            }
        }

        internal static void RPC(NetworkView view, string methodName, Player targetPlayer, bool encrpyt, params object[] parameters)
        {
            if (!VerifyCanUseNetwork())
            {
                return;
            }

            if (CurrentRoom == null)
            {
                Debug.LogWarning("RPC는 오직 룸 안에서 사용할 수 있습니다. RPC:  \"" + methodName + "\" ");
                return;
            }

            if (LocalPlayer == null)
            {
                Debug.LogError("PRC를 보낼수 없습니다. LocalPlayer가 Null상태 입니다. RPC: \"" + methodName + "\".");
            }

            if (NetworkingClient != null)
            {
                RPC(view, methodName, RpcTarget.Others, targetPlayer, encrpyt, parameters);
            }
            else
            {
                Debug.LogWarning(" RPC를 호출할 수 없습니다. " + methodName);
            }
        }

        /// <summary>
        /// 특정 타입의 GameObject를 찾습니다.
        /// </summary>
        public static HashSet<GameObject> FindGameObjectsWithComponent(Type type)
        {
            HashSet<GameObject> objectsWithComponent = new HashSet<GameObject>();

            Component[] targetComponents = (Component[])GameObject.FindObjectsOfType(type);
            for (int index = 0; index < targetComponents.Length; index++)
            {
                if (targetComponents[index] != null)
                {
                    objectsWithComponent.Add(targetComponents[index].gameObject);
                }
            }

            return objectsWithComponent;
        }


        /// <summary>
        /// 이 메서드는 비동기식으로 Scene을 로드하며 프로세스 중에 네트워크 메세지를 일시 중지합니다.
        /// </summary>
        public static void LoadLevel(int levelNumber)
        {
            if (NetworkHandler.AppQuits)
            {
                return;
            }

            if (NDG_Network.AutomaticallySyncScene)
            {
                SetLevelInPropsIfSynced(levelNumber);
            }

            NDG_Network.IsMessageQueueRunning = false;
            loadingLevelAndPausedNetwork = true;
            _AsyncLevelLoadingOperation = SceneManager.LoadSceneAsync(levelNumber, LoadSceneMode.Single);
        }

        public static void LoadLevel(string levelName)
        {
            if (NetworkHandler.AppQuits)
            {
                return;
            }

            if (NDG_Network.AutomaticallySyncScene)
            {
                SetLevelInPropsIfSynced(levelName);
            }

            NDG_Network.IsMessageQueueRunning = false;
            loadingLevelAndPausedNetwork = true;
            _AsyncLevelLoadingOperation = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Single);
        }

        private static void SetupLogging()
        {
            if(NDG_Network.LogLevel == NetLogLevel.ErrorsOnly)
            {
                NDG_Network.LogLevel = ServerSetting.NetLogging;
            }

            if(NDG_Network.NetworkingClient.LoadBalancingPeer.DebugOut == DebugLevel.ERROR)
            {
                NDG_Network.NetworkingClient.LoadBalancingPeer.DebugOut = ServerSetting.AppSettings.NetworkLogging;
            }
        }


    }
}