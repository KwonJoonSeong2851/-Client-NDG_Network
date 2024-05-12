using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG.Realtime
{
    public class Region
    {
        public string Code { get; private set; }

        public string Cluster { get; private set; }

        public string HostAndPort { get; protected internal set; }

        public int Ping { get; set; }

        public bool WasPinged { get { return this.Ping != int.MaxValue; } }

        public Region(string code, string address)
        {
            this.SetCodeAndCluster(code);
            this.HostAndPort = address;
            this.Ping = int.MaxValue;
        }

        public Region(string code, int ping)
        {
            this.SetCodeAndCluster(code);
            this.Ping = ping;
        }

        private void SetCodeAndCluster(string codeAsString)
        {
            if (codeAsString == null)
            {
                this.Code = "";
                this.Cluster = "";
                return;
            }

            codeAsString = codeAsString.ToLower();
            int slash = codeAsString.IndexOf('/');
            this.Code = slash <= 0 ? codeAsString : codeAsString.Substring(0, slash);
            this.Cluster = slash <= 0 ? "" : codeAsString.Substring(slash + 1, codeAsString.Length - slash - 1);
        }

        public override string ToString()
        {
            return this.ToString(false);
        }

        public string ToString(bool compact = false)
        {
            string regionCluster = this.Code;
            if (!string.IsNullOrEmpty(this.Cluster))
            {
                regionCluster += "/" + this.Cluster;
            }

            if (compact)
            {
                return string.Format("{0}:{1}", regionCluster, this.Ping);
            }
            else
            {
                return string.Format("{0}[{2}]: {1}ms ", regionCluster, this.Ping, this.HostAndPort);
            }
        }
    }
}
