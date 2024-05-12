

namespace NDG.Realtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Text;
    using System.Threading;


    using UnityEngine;


    /// <summary>
    /// 최적의 Ping을 가진 영역을 찾는데 사용됩니다.
    /// </summary>
    public class RegionHandler
    {
        public static Type PingImplementation;

        /// <summary>활성화 되어있는 region 리스트를 받아옵니다.   /// </summary>
        public List<Region> EnabledRegions { get; protected internal set; }

        private string availableRegionCodes;

        private Region bestRegionCache;

        /// <summary>
        /// PingMinimumOfRegions가 호출되고 완료되면 BestRegion이 설정됩니다.
        /// </summary>
        public Region BestRegion
        {
            get
            {
                if (this.EnabledRegions == null)
                {
                    return null;
                }
                if (this.bestRegionCache != null)
                {
                    return this.bestRegionCache;
                }

                this.EnabledRegions.Sort((a, b) => a.Ping.CompareTo(b.Ping));

                this.bestRegionCache = this.EnabledRegions[0];
                return this.bestRegionCache;
            }
        }

        /// <summary>
        /// 현재 사용가능한 Region에 대한 Ping 결과를 요약합니다.
        /// </summary>
        public string SummaryToCache
        {
            get
            {
                if (this.BestRegion != null)
                {
                    return this.BestRegion.Code + ";" + this.BestRegion.Ping + ";" + this.availableRegionCodes;
                }

                return this.availableRegionCodes;
            }
        }

        public string GetResults()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Region Pinging Result: {0}\n", this.BestRegion.ToString());
            if (this.pingerList != null)
            {
                foreach (RegionPinger region in this.pingerList)
                {
                    sb.AppendFormat(region.GetResults() + "\n");
                }
            }

            sb.AppendFormat("Previous summary: {0}", this.previousSummaryProvided);
            return sb.ToString();
        }

        public void SetRegions(OperationResponse opGetRegions)
        {
            if (opGetRegions.OperationCode != OperationCode.GetRegions)
            {
                return;
            }
            if (opGetRegions.ReturnCode != ErrorCode.Ok)
            {
                return;
            }

            string[] regions = opGetRegions[ParameterCode.Region] as string[];
            string[] servers = opGetRegions[ParameterCode.Address] as string[];
            if (regions == null || servers == null || regions.Length != servers.Length)
            {
                return;
            }

            this.bestRegionCache = null;
            this.EnabledRegions = new List<Region>(regions.Length);

            for (int i = 0; i < regions.Length; i++)
            {
                Region tmp = new Region(regions[i], servers[i]);
                if (string.IsNullOrEmpty(tmp.Code))
                {
                    continue;
                }

                this.EnabledRegions.Add(tmp);
            }

            Array.Sort(regions);
            this.availableRegionCodes = string.Join(",", regions);
        }

        private List<RegionPinger> pingerList;
        private Action<RegionHandler> onCompleteCall;
        private int previousPing;
        public bool IsPinging { get; private set; }
        private string previousSummaryProvided;

        public bool PingMinimumOfRegions(Action<RegionHandler> onCompleteCallback, string previousSummary)
        {
            if (this.EnabledRegions == null || this.EnabledRegions.Count == 0)
            {
                UnityEngine.Debug.LogError("사용가능한 Region이 없습니다. ");
                return false;
            }

            if (this.IsPinging)
            {
                UnityEngine.Debug.LogWarning("PingMinimumOfRegions()가 스킵되었습니다. 이미 다른 Region을 ping하고있습니다. ");
                return false;
            }

            this.IsPinging = true;
            this.onCompleteCall = onCompleteCallback;
            this.previousSummaryProvided = previousSummary;

            if (string.IsNullOrEmpty(previousSummary))
            {
                return this.PingEnabledRegions();
            }

            string[] values = previousSummary.Split(';');
            if (values.Length < 3)
            {
                return this.PingEnabledRegions();
            }

            int prevBestRegionPing;
            bool secondValueIsInt = Int32.TryParse(values[1], out prevBestRegionPing);
            if (!secondValueIsInt)
            {
                return this.PingEnabledRegions();
            }

            string prevBestRegionCode = values[0];
            string prevAvailableRegionCodes = values[2];


            if (string.IsNullOrEmpty(prevBestRegionCode))
            {
                return this.PingEnabledRegions();
            }
            if (string.IsNullOrEmpty(prevAvailableRegionCodes))
            {
                return this.PingEnabledRegions();
            }
            if (!this.availableRegionCodes.Equals(prevAvailableRegionCodes) || !this.availableRegionCodes.Contains(prevBestRegionCode))
            {
                return this.PingEnabledRegions();
            }
            if (prevBestRegionPing >= RegionPinger.PingWhenFailed)
            {
                return this.PingEnabledRegions();
            }

            this.previousPing = prevBestRegionPing;

            Region preferred = this.EnabledRegions.Find(r => r.Code.Equals(prevBestRegionCode));
            RegionPinger singlePinger = new RegionPinger(preferred, this.OnPreferredRegionPinged);
            singlePinger.Start();
            return true;
        }


        private void OnPreferredRegionPinged(Region preferredRegion)
        {
            if (preferredRegion.Ping > this.previousPing * 1.50f)
            {
                this.PingEnabledRegions();
            }
            else
            {
                this.IsPinging = false;
                this.onCompleteCall(this);
            }
        }

        private bool PingEnabledRegions()
        {
            if (this.EnabledRegions == null || this.EnabledRegions.Count == 0)
            {
                UnityEngine.Debug.LogError("사용가능한 Region이 없습니다. ");
                return false;
            }

            if (this.pingerList == null)
            {
                this.pingerList = new List<RegionPinger>();
            }
            else
            {
                lock (this.pingerList)
                {
                    this.pingerList.Clear();
                }
            }

            lock (this.pingerList)
            {
                foreach (Region region in this.EnabledRegions)
                {
                    RegionPinger rp = new RegionPinger(region, this.OnRegionDone);
                    this.pingerList.Add(rp);
                    rp.Start();
                }
            }

            return true;
        }

        private void OnRegionDone(Region region)
        {
            lock (this.pingerList)
            {
                if (this.IsPinging == false)
                {
                    return;
                }

                this.bestRegionCache = null;
                foreach (RegionPinger pinger in this.pingerList)
                {
                    if (!pinger.Done)
                    {
                        return;
                    }
                }

                this.IsPinging = false;
            }

            this.onCompleteCall(this);
        }

    }





    public class RegionPinger
    {
        public static int Attempts = 5;
        public static bool IgnoreInitialAttempt = true;
        public static int MaxMilliseconsPerPing = 800;
        public static int PingWhenFailed = Attempts * MaxMilliseconsPerPing;

        private Region region;
        private string regionAddress;
        public int CurrentAttempt = 0;

        public bool Done { get; private set; }
        private Action<Region> onDoneCall;

        private NetworkPing ping;

        private List<int> rttResults;

        public RegionPinger(Region region, Action<Region> onDoneCallback)
        {
            this.region = region;
            this.region.Ping = PingWhenFailed;
            this.Done = false;
            this.onDoneCall = onDoneCallback;
        }

        /// <summary>
        /// 가정 적합한 ping을 선택하거나 RegionHandler에 설정된 ping을 사용합니다.
        /// </summary>
        private NetworkPing GetPingImplementation()
        {
            NetworkPing ping = null;

            if (RegionHandler.PingImplementation == null || RegionHandler.PingImplementation == typeof(PingMono))
            {
                ping = new PingMono();
            }

            if (ping == null)
            {
                if (RegionHandler.PingImplementation != null)
                {
                    ping = (NetworkPing)Activator.CreateInstance(RegionHandler.PingImplementation);
                }
            }

            return ping;
        }

        public bool Start()
        {
            string address = this.region.HostAndPort;
            int indexOfColon = address.LastIndexOf(':');
            if (indexOfColon > 1)
            {
                address = address.Substring(0, indexOfColon);
            }
            this.regionAddress = ResolveHost(address);


            this.ping = this.GetPingImplementation();


            this.Done = false;
            this.CurrentAttempt = 0;
            this.rttResults = new List<int>(Attempts);

            bool queued = false;

            try
            {
                queued = ThreadPool.QueueUserWorkItem(this.RegionPingPooled);
            }
            catch
            {
                queued = false;
            }

            if (!queued)
            {
                SupportClass.StartBackgroundCalls(this.RegionPingThreaded, 0, "RegionPing_" + this.region.Code + "_" + this.region.Cluster);
            }

            return true;
        }

        protected internal void RegionPingPooled(object context)
        {
            this.RegionPingThreaded();
        }

        protected internal bool RegionPingThreaded()
        {
            this.region.Ping = PingWhenFailed;

            float rttSum = 0.0f;
            int replyCount = 0;


            Stopwatch sw = new Stopwatch();
            for (this.CurrentAttempt = 0; this.CurrentAttempt < Attempts; this.CurrentAttempt++)
            {
                bool overtime = false;
                sw.Reset();
                sw.Start();

                try
                {
                    this.ping.StartPing(this.regionAddress);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("RegionPinger.RegionPingThreaded()  Exception: " + e + " Source: " + e.Source + " Message: " + e.Message);
                    break;
                }


                while (!this.ping.Done())
                {
                    if (sw.ElapsedMilliseconds >= MaxMilliseconsPerPing)
                    {
                        overtime = true;
                        break;
                    }
#if !NETFX_CORE
                    System.Threading.Thread.Sleep(0);
#endif
                }


                sw.Stop();
                int rtt = (int)sw.ElapsedMilliseconds;
                this.rttResults.Add(rtt);

                if (IgnoreInitialAttempt && this.CurrentAttempt == 0)
                {
                    // do nothing.
                }
                else if (this.ping.Successful && !overtime)
                {
                    rttSum += rtt;
                    replyCount++;
                    this.region.Ping = (int)((rttSum) / replyCount);
                }

#if !NETFX_CORE
                System.Threading.Thread.Sleep(10);
#endif
            }

            UnityEngine.Debug.Log("Done: "+ this.region.Code);
            this.Done = true;
            this.ping.Dispose();

            this.onDoneCall(this.region);

            return false;
        }




        protected internal IEnumerator RegionPingCoroutine()
        {
            this.region.Ping = PingWhenFailed;

            float rttSum = 0.0f;
            int replyCount = 0;


            Stopwatch sw = new Stopwatch();
            for (this.CurrentAttempt = 0; this.CurrentAttempt < Attempts; this.CurrentAttempt++)
            {
                bool overtime = false;
                sw.Reset();
                sw.Start();

                try
                {
                    this.ping.StartPing(this.regionAddress);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log("catched: " + e);
                    break;
                }


                while (!this.ping.Done())
                {
                    if (sw.ElapsedMilliseconds >= MaxMilliseconsPerPing)
                    {
                        overtime = true;
                        break;
                    }
                    yield return 0; // keep this loop tight, to avoid adding local lag to rtt.
                }


                sw.Stop();
                int rtt = (int)sw.ElapsedMilliseconds;
                this.rttResults.Add(rtt);


                if (IgnoreInitialAttempt && this.CurrentAttempt == 0)
                {
                    // do nothing.
                }
                else if (this.ping.Successful && !overtime)
                {
                    rttSum += rtt;
                    replyCount++;
                    this.region.Ping = (int)((rttSum) / replyCount);
                }

                yield return new WaitForSeconds(0.1f);
            }


            //Debug.Log("Done: "+ this.region.Code);
            this.Done = true;
            this.ping.Dispose();
            this.onDoneCall(this.region);
            yield return null;
        }

        public string GetResults()
        {
            return string.Format("{0}: {1} ({2})", this.region.Code, this.region.Ping, this.rttResults.ToString());
        }


        /// <summary>
        /// hostName에서 IP 문자열로 변환을 실패한다면 빈 문자열을 반환합니다.
        /// </summary>
        public static string ResolveHost(string hostName)
        {

            if (hostName.StartsWith("wss://"))
            {
                hostName = hostName.Substring(6);
            }
            if (hostName.StartsWith("ws://"))
            {
                hostName = hostName.Substring(5);
            }

            string ipv4Address = string.Empty;

            try
            {
#if UNITY_WSA || NETFX_CORE || UNITY_WEBGL
                return hostName;
#else

                IPAddress[] address = Dns.GetHostAddresses(hostName);
                if (address.Length == 1)
                {
                    return address[0].ToString();
                }

                for (int index = 0; index < address.Length; index++)
                {
                    IPAddress ipAddress = address[index];
                    if (ipAddress != null)
                    {
                        if (ipAddress.ToString().Contains(":"))
                        {
                            return ipAddress.ToString();
                        }
                        if (string.IsNullOrEmpty(ipv4Address))
                        {
                            ipv4Address = address.ToString();
                        }
                    }
                }
#endif
            }
            catch (System.Exception e)
            {
                System.Diagnostics.Debug.WriteLine("RegionPinger.ResolveHost() catched an exception for Dns.GetHostAddresses(). Exception: " + e + " Source: " + e.Source + " Message: " + e.Message);
            }

            return ipv4Address;
        }

    }

}
