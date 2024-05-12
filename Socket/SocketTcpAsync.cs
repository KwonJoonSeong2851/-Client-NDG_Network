
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security;
using System.Diagnostics;

namespace NDG
{
    public class SocketTcpAsync : INetSocket, IDisposable
    {
        private Socket sock;
        private readonly object syncer = new object();

        public SocketTcpAsync(PeerBase peer) : base(peer)
        {
            if(this.ReportDebugOfLevel(DebugLevel.INFO))
              this.Listener.DebugReturn(DebugLevel.INFO, "SocketTcpAsync, .net, unity");
            this.PollReceive = false;
        }

        ~SocketTcpAsync() => this.Dispose();

        public void Dispose()
        {
            this.State = NetSocketState.Disconnecting;
            if(this.sock != null)
            {
                try
                {
                    if(this.sock.Connected)
                    this.sock.Close();
                }
                catch(Exception ex)
                {
                    this.EnqueueDebugReturn(DebugLevel.INFO, "Exception in Dispose(): " + ex?.ToString());
                }
            }
            this.sock = (Socket)null;
            this.State = NetSocketState.Disconnected;
        }

        public override bool Connect()
        {
            lock(this.syncer)
            {
                if(!base.Connect())
                  return false;
                this.State = NetSocketState.Connecting;
            }
            new Thread(new ThreadStart(this.DnsAndConnect))
            {
                IsBackground = true
            }.Start();
            return true;
        }



        public override bool Disconnect()
        {
            if(this.ReportDebugOfLevel(DebugLevel.INFO))
            this.EnqueueDebugReturn(DebugLevel.INFO, "SOcketTcpAsync.Disconnect()");
            lock(this.syncer)
            {
                this.State = NetSocketState.Disconnecting;
                if(this.sock != null)
                {
                    try
                    {
                        this.sock.Close();
                    }
                    catch(Exception ex)
                    {
                        if(this.ReportDebugOfLevel(DebugLevel.INFO))
                        this.EnqueueDebugReturn(DebugLevel.INFO, "Exception in Disconnect(): " + ex?.ToString());
                    }
                }
                this.State = NetSocketState.Disconnected;
            }
            return true;
        }

        public override NetSocketError Send(byte[] data, int length)
        {
            try
            {
                if(this.sock == null || !this.sock.Connected)
                return NetSocketError.Skipped;
                this.sock.Send(data, 0, length, SocketFlags.None);
            }
            catch(Exception ex)
            {
                if(this.State != NetSocketState.Disconnecting && (uint)this.State > 0U)
                {
                    if(this.ReportDebugOfLevel(DebugLevel.INFO))
                    {
                        string str = "";
                        if(this.sock != null)
                          str = string.Format(" Local: {0} Remote: {1} ({2}, {3})", (object) this.sock.LocalEndPoint, (object) this.sock.RemoteEndPoint, this.sock.Connected ? (object) "connected" : (object) "not connected", this.sock.IsBound ? (object) "bound" : (object) "not bound");
                        this.EnqueueDebugReturn(DebugLevel.INFO, string.Format("Cannot send to: {0} ({4}). Uptime: {1} ms. {2} {3}", (object) this.ServerAddress, (object) (SupportClass.GetTickCount() - this.peerBase.timeBase), this.AddressResolvedAsIpv6 ? (object) " IPv6" : (object) string.Empty, (object) str, (object) ex));
                    }
                    this.HandleException(StatusCode.SendError);
                }
                return NetSocketError.Exception;
            }
            return NetSocketError.Success;
        }

        public override NetSocketError Receive(out byte[] data)
        {
            data = (byte[]) null;
            return NetSocketError.NoData;
        }

