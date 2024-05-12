
namespace NDG
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    using System.Runtime.InteropServices;

    public class Peer
    {
        public const bool NoSocket = false;
        /// <summary>라이브러리가 Debug 설정으로 컴파일된 경우 true입니다. </summary>
        public const bool DebugBuild = true;
        /// <summary>암호화 API의 버전입니다.</summary>
        public const int NativeEncryptorApiVersion = 1;
        /// <summary>기본 플러그인의 콜백 방지. </summary>
        public static bool NoNativeCallbacks;
        /// <summary>클라이언트 SDK 식별자 </summary>
        protected internal byte ClientSdkId = 15;
        private string clientVersion;
        private static bool checkedNativeLibs = false;
        private static bool useSocketNative;
        private static bool useDiffieHellmanCryptoProvider;
        private static bool useEncryptorNative;

        /// <summary>
        /// ConnectionProtocol별로 INetSocket유형을 선택적으로 정의합니다.
        /// </summary>
        public Dictionary<ConnectionProtocol, Type> SocketImplementationConfig;
        /// <summary>
        /// Debug 출력 level을 정의합니다.
        /// </summary>
        public DebugLevel DebugOut = DebugLevel.ERROR;
        private bool reuseEventInstance;
        private bool useByteArraySlicePoolForEvents = false;

        public bool SendInCreationOrder = true;

        private byte quickResendAttempts;

        /// <summary>
        /// UDP연결에서 사용할 수 있는 채널 수를 가져오거나 설정합니다.
        /// </summary>
        public byte ChannelCount = 2;
        /// <summary>클라이언트가 보안연결에서 암호화된 플래그를 전송할지 설정합니다.</summary>
        public bool EnableEncryptedFlag = false;
        private bool crcEnabled;

        /// <summary>피어가 연결 끊김으로 간주되기 전에 전송을 재시도하는 횟수입니다.</summary>
        public int SentCountAllowance = 7;
        /// <summary>reliable command의 반복을 위한 초기 타이밍을 제한합니다. (ms단위)</summary>
        public int InitialResendTimeMax = 400;
        /// <summary>ping 송신 간격.</summary>
        public int TimePingInterval = 1000;
        /// <summary>서버에서 개별 명령을 응답해야 할 때까지의 시간.(ms단위) 이후 시간 초과 연결이 트리거됩니다.</summary>
        public int DisconnectTimeout = 10000;
        /// <summary>내부적으로 사용되는 TCP용 StreamBuffer의 초기 크기를 정의합니다.</summary>
        public static int OutgoingStreamBufferSize = 1200;
        //maximumTransferUnit
        private int mtu = 1200;
        /// <summary>암호화를 위한 키 교환이 다른 스레드에서 비동기적으로 수행되는지 여부를 정의합니다.</summary>
        public static bool AsyncKeyExchange = false;
        /// <summary>시퀀스 번호를 랜덤화해야 하는지 여부를 나타냅니다.</summary>
        internal bool RandomizeSequenceNumbers;
        /// <summary>채널의 시퀀스 번호를 수정하는데 사용되는 초기화 배열입니다.</summary>
        internal byte[] RandomizedSequenceNumbers;

        /// <summary>Gcm이 데이터그램 암호화에 사용되는 경우. </summary>
        internal bool GcmDatagramEncryption;
        private Stopwatch trafficStatsStopwatch;
        private bool trafficStatsEnabled = false;

        /// <summary>기본 네트워크 프로토콜을 기반으로 메시지 프로토콜을 구현합니다.</summary>
        internal PeerBase peerBase;
        private readonly object SendOutgoingLockObject = new object();
        private readonly object DispatchLockObject = new object();
        private readonly object EnqueueLock = new object();

        /// <summary>메세지 페이로드는 요청시 개별적으로 암호화됩니다.</summary>
        protected internal byte[] PayloadEncryptionSecret;

 
        protected internal byte ClientSdkIdShifted => (byte)((int)this.ClientSdkId << 1 | 0);

        public string ClientVersion
        {
            get
            {
                if (string.IsNullOrEmpty(this.clientVersion))
                    this.clientVersion = string.Format("{0}.{1}.{2}.{3}", (object)Version.clientVersion[0], (object)Version.clientVersion[1], (object)Version.clientVersion[2], (object)Version.clientVersion[3]);
                return this.clientVersion;
            }
        }

        /// <summary>
        /// 네트워크 소켓용 네이티브 라이브러리를 사용할 수 있는지 확인합니다.
        /// </summary>
        public static bool NativeSocketLibAvailable
        {
            get
            {
                Peer.CheckNativeLibsAvailability();
                return Peer.useSocketNative;
            }
        }

        /// <summary>
        /// Payload라이브러리를 사용할 수 있는지 확인합니다.
        /// </summary>
        public static bool NativePayloadEncryptionLibAvailable
        {
            get
            {
                Peer.CheckNativeLibsAvailability();
                return Peer.useDiffieHellmanCryptoProvider;
            }
        }

        /// <summary>
        /// 데이터그램 암호화를 위한 네이티브 라이브러리를 사용할 수 있는지 확인합니다.
        /// </summary>
        public static bool NativeDatagramEncryptionLibAvailable
        {
            get
            {
                Peer.CheckNativeLibsAvailability();
                return Peer.useEncryptorNative;
            }
        }

        private static void CheckNativeLibsAvailability()
        {
            if (Peer.checkedNativeLibs)
                return;
            Peer.checkedNativeLibs = true;
            try
            {
                Peer.useDiffieHellmanCryptoProvider = false;
                //Marshal.PrelinkAll(typeof(DiffieHellmanCryptoProviderNative));
                //Peer.useDiffieHellmanCryptoProvider = true;
            }
            catch
            {
            }
            try
            {
                Peer.useSocketNative = false;
                // Marshal.PrelinkAll(typeof(SocketNative));
                // Peer.useSocketNative = true;
            }
            catch
            {
            }
            try
            {
                Peer.useEncryptorNative = false;
                // Marshal.PrelinkAll(typeof(EncryptorNative));
                // Peer.useEncryptorNative = true;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 직렬화 프로토콜을 가져오거나 설정합니다.
        /// </summary>
        public SerializationProtocol SerializationProtocolType { get; set; }

        public Type SocketImplementation { get; internal set; }

        public INetPeerListener Listener { get; protected set; }

        /// <summary>
        /// EventData 인스턴스를 재사용하도록합니다.
        /// </summary>
        public bool ReuseEventInstance
        {
            get => this.reuseEventInstance;
            set
            {
                lock (this.DispatchLockObject)
                {
                    this.reuseEventInstance = value;
                    if (value)
                        return;
                    this.peerBase.reusableEventData = (EventData)null;
                }
            }
        }

        /// <summary>
        /// 수신 이벤트에 대해 역직렬화 최적화를 사용할지 설정합니다. 
        /// </summary>
        public bool UseByteArraySlicePoolForEvents
        {
            get => this.useByteArraySlicePoolForEvents;
            set => this.useByteArraySlicePoolForEvents = value;
        }

        /// <summary>
        /// 서버에 모든 데이터그램을 기록하도록 지시하는 디버깅 옵션입니다.
        /// </summary>
        public bool EnableServerTracing { get; set; }

        /// <summary>
        /// 신뢰할수 있는 커맨드 송신을 위해 송신횟수를 카운트합니다 (최대 4번 송신).
        /// </summary>
        public byte QuickResendAttempts
        {
            get => this.quickResendAttempts;
            set
            {
                this.quickResendAttempts = value;
                if (this.quickResendAttempts <= (byte)4)
                    return;
                this.quickResendAttempts = (byte)4;
            }
        }

        public PeerStateValue PeerState => this.peerBase.peerConnectionState == ConnectionStateValue.Connected && !this.peerBase.ApplicationIsInitialized ? PeerStateValue.InitializingApplication : (PeerStateValue)this.peerBase.peerConnectionState;


        public string PeerID => this.peerBase.PeerID;

        public int QueuedIncomingCommands => this.peerBase.QueuedIncomingCommandsCount;

        public int QueuedOutgoingCommands => this.peerBase.QueuedOutgoingCommandsCount;

        /// <summary>
        /// 연결이되지 않은 상태에서 다음 연결에서 패키지별 CRC체크섬을 사용할지 여부를 정합니다.
        /// </summary>
        public bool CrcEnabled
        {
            get => this.crcEnabled;
            set
            {
                if (this.crcEnabled == value)
                    return;
                if ((uint)this.peerBase.peerConnectionState > 0U)
                    throw new Exception("연결이 끊어진 상태에서만 cecEnabled를 설정할 수 있습니다..");
                this.crcEnabled = value;
            }
        }

        public SupportClass.IntegerMillisecondsDelegate LocalMsTimestampDelegate
        {
            set
            {
                if ((uint)this.PeerState > 0U)
                    throw new Exception("LocalMsTimestampDelegate은 연결이 끊어진 상태에서만 설정할 수 있습니다. 현재 State: " + this.PeerState.ToString());
                SupportClass.IntegerMilliseconds = value;
            }
        }


        public int ServerTimeInMilliSeconds => this.peerBase.serverTimeOffsetIsAvailable ? this.peerBase.serverTimeOffset + SupportClass.GetTickCount() : 0;

        public int ConnectionTime => this.peerBase.TimeInt;

        public int LastSendAckTime => this.peerBase.timeLastSendAck;

        public int LastSendOutgoingTime => this.peerBase.timeLastSendOutgoing;

        /// <summary>
        /// NetSocket.Send()에서 소요된 최대 ms를 측정합니다.
        /// </summary>
        public int LongestSentCall
        {
            get => this.peerBase.longestSentCall;
            set => this.peerBase.longestSentCall = value;
        }

        /// <summary>
        /// 서버가 Reliable 커맨드를 수신할때 까지의 시간입니다.
        /// </summary>
        public int RoundTripTime => this.peerBase.roundTripTime;

        /// <summary>
        /// RounTripTome의 변화값입니다.
        /// </summary>
        public int RoundTripTimeVariance => this.peerBase.roundTripTimeVariance;

        /// <summary>
        /// 서버에서 마지막으로 수신한 모든 항목의 타임스탬프입니다.
        /// </summary>
        public int TimestampOfLastSocketReceive => this.peerBase.timestampOfLastReceive;

        /// <summary>
        /// Peer에서 사용되는 서버 주소입니다.
        /// </summary>
        public string ServerAddress
        {
            get => this.peerBase.ServerAddress;
            set
            {
                if (this.DebugOut < DebugLevel.ERROR)
                    return;
                this.Listener.DebugReturn(DebugLevel.ERROR, "Failed to set ServerAddress. ");
            }
        }

        /// <summary>
        /// 이전에 확인된 ServerAddress의 IP주소를 포함합니다.
        /// </summary>
        public string ServerIpAddress => INetSocket.ServerIpAddress;

        /// <summary>
        /// 피어가 현재 연결중인 프로토콜을 가져옵니다.
        /// </summary>
        public ConnectionProtocol UsedProtocol => this.peerBase.usedTransportProtocol;

        /// <summary>
        /// 다음 연결에 사용할 전송 프로토콜입니다.
        /// </summary>
        public ConnectionProtocol TransportProtocol { get; set; }

        /// <summary>
        /// 이 피어 인스턴스에 대한 네트워크 시뮬레이션 설정을 가져옵니다.
        /// </summary>
        
        /// <summary>
        /// 패킷 컨텐츠의 크기를 정의합니다.
        /// </summary>
        public int MaximumTransferUnit
        {
            get => this.mtu;
            set
            {
                if ((uint)this.PeerState > 0U)
                    throw new Exception("MTU는 연결이 끊어진 상태에서만 설정할 수 있습니다. State: " + this.PeerState.ToString());
                if (value < 576)
                    value = 576;
                this.mtu = value;
            }
        }

        /// <summary>
        /// true인 경우 피어는 ACK를 제외한 다른 커맨드를 보내지 않습니다 (UDP 연결 에서).
        /// </summary>
        public bool IsSendingOnlyAcks { get; set; }


        /// <summary>
        /// 폐기되는 신뢰할수 없는 커맨드 갯수입니다.
        /// </summary>
        public int CountDiscarded { get; set; }

        /// <summary>
        /// 마지막 전송된 Unreliable 커맨드와 비교하여 시퀀스 간격이 얼마나 큰지 나타냅니다.
        /// </summary>
        public int DeltaUnreliableNumber { get; set; }



        /// <summary>
        /// 지정된 전송 프로토콜을 사용하여 새 Peer를 생성합니다.
        /// </summary>
        public Peer(ConnectionProtocol protocolType)
        {
            this.TransportProtocol = protocolType;
            this.CreatePeerBase();
        }

        public Peer(INetPeerListener listener, ConnectionProtocol protocolType) : this(protocolType)
        {
            this.Listener = listener;
        }

        /// <summary>
        /// 서버에 연결합니다.
        /// DNS Name을 확인 후 AppId가 전송되고 암호화가 설정됩니다.
        /// </summary>
        public virtual bool Connect(string serverAddress, string applicationName) => this.Connect(serverAddress, applicationName, (object)null);

        public virtual bool Connect(string serverAddress, string applicationName, object custom) => this.Connect(serverAddress, (string)null, applicationName, custom);

        public virtual bool Connect(
          string serverAddress,
          string proxyServerAddress,
          string applicationName,
          object custom)
        {
            lock (this.DispatchLockObject)
            {
                lock (this.SendOutgoingLockObject)
                {
                    this.CreatePeerBase();
                    if (this.peerBase == null)
                        return false;
                    if (this.peerBase.SocketImplementation == null)
                    {
                        this.peerBase.EnqueueDebugReturn(DebugLevel.ERROR, "연결실패. SocketImplementationConfig가 null상태입니다. protocol " + this.TransportProtocol.ToString() + ": " + SupportClass.DictionaryToString((IDictionary)this.SocketImplementationConfig));
                        return false;
                    }
                    if (custom == null)
                    {
                        this.RandomizedSequenceNumbers = (byte[])null;
                        this.RandomizeSequenceNumbers = false;
                        this.GcmDatagramEncryption = false;
                    }
                    this.peerBase.CustomInitData = custom;
                    this.peerBase.AppId = applicationName;
                    return this.peerBase.Connect(serverAddress, proxyServerAddress, applicationName, custom);
                }
            }
        }

        private void CreatePeerBase()
        {
            if (this.SocketImplementationConfig == null)
            {
                this.SocketImplementationConfig = new Dictionary<ConnectionProtocol, Type>(5);

                this.SocketImplementationConfig.Add(ConnectionProtocol.Tcp, typeof(SocketTcpAsync));

            }
            switch (this.TransportProtocol)
            {
                case ConnectionProtocol.Tcp:
                    this.peerBase = (PeerBase)new TPeer();
                    break;

                default:
                    break;
            }
            if (this.peerBase == null)
                throw new Exception("No PeerBase");
            this.peerBase.peer = this;
            this.peerBase.usedTransportProtocol = this.TransportProtocol;
            Type type1 = (Type)null;
            this.SocketImplementationConfig.TryGetValue(this.TransportProtocol, out type1);
            this.SocketImplementation = type1;
        }

        /// <summary>
        /// 서버와 연결을 끊습니다.
        /// 이 메서드를 호출해도 연결이 즉시 닫히지 않으며
        /// 클라이언트가 이미 연결이 끊겼거나 연결 스레드가 중지된 경우 콜백함수가 호출되지 않습니다.
        /// </summary>
        public virtual void Disconnect()
        {
            lock (this.DispatchLockObject)
            {
                lock (this.SendOutgoingLockObject)
                    this.peerBase.Disconnect();
            }
        }

        /// <summary>
        /// 연결을 즉시 닫고 관련 수신 스레드를 종료합니다.
        /// </summary>
        public virtual void StopThread()
        {
            lock (this.DispatchLockObject)
            {
                lock (this.SendOutgoingLockObject)
                    this.peerBase.StopConnection();
            }
        }

        public virtual void FetchServerTimestamp() => this.peerBase.FetchServerTimestamp();

        /// <summary>
        /// 이 메서드는 현재 클라이언트에 대한 공용키를 생성하고 서버와 교환합니다.
        /// </summary>
        public bool EstablishEncryption()
        {
            if (!Peer.AsyncKeyExchange)
                return this.peerBase.ExchangeKeysForEncryption(this.SendOutgoingLockObject);
            int num = (int)SupportClass.StartBackgroundCalls((Func<bool>)(() =>
            {
                this.peerBase.ExchangeKeysForEncryption(this.SendOutgoingLockObject);
                return false;
            }));
            return true;
        }

        public virtual bool SendOutgoingCommands()
        {
            lock (this.SendOutgoingLockObject)
                return this.peerBase.SendOutgoingCommands();
        }

        public virtual bool DispatchIncomingCommands()
        {
            lock (this.DispatchLockObject)
            {
                this.peerBase.ByteCountCurrentDispatch = 0;
                return this.peerBase.DispatchIncomingCommands();
            }
        }

        public virtual bool SendAcksOnly()
        {
            lock (this.SendOutgoingLockObject)
                return this.peerBase.SendAcksOnly();
        }

        public virtual bool SendOperation(byte operationCode, Dictionary<byte, object> operationParameters, SendOptions sendOptions)
        {
            lock (this.EnqueueLock)
                return this.peerBase.EnqueueOperation(operationParameters, operationCode, sendOptions);
        }

        public static bool RegisterType(
      Type customType,
      byte code,
      SerializeMethod serializeMethod,
      DeserializeMethod constructor)
        {
            return Protocol.TryRegisterType(customType, code, serializeMethod, constructor);
        }

        public static bool RegisterType(
          Type customType,
          byte code,
          SerializeStreamMethod serializeMethod,
          DeserializeStreamMethod constructor)
        {
            return Protocol.TryRegisterType(customType, code, serializeMethod, constructor);
        }
    }

}
