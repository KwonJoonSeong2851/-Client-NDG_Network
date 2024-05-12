


namespace NDG.UnityNet
{

    using System.Collections;
    using System.Collections.Generic;

    using UnityEngine;

    using NDG.Realtime;

#if UNITY_5_5_OR_NEWER
    using UnityEngine.Profiling;
    using System.IO;
    using System;

    //using System.Diagnostics;
#endif

    public class NetworkHandler : ConnectionHandler,IInRoomCallbacks,IMatchmakingCallbacks
    {
        private static NetworkHandler instance;

        internal static NetworkHandler Instance
        {
            get
            {
                if(instance == null)
                {
                    instance = FindObjectOfType<NetworkHandler>();
                    if(instance == null)
                    {
                        GameObject obj = new GameObject();
                        obj.name = "NetworkHandler";
                        instance = obj.AddComponent<NetworkHandler>();
                    }
                }
                return instance;
            }
        }

        /// <summary>각  LateUpdate에서 생성되는 데이터그램 수를 제한합니다. </summary>
        public static int MaxDatagrams = 10;

        /// <summary>다음 LateUpdate에서 Message들을 보내야하는지 나타냅니다.</summary>
        public static bool SendAsap;

        /// <summary>LateUpdate는 일반적으로 15ms마다 호출되므로 15ms보다 낮게 설정하는것이 좋습니다. </summary>
        private const int SerializeRateFrameCorrection = 5;

        /// <summary>SendOutgoingCommands의 호출 간격 ms 단위</summary>
        protected internal int UpdateInterval;

        /// <summary>RunViewUpdate 호출 간격 ms 단위</summary>
        protected internal int UpdateIntervalOnSerialize;

        protected internal System.Diagnostics.Stopwatch swSendOutgoing = new System.Diagnostics.Stopwatch();

        protected internal System.Diagnostics.Stopwatch swViewUpdate = new System.Diagnostics.Stopwatch();


        protected override void Awake()
        {
            this.swSendOutgoing.Start();
            this.swViewUpdate.Start();

            if(instance == null || ReferenceEquals(this,instance))
            {
                instance = this;
                base.Awake();
            }
            else
            {
                Destroy(this);
            }
        }

        protected virtual void OnEnable()
        {
            if (Instance != this)
            {
                Debug.LogError("NetworkHandler가 중복됩니다.");
                return;
            }

            this.Client = NDG_Network.NetworkingClient;

            this.UpdateInterval = 1000 / NDG_Network.SendRate;
            this.UpdateIntervalOnSerialize = 1000 / NDG_Network.SerializationRate;

            NDG_Network.AddCallbackTarget(this);
            this.StartFallbackSendAckThread();  
        }

