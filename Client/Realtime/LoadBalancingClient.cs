

namespace NDG.Realtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEngine;
    using NDG.UnityNet;

    #region Enums
    public enum ClientState
    {
        PeerCreated,

        Authenticating,

        Authenticated,

        JoiningLobby,

        JoinedLobby,

        DisconnectingFromMasterServer,

        ConnectingToGameServer,

        ConnectedToGameServer,

        Joining,

        Joined,

        Leaving,

        DisconnectingFromGameServer,

        ConnectingToMasterServer,

        Disconnecting,

        Disconnected,

        ConnectedToMasterServer,

        ConnectingToNameServer,

        ConnectedToNameServer,

        DisconnectingFromNameServer,

        ConnectWithFallbackProtocol
    }

    public enum DisconnectCause
    {
        None,

        ExceptionOnConnect,

        Exception,

        ServerTimeout,

        ClientTimeout,

        DisconnectByServerLogic,

        DisconnectByServerReasonUnknown,

        InvalidAuthentication,

        CustomAuthenticationFailed,

        AuthenticationTicketExpired,

        MaxCcuReached,

        InvalidRegion,

        OperationNotAllowedInCurrentState,

        DisconnectByClientLogic
    }

    public enum JoinType
    {
        CreateRoom,

        JoinRoom,

        JoinRandomRoom,

        JoinRandomOrCreateRoom,

        JoinOrCreateRoom
    }

    public enum ServerConnection
    {
        MasterServer,

        GameServer,

        NameServer,
    }

    public enum EncryptionMode
    {
        PayloadEncryption,

        DatagramEncryption = 10,

        DatagramEncryptionRandomSequence = 11,

        DatagramEncryptionGCMRandomSequence = 12,

    }

    public static class EncryptionDataParameters
    {
        public const byte Mode = 0;

        public const byte Secret1 = 1;

        public const byte Secret2 = 2;

    }
    #endregion


    public class LoadBalancingClient : INetPeerListener
    {

        //????????? LoadBalancingPeer?? API?? ?????? ?????? ???????.
        public LoadBalancingPeer LoadBalancingPeer { get; private set; }

        //??????????? ????? ???????? ?????? ????????? ????????.
        public SerializationProtocol SerializationProtocol
        {
            get
            {
                return this.LoadBalancingPeer.SerializationProtocolType;
            }
            set
            {
                this.LoadBalancingPeer.SerializationProtocolType = value;
            }
        }

        //????????? ????????. ???????? ??????????? ?��?????.
        public string AppVersion { get; set; }

        //???????? ???? AppID????.
        public string AppId { get; set; }

        //?? ???? ????���ﮜ ???? ????.
        public AuthenticationValues AuthValues { get; set; }

        //????? ???? ??? ????? ????????.
        public AuthModeOption AuthMode = AuthModeOption.Auth;

        //????? ???? ??? ????? ????????.
        public EncryptionMode EncryptionMode = EncryptionMode.PayloadEncryption;

        public ConnectionProtocol? ExpectedProtocol { get; private set; }
        private string TokenForInit
        {
            get
            {
                if (this.AuthMode == AuthModeOption.Auth)
                {
                    return null;
                }
                return (this.AuthValues != null) ? this.AuthValues.Token : null;
            }
        }

        private string tokenCache;

        /// <summary>
        /// ????????? NameServer?? ?????? Master ???? ???? ???????? ??? True????.
        /// </summary>
        public bool IsUsingNameServer { get; set; }


        public string NameServerHost = "";

        public string NameServerHttp = "";

        public string NameServerAddress { get { return this.GetNameServerAddress(); } }

        private static readonly Dictionary<ConnectionProtocol, int> ProtocolToNameServerPort = new Dictionary<ConnectionProtocol, int>() { { ConnectionProtocol.Udp, 5058 }, { ConnectionProtocol.Tcp, 4533 } };

        public bool UseAlternativeUdpPorts { get; set; }

        public bool EnableProtocolFallback { get; set; }

        public string CurrentServerAddress { get { return this.LoadBalancingPeer.ServerAddress; } }



        public string MasterServerAddress { get; set; }

        public string GameServerAddress { get; protected internal set; }

        public ServerConnection Server { get; private set; }

        public int MasterServerPort { get; set; }



        private ClientState state = ClientState.PeerCreated;

        public string ProxyServerAddress;



        public ClientState State
        {
            get
            {
                return this.state;
            }

            set
            {
                if (this.state == value)
                {
                    return;
                }
                ClientState previousState = this.state;
                this.state = value;
                if (StateChanged != null)
                    StateChanged(previousState, this.state);
            }
        }

        /// <summary>
        /// ????????? ???? ?????? ??????????? ???��? ???????.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return this.LoadBalancingPeer != null && this.State != ClientState.PeerCreated && this.State != ClientState.Disconnected;
            }
        }

        /// <summary>
        /// ???? ?? Operation?? ?????? ??? ???????? ???????.
        /// </summary>
        public bool IsConnectedAndReady
        {
            get
            {
                if (this.LoadBalancingPeer == null)
                    return false;

                switch (this.state)
                {
                    case ClientState.PeerCreated:
                    case ClientState.Disconnected:
                    case ClientState.Disconnecting:
                    case ClientState.DisconnectingFromGameServer:
                    case ClientState.DisconnectingFromMasterServer:
                    case ClientState.ConnectingToGameServer:
                    case ClientState.ConnectingToMasterServer:
                    case ClientState.Joining:
                    case ClientState.Leaving:
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// ????????? ???��? ?????????? ????? ????? ???????.
        /// </summary>
        public event Action<ClientState, ClientState> StateChanged;

        /// <summary>
        /// ???? ???? ????? ????? ???????.
        /// </summary>
        public event Action<EventData> EventReceived;

        /// <summary>
        /// Operation response ????? ????? ????? ???????.
        /// </summary>
        public event Action<OperationResponse> OpResponseReceived;

        public ConnectionCallbacksContainer ConnectionCallbackTargets;

        public MatchMakingCallbacksContainer MatchMakingCallbackTargets;

        internal InRoomCallbacksContainer InRoomCallbackTargets;

        internal LobbyCallbacksContainer LobbyCallbackTargets;

        internal ErrorInfoCallbacksContainer ErrorInfoCallbackTargets;

        public DisconnectCause DisconnectedCause { get; protected set; }


        public bool InLobby
        {
            get
            {
                return this.State == ClientState.JoinedLobby;
            }
        }

        public TypedLobby CurrentLobby { get; internal set; }

        /// <summary>
        /// true 일시 클라이언트는 마스터 서버에서 사용 가능한 로비 목록을 가져옵니다.
        /// </summary>
        public bool EnableLobbyStatistics;

        private readonly List<TypedLobbyInfo> lobbyStatistics = new List<TypedLobbyInfo>();

        public Player LocalPlayer { get; internal set; }

        public string NickName
        {
            get
            {
                return this.LocalPlayer.NickName;
            }

            set
            {
                if (this.LocalPlayer == null)
                {
                    return;
                }

                this.LocalPlayer.NickName = value;
            }
        }

        public string UserId
        {
            get
            {
                if (this.AuthValues != null)
                {
                    return this.AuthValues.UserId;
                }
                return null;
            }
            set
            {
                if (this.AuthValues == null)
                {
                    this.AuthValues = new AuthenticationValues();
                }
                this.AuthValues.UserId = value;
            }
        }

        public Room CurrentRoom { get; set; }

        public bool InRoom
        {
            get
            {
                return this.state == ClientState.Joined && this.CurrentRoom != null;
            }
        }

        /// ?????? ????????? ?��???? ????
        public int PlayersOnMasterCount { get; internal set; }

        public int PlayersInRoomsCount { get; internal set; }

        public int RoomsCount { get; internal set; }

        private JoinType lastJoinType;

        private EnterRoomParams enterRoomParamsCache;

        /// <summary>
        /// ?? ???? ???��? ?????? ?????? ??????? ???? ??????.
        /// </summary>
        private OperationResponse failedRoomEntryOperation;

        public string CloudRegion { get; private set; }

        public string CurrentCluster { get; private set; }

        public RegionHandler RegionHandler;

        private string bestRegionSummaryFromStorage;

        public string SummaryToCache;

        private bool connectToBestRegion = true;



        private class CallbackTargetChange
        {
            public readonly object Target;
            public readonly bool AddTarget;

            public CallbackTargetChange(object target, bool addTarget)
            {
                this.Target = target;
                this.AddTarget = addTarget;
            }
        }

        private readonly Queue<CallbackTargetChange> callbackTargetChanges = new Queue<CallbackTargetChange>();
        private readonly HashSet<object> callbackTargets = new HashSet<object>();




        /// <summary>
        /// UDP ???????? ??? ?????? ?????????? ?????? LoadBalancingClient?? ????????.
        /// </summary>
        /// <param name="protocol"></param>
        public LoadBalancingClient(ConnectionProtocol protocol = ConnectionProtocol.Udp)
        {
            this.ConnectionCallbackTargets = new ConnectionCallbacksContainer(this);
            this.MatchMakingCallbackTargets = new MatchMakingCallbacksContainer(this);
            this.InRoomCallbackTargets = new InRoomCallbacksContainer(this);
            this.LobbyCallbackTargets = new LobbyCallbacksContainer(this);
            this.ErrorInfoCallbackTargets = new ErrorInfoCallbacksContainer(this);

            this.LoadBalancingPeer = new LoadBalancingPeer(this, protocol);
            this.SerializationProtocol = SerializationProtocol.GpBinaryV18;
            this.LocalPlayer = this.CreatePlayer(string.Empty, -1, true, null);

            this.State = ClientState.PeerCreated;
        }

        /// <summary>
        /// ??????? ???? ????? ???? ??????? LoadBalancingClient?? ????????.
        /// </summary>
        /// <param name="masterAddress"></param>
        /// <param name="appId"></param>
        /// <param name="gameVersion"></param>
        /// <param name="protocol"></param>
        public LoadBalancingClient(string masterAddress, string appId, string gameVersion, ConnectionProtocol protocol = ConnectionProtocol.Udp) : this(protocol)
        {
            this.MasterServerAddress = masterAddress;
            this.AppId = appId;
            this.AppVersion = gameVersion;
        }

        public int NameServerPortOverride;

        /// <summary>
        /// ?????? ???????? ???????? Name Server?? ????????.
        /// </summary>
        /// <returns></returns>
        private string GetNameServerAddress()
        {
            var protocolPort = 0;
            ProtocolToNameServerPort.TryGetValue(this.LoadBalancingPeer.TransportProtocol, out protocolPort);
            if (this.LoadBalancingPeer.TransportProtocol == ConnectionProtocol.Udp && this.UseAlternativeUdpPorts)
            {
                protocolPort = 27000;
            }

            if (this.NameServerPortOverride != 0)
            {
                this.DebugReturn(DebugLevel.INFO, string.Format("??????? NameServerPortOverride: {0}", this.NameServerPortOverride));
                protocolPort = this.NameServerPortOverride;
            }

            switch (this.LoadBalancingPeer.TransportProtocol)
            {
                case ConnectionProtocol.Udp:
                case ConnectionProtocol.Tcp:
                    return string.Format("{0}:{1}", NameServerHost, protocolPort);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }



        #region Operations and Commands

        public virtual bool ConnectUsingSettings(AppSettings appSettings)
        {
            if (appSettings == null)
            {
                this.DebugReturn(DebugLevel.ERROR, "ConnectUsingSettings failed. The appSettings can't be null.'");
                return false;
            }

            this.AppId = appSettings.AppIdRealtime;
            this.AppVersion = appSettings.AppVersion;

            this.IsUsingNameServer = appSettings.UseNameServer;

            this.EnableLobbyStatistics = appSettings.EnableLobbyStatistics;
            this.LoadBalancingPeer.DebugOut = appSettings.NetworkLogging;

            this.AuthMode = appSettings.AuthMode;
            this.LoadBalancingPeer.TransportProtocol = appSettings.Protocol;
            this.ExpectedProtocol = appSettings.Protocol;
            this.EnableProtocolFallback = appSettings.EnableProtocolFallback;

            this.connectToBestRegion = true;
            this.DisconnectedCause = DisconnectCause.None;


            if (this.IsUsingNameServer)
            {
                this.Server = ServerConnection.NameServer;
                if (!appSettings.IsDefaultNameServer)
                {
                    this.NameServerHost = appSettings.Server;
                }

                this.ProxyServerAddress = appSettings.ProxyServer;
                this.NameServerPortOverride = appSettings.Port;
                if (!this.LoadBalancingPeer.Connect(this.NameServerAddress, this.ProxyServerAddress, this.AppId, this.TokenForInit))
                {
                    return false;
                }

                this.State = ClientState.ConnectingToNameServer;
            }
            else
            {
                this.Server = ServerConnection.MasterServer;
                int portToUse = appSettings.IsDefaultPort ? 5055 : appSettings.Port;    // TODO: setup new (default) port config
                this.MasterServerAddress = string.Format("{0}:{1}", appSettings.Server, portToUse);
                this.SerializationProtocol = SerializationProtocol.GpBinaryV16; // this is a workaround to use On Premises Servers, which don't support GpBinaryV18 yet.
                if (!this.LoadBalancingPeer.Connect(this.MasterServerAddress, this.ProxyServerAddress, this.AppId, this.TokenForInit))
                {
                    return false;
                }

                this.State = ClientState.ConnectingToMasterServer;
            }

            return true;
        }


        /// <summary>
        /// ?????? ???? ??? ?? AppId ????? ?????? ?????? ?????? ??????? ???��????? ????????.
        /// </summary>
        /// <returns></returns>
        public virtual bool ConnectToMasterServer()
        {
            if (string.IsNullOrEmpty(this.AppId) || !this.IsUsingNameServer)
            {
                this.SerializationProtocol = SerializationProtocol.GpBinaryV16;
            }

            this.connectToBestRegion = false;
            this.DisconnectedCause = DisconnectCause.None;
            if (this.LoadBalancingPeer.Connect(this.MasterServerAddress, this.ProxyServerAddress, this.AppId, this.TokenForInit))
            {
                this.State = ClientState.ConnectingToMasterServer;
                this.Server = ServerConnection.MasterServer;
                return true;
            }

            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool ConnectToNameServer()
        {
            this.IsUsingNameServer = true;
            this.CloudRegion = null;


            this.connectToBestRegion = false;
            this.DisconnectedCause = DisconnectCause.None;
            if (!this.LoadBalancingPeer.Connect(this.NameServerAddress, this.ProxyServerAddress, "NameServer", this.TokenForInit))
            {
                return false;
            }

            this.State = ClientState.ConnectingToNameServer;
            this.Server = ServerConnection.NameServer;
            return true;
        }

        /// <summary>
        /// ??? ???? ?????? ???? ????
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public bool ConnectToRegionMaster(string region)
        {
            if (string.IsNullOrEmpty(region))
            {
                this.DebugReturn(DebugLevel.ERROR, "ConnectToRegionMaster() ????. egion?? null???????? empty ????????.");
                return false;
            }

            this.IsUsingNameServer = true;

            if (this.State == ClientState.ConnectedToNameServer)
            {
                this.CloudRegion = region;
                return this.CallAuthenticate();
            }

            this.LoadBalancingPeer.Disconnect();

            if (!string.IsNullOrEmpty(region) && !region.Contains("/"))
            {
                region = region + "/*";
            }
            this.CloudRegion = region;




            this.connectToBestRegion = false;
            this.DisconnectedCause = DisconnectCause.None;
            if (!this.LoadBalancingPeer.Connect(this.NameServerAddress, this.ProxyServerAddress, "NameServer", null))
            {
                return false;
            }

            this.State = ClientState.ConnectingToNameServer;
            this.Server = ServerConnection.NameServer;
            return true;
        }

        private bool Connect(string serverAddress, string proxyServerAddress, ServerConnection serverType)
        {

            if (this.State == ClientState.Disconnecting)
            {
                this.DebugReturn(DebugLevel.ERROR, "Connect() failed. Can't connect while disconnecting (still). Current state: " + this.State);
                return false;
            }

            // DNS ????? ????? ?? ????? ??�N???? ?????? ?????? ???��?? ?????? ?????? ?? ??????.
            this.DisconnectedCause = DisconnectCause.None;
            bool connecting = this.LoadBalancingPeer.Connect(serverAddress, proxyServerAddress, this.AppId, this.TokenForInit);

            if (connecting)
            {
                this.Server = serverType;

                switch (serverType)
                {
                    case ServerConnection.NameServer:
                        State = ClientState.ConnectingToNameServer;
                        break;
                    case ServerConnection.MasterServer:
                        State = ClientState.ConnectingToMasterServer;
                        break;
                    case ServerConnection.GameServer:
                        State = ClientState.ConnectingToGameServer;
                        break;
                }
            }

            return connecting;
        }

        /// <summary>
        /// ????????? ?????? ?????? ???????.
        /// </summary>
        /// <param name="cause"></param>
        public void Disconnect(DisconnectCause cause = DisconnectCause.DisconnectByClientLogic)
        {
            if (this.State != ClientState.Disconnected)
            {
                this.State = ClientState.Disconnecting;
                this.DisconnectedCause = cause;
                this.LoadBalancingPeer.Disconnect();
            }
        }

        private void DisconnectToReconnect()
        {
            switch (this.Server)
            {
                case ServerConnection.NameServer:
                    this.State = ClientState.DisconnectingFromNameServer;
                    break;
                case ServerConnection.MasterServer:
                    this.State = ClientState.DisconnectingFromMasterServer;
                    break;
                case ServerConnection.GameServer:
                    this.State = ClientState.DisconnectingFromGameServer;
                    break;
            }

            this.LoadBalancingPeer.Disconnect();
        }

        private bool CallAuthenticate()
        {
            if (this.AuthMode == AuthModeOption.Auth)
            {
                if (!this.CheckIfOpCanBeSent(OperationCode.Authenticate, this.Server, "Authenticate"))
                {
                    return false;
                }
                return this.LoadBalancingPeer.OpAuthenticate(this.AppId, this.AppVersion, this.AuthValues, this.CloudRegion, (this.EnableLobbyStatistics && this.Server == ServerConnection.MasterServer));
            }
            else
            {
                if (!this.CheckIfOpCanBeSent(OperationCode.AuthenticateOnce, this.Server, "AuthenticateOnce"))
                {
                    return false;
                }

                ConnectionProtocol targetProtocolPastNameServer = this.ExpectedProtocol != null ? (ConnectionProtocol)this.ExpectedProtocol : this.LoadBalancingPeer.TransportProtocol;
                return this.LoadBalancingPeer.OpAuthenticateOnce(this.AppId, this.AppVersion, this.AuthValues, this.CloudRegion, this.EncryptionMode, targetProtocolPastNameServer);
            }
        }

        private bool OpGetRegions()
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.GetRegions, this.Server, "GetRegions"))
            {
                return false;
            }

            bool sent = this.LoadBalancingPeer.OpGetRegions(this.AppId);
            return sent;
        }

        public bool OpJoinLobby(TypedLobby lobby)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinLobby, this.Server, "JoinLobby"))
            {
                return false;
            }

            if (lobby == null)
            {
                lobby = TypedLobby.Default;
            }
            bool sent = this.LoadBalancingPeer.OpJoinLobby(lobby);
            if (sent)
            {
                this.CurrentLobby = lobby;
                this.State = ClientState.JoiningLobby;
            }

            return sent;
        }

        public bool OpLeaveLobby()
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.LeaveLobby, this.Server, "LeaveLobby"))
            {
                return false;
            }
            return this.LoadBalancingPeer.OpLeaveLobby();
        }

        public bool OpJoinRandomRoom(OpJoinRandomRoomParams opJoinRandomRoomParams = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinRandomGame, this.Server, "JoinRandomGame"))
            {
                return false;
            }

            if (opJoinRandomRoomParams == null)
            {
                opJoinRandomRoomParams = new OpJoinRandomRoomParams();
            }

            this.enterRoomParamsCache = new EnterRoomParams();
            this.enterRoomParamsCache.Lobby = opJoinRandomRoomParams.TypedLobby;
            this.enterRoomParamsCache.ExpectedUsers = opJoinRandomRoomParams.ExpectedUsers;


            bool sending = this.LoadBalancingPeer.OpJoinRandomRoom(opJoinRandomRoomParams);
            if (sending)
            {
                this.lastJoinType = JoinType.JoinRandomRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }

        public bool OpCreateRoom(EnterRoomParams enterRoomParams)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.CreateGame, this.Server, "CreateGame"))
            {
                return false;
            }
            //bool onGameServer = this.Server == ServerConnection.GameServer;
            bool onGameServer = true; // 분산 처리시 수정
            enterRoomParams.OnGameServer = onGameServer;
            State = ClientState.ConnectingToGameServer;
            OnStatusChanged(StatusCode.Connect);
            ///////////////////////////////////////////////
            // if (!onGameServer)
            // {
            // }

            Hashtable allProps = new Hashtable();
            allProps.MergeStringKeys(this.LocalPlayer.CustomProperties);

            if (!string.IsNullOrEmpty(this.LocalPlayer.NickName))
            {
                allProps[ActorProperties.PlayerName] = this.LocalPlayer.NickName;
            }

            enterRoomParams.PlayerProperties = allProps;

            this.enterRoomParamsCache = enterRoomParams;


            bool sending = this.LoadBalancingPeer.OpCreateRoom(enterRoomParams);
            if (sending)
            {
                this.lastJoinType = JoinType.CreateRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }

        public bool OpJoinRoom(EnterRoomParams enterRoomParams)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.JoinGame, this.Server, "JoinRoom"))
            {
                return false;
            }
            //bool onGameServer = this.Server == ServerConnection.GameServer;
            bool onGameServer = true;
            state = ClientState.ConnectingToGameServer;
            OnStatusChanged(StatusCode.Connect);

             Hashtable allProps = new Hashtable();
            allProps.MergeStringKeys(this.LocalPlayer.CustomProperties);

            if (!string.IsNullOrEmpty(this.LocalPlayer.NickName))
            {
                allProps[ActorProperties.PlayerName] = this.LocalPlayer.NickName;
            }
            enterRoomParams.PlayerProperties = allProps;


            enterRoomParams.OnGameServer = onGameServer;
            // if (!onGameServer)
            // {
                this.enterRoomParamsCache = enterRoomParams;
            //}

            bool sending = this.LoadBalancingPeer.OpJoinRoom(enterRoomParams);
            if (sending)
            {
                this.lastJoinType = (enterRoomParams.CreateIfNotExists) ? JoinType.JoinOrCreateRoom : JoinType.JoinRoom;
                this.State = ClientState.Joining;
            }
            return sending;
        }

        public bool OpLeaveRoom(bool becomeInactive, bool sendAuthCookie = false)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.Leave, this.Server, "LeaveRoom"))
            {
                return false;
            }

            this.State = ClientState.Leaving;
            return this.LoadBalancingPeer.OpLeaveRoom(becomeInactive, sendAuthCookie);
        }

        public bool OpGetGameList(TypedLobby typedLobby, string sqlLobbyFilter)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.GetGameList, this.Server, "GetGameList"))
            {
                return false;
            }
            return this.LoadBalancingPeer.OpGetGameList(typedLobby, sqlLobbyFilter);
        }

        protected internal bool OpSetPropertiesOfActor(int actorNr, Hashtable actorProperties, Hashtable expectedProperties = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.SetProperties, this.Server, "SetProperties"))
            {
                return false;
            }
            if (actorProperties == null || actorProperties.Count == 0)
            {
                this.DebugReturn(DebugLevel.ERROR, "OpSetPropertiesOfActor() ????. actorProperties?? null???????? ?????????.");
                return false;
            }
            bool res = this.LoadBalancingPeer.OpSetPropertiesOfActor(actorNr, actorProperties, expectedProperties);
            if (res && !this.CurrentRoom.BroadcastPropertiesChangeToAll && (expectedProperties == null || expectedProperties.Count == 0))
            {
                Player target = this.CurrentRoom.GetPlayer(actorNr);
                if (target != null)
                {
                    target.InternalCacheProperties(actorProperties);
                    this.InRoomCallbackTargets.OnPlayerPropertiesUpdate(target, actorProperties);
                }
            }
            return res;
        }

        public bool OpSetCustomPropertiesOfRoom(Hashtable propertiesToSet, Hashtable expectedProperties = null)
        {
            if (propertiesToSet == null || propertiesToSet.Count == 0)
            {
                this.DebugReturn(DebugLevel.ERROR, "OpSetCustomPropertiesOfRoom() ????. propertiesToSet?? null???? ?????????.");
                return false;
            }
            Hashtable customGameProps = new Hashtable();
            customGameProps.MergeStringKeys(propertiesToSet);
            if (customGameProps.Count == 0)
            {
                this.DebugReturn(DebugLevel.ERROR, "OpSetCustomPropertiesOfRoom() ????. ��???? ??? ??????? ?????? ??????.");
                return false;
            }
            return this.OpSetPropertiesOfRoom(customGameProps, expectedProperties);
        }

        protected internal bool OpSetPropertyOfRoom(byte propCode, object value)
        {
            Hashtable properties = new Hashtable();
            properties[propCode] = value;
            return this.OpSetPropertiesOfRoom(properties);
        }

        protected internal bool OpSetPropertiesOfRoom(Hashtable gameProperties, Hashtable expectedProperties = null)
        {
            if (!this.CheckIfOpCanBeSent(OperationCode.SetProperties, this.Server, "SetProperties"))
            {
                return false;
            }
            if (gameProperties == null || gameProperties.Count == 0)
            {
                this.DebugReturn(DebugLevel.ERROR, "OpSetPropertiesOfRoom() ????. gameProperties?? Null???????? ?????? ???????.");
                return false;
            }
            bool res = this.LoadBalancingPeer.OpSetPropertiesOfRoom(gameProperties, expectedProperties);
            if (res && !this.CurrentRoom.BroadcastPropertiesChangeToAll && (expectedProperties == null || expectedProperties.Count == 0))
            {
                this.CurrentRoom.InternalCacheProperties(gameProperties);
                this.InRoomCallbackTargets.OnRoomPropertiesUpdate(gameProperties);
            }
            return res;
        }

        /// <summary>
        /// ???? ?? ��???? ??? ???????? ???? ?�� ??? ??? ?��?????? ???????.
        /// </summary>
        /// <param name="eventCode"></param>
        /// <param name="customEventContent"></param>
        /// <param name="raiseEventOptions"></param>
        /// <param name="sendOptions"></param>
        /// <returns></returns>
        public virtual bool OpRaiseEvent(byte eventCode, object customEventContent, RaiseEventOptions raiseEventOptions, SendOptions sendOptions)
        {
            if (this.LoadBalancingPeer == null)
            {
                return false;
            }
            if (!this.CheckIfOpCanBeSent(OperationCode.RaiseEvent, this.Server, "RaiseEvent"))
            {
                return false;
            }
            return this.LoadBalancingPeer.OpRaiseEvent(eventCode, customEventContent, raiseEventOptions, sendOptions);
        }

        public virtual bool OpChangeGroups(byte[] groupsToRemove, byte[] groupsToAdd)
        {
            if (this.LoadBalancingPeer == null)
            {
                return false;
            }
            if (!this.CheckIfOpCanBeSent(OperationCode.ChangeGroups, this.Server, "ChangeGroups"))
            {
                return false;
            }
            return this.LoadBalancingPeer.OpChangeGroups(groupsToRemove, groupsToAdd);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// ??????????? ?��? ???? ??????.
        /// </summary>
        private void ReadoutProperties(Hashtable gameProperties, Hashtable actorProperties, int targetActorNr)
        {
            if (this.CurrentRoom != null && gameProperties != null)
            {
                this.CurrentRoom.InternalCacheProperties(gameProperties);
                if (this.InRoom)
                {
                    this.InRoomCallbackTargets.OnRoomPropertiesUpdate(gameProperties);
                }
            }

            if (actorProperties != null && actorProperties.Count > 0)
            {
                if (targetActorNr > 0)
                {
                    Player target = this.CurrentRoom.GetPlayer(targetActorNr);
                    if (target != null)
                    {
                        Hashtable props = this.ReadoutPropertiesForActorNr(actorProperties, targetActorNr);
                        target.InternalCacheProperties(props);
                        this.InRoomCallbackTargets.OnPlayerPropertiesUpdate(target, props);
                    }
                }
                else
                {
                    int actorNr;
                    Hashtable props;
                    string newName;
                    Player target;

                    foreach (object key in actorProperties.Keys)
                    {
                        actorNr = (int)key;
                        props = (Hashtable)actorProperties[key];
                        newName = (string)props[ActorProperties.PlayerName];

                        target = this.CurrentRoom.GetPlayer(actorNr);
                        if (target == null)
                        {
                            target = this.CreatePlayer(newName, actorNr, false, props);
                            this.CurrentRoom.StorePlayer(target);
                        }
                        target.InternalCacheProperties(props);
                    }
                }
            }
        }

        private Hashtable ReadoutPropertiesForActorNr(Hashtable actorProperties, int actorNr)
        {
            if (actorProperties.ContainsKey(actorNr))
            {
                return (Hashtable)actorProperties[actorNr];
            }

            return actorProperties;
        }

        /// <summary>
        /// ???? ?��?????? ID?? ??????? ??????.
        /// </summary>
        public void ChangeLocalID(int newID)
        {
            if (this.LocalPlayer == null)
            {
                this.DebugReturn(DebugLevel.WARNING, string.Format("???? Actor?? null????????. mLocalActor: {0} mActors==null: {1} newID: {2}", this.LocalPlayer, this.CurrentRoom.Players == null, newID));
            }

            if (this.CurrentRoom == null)
            {
                this.LocalPlayer.ChangeLocalID(newID);
                this.LocalPlayer.RoomReference = null;
            }
            else
            {
                this.CurrentRoom.RemovePlayer(this.LocalPlayer);

                this.LocalPlayer.ChangeLocalID(newID);

                this.CurrentRoom.StorePlayer(this.LocalPlayer);
            }
        }

        /// <summary>
        /// Game Server에서 방에 성공적으로 참여하거나 생성되었을때 호출됩니다.
        /// response를 읽어 로컬 플레이어의 액터번호를 찾은 후 방과 플레이어의 property를 설정합니다.
        /// </summary>
        private void GameEnteredOnGameServer(OperationResponse operationResponse)
        {
            this.CurrentRoom = this.CreateRoom(this.enterRoomParamsCache.RoomName, this.enterRoomParamsCache.RoomOptions);
            this.CurrentRoom.LoadBalancingClient = this;

            // ActorList는 ID를 사용하여 업데이트하므로 actorList를 업데이트 하는 대신 로컬 ID를 먼저 변경합니다.
            int localActorNr = (int)operationResponse[ParameterCode.ActorNr];
            this.ChangeLocalID(localActorNr);

            if (operationResponse.Parameters.ContainsKey(ParameterCode.ActorList))
            {
                int[] actorsInRoom = (int[])operationResponse.Parameters[ParameterCode.ActorList];
                this.UpdatedActorList(actorsInRoom);
            }


            Hashtable actorProperties = (Hashtable)operationResponse[ParameterCode.PlayerProperties];
            Hashtable gameProperties = (Hashtable)operationResponse[ParameterCode.GameProperties];
            this.ReadoutProperties(gameProperties, actorProperties, 0);

            // object temp;
            // if (operationResponse.Parameters.TryGetValue(ParameterCode.RoomOptionFlags, out temp))
            // {
            //     this.CurrentRoom.InternalCacheRoomFlags((int)temp);
            // }

            this.State = ClientState.Joined;


            // //SuppressRoomEvents = true 일시 OnCreatedRoom 및 OnJoinedRoom 콜백이 호출되며 여기에는 방과 플레이어에 대한 정보가 포함되어있습니다.
            // if (this.CurrentRoom.SuppressRoomEvents)
            // {
            if (this.lastJoinType == JoinType.CreateRoom || (this.lastJoinType == JoinType.JoinOrCreateRoom && this.LocalPlayer.ActorNumber == 1))
            {
                this.MatchMakingCallbackTargets.OnCreatedRoom();
            }

            this.MatchMakingCallbackTargets.OnJoinedRoom();
            // }
        }

        private void UpdatedActorList(int[] actorsInGame)
        {
            if (actorsInGame != null)
            {
                foreach (int userId in actorsInGame)
                {
                    Player target = this.CurrentRoom.GetPlayer(userId);
                    if (target == null)
                    {
                        this.CurrentRoom.StorePlayer(this.CreatePlayer(string.Empty, userId, false, null));
                    }
                }
            }
        }

        protected internal virtual Player CreatePlayer(string actorName, int actorNumber, bool isLocal, Hashtable actorProperties)
        {
            Player newPlayer = new Player(actorName, actorNumber, isLocal, actorProperties);
            return newPlayer;
        }

        protected internal virtual Room CreateRoom(string roomName, RoomOptions opt)
        {
            Room r = new Room(roomName, opt);
            return r;
        }

        private bool CheckIfOpAllowedOnServer(byte opCode, ServerConnection serverConnection)
        {
            switch (serverConnection)
            {
                case ServerConnection.MasterServer:
                    switch (opCode)
                    {
                        case OperationCode.CreateGame:
                        case OperationCode.Authenticate:
                        case OperationCode.AuthenticateOnce:
                        case OperationCode.GetGameList:
                        case OperationCode.GetLobbyStats:
                        case OperationCode.JoinGame:
                        case OperationCode.JoinLobby:
                        case OperationCode.LeaveLobby:
                        case OperationCode.ServerSettings:
                        case OperationCode.JoinRandomGame:
                            return true;
                    }
                    break;
                case ServerConnection.GameServer:
                    switch (opCode)
                    {
                        case OperationCode.CreateGame:
                        case OperationCode.Authenticate:
                        case OperationCode.AuthenticateOnce:
                        case OperationCode.ChangeGroups:
                        case OperationCode.GetProperties:
                        case OperationCode.JoinGame:
                        case OperationCode.Leave:
                        case OperationCode.ServerSettings:
                        case OperationCode.SetProperties:
                        case OperationCode.RaiseEvent:
                            return true;
                    }
                    break;
                case ServerConnection.NameServer:
                    switch (opCode)
                    {
                        case OperationCode.Authenticate:
                        case OperationCode.AuthenticateOnce:
                        case OperationCode.GetRegions:
                        case OperationCode.ServerSettings:
                            return true;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException("serverConnection", serverConnection, null);
            }
            return false;
        }


        private bool CheckIfOpCanBeSent(byte opCode, ServerConnection serverConnection, string opName)
        {
            if (this.LoadBalancingPeer == null)
            {
                this.DebugReturn(DebugLevel.ERROR, string.Format("Operation {0} ({1}) can't be sent because peer is null", opName, opCode));
                return false;
            }
            if (!this.CheckIfOpAllowedOnServer(opCode, serverConnection))
            {
                if (this.LoadBalancingPeer.DebugOut >= DebugLevel.ERROR)
                {
                    this.DebugReturn(DebugLevel.ERROR, string.Format("Operation {0} ({1}) not allowed on current server ({2})", opName, opCode, serverConnection));
                }
                return false;
            }
            if (!this.CheckIfClientIsReadyToCallOperation(opCode))
            {
                if (this.LoadBalancingPeer.DebugOut >= DebugLevel.ERROR)
                {
                    this.DebugReturn(DebugLevel.ERROR, string.Format("Operation {0} ({1}) not called because client is not connected or not ready yet, client state: {2}", opName, opCode, Enum.GetName(typeof(ClientState), this.State)));
                }
                return false;
            }
            if (this.LoadBalancingPeer.PeerState != PeerStateValue.Connected)
            {
                this.DebugReturn(DebugLevel.ERROR, string.Format("Operation {0} ({1}) can't be sent because peer is not connected, peer state: {2}", opName, opCode, this.LoadBalancingPeer.PeerState));
                return false;
            }
            return true;
        }


        private bool CheckIfClientIsReadyToCallOperation(byte opCode)
        {
            switch (opCode)
            {

                case OperationCode.Authenticate:
                case OperationCode.AuthenticateOnce:
                    return this.IsConnectedAndReady ||
                         this.State == ClientState.ConnectingToNameServer || // this is required since we do not set state to ConnectedToNameServer before authentication
                        this.State == ClientState.ConnectingToMasterServer || // this is required since we do not set state to ConnectedToMasterServer before authentication
                        this.State == ClientState.ConnectingToGameServer; // this is required since we do not set state to ConnectedToGameServer before authentication

                case OperationCode.ChangeGroups:
                case OperationCode.GetProperties:
                case OperationCode.SetProperties:
                case OperationCode.RaiseEvent:
                case OperationCode.Leave:
                    return this.InRoom;

                case OperationCode.JoinGame:
                case OperationCode.CreateGame:
                    return this.State == ClientState.ConnectedToMasterServer || this.InLobby || this.State == ClientState.ConnectedToGameServer; // CurrentRoom can be not null in case of quick rejoin

                case OperationCode.LeaveLobby:
                    return this.InLobby;

                case OperationCode.JoinRandomGame:
                case OperationCode.GetGameList:
                case OperationCode.GetLobbyStats: // do we need to be inside lobby to call this?
                case OperationCode.JoinLobby: // You don't have to explicitly leave a lobby to join another (client can be in one max, at any time)
                    return this.State == ClientState.ConnectedToMasterServer || this.InLobby;
                case OperationCode.GetRegions:
                    return this.State == ClientState.ConnectedToNameServer;
            }
            return this.IsConnected;
        }



        #endregion

        #region INetPeerListner ????

        public virtual void DebugReturn(DebugLevel level, string message)
        {
            if (this.LoadBalancingPeer.DebugOut != DebugLevel.ALL && level > this.LoadBalancingPeer.DebugOut)
            {
                return;
            }

            if (level == DebugLevel.ERROR)
            {
                Debug.LogError(message);
            }
            else if (level == DebugLevel.WARNING)
            {
                Debug.LogWarning(message);
            }
            else if (level == DebugLevel.INFO)
            {
                Debug.Log(message);
            }
            else if (level == DebugLevel.ALL)
            {
                Debug.Log(message);
            }

        }

        private void CallbackRoomEnterFailed(OperationResponse operationResponse)
        {
            if (operationResponse.ReturnCode != 0)
            {
                if (operationResponse.OperationCode == OperationCode.JoinGame)
                {
                    this.MatchMakingCallbackTargets.OnJoinRoomFailed(operationResponse.ReturnCode, operationResponse.DebugMessage);
                }
                else if (operationResponse.OperationCode == OperationCode.CreateGame)
                {
                    this.MatchMakingCallbackTargets.OnCreateRoomFailed(operationResponse.ReturnCode, operationResponse.DebugMessage);
                }
                else if (operationResponse.OperationCode == OperationCode.JoinRandomGame)
                {
                    this.MatchMakingCallbackTargets.OnJoinRandomFailed(operationResponse.ReturnCode, operationResponse.DebugMessage);
                }
            }
        }

        /// <summary>
        /// ?????��??? ?????? OperationResponse???? ???????.
        /// </summary>
        /// <param name="operationResponse"></param>
        public virtual void OnOperationResponse(OperationResponse operationResponse)
        {
            Debug.Log("OnOperationResponse : opCode[" + operationResponse.OperationCode + "]");

            if (operationResponse.Parameters.ContainsKey(ParameterCode.Secret))
            {
                if (this.AuthValues == null)
                {
                    this.AuthValues = new AuthenticationValues();
                }

                this.AuthValues.Token = operationResponse[ParameterCode.Secret] as string;
                this.tokenCache = this.AuthValues.Token;
            }

            switch (operationResponse.OperationCode)
            {
                case OperationCode.Authenticate:
                case OperationCode.AuthenticateOnce:
                    {
                        if (operationResponse.ReturnCode != 0)
                        {
                            this.DebugReturn(DebugLevel.ERROR, operationResponse.ToString() + " Server: " + this.Server + " Address: " + this.LoadBalancingPeer.ServerAddress);

                            switch (operationResponse.ReturnCode)
                            {
                                case ErrorCode.InvalidAuthentication:
                                    this.DisconnectedCause = DisconnectCause.InvalidAuthentication;
                                    break;
                                case ErrorCode.CustomAuthenticationFailed:
                                    this.DisconnectedCause = DisconnectCause.CustomAuthenticationFailed;
                                    this.ConnectionCallbackTargets.OnCustomAuthenticationFailed(operationResponse.DebugMessage);
                                    break;
                                case ErrorCode.InvalidRegion:
                                    this.DisconnectedCause = DisconnectCause.InvalidRegion;
                                    break;
                                case ErrorCode.MaxCcuReached:
                                    this.DisconnectedCause = DisconnectCause.MaxCcuReached;
                                    break;
                                case ErrorCode.OperationNotAllowedInCurrentState:
                                    this.DisconnectedCause = DisconnectCause.OperationNotAllowedInCurrentState;
                                    break;
                                case ErrorCode.AuthenticationTicketExpired:
                                    this.DisconnectedCause = DisconnectCause.AuthenticationTicketExpired;
                                    break;
                            }

                            this.Disconnect(this.DisconnectedCause);
                            break;  //?????? ??????? ?????? ???? ???????? ????????.
                        }

                        if (this.Server == ServerConnection.NameServer || this.Server == ServerConnection.MasterServer)
                        {
                            if (operationResponse.Parameters.ContainsKey(ParameterCode.UserId))
                            {
                                string incomingId = (string)operationResponse.Parameters[ParameterCode.UserId];
                                if (!string.IsNullOrEmpty(incomingId))
                                {
                                    this.UserId = incomingId;
                                    this.LocalPlayer.UserId = incomingId;
                                    this.DebugReturn(DebugLevel.INFO, string.Format("???????? UserId?? ????????????. ????????? ???? value to: {0}", this.UserId));
                                }
                            }
                            if (operationResponse.Parameters.ContainsKey(ParameterCode.NickName))
                            {
                                this.NickName = (string)operationResponse.Parameters[ParameterCode.NickName];
                                this.DebugReturn(DebugLevel.INFO, string.Format("???????? Nickname?? ????????????. ????????? ???? value to: {0}", this.NickName));
                            }

                        }

                        if (this.Server == ServerConnection.NameServer)
                        {
                            string receivedCluster = operationResponse[ParameterCode.Cluster] as string;
                            if (!string.IsNullOrEmpty(receivedCluster))
                            {
                                this.CurrentCluster = receivedCluster;
                            }

                            // on the NameServer, authenticate returns the MasterServer address for a region and we hop off to there
                            this.MasterServerAddress = operationResponse[ParameterCode.Address] as string;
                            if (this.LoadBalancingPeer.TransportProtocol == ConnectionProtocol.Udp && this.UseAlternativeUdpPorts)
                            {
                                // TODO: Make this work with AuthOnceWss, which uses WSS on NameServer but "expects" to use UDP...
                                this.MasterServerAddress = this.MasterServerAddress.Replace("5058", "27000").Replace("5055", "27001").Replace("5056", "27002");
                            }

                            if (this.AuthMode == AuthModeOption.AuthOnceWss && this.ExpectedProtocol != null)
                            {
                                this.DebugReturn(DebugLevel.INFO, string.Format("AuthOnceWss mode. Auth response switches TransportProtocol to ExpectedProtocol: {0}.", this.ExpectedProtocol));
                                this.LoadBalancingPeer.TransportProtocol = (ConnectionProtocol)this.ExpectedProtocol;
                                this.ExpectedProtocol = null;
                            }
                            this.DisconnectToReconnect();
                        }
                        else if (this.Server == ServerConnection.MasterServer)
                        {
                            this.State = ClientState.ConnectedToMasterServer;
                            if (this.failedRoomEntryOperation == null)
                            {
                                this.ConnectionCallbackTargets.OnConnectedToMaster();
                            }
                            else
                            {
                                this.CallbackRoomEnterFailed(this.failedRoomEntryOperation);
                                this.failedRoomEntryOperation = null;
                            }

                            if (this.AuthMode != AuthModeOption.Auth)
                            {
                                this.LoadBalancingPeer.OpSettings(this.EnableLobbyStatistics);
                            }
                        }
                        else if (this.Server == ServerConnection.GameServer)
                        {
                            this.State = ClientState.Joining;

                            if (this.enterRoomParamsCache.RejoinOnly)
                            {
                                this.enterRoomParamsCache.PlayerProperties = null;
                            }
                            else
                            {
                                Hashtable allProps = new Hashtable();
                                allProps.MergeStringKeys(this.LocalPlayer.CustomProperties);

                                if (!string.IsNullOrEmpty(this.LocalPlayer.NickName))
                                {
                                    allProps[ActorProperties.PlayerName] = this.LocalPlayer.NickName;
                                }

                                this.enterRoomParamsCache.PlayerProperties = allProps;
                            }

                            this.enterRoomParamsCache.OnGameServer = true;

                            if (this.lastJoinType == JoinType.JoinRoom || this.lastJoinType == JoinType.JoinRandomRoom || this.lastJoinType == JoinType.JoinRandomOrCreateRoom || this.lastJoinType == JoinType.JoinOrCreateRoom)
                            {
                                this.LoadBalancingPeer.OpJoinRoom(this.enterRoomParamsCache);
                            }
                            else if (this.lastJoinType == JoinType.CreateRoom)
                            {
                                this.LoadBalancingPeer.OpCreateRoom(this.enterRoomParamsCache);
                            }
                            break;
                        }

                        Dictionary<string, object> data = (Dictionary<string, object>)operationResponse[ParameterCode.Data];
                        if (data != null)
                        {
                            this.ConnectionCallbackTargets.OnCustomAuthenticationResponse(data);
                        }
                        break;
                    }

                case OperationCode.GetRegions:
                    if (operationResponse.ReturnCode == ErrorCode.InvalidAuthentication)
                    {
                        this.DebugReturn(DebugLevel.ERROR, string.Format("GetRegions failed. AppId is unknown on the (cloud) server. " + operationResponse.DebugMessage));
                        this.Disconnect(DisconnectCause.InvalidAuthentication);
                        break;
                    }
                    if (operationResponse.ReturnCode != ErrorCode.Ok)
                    {
                        this.DebugReturn(DebugLevel.ERROR, "GetRegions failed. Can't provide regions list. ReturnCode: " + operationResponse.ReturnCode + ": " + operationResponse.DebugMessage);
                        this.Disconnect(DisconnectCause.InvalidAuthentication);
                        break;
                    }
                    if (this.RegionHandler == null)
                    {
                        this.RegionHandler = new RegionHandler();
                    }

                    if (this.RegionHandler.IsPinging)
                    {
                        this.DebugReturn(DebugLevel.WARNING, "Received an response for OpGetRegions while the RegionHandler is pinging regions already. Skipping this response in favor of completing the current region-pinging.");
                        return; // in this particular case, we suppress the duplicate GetRegion response. we don't want a callback for this, cause there is a warning already.
                    }

                    this.RegionHandler.SetRegions(operationResponse);
                    this.ConnectionCallbackTargets.OnRegionListReceived(this.RegionHandler);

                    if (this.connectToBestRegion)
                    {
                        // ping minimal regions (if one is known) and connect
                        this.RegionHandler.PingMinimumOfRegions(this.OnRegionPingCompleted, this.bestRegionSummaryFromStorage);
                    }
                    break;

                case OperationCode.JoinRandomGame:  // this happens only on the master server. on gameserver this is a "regular" join
                case OperationCode.CreateGame:
                case OperationCode.JoinGame:

                    if (operationResponse.ReturnCode != 0)
                    {
                        if (this.Server == ServerConnection.GameServer)
                        {
                            this.failedRoomEntryOperation = operationResponse;
                            this.DisconnectToReconnect();
                        }
                        else
                        {
                            this.State = (this.InLobby) ? ClientState.JoinedLobby : ClientState.ConnectedToMasterServer;
                            this.CallbackRoomEnterFailed(operationResponse);
                        }
                    }
                    else
                    {
                        if (this.Server == ServerConnection.GameServer)
                        {
                            this.GameEnteredOnGameServer(operationResponse);
                        }
                        else
                        {
                            this.GameServerAddress = (string)operationResponse[ParameterCode.Address];
                            if (this.LoadBalancingPeer.TransportProtocol == ConnectionProtocol.Udp && this.UseAlternativeUdpPorts)
                            {
                                this.GameServerAddress = this.GameServerAddress.Replace("5058", "27000").Replace("5055", "27001").Replace("5056", "27002");
                            }

                            string roomName = operationResponse[ParameterCode.RoomName] as string;
                            if (!string.IsNullOrEmpty(roomName))
                            {
                                this.enterRoomParamsCache.RoomName = roomName;
                            }

                            this.DisconnectToReconnect();
                        }
                    }
                    break;

                case OperationCode.GetGameList:
                    if (operationResponse.ReturnCode != 0)
                    {
                        this.DebugReturn(DebugLevel.ERROR, "GetGameList failed: " + operationResponse.ToString());
                        break;
                    }

                    List<RoomInfo> _RoomInfoList = new List<RoomInfo>();

                    Hashtable games = (Hashtable)operationResponse[ParameterCode.GameList];
                    foreach (string gameName in games.Keys)
                    {
                        _RoomInfoList.Add(new RoomInfo(gameName, (Hashtable)games[gameName]));
                    }

                    this.LobbyCallbackTargets.OnRoomListUpdate(_RoomInfoList);
                    break;

                case OperationCode.JoinLobby:
                    this.State = ClientState.JoinedLobby;
                    this.LobbyCallbackTargets.OnJoinedLobby();
                    break;

                case OperationCode.LeaveLobby:
                    this.State = ClientState.ConnectedToMasterServer;
                    this.LobbyCallbackTargets.OnLeftLobby();
                    break;

                case OperationCode.Leave:
                    this.DisconnectToReconnect();
                    break;

            }

            if (this.OpResponseReceived != null) this.OpResponseReceived(operationResponse);
        }


        /// <summary>
        /// statusCode?? ???? ???��????? ???????.
        /// </summary>
        /// <param name="statusCode"></param>
        public virtual void OnStatusChanged(StatusCode statusCode)
        {
            this.DebugReturn(DebugLevel.ALL, "Status changed : " + statusCode);

            switch (statusCode)
            {
                case StatusCode.Connect:
                    if (this.State == ClientState.ConnectingToNameServer)
                    {
                        if (this.LoadBalancingPeer.DebugOut >= DebugLevel.ALL)
                        {
                            this.DebugReturn(DebugLevel.ALL, "Connected to nameserver.");
                        }

                        this.Server = ServerConnection.NameServer;
                        if (this.AuthValues != null)
                        {
                            this.AuthValues.Token = null;
                        }
                    }

                    if (this.State == ClientState.ConnectingToGameServer)
                    {
                        if (this.LoadBalancingPeer.DebugOut >= DebugLevel.ALL)
                        {
                            this.DebugReturn(DebugLevel.ALL, "Connected to gameserver.");
                        }

                        this.Server = ServerConnection.GameServer;
                    }

                    if (this.State == ClientState.ConnectingToMasterServer)
                    {
                        if (this.LoadBalancingPeer.DebugOut >= DebugLevel.ALL)
                        {
                            this.DebugReturn(DebugLevel.ALL, "Connected to masterserver.");
                        }

                        this.Server = ServerConnection.MasterServer;
                        this.ConnectionCallbackTargets.OnConnected();
                        this.State = ClientState.ConnectedToMasterServer;
                    }

                    if (this.State == ClientState.ConnectedToMasterServer)
                    {
                        this.ConnectionCallbackTargets.OnConnectedToMaster();
                    }



                    // if (this.Server == ServerConnection.NameServer || this.AuthMode == AuthModeOption.Auth)
                    // {
                    //     this.LoadBalancingPeer.EstablishEncryption();
                    // }


                    break;

                case StatusCode.EncryptionEstablished:
                    if (this.Server == ServerConnection.NameServer)
                    {
                        this.State = ClientState.ConnectedToNameServer;

                        //?????? Region?? ?????? NameServer???? ??? ?????? Region?? ????????.
                        if (string.IsNullOrEmpty(this.CloudRegion))
                        {
                            this.OpGetRegions();
                            break;
                        }
                    }
                    else
                    {
                        if (this.AuthMode == AuthModeOption.AuthOnce || this.AuthMode == AuthModeOption.AuthOnceWss)
                        {
                            break;
                        }
                    }

                    bool authenticating = this.CallAuthenticate();
                    if (authenticating)
                    {
                        this.State = ClientState.Authenticating;
                    }
                    else
                    {
                        this.DebugReturn(DebugLevel.ERROR, "OpAuthenticate failed. State: " + this.State);
                    }
                    break;

                case StatusCode.Disconnect:

                    bool wasInRoom = this.CurrentRoom != null;
                    this.CurrentRoom = null;
                    this.ChangeLocalID(-1);

                    if (this.Server == ServerConnection.GameServer && wasInRoom)
                    {
                        this.MatchMakingCallbackTargets.OnLeftRoom();
                    }

                    if (this.ExpectedProtocol != null)
                    {
                        this.DebugReturn(DebugLevel.INFO, string.Format("AuthOnceWss mode. On disconnect switches TransportProtocol to ExpectedProtocol: {0}.", this.ExpectedProtocol));
                        this.LoadBalancingPeer.TransportProtocol = (ConnectionProtocol)this.ExpectedProtocol;
                        this.ExpectedProtocol = null;
                    }

                    switch (this.State)
                    {
                        case ClientState.ConnectWithFallbackProtocol:
                            this.EnableProtocolFallback = false;    // the client does a fallback only one time
                            this.LoadBalancingPeer.TransportProtocol = (this.LoadBalancingPeer.TransportProtocol == ConnectionProtocol.Tcp) ? ConnectionProtocol.Udp : ConnectionProtocol.Tcp;
                            this.NameServerPortOverride = 0;
                            this.ConnectToNameServer();
                            break;
                        case ClientState.PeerCreated:
                        case ClientState.Disconnecting:
                            if (this.AuthValues != null)
                            {
                                this.AuthValues.Token = null; // when leaving the server, invalidate the secret (but not the auth values)
                            }
                            this.State = ClientState.Disconnected;
                            this.ConnectionCallbackTargets.OnDisconnected(this.DisconnectedCause);
                            break;

                        case ClientState.DisconnectingFromGameServer:
                        case ClientState.DisconnectingFromNameServer:
                            this.ConnectToMasterServer();                 // this gets the client back to the Master Server
                            break;

                        case ClientState.DisconnectingFromMasterServer:
                            this.Connect(this.GameServerAddress, this.ProxyServerAddress, ServerConnection.GameServer);     // this connects the client with the Game Server (when joining/creating a room)
                            break;

                        case ClientState.Disconnected:
                            // this client is already Disconnected, so no further action is needed.
                            // this.DebugReturn(DebugLevel.INFO, "LBC.OnStatusChanged(Disconnect) this.State: " + this.State + ". Server: " + this.Server);
                            break;

                        default:
                            string stacktrace = "";
                            this.DebugReturn(DebugLevel.WARNING, "Got a unexpected Disconnect in LoadBalancingClient State: " + this.State + ". Server: " + this.Server + " Trace: " + stacktrace);

                            if (this.AuthValues != null)
                            {
                                this.AuthValues.Token = null; // when leaving the server, invalidate the secret (but not the auth values)
                            }
                            this.State = ClientState.Disconnected;
                            this.ConnectionCallbackTargets.OnDisconnected(this.DisconnectedCause);
                            break;
                    }
                    break;

                case StatusCode.DisconnectByServerUserLimit:
                    this.DebugReturn(DebugLevel.ERROR, "This connection was rejected due to the apps CCU limit.");
                    this.DisconnectedCause = DisconnectCause.MaxCcuReached;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.ExceptionOnConnect:
                case StatusCode.SecurityExceptionOnConnect:
                case StatusCode.EncryptionFailedToEstablish:
                    this.DisconnectedCause = DisconnectCause.ExceptionOnConnect;

                    // if enabled, the client can attempt to connect with another networking-protocol to check if that connects
                    if (this.EnableProtocolFallback && this.State == ClientState.ConnectingToNameServer)
                    {
                        this.State = ClientState.ConnectWithFallbackProtocol;
                    }
                    else
                    {
                        this.State = ClientState.Disconnecting;
                    }
                    break;
                case StatusCode.Exception:
                case StatusCode.ExceptionOnReceive:
                case StatusCode.SendError:
                    this.DisconnectedCause = DisconnectCause.Exception;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DisconnectByServerTimeout:
                    this.DisconnectedCause = DisconnectCause.ServerTimeout;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DisconnectByServerLogic:
                    this.DisconnectedCause = DisconnectCause.DisconnectByServerLogic;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.DisconnectByServerReasonUnknown:
                    this.DisconnectedCause = DisconnectCause.DisconnectByServerReasonUnknown;
                    this.State = ClientState.Disconnecting;
                    break;
                case StatusCode.TimeoutDisconnect:
                    this.DisconnectedCause = DisconnectCause.ClientTimeout;

                    // if enabled, the client can attempt to connect with another networking-protocol to check if that connects
                    if (this.EnableProtocolFallback && this.State == ClientState.ConnectingToNameServer)
                    {
                        this.State = ClientState.ConnectWithFallbackProtocol;
                    }
                    else
                    {
                        this.State = ClientState.Disconnecting;
                    }
                    break;
            }
        }

        /// <summary>
        /// EventData ???
        /// </summary>
        public virtual void OnEvent(EventData netEvent)
        {
            int actorNr = netEvent.Sender;
            Player originatingPlayer = (this.CurrentRoom != null) ? this.CurrentRoom.GetPlayer(actorNr) : null;

            switch (netEvent.Code)
            {
                case EventCode.GameList:
                case EventCode.GameListUpdate:

                    List<RoomInfo> _RoomInfoList = new List<RoomInfo>();

                    Hashtable games = (Hashtable)netEvent[ParameterCode.GameList];
                    foreach (string gameName in games.Keys)
                    {
                        _RoomInfoList.Add(new RoomInfo(gameName, (Hashtable)games[gameName]));
                    }

                    this.LobbyCallbackTargets.OnRoomListUpdate(_RoomInfoList);

                    break;

                case EventCode.Join:
                    Hashtable actorProperties = (Hashtable)netEvent[ParameterCode.PlayerProperties];

                    if (originatingPlayer == null)
                    {
                        originatingPlayer = this.CreatePlayer(string.Empty, actorNr, false, actorProperties);
                        this.CurrentRoom.StorePlayer(originatingPlayer);
                    }
                    else
                    {
                        originatingPlayer.InternalCacheProperties(actorProperties);
                        originatingPlayer.IsInactive = false;
                        originatingPlayer.HasRejoined = actorNr != this.LocalPlayer.ActorNumber;
                    }

                    if (actorNr == this.LocalPlayer.ActorNumber)
                    {
                        int[] actorsInRoom = (int[])netEvent[ParameterCode.ActorList];
                        this.UpdatedActorList(actorsInRoom);

                        originatingPlayer.HasRejoined = this.enterRoomParamsCache.RejoinOnly;

                        if (this.lastJoinType == JoinType.CreateRoom || (this.lastJoinType == JoinType.JoinOrCreateRoom && this.LocalPlayer.ActorNumber == 1))
                        {
                            this.MatchMakingCallbackTargets.OnCreatedRoom();
                        }

                        this.MatchMakingCallbackTargets.OnJoinedRoom();
                    }
                    else
                    {
                        this.InRoomCallbackTargets.OnPlayerEnteredRoom(originatingPlayer);
                    }
                    break;

                case EventCode.Leave:
                    if (originatingPlayer != null)
                    {
                        bool isInactive = false;
                        if (netEvent.Parameters.ContainsKey(ParameterCode.IsInactive))
                        {
                            isInactive = (bool)netEvent.Parameters[ParameterCode.IsInactive];
                        }

                        if (isInactive)
                        {
                            originatingPlayer.IsInactive = true;
                        }
                        else
                        {
                            originatingPlayer.IsInactive = false;
                            this.CurrentRoom.RemovePlayer(actorNr);
                        }
                    }

                    if (netEvent.Parameters.ContainsKey(ParameterCode.MasterClientId))
                    {
                        int newMaster = (int)netEvent[ParameterCode.MasterClientId];
                        if (newMaster != 0)
                        {
                            this.CurrentRoom.masterClientId = newMaster;
                            this.InRoomCallbackTargets.OnMasterClientSwitched(this.CurrentRoom.GetPlayer(newMaster));
                        }
                    }
                    this.InRoomCallbackTargets.OnPlayerLeftRoom(originatingPlayer);
                    break;

                case EventCode.PropertiesChanged:
                Debug.Log("RecievChanged ~~~~~~~~~~~~~~~~~~~~~");
                    int targetActorNr = 0;
                    if (netEvent.Parameters.ContainsKey(ParameterCode.TargetActorNr))
                    {
                        Debug.Log("RecievChanged ~~~~~~~~~~~~~~~~~~~~~ TargetActor" +(int)netEvent[ParameterCode.TargetActorNr]);
                        targetActorNr = (int)netEvent[ParameterCode.TargetActorNr];
                    }

                    Hashtable gameProperties = null;
                    Hashtable actorProps = null;
                    if (targetActorNr == 0)
                    {
                        gameProperties = (Hashtable)netEvent[ParameterCode.Properties];
                        Debug.Log("Game Props~~~~~~~~" + gameProperties.Count);
                    }
                    else
                    {
                        actorProps = (Hashtable)netEvent[ParameterCode.Properties];
                    }

                    this.ReadoutProperties(gameProperties, actorProps, targetActorNr);
                    break;

                case EventCode.AppStats:
                    //?????? ?????? 1?? ???????? ???? ?
                    this.PlayersInRoomsCount = (int)netEvent[ParameterCode.PeerCount];
                    this.RoomsCount = (int)netEvent[ParameterCode.GameCount];
                    this.PlayersOnMasterCount = (int)netEvent[ParameterCode.MasterPeerCount];
                    break;

                case EventCode.LobbyStats:
                    string[] names = netEvent[ParameterCode.LobbyName] as string[];
                    int[] peers = netEvent[ParameterCode.PeerCount] as int[];
                    int[] rooms = netEvent[ParameterCode.GameCount] as int[];

                    byte[] types;
                    ByteArraySlice slice = netEvent[ParameterCode.LobbyType] as ByteArraySlice;
                    bool useByteArraySlice = slice != null;

                    if (useByteArraySlice)
                    {
                        types = slice.Buffer;
                    }
                    else
                    {
                        types = netEvent[ParameterCode.LobbyType] as byte[];
                    }

                    this.lobbyStatistics.Clear();
                    for (int i = 0; i < names.Length; i++)
                    {
                        TypedLobbyInfo info = new TypedLobbyInfo();
                        info.Name = names[i];
                        info.Type = (LobbyType)types[i];
                        info.PlayerCount = peers[i];
                        info.RoomCount = rooms[i];

                        this.lobbyStatistics.Add(info);
                    }

                    if (useByteArraySlice)
                    {
                        slice.Release();
                    }

                    this.LobbyCallbackTargets.OnLobbyStatisticsUpdate(this.lobbyStatistics);
                    break;

                case EventCode.ErrorInfo:
                    this.ErrorInfoCallbackTargets.OnErrorInfo(new ErrorInfo(netEvent));
                    break;

                case EventCode.AuthEvent:
                    if (this.AuthValues == null)
                    {
                        this.AuthValues = new AuthenticationValues();
                    }

                    this.AuthValues.Token = netEvent[ParameterCode.Secret] as string;
                    this.tokenCache = this.AuthValues.Token;
                    break;

            }

            this.UpdateCallbackTargets();
            if (this.EventReceived != null) this.EventReceived(netEvent);
        }

        public virtual void OnMessage(object message)
        {
            this.DebugReturn(DebugLevel.ALL, string.Format("got OnMessage {0}", message));
        }
        #endregion



        /// <summary>A callback of the RegionHandler, provided in OnRegionListReceived.</summary>
        /// <param name="regionHandler">The regionHandler wraps up best region and other region relevant info.</param>
        private void OnRegionPingCompleted(RegionHandler regionHandler)
        {
            //Debug.Log("OnRegionPingCompleted " + regionHandler.BestRegion);
            //Debug.Log("RegionPingSummary: " + regionHandler.SummaryToCache);
            this.SummaryToCache = regionHandler.SummaryToCache;
            this.ConnectToRegionMaster(regionHandler.BestRegion.Code);
        }

        /// <summary>
        /// ??? ??????????? ?????? ????? ???????.
        /// </summary>
        public void AddCallbackTarget(object target)
        {
            this.callbackTargetChanges.Enqueue(new CallbackTargetChange(target, true));
        }

        /// <summary>
        /// ???? ????????? ????? ????????
        /// </summary>
        public void RemoveCallbackTarget(object target)
        {
            this.callbackTargetChanges.Enqueue(new CallbackTargetChange(target, false));
        }

        /// <summary>
        /// ??? ????? ???????????.
        /// </summary>
        protected internal void UpdateCallbackTargets()
        {
            while (this.callbackTargetChanges.Count > 0)
            {
                CallbackTargetChange change = this.callbackTargetChanges.Dequeue();

                if (change.AddTarget)
                {
                    if (this.callbackTargets.Contains(change.Target))
                    {
                        continue;
                    }

                    this.callbackTargets.Add(change.Target);
                }
                else
                {
                    if (!this.callbackTargets.Contains(change.Target))
                    {
                        continue;
                    }

                    this.callbackTargets.Remove(change.Target);
                }

                this.UpdateCallbackTarget<IInRoomCallbacks>(change, this.InRoomCallbackTargets);
                this.UpdateCallbackTarget<IConnectionCallbacks>(change, this.ConnectionCallbackTargets);
                this.UpdateCallbackTarget<IMatchmakingCallbacks>(change, this.MatchMakingCallbackTargets);
                this.UpdateCallbackTarget<ILobbyCallbacks>(change, this.LobbyCallbackTargets);
                this.UpdateCallbackTarget<IErrorInfoCallback>(change, this.ErrorInfoCallbackTargets);

                IOnEventCallback onEventCallback = change.Target as IOnEventCallback;
                if (onEventCallback != null)
                {
                    if (change.AddTarget)
                    {
                        EventReceived += onEventCallback.OnEvent;
                    }
                    else
                    {
                        EventReceived -= onEventCallback.OnEvent;
                    }
                }
            }
        }

        /// <summary>
        /// ??? ?????? ��?????? ??????? ?????????.
        /// </summary>
        private void UpdateCallbackTarget<T>(CallbackTargetChange change, List<T> container) where T : class
        {
            T target = change.Target as T;
            if (target != null)
            {
                if (change.AddTarget)
                {
                    container.Add(target);
                }
                else
                {
                    container.Remove(target);
                }
            }
        }
    }


    public class ErrorInfo
    {
        public readonly string Info;

        public ErrorInfo(EventData eventData)
        {
            this.Info = eventData[ParameterCode.Info] as string;
        }

        public override string ToString()
        {
            return string.Format("ErrorInfo: {0}", this.Info);
        }
    }




}
