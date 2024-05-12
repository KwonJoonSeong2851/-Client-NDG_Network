using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    internal class TPeer : PeerBase
    {
        /// <summary>TCP "Package" header: 7 bytes</summary>
        internal const int TCP_HEADER_BYTES = 7;
        /// <summary>TCP "Message" header: 2 bytes</summary>
        internal const int MSG_HEADER_BYTES = 2;
        /// <summary>TCP header combined: 9 bytes</summary>
        public const int ALL_HEADER_BYTES = 9;
        private Queue<byte[]> incomingList = new Queue<byte[]>(32);
        internal List<StreamBuffer> outgoingStream;
        private int lastPingResult;
        private byte[] pingRequest = new byte[5]
        {
      (byte) 240,
      (byte) 0,
      (byte) 0,
      (byte) 0,
      (byte) 0
        };
        internal static readonly byte[] tcpFramedMessageHead = new byte[9]
        {
      (byte) 251,
      (byte) 0,
      (byte) 0,
      (byte) 0,
      (byte) 0,
      (byte) 0,
      (byte) 0,
      (byte) 243,
      (byte) 2
        };
        internal static readonly byte[] tcpMsgHead = new byte[2]
        {
      (byte) 243,
      (byte) 2
        };
        internal byte[] messageHeader;

        protected internal bool DoFraming = true;

        internal override int QueuedIncomingCommandsCount => this.incomingList.Count;

        internal override int QueuedOutgoingCommandsCount => this.outgoingCommandsInStream;

        internal override void InitPeerBase()
        {
            base.InitPeerBase();
            this.incomingList = new Queue<byte[]>(32);
            this.timestampOfLastReceive = SupportClass.GetTickCount();
        }

        internal override bool Connect(string serverAddress, string appID, object customData = null) => this.Connect(serverAddress, (string)null, appID, customData);

        internal override bool Connect(string serverAddress, string proxyServerAddress, string appID, object customData)
        {
            if ((uint)this.peerConnectionState > 0U)
            {
                this.Listener.DebugReturn(DebugLevel.WARNING, "피어가 연결이 끊어지지 않은 경우 Connect()를 호출할 수 없습니다.");
                return false;
            }
            if (this.debugOut >= DebugLevel.ALL)
                this.Listener.DebugReturn(DebugLevel.ALL, "Connect()");

            this.ServerAddress = serverAddress;
            this.ProxyServerAddress = proxyServerAddress;
            this.InitPeerBase();
            this.outgoingStream = new List<StreamBuffer>();
            if (this.SocketImplementation != null)
                this.NetSocket = (INetSocket)Activator.CreateInstance(this.SocketImplementation, (object)this);
            else
                this.NetSocket = (INetSocket)new SocketTcpAsync((PeerBase)this);
            if (this.NetSocket == null)
            {
                this.Listener.DebugReturn(DebugLevel.ERROR, " SocketImplementation 또는 소켓이 null이기 때문에 Connect()가 실패했습니다. Connect() 전에 NetPeer.SocketImplementation을 설정하세요. SocketImplementation: " + this.SocketImplementation?.ToString());
                return false;
            }

            this.messageHeader = this.DoFraming ? TPeer.tcpFramedMessageHead : TPeer.tcpMsgHead;
            if (this.NetSocket.Connect())
            {
                this.peerConnectionState = ConnectionStateValue.Connecting;
                return true;
            }
            this.peerConnectionState = ConnectionStateValue.Disconnected;
            return false;
        }

        public override void OnConnect()
        {
            this.EnqueueDebugReturn(DebugLevel.INFO, "TPeer : OnConnect() ");
            this.lastPingResult = SupportClass.GetTickCount();
            if (this.DoFraming || this.CustomInitData != null)
                this.EnqueueInit(this.PrepareConnectData(this.ServerAddress, this.AppId, this.CustomInitData));
            this.SendOutgoingCommands();
        }

        internal override void Disconnect()
        {
            if (this.peerConnectionState == ConnectionStateValue.Disconnected || this.peerConnectionState == ConnectionStateValue.Disconnecting)
                return;
            if (this.debugOut >= DebugLevel.ALL)
                this.Listener.DebugReturn(DebugLevel.ALL, "TPeer.Disconnect()");
            this.StopConnection();
        }

        internal override void StopConnection()
        {
            this.peerConnectionState = ConnectionStateValue.Disconnecting;
            if (this.NetSocket != null)
                this.NetSocket.Disconnect();
            lock (this.incomingList)
                this.incomingList.Clear();
            this.peerConnectionState = ConnectionStateValue.Disconnected;
            this.EnqueueStatusCallback(StatusCode.Disconnect);
        }

        internal override void FetchServerTimestamp()
        {
            if (this.peerConnectionState != ConnectionStateValue.Connected)
            {
                if (this.debugOut >= DebugLevel.INFO)
                    this.Listener.DebugReturn(DebugLevel.INFO, "클라이언트가 연결상태가 아니므로 FetchServerTimestamp()를 건너 뛰었습니다. Current ConnectionState: " + this.peerConnectionState.ToString());
                this.Listener.OnStatusChanged(StatusCode.SendError);
            }
            else
            {
                this.SendPing();
                this.serverTimeOffsetIsAvailable = false;
            }
        }

        private void EnqueueInit(byte[] data)
        {
            StreamBuffer opMessage = new StreamBuffer(data.Length + 32);
            byte[] numArray1 = new byte[7];
            numArray1[0] = (byte)251;
            numArray1[6] = (byte)1;
            byte[] numArray2 = numArray1;
            int targetOffset = 1;
            Protocol.Serialize(data.Length + numArray2.Length, numArray2, ref targetOffset);
            opMessage.Write(numArray2, 0, numArray2.Length);
            opMessage.Write(data, 0, data.Length);

            this.EnqueueMessageAsPayload(DeliveryMode.Reliable, opMessage, (byte)0);
        }

        internal override bool DispatchIncomingCommands()
        {
            if (this.peerConnectionState == ConnectionStateValue.Connected && SupportClass.GetTickCount() - this.timestampOfLastReceive > this.DisconnectTimeout)
            {
                this.EnqueueStatusCallback(StatusCode.TimeoutDisconnect);
                this.EnqueueActionForDispatch(new PeerBase.MyAction(((PeerBase)this).Disconnect));
            }

            while (true)
            {
                PeerBase.MyAction myAction;
                lock (this.ActionQueue)
                {
                    if (this.ActionQueue.Count > 0)
                        myAction = this.ActionQueue.Dequeue();
                    else
                        break;
                }
                myAction();
            }
            byte[] buf;
            lock (this.incomingList)
            {
                if (this.incomingList.Count <= 0)
                    return false;
                buf = this.incomingList.Dequeue();
            }
            this.ByteCountCurrentDispatch = buf.Length + 3;
            return this.DeserializeMessageAndCallback(new StreamBuffer(buf));
        }

        internal override bool SendOutgoingCommands()
        {
            if (this.peerConnectionState == ConnectionStateValue.Disconnected || !this.NetSocket.Connected)
                return false;

            this.timeLastSendOutgoing = this.TimeInt;
            if (this.peerConnectionState == ConnectionStateValue.Connected && Math.Abs(SupportClass.GetTickCount() - this.lastPingResult) > this.timePingInterval)
                this.SendPing();

            lock (this.outgoingStream)
            {
                for (int index = 0; index < this.outgoingStream.Count; ++index)
                {
                    StreamBuffer buff = this.outgoingStream[index];
                    this.SendData(buff.GetBuffer(), buff.Length);
                    PeerBase.MessageBufferPoolPut(buff);
                }
                this.outgoingStream.Clear();
                this.outgoingCommandsInStream = 0;
            }
            return false;
        }

        internal override bool SendAcksOnly()
        {
            if (this.NetSocket == null || !this.NetSocket.Connected || this.peerConnectionState != ConnectionStateValue.Connected || SupportClass.GetTickCount() - this.lastPingResult <= this.timePingInterval)
                return false;
            this.SendPing();
            return false;
        }

        internal override bool EnqueueOperation(Dictionary<byte, object> parameters, byte opCode, SendOptions sendParams, EgMessageType messageType = EgMessageType.Operation)
        {
            if (this.peerConnectionState != ConnectionStateValue.Connected)
            {
                if (this.debugOut >= DebugLevel.ERROR)
                    this.Listener.DebugReturn(DebugLevel.ERROR, "전송 실패 op: " + opCode.ToString() + "! 연결되어 있지 않습니다. PeerState: " + this.peerConnectionState.ToString());
                this.Listener.OnStatusChanged(StatusCode.SendError);
                return false;
            }
            if ((int)sendParams.Channel >= (int)this.ChannelCount)
            {
                if (this.debugOut >= DebugLevel.ERROR)
                    this.Listener.DebugReturn(DebugLevel.ERROR, "전송 실패 op: 선택된 channel (" + sendParams.Channel.ToString() + ")>= channelCount (" + this.ChannelCount.ToString() + ").");
                this.Listener.OnStatusChanged(StatusCode.SendError);
                return false;
            }

            if (this.debugOut >= DebugLevel.INFO)
                this.Listener.DebugReturn(DebugLevel.INFO, "TPeer: EnqueueOperation Code: " + opCode + " paramCounters: " + parameters.Count);


            StreamBuffer message = this.SerializeOperationToMessage(opCode, parameters, messageType, sendParams.Encrypt);
            return this.EnqueueMessageAsPayload(sendParams.DeliveryMode, message, sendParams.Channel);
        }


        internal override bool EnqueueMessage(object msg, SendOptions sendOptions)
        {
            if (this.peerConnectionState != ConnectionStateValue.Connected)
            {
                if (this.debugOut >= DebugLevel.ERROR)
                    this.Listener.DebugReturn(DebugLevel.ERROR, "메세지 전송에 실패했습니다. 연결되어있지 않습니다. PeerState: " + this.peerConnectionState.ToString());
                this.Listener.OnStatusChanged(StatusCode.SendError);
                return false;
            }

            byte channel = sendOptions.Channel;
            if ((int)channel >= (int)this.ChannelCount)
            {
                if (this.debugOut >= DebugLevel.ERROR)
                    this.Listener.DebugReturn(DebugLevel.ERROR, "전송에 실패했습니다. op: 선택된 channel (" + channel.ToString() + ")>= channelCount (" + this.ChannelCount.ToString() + ").");
                this.Listener.OnStatusChanged(StatusCode.SendError);
                return false;
            }

            StreamBuffer message = this.SerializeMessageToMessage(msg, sendOptions.Encrypt, this.messageHeader);
            return this.EnqueueMessageAsPayload(sendOptions.DeliveryMode, message, channel);
        }


        /// <summary>
        /// Operation을 직렬화합니다 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="parameters"></param>
        /// <param name="messageType"></param>
        /// <param name="encrypt"></param>
        /// <returns></returns>
        internal override StreamBuffer SerializeOperationToMessage(byte opCode, Dictionary<byte, object> parameters, EgMessageType messageType, bool encrypt)
        {
            bool flag = encrypt;
            StreamBuffer stream = PeerBase.MessageBufferPoolGet();
            stream.SetLength(0L);

            if (!flag)
                stream.Write(this.messageHeader, 0, this.messageHeader.Length);
            this.SerializationProtocol.SerializeOperationRequest(stream, opCode, parameters, false);


            byte[] buffer1 = stream.GetBuffer();
            if (messageType != EgMessageType.Operation)
                buffer1[this.messageHeader.Length - 1] = (byte)messageType;

            if (flag || encrypt && this.peer.EnableEncryptedFlag)
                buffer1[this.messageHeader.Length - 1] = (byte)((uint)buffer1[this.messageHeader.Length - 1] | 128U);

            if (this.DoFraming)
            {
                int targetOffset = 1;
                Protocol.Serialize(stream.Length, buffer1, ref targetOffset);
            }
            return stream;
        }


        /// <summary>
        /// 직렬화된 operation을 tcp stream / package로 전송하기 위해 queue에 삽입합니다.
        /// </summary>

        internal bool EnqueueMessageAsPayload(DeliveryMode deliveryMode, StreamBuffer opMessage, byte channelId)
        {
            if (opMessage == null)
                return false;
            if (this.DoFraming)
            {
                byte[] buffer = opMessage.GetBuffer();
                buffer[5] = channelId;
                switch (deliveryMode)
                {
                    case DeliveryMode.Unreliable:
                        buffer[6] = (byte)0;
                        break;
                    case DeliveryMode.Reliable:
                        buffer[6] = (byte)1;
                        break;
                    case DeliveryMode.UnreliableUnsequenced:
                        buffer[6] = (byte)2;
                        break;
                    case DeliveryMode.ReliableUnsequenced:
                        buffer[6] = (byte)3;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("DeliveryMode", (object)deliveryMode, (string)null);
                }
            }

            lock (this.outgoingStream)
            {
                this.EnqueueDebugReturn(DebugLevel.INFO, "TPeer: OutgoingStream Length: " + opMessage.Length);
                this.outgoingStream.Add(opMessage);
                ++this.outgoingCommandsInStream;
            }
            int length = opMessage.Length;
            this.ByteCountLastOperation = length;

            return true;
        }

        /// <summary>
        /// 핑을 전송하고 잠시 동안 다른 핑을 피하기 위해 this.lastPingResult를 수정합니다.
        /// </summary>
        internal void SendPing()
        {
            int tickCount = SupportClass.GetTickCount();
            this.lastPingResult = tickCount;
            if (!this.DoFraming)
            {
                SendOptions sendOptions = new SendOptions()
                {
                    DeliveryMode = DeliveryMode.Reliable
                };
                int ping = (int)NetworkCodes.Ping;

                Dictionary<byte, object> parameters = new Dictionary<byte, object>();
                parameters.Add((byte)1, (object)tickCount);
                int num = sendOptions.Encrypt ? 1 : 0;
                StreamBuffer message = this.SerializeOperationToMessage((byte)ping, parameters, EgMessageType.InternalOperationRequest, num != 0);
                this.SendData(message.GetBuffer(), message.Length);

                PeerBase.MessageBufferPoolPut(message);
            }
            else
            {
                int targetOffset = 1;
                Protocol.Serialize(tickCount, this.pingRequest, ref targetOffset);

                this.SendData(this.pingRequest, this.pingRequest.Length);
            }
        }

        internal void SendData(byte[] data, int length)
        {
            try
            {
                this.bytesOut += (long)length;



                int num = (int)this.NetSocket.Send(data, length);
                //this.EnqueueDebugReturn(DebugLevel.INFO, "TPeer: Succecesed send byte :" + length);

            }
            catch (Exception ex)
            {
                this.EnqueueDebugReturn(DebugLevel.INFO, "TPeer: failed send byte :" + length);

                if (this.debugOut >= DebugLevel.ERROR)
                {
                    this.Listener.DebugReturn(DebugLevel.ERROR, ex.ToString() + "  " + ex.StackTrace);
                }
                SupportClass.WriteStackTrace(ex);
            }
        }


        internal override void ReceiveIncomingCommands(byte[] inBuff, int dataLength)
        {
            if (inBuff == null)
            {
                if (this.debugOut < DebugLevel.ERROR)
                    return;
                this.EnqueueDebugReturn(DebugLevel.ERROR, "checkAndQueueIncomingCommands() inBuff: null");
            }
            else
            {
                this.timestampOfLastReceive = SupportClass.GetTickCount();
                this.bytesIn += (long)(dataLength + 7);

                if (inBuff[0] == (byte)243)
                {
                    byte num1 = (byte)((uint)inBuff[1] & (uint)sbyte.MaxValue);
                    byte num2 = inBuff[2];
                    if (num1 == (byte)7 && (int)num2 == (int)NetworkCodes.Ping)
                    {
                        this.DeserializeMessageAndCallback(new StreamBuffer(inBuff));
                    }
                    else
                    {
                        byte[] numArray = new byte[dataLength];
                        Buffer.BlockCopy((Array)inBuff, 0, (Array)numArray, 0, dataLength);
                        lock (this.incomingList)
                            this.incomingList.Enqueue(numArray);
                    }
                }
                else if (inBuff[0] == (byte)240)
                {
                    this.ReadPingResult(inBuff);
                }
                else
                {
                    if (this.debugOut < DebugLevel.ERROR)
                        return;
                    this.EnqueueDebugReturn(DebugLevel.ERROR, "receiveIncomingCommands() MagicNumber should be 0xF0 or 0xF3. Is: " + inBuff[0].ToString());
                }
            }
        }


        private void ReadPingResult(byte[] inbuff)
        {
            int num1 = 0;
            int num2 = 0;
            int offset = 1;
            Protocol.Deserialize(out num1, inbuff, ref offset);
            Protocol.Deserialize(out num2, inbuff, ref offset);
            this.lastRoundTripTime = SupportClass.GetTickCount() - num2;

            if (!this.serverTimeOffsetIsAvailable)
                this.roundTripTime = this.lastRoundTripTime;
            this.UpdateRoundTripTimeAndVariance(this.lastRoundTripTime);
            if (this.serverTimeOffsetIsAvailable)
                return;
            this.serverTimeOffset = num1 + (this.lastRoundTripTime >> 1) - SupportClass.GetTickCount();
            this.serverTimeOffsetIsAvailable = true;
        }


        protected internal void ReadPingResult(OperationResponse operationResponse)
        {
            int parameter1 = (int)operationResponse.Parameters[(byte)2];
            int parameter2 = (int)operationResponse.Parameters[(byte)1];
            this.lastRoundTripTime = SupportClass.GetTickCount() - parameter2;
            if (!this.serverTimeOffsetIsAvailable)
                this.roundTripTime = this.lastRoundTripTime;
            this.UpdateRoundTripTimeAndVariance(this.lastRoundTripTime);
            if (this.serverTimeOffsetIsAvailable)
                return;
            this.serverTimeOffset = parameter1 + (this.lastRoundTripTime >> 1) - SupportClass.GetTickCount();
            this.serverTimeOffsetIsAvailable = true;
        }
    }
}
