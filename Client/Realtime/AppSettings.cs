using System.Collections;
using System.Collections.Generic;
using System;

namespace NDG.Realtime
{
    [Serializable]
    public class AppSettings
    {
        public string AppIdRealtime;

        public string AppVersion;

        public bool UseNameServer = true;

        private string FixedRegion;

        private string BestRegionSummaryFromStorage;

        /// <summary>
        /// 서버 주소
        /// </summary>
        public string Server;

        public int Port;

        public string ProxyServer;

        public ConnectionProtocol Protocol = ConnectionProtocol.Tcp;

        public bool EnableProtocolFallback = true;

        public AuthModeOption AuthMode = AuthModeOption.Auth;

        public DebugLevel NetworkLogging = DebugLevel.ERROR;

        public bool EnableLobbyStatistics;

        public bool IsMasterServerAddress { get { return !this.UseNameServer; } }

        public bool IsBestRegion { get { return this.UseNameServer && string.IsNullOrEmpty(this.FixedRegion); } }

        public bool IsDefaultNameServer { get { return this.UseNameServer && string.IsNullOrEmpty(this.Server); } }

        public bool IsDefaultPort { get { return this.Port <= 0; } }
    }
}
