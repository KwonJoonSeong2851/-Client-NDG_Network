


namespace NDG.Realtime
{
    using System;
    using System.Diagnostics;
    using UnityEngine;

    public class ConnectionHandler : MonoBehaviour
    {
        public LoadBalancingClient Client { get; set; }

        /// <summary>
        /// fallback thread가 KeepAliveInBackground 시간 후에 Disconnect를 호출하도록 하는 옵션입니다.
        /// </summary>
        public bool DisconnectAfterKeepAlive = false;

        private byte fallbackThreadId = 255;

        private bool didSendAcks;

        private readonly Stopwatch backgroundStopwatch = new Stopwatch();
        //private int startedAckingTimestamp;
        //private int deltaSinceStartedToAck;

        public int KeepAliveInBackground = 60000;

        public int CountSendAcksOnly { get; private set; }

        public bool FallbackThreadRunning
        {
            get { return this.fallbackThreadId < 255; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void StaticReset()
        {
            AppQuits = false;
        }

        public static bool AppQuits;

        public bool ApplyDontDestryOnLoad = true;

        protected void OnApplicationQuit()
        {
            AppQuits = true;
        }

        protected virtual void Awake()
        {
            if(this.ApplyDontDestryOnLoad)
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }

        protected virtual void OnDisable()
        {
            this.StopFallbackSendAckThread();

            if(AppQuits)
            {
                if(this.Client != null && this.Client.IsConnected)
                {
                    this.Client.Disconnect();
                    this.Client.LoadBalancingPeer.StopThread();
                }

                SupportClass.StopAllBackgroundCalls();
            }
        }

        public void StartFallbackSendAckThread()
        {

            this.fallbackThreadId = SupportClass.StartBackgroundCalls(this.RealtimeFallbackThread, 50, "RealtimeFallbackThread");
        }

        public void StopFallbackSendAckThread()
        {
            if(!this.FallbackThreadRunning)
            {
                return;
            }
            SupportClass.StopBackgroundCalls(this.fallbackThreadId);
            this.fallbackThreadId = 255;
        }

        /// <summary>
        /// Update() 호출과 독립적으로 실행되는 스레드입니다.
        /// Load중이거나 BackGround상황에서 연결을 온라인 상태로 유지합니다.
        /// </summary>
        public bool RealtimeFallbackThread()
        {
            if (this.Client != null)
            {
                if (!this.Client.IsConnected)
                {
                    this.didSendAcks = false;
                    return true;
                }

                if (this.Client.LoadBalancingPeer.ConnectionTime - this.Client.LoadBalancingPeer.LastSendOutgoingTime > 100)
                {
                    if (this.didSendAcks)
                    {
                        backgroundStopwatch.Reset();
                        backgroundStopwatch.Start();
                        // this.deltaSinceStartedToAck = Environment.TickCount - this.startedAckingTimestamp;
                        // if (this.deltaSinceStartedToAck > this.KeepAliveInBackground)
                        // {
                        //     return true;
                        // }

                        if(backgroundStopwatch.ElapsedMilliseconds > this.KeepAliveInBackground)
                        {
                            if(this.DisconnectAfterKeepAlive)
                            {
                                this.Client.Disconnect();
                            }
                            return true;
                        }
                    }


                    this.didSendAcks = true;
                    this.CountSendAcksOnly++;
                    this.Client.LoadBalancingPeer.SendAcksOnly();
                }
                else
                {
                    this.didSendAcks = false;
                }
            }

            return true;
        }


    }

}
