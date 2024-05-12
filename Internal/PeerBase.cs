

namespace NDG
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public abstract class PeerBase
    {
        internal Peer peer;
        public IProtocol SerializationProtocol;
        internal ConnectionProtocol usedTransportProtocol;
        internal INetSocket NetSocket;

        /// <summary>피어의 연결상태 (low level)</summary>
        internal ConnectionStateValue peerConnectionState;
        /// <summary>마지막으로 보낸 operation의 byte 갯수 입니다. (직렬화 중에 설정됩니다.) </summary>
        internal int ByteCountLastOperation;
        /// <summary>마지막으로 dispatch된 byte 갯수 입니다. (dispatch/deserialization 중 설정됩니다) </summary>
        internal int ByteCountCurrentDispatch;

        /// <summary>현재 처리되고있는 Command입니다.</summary>
        internal int packetLossByCrc;
        internal int packetLossByChallenge;
        internal readonly Queue<PeerBase.MyAction> ActionQueue = new Queue<PeerBase.MyAction>();

        /// <summary>이 ID는 연결시 서버에 의해 할당됩니다.</summary>
        internal short peerID = -1;

        /// <summary>serverTimeOffset은 serverTImestamp - localTime입니다.  </summary>
        internal int serverTimeOffset;
        internal bool serverTimeOffsetIsAvailable;
        internal int roundTripTime;
        internal int roundTripTimeVariance;
        internal int lastRoundTripTime;
        internal int lowestRoundTripTime;
        internal int highestRoundTripTimeVariance;
        internal int timestampOfLastReceive;

        internal static short peerCount;
        internal long bytesOut;
        internal long bytesIn;
        internal object CustomInitData;

        public string AppId;
        internal EventData reusableEventData;
        internal int timeBase;
        internal int timeoutInt;
        internal int timeLastAckReceive;
        internal int longestSentCall;
        internal int timeLastSendAck;
        internal int timeLastSendOutgoing;

        internal bool ApplicationIsInitialized;
        internal int outgoingCommandsInStream = 0;

        protected internal static Queue<StreamBuffer> MessageBufferPool = new Queue<StreamBuffer>(32);
        private readonly Random lagRandomizer = new Random();
        internal Type SocketImplementation => this.peer.SocketImplementation;

        /// <summary>Connect()호출에 의해 설정된 서버의 주소입니다.</summary>
        public string ServerAddress { get; internal set; }

        public string ProxyServerAddress { get; internal set; }

        internal INetPeerListener Listener => this.peer.Listener;

        public DebugLevel debugOut => this.peer.DebugOut;

        internal int DisconnectTimeout => this.peer.DisconnectTimeout;

        internal int timePingInterval => this.peer.TimePingInterval;

        internal byte ChannelCount => this.peer.ChannelCount;

        internal long BytesOut => this.bytesOut;

        internal long BytesIn => this.bytesIn;

        /// <summary>수신 명령 카운트</summary>
        internal abstract int QueuedIncomingCommandsCount { get; }

        /// <summary>발신 명령 카운트</summary>
        internal abstract int QueuedOutgoingCommandsCount { get; }

        internal virtual int SentReliableCommandsCount => 0;

        public virtual string PeerID => ((ushort)this.peerID).ToString();

        internal int TimeInt => SupportClass.GetTickCount() - this.timeBase;

        internal static int outgoingStreamBufferSize => Peer.OutgoingStreamBufferSize;

        internal bool IsSendingOnlyAcks => this.peer.IsSendingOnlyAcks;

        /// <summary>UDP + TCP에 사용할 최대 전송 단위 입니다. </summary>
        internal int mtu => this.peer.MaximumTransferUnit;

        /// <summary>INetSocekt.Connected가 참이면 이값은 서버의 주소가 IPv6로 확인되는지 여부를 나타냅니다. </summary>
        protected internal bool IsIpv6 => this.NetSocket != null && this.NetSocket.AddressResolvedAsIpv6;

        protected PeerBase()
        {
            ++PeerBase.peerCount;
        }

        public static StreamBuffer MessageBufferPoolGet()
        {
            lock (PeerBase.MessageBufferPool)
                return PeerBase.MessageBufferPool.Count > 0 ? PeerBase.MessageBufferPool.Dequeue() : new StreamBuffer(75);
        }

        public static void MessageBufferPoolPut(StreamBuffer buff)
        {
            buff.Position = 0;
            buff.SetLength(0L);
            lock (PeerBase.MessageBufferPool)
                PeerBase.MessageBufferPool.Enqueue(buff);
        }

        internal virtual void InitPeerBase()
        {
            this.SerializationProtocol = SerializationProtocolFactory.Create(this.peer.SerializationProtocolType);
            this.ByteCountLastOperation = 0;
            this.ByteCountCurrentDispatch = 0;
            this.bytesIn = 0L;
            this.bytesOut = 0L;
            this.packetLossByCrc = 0;
            this.packetLossByChallenge = 0;
            this.peerConnectionState = ConnectionStateValue.Disconnected;
            this.timeBase = SupportClass.GetTickCount();
            this.ApplicationIsInitialized = false;
            this.roundTripTime = 200;
            this.roundTripTimeVariance = 5;
            this.serverTimeOffsetIsAvailable = false;
            this.serverTimeOffset = 0;
        }

        /// <summary>서버에 연결하여 Init(AppID포함)을 전송합니다.</summary>
        internal abstract bool Connect(string serverAddress, string appID, object customData = null);
        internal abstract bool Connect(string serverAddress, string proxyServerAddress, string appID, object customData);


        private string GetHttpKeyValueString(Dictionary<string, string> dic)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (KeyValuePair<string, string> keyValuePair in dic)
                stringBuilder.Append(keyValuePair.Key).Append("=").Append(keyValuePair.Value).Append("&");
            return stringBuilder.ToString();
        }

        internal byte[] PrepareConnectData(string serverAddress, string appID, object custom)
        {
            if (this.NetSocket == null || !this.NetSocket.Connected)
                this.EnqueueDebugReturn(DebugLevel.WARNING, "The peer attempts to prepare an Init-Request but the socket is not connected!?");
            if (custom == null)
            {
                byte[] numArray = new byte[41];
                byte[] clientVersion = Version.clientVersion;
                numArray[0] = (byte)243;
                numArray[1] = (byte)0;
                numArray[2] = this.SerializationProtocol.VersionBytes[0];
                numArray[3] = this.SerializationProtocol.VersionBytes[1];
                numArray[4] = this.peer.ClientSdkIdShifted;
                numArray[5] = (byte)((uint)(byte)((uint)clientVersion[0] << 4) | (uint)clientVersion[1]);
                numArray[6] = clientVersion[2];
                numArray[7] = clientVersion[3];
                numArray[8] = (byte)0;
                if (string.IsNullOrEmpty(appID))
                    appID = "LoadBalancing";
                for (int index = 0; index < 32; ++index)
                    numArray[index + 9] = index < appID.Length ? (byte)appID[index] : (byte)0;
                if (this.IsIpv6)
                    numArray[5] |= (byte)128;
                else
                    numArray[5] &= (byte)127;
                return numArray;
            }
            if (custom == null)
                return (byte[])null;
            Dictionary<string, string> dic = new Dictionary<string, string>();
            dic["init"] = (string)null;
            dic["app"] = appID;
            dic["clientversion"] = this.peer.ClientVersion;
            dic["protocol"] = this.SerializationProtocol.ProtocolType;
            dic["sid"] = this.peer.ClientSdkIdShifted.ToString();
            byte[] numArray1 = (byte[])null;
            int num = 0;
            if (custom != null)
            {
                numArray1 = this.SerializationProtocol.Serialize(custom);
                num += numArray1.Length;
            }
            string httpKeyValueString = this.GetHttpKeyValueString(dic);
            if (this.IsIpv6)
                httpKeyValueString += "&IPv6";
            string s = string.Format("POST /?{0} HTTP/1.1\r\nHost: {1}\r\nContent-Length: {2}\r\n\r\n", (object)httpKeyValueString, (object)serverAddress, (object)num);
            byte[] numArray2 = new byte[s.Length + num];
            if (numArray1 != null)
                Buffer.BlockCopy((Array)numArray1, 0, (Array)numArray2, s.Length, numArray1.Length);
            Buffer.BlockCopy((Array)Encoding.UTF8.GetBytes(s), 0, (Array)numArray2, 0, s.Length);
            return numArray2;
        }


        public abstract void OnConnect();

        /// <summary>서버의 Init Response가 도착했을때 호출됩니다.</summary>
        internal void InitCallback()
        {
            this.EnqueueDebugReturn(DebugLevel.INFO, "InitCallback");
            if (this.peerConnectionState == ConnectionStateValue.Connecting)
                this.peerConnectionState = ConnectionStateValue.Connected;
            this.ApplicationIsInitialized = true;
            this.FetchServerTimestamp();
            this.Listener.OnStatusChanged(StatusCode.Connect);
        }

        internal abstract void Disconnect();

        internal abstract void StopConnection();

        internal abstract void FetchServerTimestamp();

        internal abstract bool EnqueueOperation(Dictionary<byte, object> parameters, byte opCode, SendOptions sendParams, EgMessageType messageType = EgMessageType.Operation);

        internal abstract StreamBuffer SerializeOperationToMessage(byte opCode, Dictionary<byte, object> parameters, EgMessageType messageType, bool encrypt);

        internal abstract bool EnqueueMessage(object message, SendOptions sendOptions);

        internal StreamBuffer SerializeMessageToMessage(object message, bool encrypt, byte[] messageHeader, bool writeLength = true)
        {
            bool flag = encrypt;
            StreamBuffer ms = PeerBase.MessageBufferPoolGet();
            ms.SetLength(0L);
            if (!flag)
                ms.Write(messageHeader, 0, messageHeader.Length);
            if (message is byte[])
            {
                byte[] buffer = message as byte[];
                ms.Write(buffer, 0, buffer.Length);
            }
            else
                this.SerializationProtocol.SerializeMessage(ms, message);

            byte[] buffer1 = ms.GetBuffer();
            buffer1[messageHeader.Length - 1] = message is byte[]? (byte)9 : (byte)8;
            if (flag)
                buffer1[messageHeader.Length - 1] = (byte)((uint)buffer1[messageHeader.Length - 1] | 128U);
            if (writeLength)
            {
                int targetOffset = 1;
                Protocol.Serialize(ms.Length, buffer1, ref targetOffset);
            }
            return ms;
        }

        /// <summary>
        /// outgoing queue에서 보낼 command들을 확인하고 전송합니다
        /// </summary>
        internal abstract bool SendOutgoingCommands();

        internal virtual bool SendAcksOnly() => false;

        internal abstract void ReceiveIncomingCommands(byte[] inBuff, int dataLength);



        /// <summary>
        /// 수신 queue를 확인하고 가능한 경우 수신된 데이터를 dispatch합니다.
        /// </summary>
        /// <returns>dispatch가 발생했는지 여부는 디스패치가 더 필요한지 여부를 나타냅니다.</returns>
        internal abstract bool DispatchIncomingCommands();

        internal virtual bool DeserializeMessageAndCallback(StreamBuffer stream)
        {
            if (stream.Length < 2)
            {
                if (this.debugOut >= DebugLevel.ERROR)
                    this.Listener.DebugReturn(DebugLevel.ERROR, "수신된  데이터가 너무 작습니다. " + stream.Length.ToString());
                return false;
            }
            byte num1 = stream.ReadByte();
            if (num1 != (byte)243 && num1 != (byte)253)
            {
                if (this.debugOut >= DebugLevel.ERROR)
                    this.Listener.DebugReturn(DebugLevel.ALL, "정의되지 않은  메세지입니다. message:" + num1.ToString());
                return false;
            }
            byte num2 = stream.ReadByte();
            byte num3 = (byte)((uint)num2 & (uint)sbyte.MaxValue); //num2가 1 or 129일경우 1
            bool flag = ((int)num2 & 128) > 0; //num2가 128 이상 일경우 true
            if (num3 != (byte)1) 
            {
                try
                {
                        stream.Seek(2L, SeekOrigin.Begin);
                }
                catch (Exception ex)
                {
                    if (this.debugOut >= DebugLevel.ERROR)
                        this.Listener.DebugReturn(DebugLevel.ERROR, "msgType: " + num3.ToString() + " exception: " + ex.ToString());
                    SupportClass.WriteStackTrace(ex);
                    return false;
                }
            }
            //int num4 = 0;
            this.EnqueueDebugReturn(DebugLevel.INFO, "ReceivedOperationCode:"+((int)num3 -1));
            switch ((int)num3 - 1)
            {
                case 0:
                    this.InitCallback();
                    break;
                case 2:
                    OperationResponse operationResponse1;
                    try
                    {
                        operationResponse1 = this.SerializationProtocol.DeserializeOperationResponse(stream);
                    }
                    catch (Exception ex)
                    {
                        this.EnqueueDebugReturn(DebugLevel.ERROR, "Operation Response에 대한 역직렬화에 실패했습니다. " + ex?.ToString());
                        return false;
                    }

                    this.Listener.OnOperationResponse(operationResponse1);

                    break;
                case 3:
                    EventData eventData;
                    try
                    {
                        IProtocol.DeserializationFlags flags = this.peer.UseByteArraySlicePoolForEvents ? IProtocol.DeserializationFlags.AllowPooledByteArray : IProtocol.DeserializationFlags.None;
                        eventData = this.SerializationProtocol.DeserializeEventData(stream, this.reusableEventData, flags);
                    }
                    catch (Exception ex)
                    {
                        this.EnqueueDebugReturn(DebugLevel.ERROR, "Event에 대한 역직력화에 실패했습니다.  " + ex?.ToString());
                        return false;
                    }

                    this.Listener.OnEvent(eventData);

                    break;
                case 6:
                    OperationResponse operationResponse2;
                    try
                    {
                        operationResponse2 = this.SerializationProtocol.DeserializeOperationResponse(stream);
                    }
                    catch (Exception ex)
                    {
                        this.EnqueueDebugReturn(DebugLevel.ERROR, "internal Operation Response에 대한 역직렬화에 실패했습니다. " + ex?.ToString());
                        return false;
                    }

                    if ((int)operationResponse2.OperationCode == (int)NetworkCodes.InitEncryption)
                        this.DeriveSharedKey(operationResponse2);
                    else if ((int)operationResponse2.OperationCode == (int)NetworkCodes.Ping)
                    {
                        if (this is TPeer tpeer4)
                            tpeer4.ReadPingResult(operationResponse2);
                    }
                    else
                        this.EnqueueDebugReturn(DebugLevel.ERROR, "Received unknown internal operation. " + operationResponse2.ToString());

                    break;
                case 7:
                    this.SerializationProtocol.DeserializeMessage(stream);

                    break;
                case 8:
 
                    stream.ToArrayFromPos();

                    break;
                default:
                    this.EnqueueDebugReturn(DebugLevel.ERROR, "예기치 않은 message type입니다.  " + num3.ToString());
                    break;
            }
            return true;
        }

        internal void UpdateRoundTripTimeAndVariance(int lastRoundtripTime)
        {
            if (lastRoundtripTime < 0)
                return;
            this.roundTripTimeVariance -= this.roundTripTimeVariance / 4;
            if (lastRoundtripTime >= this.roundTripTime)
            {
                this.roundTripTime += (lastRoundtripTime - this.roundTripTime) / 8;
                this.roundTripTimeVariance += (lastRoundtripTime - this.roundTripTime) / 4;
            }
            else
            {
                this.roundTripTime += (lastRoundtripTime - this.roundTripTime) / 8;
                this.roundTripTimeVariance -= (lastRoundtripTime - this.roundTripTime) / 4;
            }
            if (this.roundTripTime < this.lowestRoundTripTime)
                this.lowestRoundTripTime = this.roundTripTime;
            if (this.roundTripTimeVariance <= this.highestRoundTripTimeVariance)
                return;
            this.highestRoundTripTimeVariance = this.roundTripTimeVariance;
        }

        /// <summary>
        /// 내부적으로 서버와 암호화 키를 교환하는 작업에 사용합니다.
        /// </summary>
        internal bool ExchangeKeysForEncryption(object lockObject)
        {
            Dictionary<byte, object> parameters = new Dictionary<byte, object>(1);
            if (lockObject != null)
            {
                lock (lockObject)
                {
                    SendOptions sendParams = new SendOptions()
                    {
                        Channel = 0,
                        Encrypt = false,
                        DeliveryMode = DeliveryMode.Reliable
                    };
                    return this.EnqueueOperation(parameters, (byte)NetworkCodes.InitEncryption, sendParams, EgMessageType.InternalOperationRequest);
                }
            }
            else
            {
                SendOptions sendParams = new SendOptions()
                {
                    Channel = 0,
                    Encrypt = false,
                    DeliveryMode = DeliveryMode.Reliable
                };
                return this.EnqueueOperation(parameters, (byte)NetworkCodes.InitEncryption, sendParams, EgMessageType.InternalOperationRequest);
            }
        }

        internal void DeriveSharedKey(OperationResponse operationResponse)
        {
            if ((uint)operationResponse.ReturnCode > 0U)
            {
                this.EnqueueDebugReturn(DebugLevel.ERROR, "암호화 키를 설정하는데 실패하였습니다.  " + operationResponse.ToString());
                this.EnqueueStatusCallback(StatusCode.EncryptionFailedToEstablish);
            }
            else
            {
                byte[] otherPartyPublicKey = (byte[])operationResponse[(byte)NetworkCodes.ServerKey];
                if (otherPartyPublicKey == null || otherPartyPublicKey.Length == 0)
                {
                    this.EnqueueDebugReturn(DebugLevel.ERROR, "암호화 키를 설정하지 못했습니다. 서버의 공용 키가 null이거나 비어있습니다.  " + operationResponse.ToString());
                    this.EnqueueStatusCallback(StatusCode.EncryptionFailedToEstablish);
                }
                else
                {
                    this.EnqueueStatusCallback(StatusCode.EncryptionEstablished);
                }
            }
        }

        internal void EnqueueActionForDispatch(PeerBase.MyAction action)
        {
            lock (this.ActionQueue)
                this.ActionQueue.Enqueue(action);
        }

        internal void EnqueueDebugReturn(DebugLevel level, string debugReturn)
        {
            lock (this.ActionQueue)
                this.ActionQueue.Enqueue((PeerBase.MyAction)(() => this.Listener.DebugReturn(level, debugReturn)));
        }

        internal void EnqueueStatusCallback(StatusCode statusValue)
        {
            lock (this.ActionQueue)
                this.ActionQueue.Enqueue((PeerBase.MyAction)(() => this.Listener.OnStatusChanged(statusValue)));
        }

        internal delegate void MyAction();
    }
}

