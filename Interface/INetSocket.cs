using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    public abstract class INetSocket
    {
        protected internal PeerBase peerBase;
        /// <summary>생성자에서 정의된 이 소켓의 프로토콜입니다.</summary>
        protected readonly ConnectionProtocol Protocol;
        public bool PollReceive;
        /// <summary>Connect() 호출을 통해 정의된 주소. </summary>
        public string ConnectAddress;

        protected INetPeerListener Listener => this.peerBase.Listener;

        protected internal int MTU => this.peerBase.mtu;

        public NetSocketState State { get; protected set; }

        public bool Connected => this.State == NetSocketState.Connected;

        /// <summary>서버의 host이름(프로토콜,포트 및 경로를 제외한)만 포함됩니다.</summary>
        public string ServerAddress { get; protected set; }

        public string ProxyServerAddres { get; protected set; }

        /// <summary>ServerAddress의 IP주소를 포함합니다. (GetIPAddress를 사용하지 않은 경우 비어있음.)</summary>
        public static string ServerIpAddress { get; protected set; }

        /// <summary>서버의 포트만 포함합니다. </summary>
        public int ServerPort { get; protected set; }

        /// <summary>사용 가능한 경우 서버의 주소가 IPv6주소로 확인되었는지 여부를 표시합니다.</summary>
        public bool AddressResolvedAsIpv6 { get; protected internal set; }

        public string UrlProtocol { get; protected set; }

        public string UrlPath { get; protected set; }

        protected internal string SerializationProtocol => this.peerBase == null || this.peerBase.peer == null ? "GpBinaryV18" : System.Enum.GetName(typeof(SerializationProtocol), (object)this.peerBase.peer.SerializationProtocolType);

        public INetSocket(PeerBase peerBase)
        {
            this.Protocol = peerBase != null ? peerBase.usedTransportProtocol : throw new Exception("peer 없이는 초기화 할 수 없습니다.");
            this.peerBase = peerBase;
            this.ConnectAddress = this.peerBase.ServerAddress;
        }

        public virtual bool Connect()
        {
            if ((uint)this.State > 0U)
            {
                if (this.peerBase.debugOut >= DebugLevel.ERROR)
                    this.peerBase.Listener.DebugReturn(DebugLevel.ERROR, "Connect() 실패: connection 상태 : " + this.State.ToString());
                return false;
            }

            if (this.peerBase == null || this.Protocol != this.peerBase.usedTransportProtocol)
                return false;
            string address;
            ushort port;
            string urlProtocol;
            string urlPath;
            if (!this.TryParseAddress(this.peerBase.ServerAddress, out address, out port, out urlProtocol, out urlPath))
            {
                if (this.peerBase.debugOut >= DebugLevel.ERROR)
                    this.peerBase.Listener.DebugReturn(DebugLevel.ERROR, "주소를 얻어오는데 실패했습니다. :" + this.peerBase.ServerAddress);
                return false;
            }
            INetSocket.ServerIpAddress = string.Empty;
            this.ServerAddress = address;
            this.ServerPort = (int)port;
            this.UrlProtocol = urlProtocol;
            this.UrlPath = urlPath;
            if (this.peerBase.debugOut >= DebugLevel.ALL)
                this.Listener.DebugReturn(DebugLevel.ALL, "INetSocekt.Connect() " + this.ServerAddress + ":" + this.ServerPort.ToString() + " this.Protocol: " + this.Protocol.ToString());
            return true;
        }

        public abstract bool Disconnect();

        public abstract NetSocketError Send(byte[] data, int length);

        public abstract NetSocketError Receive(out byte[] data);

        public void HandleReceivedDatagram(byte[] inBuffer, int length, bool willBeReused)
        {
            this.peerBase.ReceiveIncomingCommands(inBuffer, length);
        }

        public bool ReportDebugOfLevel(DebugLevel levelOfMessage) => this.peerBase.debugOut >= levelOfMessage;

        public void EnqueueDebugReturn(DebugLevel debugLevel, string message) => this.peerBase.EnqueueDebugReturn(debugLevel, message);

        protected internal void HandleException(StatusCode statusCode)
        {
            this.State = NetSocketState.Disconnecting;
            this.peerBase.EnqueueStatusCallback(statusCode);
            this.peerBase.EnqueueActionForDispatch((PeerBase.MyAction)(() => this.peerBase.Disconnect()));
        }

        /// <summary>
        /// 주어진 주소에서 주소와 포트로 구분합니다. 포트는 콜론뒤에 반드시 포함되어있어야합니다.
        /// </summary>
        protected internal bool TryParseAddress(string url, out string address, out ushort port, out string urlProtocol, out string urlPath)
        {
            address = string.Empty;
            port = (ushort)0;
            urlProtocol = string.Empty;
            urlPath = string.Empty;
            string str = url;

            if (string.IsNullOrEmpty(str))
                return false;

            int length1 = str.IndexOf("://");
            if (length1 >= 0)
            {
                urlProtocol = str.Substring(0, length1);
                str = str.Substring(length1 + 3);
            }
            int num = str.IndexOf("/");
            if (num >= 0)
            {
                urlPath = str.Substring(num);
                str = str.Substring(0, num);
            }

            int length2 = str.LastIndexOf(':');
            if (length2 < 0 || str.IndexOf(':') != length2 && (!str.Contains("[") || !str.Contains("]")))
                return false;
            address = str.Substring(0, length2);
            return ushort.TryParse(str.Substring(length2 + 1), out port);
        }

        /// <summary>
        /// 콜론이 있는지 확인하여 간단하게 IPAddress가 IPv6인지 확인합니다.
        /// </summary>
        protected internal bool IsIpv6SimpleCheck(IPAddress address) => address != null && address.ToString().Contains(":");

        /// <summary>
        /// 주소 배열을 반환하도록 DNS호출을 래핑하여 IPv6 주소가 먼저 정렬되도록 합니다.
        /// </summary>
        /// <remarks>
        /// 호스트 이름이 IPv4 주소인 경우 DNS조회를 건너 뛰며 이 주소만 그대로 사용됩니다.
        /// DNS 조회에 시간이 걸릴 수 있으므로 추가 스레드에서 이 작업을 수행하는 것이 좋습니다.
        /// </remarks>
        protected internal IPAddress[] GetIpAddresses(string hostname)
        {
            IPAddress address = (IPAddress)null;
            if (IPAddress.TryParse(hostname, out address))
                return new IPAddress[1] { address };

            IPAddress[] addressList;
            try
            {
                addressList = Dns.GetHostEntry(this.ServerAddress).AddressList;
            }
            catch (Exception ex)
            {
                if (this.ReportDebugOfLevel(DebugLevel.ERROR))
                    this.EnqueueDebugReturn(DebugLevel.ERROR, "DNS.GetHostEntry() 실패 : " + this.ServerAddress + ". Exception: " + ex?.ToString());
                this.HandleException(StatusCode.ExceptionOnConnect);
                return (IPAddress[])null;
            }
            Array.Sort<IPAddress>(addressList, new Comparison<IPAddress>(this.AddressSortComparer));
            if (this.ReportDebugOfLevel(DebugLevel.INFO))
            {
                string[] array = ((IEnumerable<IPAddress>)addressList).Select<IPAddress, string>((Func<IPAddress, string>)(x => x.ToString() + " (" + x.AddressFamily.ToString() + "(" + ((int)x.AddressFamily).ToString() + "))")).ToArray<string>();
                string str = string.Join(",", array);
                if (this.ReportDebugOfLevel(DebugLevel.INFO))
                    this.EnqueueDebugReturn(DebugLevel.INFO, this.ServerAddress + " resolved to " + array.Length.ToString() + " address(es): " + str);
            }
            return addressList;
        }

        private int AddressSortComparer(IPAddress x, IPAddress y) => x.AddressFamily == y.AddressFamily ? 0 : (x.AddressFamily == AddressFamily.InterNetworkV6 ? -1 : 1);

    }
}
