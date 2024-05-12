using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    public enum DebugLevel : byte
    {
        OFF = 0,
        ERROR = 1,
        WARNING = 2,
        INFO = 3,
        ALL = 5,
    }

    public enum StatusCode
    {
        SecurityExceptionOnConnect = 1022, // 0x000003FE
        ExceptionOnConnect = 1023, // 0x000003FF
        Connect = 1024, // 0x00000400
        Disconnect = 1025, // 0x00000401
        Exception = 1026, // 0x00000402
        SendError = 1030, // 0x00000406
        ExceptionOnReceive = 1039, // 0x0000040F
        TimeoutDisconnect = 1040, // 0x00000410
        DisconnectByServerTimeout = 1041, // 0x00000411
        DisconnectByServerUserLimit = 1042, // 0x00000412
        DisconnectByServerLogic = 1043, // 0x00000413
        DisconnectByServerReasonUnknown = 1044, // 0x00000414
        EncryptionEstablished = 1048, // 0x00000418
        EncryptionFailedToEstablish = 1049, // 0x00000419
    }

    public enum ConnectionProtocol : byte
    {
        Udp = 0,
        Tcp = 1,
    }



    public enum ConnectionStateValue : byte
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 3,
        Disconnecting = 4,
        /// <summary>서버와 연결이 끊겼음을 확인하는 상태입니다. state 콜백을 기다립니다.</summary>
        AcknowledgingDisconnect = 5,
        /// <summary>서버와 연결이 제대로 끊어지지 않은 상태입니다.</summary>
        Zombie = 6,
    }

    public enum NetworkCodes : byte
    {
        /// <summary>operation 결과 코드입니다.</summary>
        Ok = 0,
        /// <summary></summary>
        InitEncryption = Ok,
        /// <summary></summary>
        ClientKey = 1,
        /// <summary></summary>
        ServerKey = ClientKey,
        /// <summary></summary>
        Ping = ClientKey,
        /// <summary></summary>
        ModeKey = 2,
    }

    public enum SerializationProtocol
    {
        GpBinaryV16,
        GpBinaryV18,
    }

    /// <summary>
    /// Peer의 상태에 대한 값입니다.
    /// </summary>
    public enum PeerStateValue : byte
    {
        /// <summary>연결이 끊어져서 Operation을 호출할 수 없는 상태입니다.</summary>
        Disconnected = 0,
        /// <summary>소켓을 열고 있거나 서버와 패키지 교환 등의 연결을 설정하는 중입니다.</summary>
        Connecting = 1,
        /// <summary>피어가 연결되고 초기화됩니다. 이제 operation을 송신할 수 있습니다.</summary>
        Connected = 3,
        /// <summary>연결이 끊기는 중입니다. 서버에 연결 끊기를 전송하여 연결이 끊겼음을 확인합니다.</summary>
        Disconnecting = 4,
        /// <summary>
        /// 연결이 설정되고 응용프로그램 이름을 서버로 보냅니다.
        /// Peer.Connect()를 호출하여 응용프로그램 이름을 설정합니다.
        /// </summary>
        InitializingApplication = 10, // 0x0A
    }


    public enum DeliveryMode
    {
        /// <summary>확인 또는 반복 전송 없이 한 번만 전송됩니다. 메세지의 순서가 보장됩니다.</summary>
        Unreliable,
        /// <summary> 확인을 요청합니다. ACK가 도착할때까지 기다립니다. 메세지의 순서가 보장됩니다.</summary>
        Reliable,
        /// <summary>한 번 전송되며, 잘못 도착할 수 있습니다. 사용자 자신의 시퀀싱에 가장 적합합니다.</summary>
        UnreliableUnsequenced,

        ReliableUnsequenced,
    }

    public enum NetSocketState
    {
        Disconnected,
        Connecting,
        Connected,
        Disconnecting
    }

    public enum NetSocketError
    {
        Success,
        Skipped,
        NoData,
        Exception,
        Busy,
    }

    internal enum EgMessageType : byte
    {
        Init = 0,
        InitResponse = 1,
        Operation = 2,
        OperationResponse = 3,
        Event = 4,
        InternalOperationRequest = 6,
        InternalOperationResponse = 7,
        Message = 8,
        RawMessage = 9,
    }
}