        protected void Start()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, loadingMode) =>
            {
                NDG_Network.NewSceneLoaded();
            };
        }

        protected override void OnDisable()
        {
            NDG_Network.RemoveCallbackTarget(this);
            base.OnDisable();
            Debug.Log("NetworkHandler OnDisable . ");
        }

        protected void FixedUpdate()
        {
            if(Time.timeScale > NDG_Network.MinimalTimeScaleToDispatchInFixedUpdate)
            {
                this.Dispatch();
            }
        
        }

        protected void LateUpdate()
        {
            if(NDG_Network.IsMessageQueueRunning && this.swViewUpdate.ElapsedMilliseconds >= this.UpdateIntervalOnSerialize - SerializeRateFrameCorrection)
            {
                NDG_Network.RunViewUpdate();
                this.swViewUpdate.Restart();
                SendAsap = true;
            }

            if(SendAsap || this.swSendOutgoing.ElapsedMilliseconds >= this.UpdateInterval)
            {
                SendAsap = false;
                bool doSend = true;
                int sendCounter = 0;
                while(NDG_Network.IsMessageQueueRunning && doSend && sendCounter < MaxDatagrams)
                {
                    Profiler.BeginSample("SendOutoingComands");
                    doSend = NDG_Network.NetworkingClient.LoadBalancingPeer.SendOutgoingCommands();
                    sendCounter++;
                    Profiler.EndSample();
                }
                if(sendCounter >= MaxDatagrams)
                {
                    SendAsap = true;
                }

                this.swSendOutgoing.Restart();
            }
        }

        /// <summary>
        /// 수신한 network message들을 처리합니다.
        /// </summary>
        protected void Dispatch()
        {
            if (NDG_Network.NetworkingClient == null)
            {
                Debug.LogError("NetworkClient가 null상태입니다.");
                return;
            }

            bool doDispatch = true;
            Exception ex = null;
            int exceptionCount = 0;

            while (NDG_Network.IsMessageQueueRunning && doDispatch)
            {
                // DispatchIncomingCommands()은 어떤 명령어든 찾았으면 true를 반환합니다.
                Profiler.BeginSample("DispatchIncomingCommands");
                try
                {
                doDispatch = NDG_Network.NetworkingClient.LoadBalancingPeer.DispatchIncomingCommands();
                }
                catch (Exception e)
                {
                    exceptionCount++;
                    if(ex == null)
                    {
                        ex = e;
                    }
                }
                Profiler.EndSample();
            }
            if(ex != null)
            {
                throw new AggregateException("Caught " + exceptionCount + " exceptions in methods caled by DispatchIncomingComamands(). Rethrowing first only (see above).", ex);
            }
        }

        public void OnCreatedRoom()
        {
            NDG_Network.SetLevelInPropsIfSynced(SceneManagerHelper.ActiveSceneName);
        }

        public void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            Debug.Log("NetworkHandler OnRoomPropertiesChanged");
            NDG_Network.LoadLevelIfSynced();
        }


        public void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps) { }

        public void OnMasterClientSwitched(Player newMasterClient)
        {
            var views = NDG_Network.NetworkViewCollection;
            foreach (var view in views)
            {
                view.RebuildControllerCache();
            }
        }

        public void OnCreateRoomFailed(short returnCode, string message) { }

        public void OnJoinRoomFailed(short returnCode, string message) { }

        public void OnJoinRandomFailed(short returnCode, string message) { }

        protected List<int> reusableIntList = new List<int>();

        public void OnJoinedRoom()
        {

            if (NDG_Network.ViewCount == 0)
                return;

            var views = NDG_Network.NetworkViewCollection;

            bool amMasterClient = NDG_Network.IsMasterClient;
            bool amRejoiningMaster = amMasterClient && NDG_Network.CurrentRoom.PlayerCount > 1;

            if (amRejoiningMaster)
                reusableIntList.Clear();

            foreach (var view in views)
            {
                int viewOwnerId = view.OwnerActorNumber;
                int viewCreatorId = view.CreatorActorNumber;

                // Scene objects는 컨트롤러를 마스터 클라이언트로 설정해야합니다
                if (NDG_Network.IsMasterClient)
                    view.RebuildControllerCache();

                if (amRejoiningMaster)
                    if (viewOwnerId != viewCreatorId)
                    {
                        reusableIntList.Add(view.ViewID);
                        reusableIntList.Add(viewOwnerId);
                    }
            }

            if (amRejoiningMaster && reusableIntList.Count > 0)
            {
                NDG_Network.OwnershipUpdate(reusableIntList.ToArray());
            }
        }

        public void OnLeftRoom()
        {
            // 생성된 오브젝트와 Scene 오브젝트를 초기화 시킵니다.
            NDG_Network.LocalCleanupAnythingInstantiated(true);
        }


        public void OnPlayerEnteredRoom(Player newPlayer)
        {

            bool isRejoiningMaster = newPlayer.IsMasterClient;
            bool amMasterClient = NDG_Network.IsMasterClient;

            // 마스터 클라이언트가 아니면 생략합니다.
            if (!isRejoiningMaster && !amMasterClient)
                return;

            var views = NDG_Network.NetworkViewCollection;


            if (amMasterClient)
                reusableIntList.Clear();

            foreach (var view in views)
            {
                view.RebuildControllerCache();

                ///이 사용자가 마스터 클라이언트이고 다른 플레이어가 참여한 경우 소유권을 플레이어에게 알립니다.
                if (amMasterClient)
                {
                    int viewOwnerId = view.OwnerActorNumber;
                    // TODO: Ideally all of this would only be targetted at the new player.
                    if (viewOwnerId != view.CreatorActorNumber)
                    {
                        reusableIntList.Add(view.ViewID);
                        reusableIntList.Add(viewOwnerId);
                    }
                }

                else if (isRejoiningMaster)
                {
                    view.ResetOwnership();
                }
            }

            if (amMasterClient)
            {
                NDG_Network.OwnershipUpdate(reusableIntList.ToArray(), newPlayer.ActorNumber);
            }

        }

        public void OnPlayerLeftRoom(Player otherPlayer)
        {
            var views = NDG_Network.NetworkViewCollection;

            int leavingPlayerId = otherPlayer.ActorNumber;
            bool isInactive = otherPlayer.IsInactive;

            //SOFT DISCONNECT
            //플레이어가 릴레이 시간을 초과했지만 아직 PlayerTTL을 초과하지 않는다면 다시 연결할 수 있습니다.
            //마스터 클라이언트가 hard disconnects를 하거나 돌아올때까지 이 플레이어를 제어합니다.
            if (isInactive)
            {
                foreach (var view in views)
                {
                    if (view.OwnerActorNumber == leavingPlayerId)
                        view.RebuildControllerCache(true);
                }

            }
            // HARD DISCONNECT: 플레이어를 영구 삭제합니다. 해당 플레이어가 생성한 모든 항목을 정리합니다.
            else
            {
                bool autocleanup = NDG_Network.CurrentRoom.AutoCleanUp;

                foreach (var view in views)
                {
                    if (autocleanup && view.CreatorActorNumber == leavingPlayerId)
                        continue;

                    // 떠나는 플레이어가 소요했던 모든 View들의 소유권은 null로 설정되며 마스터 클라이언트가 제어합니다.
                    if (view.OwnerActorNumber == leavingPlayerId || view.ControllerActorNumber == leavingPlayerId)
                    {
                        view.SetOwnerInternal(null, 0);
                    }
                }
            }

        }
    }
}