    internal void DnsAndConnect()
    {
      this.EnqueueDebugReturn(DebugLevel.INFO, "DnsAndConnect() Start.");
      IPAddress[] ipAddresses = this.GetIpAddresses(this.ServerAddress);
      if (ipAddresses == null)
        return;
      string str = string.Empty;
      foreach (IPAddress address in ipAddresses)
      {
        try
        {
          this.sock = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
          this.sock.NoDelay = true;
          this.sock.ReceiveTimeout = this.peerBase.DisconnectTimeout;
          this.sock.SendTimeout = this.peerBase.DisconnectTimeout;
          this.sock.Connect(address, this.ServerPort);
          if (this.sock != null && this.sock.Connected)
            break;
        }
        catch (SecurityException ex)
        {
          if (this.ReportDebugOfLevel(DebugLevel.ERROR))
          {
            str = str + ex?.ToString() + " ";
            this.EnqueueDebugReturn(DebugLevel.WARNING, "SecurityException catched: " + ex?.ToString());
          }
        }
        catch (SocketException ex)
        {
          if (this.ReportDebugOfLevel(DebugLevel.WARNING))
          {
            str = str + ex?.ToString() + " " + ex.ErrorCode.ToString() + "; ";
            this.EnqueueDebugReturn(DebugLevel.WARNING, "SocketException catched: " + ex?.ToString() + " ErrorCode: " + ex.ErrorCode.ToString());
          }
        }
        catch (Exception ex)
        {
          if (this.ReportDebugOfLevel(DebugLevel.WARNING))
          {
            str = str + ex?.ToString() + "; ";
            this.EnqueueDebugReturn(DebugLevel.WARNING, "Exception catched: " + ex?.ToString());
          }
        }
      }
      this.EnqueueDebugReturn(DebugLevel.INFO, "sock state: " + this.sock.Connected);
      if (this.sock == null || !this.sock.Connected)
      {
        if (this.ReportDebugOfLevel(DebugLevel.ERROR))
          this.EnqueueDebugReturn(DebugLevel.ERROR, "Failed to connect to server after testing each known IP. Error(s): " + str);
        this.HandleException(StatusCode.ExceptionOnConnect);
      }
      else
      {
        this.AddressResolvedAsIpv6 = this.sock.AddressFamily == AddressFamily.InterNetworkV6;
        INetSocket.ServerIpAddress = this.sock.RemoteEndPoint.ToString();
        this.State = NetSocketState.Connected;
        this.peerBase.OnConnect();
        this.ReceiveAsync();
      }
    }

    private void ReceiveAsync(SocketTcpAsync.ReceiveContext context = null)
    {
        if(context == null)
        context = new SocketTcpAsync.ReceiveContext(this.sock, new byte[9], new byte[this.MTU]);
        try
        {
            this.sock.BeginReceive(context.CurrentBuffer, context.CurrentOffset, context.CurrrentExpected - context.CurrentOffset, SocketFlags.None, new AsyncCallback(this.ReceiveAsync),(object)context);
        }
        catch(Exception ex)
        {
            if(this.State == NetSocketState.Disconnecting || (uint) this.State <= 0U)
              return;
            if(this.ReportDebugOfLevel(DebugLevel.ERROR))
              this.EnqueueDebugReturn(DebugLevel.ERROR, "SocketTcpAsync.ReceiveAsync Exception. State: " + this.State.ToString() + ". Server: '" + this.ServerAddress + "' Exception: " + ex?.ToString());
            this.HandleException(StatusCode.ExceptionOnReceive);
        }
    }

