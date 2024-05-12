

namespace NDG.Realtime
{
    using System;
    using System.Net.Sockets;

    /// <summary>
    /// 서버가 최선의 Region을 찾을 수 있게 하는 NetworkPing의 추상 클래스
    /// </summary>
    public abstract class NetworkPing :IDisposable
    {
        public string DebugString = "";
        public bool Successful;

        protected internal bool GotResult;

        protected internal int PingLength = 13;

        protected internal byte[] PingBytes = new byte[] { 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x7d, 0x00 };

        protected internal byte PingId;

        private static readonly System.Random RandomIdProvider = new System.Random();

        public virtual bool StartPing(string ip)
        {
            throw new NotImplementedException();
        }

        public virtual bool Done()
        {
            throw new NotImplementedException();
        }

        public virtual void Dispose()
        {
            throw new NotImplementedException();
        }

        protected internal void Init()
        {
            this.GotResult = false;
            this.Successful = false;
            this.PingId = (byte)(RandomIdProvider.Next(255));
        }
    }

    /// <summary>
    /// C# Socket클래스를 사용하여 Ping을 테스트합니다.
    /// </summary>
    public class PingMono : NetworkPing
    {
        private Socket sock;

        /// <summary>
        /// 서버에 Ping을 보냅니다. 성공하면 true를 반환합니다.
        /// </summary>
        public override bool StartPing(string ip)
        {
            this.Init();

            try
            {
                if (this.sock == null)
                {
                    if (ip.Contains("."))
                    {
                        this.sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    }
                    else
                    {
                        this.sock = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                    }

                    this.sock.ReceiveTimeout = 5000;
                    this.sock.Connect(ip, 5055);
                }


                this.PingBytes[this.PingBytes.Length - 1] = this.PingId;
                this.sock.Send(this.PingBytes);
                this.PingBytes[this.PingBytes.Length - 1] = (byte)(this.PingId + 1);  // 이 버퍼는 result/receive에 다시 사용됩니다. 
            }
            catch (Exception e)
            {
                this.sock = null;
                Console.WriteLine(e);
            }

            return false;
        }

        public override bool Done()
        {
            if (this.GotResult || this.sock == null)
            {
                return true;    // 핑이 더이상 대기하고 있지 않다는것을 나타냅니다.
            }

            int read = 0;
            try
            {
                if (!this.sock.Poll(0, SelectMode.SelectRead))
                {
                    return false;
                }

                read = this.sock.Receive(this.PingBytes, SocketFlags.None);
            }
            catch (Exception ex)
            {
                if (this.sock != null)
                {
                    this.sock.Close();
                    this.sock = null;
                }
                this.DebugString += " Exception socket" + ex.GetType() + " ";
                return true;    
            }

            bool replyMatch = this.PingBytes[this.PingBytes.Length - 1] == this.PingId && read == this.PingLength;
            if (!replyMatch)
            {
                this.DebugString += " ping이 일치하지 않습니다. ";
            }


            this.Successful = replyMatch;
            this.GotResult = true;
            return true;
        }

        public override void Dispose()
        {
            try
            {
                this.sock.Close();
            }
            catch
            {
            }

            this.sock = null;
        }

    }
}