    private void ReceiveAsync(IAsyncResult ar)
    {
        if(this.State == NetSocketState.Disconnecting || this.State == NetSocketState.Disconnected)
        return;

        int num1 = 0;
        try
        {
            num1 = this.sock.EndReceive(ar);
            //this.EnqueueDebugReturn(DebugLevel.ALL, "EndReceive : " + num1);
            if(num1 == 0)
            throw new SocketException(10054);
        }
        catch(SocketException ex)
        {
            if (this.State != NetSocketState.Disconnecting && (uint) this.State > 0U)
            {
                if(this.ReportDebugOfLevel(DebugLevel.ERROR))
                  this.EnqueueDebugReturn(DebugLevel.ERROR, "SocketTcpAsync.EndReceive SocketException. State: " + this.State.ToString() + ". Server: '" + this.ServerAddress + "' ErrorCode: " + ex.ErrorCode.ToString() + " SocketErrorCode: " + ex.SocketErrorCode.ToString() + " Message: " + ex.Message + " " + ex?.ToString());
              this.HandleException(StatusCode.ExceptionOnReceive);
              return;
            }
        }
        catch(Exception ex)
        {
            if(this.State != NetSocketState.Disconnecting && (uint) this.State > 0U)
            {
                if(this.ReportDebugOfLevel(DebugLevel.ERROR))
                  this.EnqueueDebugReturn(DebugLevel.ERROR, "SocketTcpAsync.EndReceive Exception. State: " + this.State.ToString() + ". Server: '" + this.ServerAddress + "' Exception: " + ex?.ToString());
                this.HandleException(StatusCode.ExceptionOnReceive);
                return;
            }
        }
        SocketTcpAsync.ReceiveContext asyncState = (SocketTcpAsync.ReceiveContext) ar.AsyncState;
        if(num1 + asyncState.CurrentOffset != asyncState.CurrrentExpected)
        {
            if(asyncState.ReadingHeader)
              asyncState.ReceivedHeaderBytes += num1;
            else
              asyncState.ReceivedMessageBytes += num1;
            this.ReceiveAsync(asyncState);
        }
        else if (asyncState.ReadingHeader)
        {
            byte[] headerBuffer = asyncState.HeaderBuffer;
            if(headerBuffer[0] == (byte) 240)
            {
                this.HandleReceivedDatagram(headerBuffer, headerBuffer.Length, true);
                asyncState.Reset();
                this.ReceiveAsync(asyncState);
            }
            else
            {
                int num2 = (int) headerBuffer[1] << 24 | (int) headerBuffer[2] << 16 | (int)headerBuffer[3] << 8 | (int)headerBuffer[4];
                asyncState.ExpectedMessageBytes = num2 - 7;
                if(asyncState.ExpectedMessageBytes > asyncState.MessageBuffer.Length)
                  asyncState.MessageBuffer = new byte[asyncState.ExpectedMessageBytes];
                asyncState.MessageBuffer[0] = headerBuffer[7];
                asyncState.MessageBuffer[1] = headerBuffer[8];
                asyncState.ReceivedMessageBytes = 2;
                this.ReceiveAsync(asyncState);
            }
        }
        else
        {
            this.HandleReceivedDatagram(asyncState.MessageBuffer, asyncState.ExpectedMessageBytes, true);
            asyncState.Reset();
            this.ReceiveAsync(asyncState);
        }

    }

    private class ReceiveContext
    {
        public Socket workSocket;
        public int ReceivedHeaderBytes;
        public byte[] HeaderBuffer;
        public int ExpectedMessageBytes;
        public int ReceivedMessageBytes;
        public byte[] MessageBuffer;
        public ReceiveContext(Socket socket, byte[] headerBuffer, byte[] messageBuffer)
        {
            this.HeaderBuffer = headerBuffer;
            this.MessageBuffer = messageBuffer;
            this.workSocket = socket;
        }

        public bool ReadingHeader => this.ExpectedMessageBytes == 0;
        public bool ReadingMessage => (uint)this.ExpectedMessageBytes > 0U;

        public byte[] CurrentBuffer => this.ReadingHeader ? this.HeaderBuffer : this.MessageBuffer;
        public int CurrentOffset => this.ReadingHeader ? this.ReceivedHeaderBytes : this.ReceivedMessageBytes;
        public int CurrrentExpected => this.ReadingHeader ? 9 : this.ExpectedMessageBytes;

        public void Reset()
        {
            this.ReceivedHeaderBytes = 0;
            this.ExpectedMessageBytes = 0;
            this.ReceivedMessageBytes = 0;
        }
    }

    }
}
