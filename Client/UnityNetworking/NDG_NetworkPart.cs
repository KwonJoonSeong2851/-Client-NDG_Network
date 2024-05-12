
namespace NDG.UnityNet
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEngine;
    using NDG.Realtime;
    using System.Reflection;

    public static partial class NDG_Network
    {

        private static HashSet<byte> allowedReceivingGroups = new HashSet<byte>();

        private static HashSet<byte> blockedSendingGroups = new HashSet<byte>();

        private static HashSet<NetworkView> reusableNVHashset = new HashSet<NetworkView>();

        //NetworkView List
        private static Dictionary<int, NetworkView> networkViewList = new Dictionary<int, NetworkView>();

        public static NetworkView[] NetworkViews
        {
            get
            {
                var views = new NetworkView[networkViewList.Count];
                int idx = 0;
                foreach (var v in networkViewList.Values)
                {
                    views[idx] = v;
                    idx++;
                }
                return views;
            }
        }

        /// <summary>
        /// 현재 NetworkView 이터레이터 반환
        /// </summary>
        public static Dictionary<int, NetworkView>.ValueCollection NetworkViewCollection
        {
            get { return networkViewList.Values; }
        }

        public static int ViewCount
        {
            get { return networkViewList.Count; }
        }

        //NetworkView 소유권이 변경되기전 소유자 
        private static event Action<NetworkView, Player> OnOwnershipRequestEv;
        //NetworkView 소유권을 요청한 플레이어
        private static event Action<NetworkView, Player> OnOwnershipTransferedEv;

        /// <summary>
        ///  콜백 인터페이스가 구현된 오브젝트를 등록합니다.
        /// </summary>
        public static void AddCallbackTarget(object target)
        {
            if (target is NetworkView)
            {
                return;
            }

            INetOwnershipCallbacks netOwnershipCallbacks = target as INetOwnershipCallbacks;
            if (netOwnershipCallbacks != null)
            {
                OnOwnershipRequestEv += netOwnershipCallbacks.OnOwnershipRequest;
                OnOwnershipTransferedEv += netOwnershipCallbacks.OnOwnershipTransfered;
            }

            NetworkingClient.AddCallbackTarget(target);
        }

        /// <summary>
        /// 콜백 인터페이스를 등록 해제합니다.
        /// </summary>
        public static void RemoveCallbackTarget(object target)
        {
            if (target is NetworkView || NetworkingClient == null)
            {
                return;
            }

            INetOwnershipCallbacks netOwnershipCallback = target as INetOwnershipCallbacks;
            if (netOwnershipCallback != null)
            {
                OnOwnershipRequestEv -= netOwnershipCallback.OnOwnershipRequest;
                OnOwnershipTransferedEv -= netOwnershipCallback.OnOwnershipTransfered;
            }

            NetworkingClient.RemoveCallbackTarget(target);
        }


        internal static byte currentLevelPrefix = 0;

        //Scene 동기화 상황으로 인해 네트워크가 비활성화 되었는지 여부를 표시합니다.
        internal static bool loadingLevelAndPausedNetwork = false;

        //자동으로 Scene 동기화를 위해 로드된 Scene의 이름을 변경합니다.
        internal const string CurrentSceneProperty = "curScn";
        internal const string CurrentScenePropertyLoadAsync = "curScnLa";

        public static INetPrefabPool PrefabPool
        {
            get
            {
                return prefabPool;
            }
            set
            {
                if (value == null)
                {
                    Debug.LogWarning("NDG_Network.PrefabPool이 null상태입니다. ");
                    prefabPool = new DefaultPool();
                }
                else
                {
                    prefabPool = value;
                }
            }
        }

        private static INetPrefabPool prefabPool;


        //비동기식 네트워크 동기화 로드용
        private static AsyncOperation _AsyncLevelLoadingOperation;

        private static float _levelLoadingProgress = 0f;

        /// <summary>
        /// LoadLevel()을 사용할때 Load 진행율을 나타냅니다.
        /// </summary>
        public static float LevelLoadingProgress
        {
            get
            {
                if (_AsyncLevelLoadingOperation != null)
                {
                    _levelLoadingProgress = _AsyncLevelLoadingOperation.progress;
                }
                else if (_levelLoadingProgress > 0f)
                {
                    _levelLoadingProgress = 1f;
                }

                return _levelLoadingProgress;
            }
        }

        /// <summary>
        /// 클라이언트가 룸을 나갈때 호출됩니다.
        /// </summary>
        private static void LeftRoomCleanup()
        {
            // 비동기식 로딩을 정리합니다.
            if (_AsyncLevelLoadingOperation != null)
            {
                _AsyncLevelLoadingOperation.allowSceneActivation = false;
                _AsyncLevelLoadingOperation = null;
            }


            bool wasInRoom = NetworkingClient.CurrentRoom != null;

            bool autoCleanupSettingOfRoom = wasInRoom && CurrentRoom.AutoCleanUp;

            allowedReceivingGroups = new HashSet<byte>();
            blockedSendingGroups = new HashSet<byte>();

            // 모든 네트워크 개체를 정리합니다.
            if (autoCleanupSettingOfRoom || offlineModeRoom != null)
            {
                LocalCleanupAnythingInstantiated(true);
            }
        }

        /// <summary>
        /// 게임 안에서 인스턴스화된 모든 항목을 정리합니다.
        /// </summary>
        internal static void LocalCleanupAnythingInstantiated(bool destroyInstantiatedGameObjects)
        {

            if (destroyInstantiatedGameObjects)
            {
                // 생성된 인스턴스들을 리스트에 채웁니다.
                HashSet<GameObject> instantiatedGos = new HashSet<GameObject>();
                foreach (NetworkView view in networkViewList.Values)
                {
                    if (view.isRuntimeInstantiated)
                    {
                        instantiatedGos.Add(view.gameObject);
                    }

                    else
                        view.ResetNetworkView(true); //인스턴스화 되지않은 객체일 경우.
                }

                foreach (GameObject go in instantiatedGos)
                {
                    RemoveInstantiatedGO(go, true);
                }
            }

            NDG_Network.lastUsedViewSubId = 0;
            NDG_Network.lastUsedViewSubIdStatic = 0;
        }


        private static void ResetNetworkViewsOnSerialize()
        {
            foreach (NetworkView photonView in networkViewList.Values)
            {
                photonView.lastOnSerializeDataSent = null;
            }
        }


        /// <summary>
        /// 해당 플레이어가 생성한 오브젝트를 삭제합니다.
        /// </summary>
        public static void DestroyPlayerObjects(int playerId, bool localOnly)
        {
            if (playerId <= 0)
            {
                Debug.LogError("해당 플레이어의 오브젝트를 삭제하는데에 실패하였습니다. playerId: " + playerId);
                return;
            }

            if (!localOnly)
            {
                // 서버 Instatiate 및 RPC 버퍼 정리
                OpRemoveFromServerInstantiationsOfPlayer(playerId);
                OpCleanActorRpcBuffer(playerId);

                // 다른 플레이어에게 Destroy 정보를 전송합니다.
                SendDestroyOfPlayer(playerId);
            }

            //로컬에서 해당 플레이어의 개체를 정리합니다.
            HashSet<GameObject> playersGameObjects = new HashSet<GameObject>();


            //소유권이 이전되면 일부 객체는 소유권을 잃습니다.
            //그런 경우 생성자가 다시 소유권을 갖습니다.
            foreach (NetworkView view in networkViewList.Values)
            {
                if (view == null)
                {
                    Debug.LogError("Null view");
                    continue;
                }

                // view의 생성자가 해당 playerId와 일치할 경우 리스트에 추가합니다.
                if (view.CreatorActorNumber == playerId)
                {
                    playersGameObjects.Add(view.gameObject);
                    continue;
                }

                if (view.OwnerActorNumber == playerId)
                {
                    var previousOwner = view.Owner;

                    var newOwnerId = view.CreatorActorNumber;
                    var newOwner = CurrentRoom.GetPlayer(newOwnerId);

                    view.SetOwnerInternal(newOwner, newOwnerId);

                    if (NDG_Network.OnOwnershipTransferedEv != null)
                    {
                        NDG_Network.OnOwnershipTransferedEv(view, previousOwner);
                    }
                }
            }

            foreach (GameObject gameObject in playersGameObjects)
            {
                RemoveInstantiatedGO(gameObject, true);
            }
        }


        public static void DestroyAll(bool localOnly)
        {
            if (!localOnly)
            {
                OpRemoveCompleteCache();
                SendDestroyOfAll();
            }

            LocalCleanupAnythingInstantiated(true);
        }

        internal static List<NetworkView> foundNVs = new List<NetworkView>();

        /// <summary>
        /// 로컬 목록에서 GameObject와 NetworkView를 제거합니다.
        /// GameObject는 마지막에 제거됩니다.
        /// </summary>
        internal static void RemoveInstantiatedGO(GameObject go, bool localOnly)
        {

            if (ConnectionHandler.AppQuits)
                return;

            if (go == null)
            {
                Debug.LogError("RemoveInstantiatedGO 실패 GameObject가 Null 상태입니다.");
                return;
            }

            // NetworkView가 없는 경우 GameObject를 제거하지 않습니다.
            go.GetComponentsInChildren<NetworkView>(true, foundNVs);
            if (foundNVs.Count <= 0)
            {
                Debug.LogError("NetworkView가 없기때문에 제거할수 없습니다. GameObject: " + go);
                return;
            }

            NetworkView viewZero = foundNVs[0];

            // 다른 사용자에게 소유권이있는 GameObject는 제거하지 않습니다.
            if (!localOnly)
            {
                if (!viewZero.IsMine)
                {
                    Debug.LogError("RemoveInstantiatedGO 실패. 현재 소유자가 아닙니다.  " + viewZero);
                    return;
                }
            }

            // 인스턴스화 된것을 정리합니다. 
            if (!localOnly)
            {
                ServerCleanInstantiateAndDestroy(viewZero);
            }

            int creatorActorNr = viewZero.CreatorActorNumber;

            // NetworkView 및 RPC 정리 (로컬이 아닌경우에만)
            for (int j = foundNVs.Count - 1; j >= 0; j--)
            {
                NetworkView view = foundNVs[j];
                if (view == null)
                {
                    continue;
                }


                if (j != 0)
                {
                    // 객체를 생성한 부모가 다른 플레이어일 경우
                    if (view.CreatorActorNumber != creatorActorNr)
                    {
                        view.transform.SetParent(null, true);
                        continue;
                    }
                }

                // 모든 자식 객체에게 Destroy 콜백
                view.OnPreNetDestroy(viewZero);

                // NDG_Network.Instantiate로 생성된 객체만 제거합니다.
                if (view.InstantiationId >= 1)
                {
                    LocalCleanNetworkView(view);
                }
                if (!localOnly)
                {
                    OpCleanRpcBuffer(view);
                }
            }

            if (NDG_Network.LogLevel >= NetLogLevel.Full)
            {
                Debug.Log("Network destroy Instantiated GO: " + go.name);
            }

            go.SetActive(false);

            prefabPool.Destroy(go);
        }

        private static readonly Hashtable removeFilter = new Hashtable();
        private static readonly Hashtable ServerCleanDestroyEvent = new Hashtable();
        private static readonly RaiseEventOptions ServerCleanOptions = new RaiseEventOptions() { CachingOption = EventCaching.RemoveFromRoomCache };

        internal static RaiseEventOptions SendToAllOptions = new RaiseEventOptions() { Receivers = ReceiverGroup.All };
        internal static RaiseEventOptions SendToOthersOptions = new RaiseEventOptions() { Receivers = ReceiverGroup.Others };
        internal static RaiseEventOptions SendToSingleOptions = new RaiseEventOptions() { TargetActors = new int[1] };


        private static void ServerCleanInstantiateAndDestroy(NetworkView networkView)
        {
            int filterId;
            if (networkView.isRuntimeInstantiated)
            {
                filterId = networkView.InstantiationId;
                removeFilter[keyByteSeven] = filterId;
                ServerCleanOptions.CachingOption = EventCaching.RemoveFromRoomCache;
                NDG_Network.RaiseEventInternal(NetEvent.Instantiation, removeFilter, ServerCleanOptions, SendOptions.SendReliable);
            }
            //올바른 ID가 아닌경우 서버에서 인스턴스를 제거하지 않습니다.
            else
            {
                filterId = networkView.ViewID;
            }

            // 모두에게 DestroyEvent 전송
            ServerCleanDestroyEvent[keyByteZero] = filterId;
            ServerCleanOptions.CachingOption = networkView.isRuntimeInstantiated ? EventCaching.DoNotCache : EventCaching.AddToRoomCacheGlobal;

            NDG_Network.RaiseEventInternal(NetEvent.Destroy, ServerCleanDestroyEvent, ServerCleanOptions, SendOptions.SendReliable);
        }

        private static void SendDestroyOfPlayer(int actorNr)
        {
            Hashtable evData = new Hashtable();
            evData[(byte)0] = actorNr;

            NDG_Network.RaiseEventInternal(NetEvent.DestroyPlayer, evData, null, SendOptions.SendReliable);

        }

        private static void SendDestroyOfAll()
        {
            Hashtable evData = new Hashtable();
            evData[(byte)0] = -1;

            NDG_Network.RaiseEventInternal(NetEvent.DestroyPlayer, evData, null, SendOptions.SendReliable);
        }

        private static void OpRemoveFromServerInstantiationsOfPlayer(int actorNr)
        {
            RaiseEventOptions options = new RaiseEventOptions() { CachingOption = EventCaching.RemoveFromRoomCache, TargetActors = new int[] { actorNr } };
            NDG_Network.RaiseEventInternal(NetEvent.Instantiation, null, options, SendOptions.SendReliable);
        }

        internal static void RequestOwnership(int viewID, int fromOwner)
        {
            Debug.Log("RequestOwnership(): " + viewID + " from: " + fromOwner + " Time: " + Environment.TickCount % 1000);
            NDG_Network.RaiseEventInternal(NetEvent.OwnershipRequest, new int[] { viewID, fromOwner }, SendToAllOptions, SendOptions.SendReliable);
        }

        internal static void TransferOwnership(int viewID, int playerID)
        {
            Debug.Log("TransferOwnership() view " + viewID + " to: " + playerID + " Time: " + Environment.TickCount % 1000);
            NDG_Network.RaiseEventInternal(NetEvent.OwnershipTransfer, new int[] { viewID, playerID }, SendToAllOptions, SendOptions.SendReliable);
        }

        /// <summary>
        /// 소유권을 업데이트 합니다.
        /// </summary>
        internal static void OwnershipUpdate(int[] viewOwnerPairs, int targetActor = -1)
        {
            RaiseEventOptions opts;
            if (targetActor == -1)
            {
                opts = SendToOthersOptions;
            }
            else
            {
                SendToSingleOptions.TargetActors[0] = targetActor;
                opts = SendToSingleOptions;
            }
            NDG_Network.RaiseEventInternal(NetEvent.OwnershipUpdate, viewOwnerPairs, opts, SendOptions.SendReliable);
        }

        public static bool LocalCleanNetworkView(NetworkView view)
        {
            view.removedFromLocalViewList = true;
            return networkViewList.Remove(view.ViewID);
        }

        public static NetworkView GetNetworkView(int viewID)
        {
            NetworkView result = null;
            networkViewList.TryGetValue(viewID, out result);

            return result;
        }


        public static void RegisterNetworkView(NetworkView netView)
        {
            if (!Application.isPlaying)
            {
                networkViewList = new Dictionary<int, NetworkView>();
                return;
            }

            if (netView.ViewID == 0)
            {
                Debug.LogError("View Id가 0인 NetworkView는 등록할수없습니다.");
                return;
            }

            NetworkView listedView = null;
            bool isViewListed = networkViewList.TryGetValue(netView.ViewID, out listedView);
            if (isViewListed)
            {
                if (netView != listedView)
                {
                    Debug.LogError(string.Format("NetworkView ID가 중복됩니다. 중복된 NetworkView ID:{0} ", netView.ViewID));
                }
                else
                {
                    return;
                }

                RemoveInstantiatedGO(listedView.gameObject, true);
            }

            networkViewList.Add(netView.ViewID, netView);

            if (NDG_Network.LogLevel >= NetLogLevel.Full)
            {
                Debug.Log("Registered NetworkVIew: " + netView.ViewID);
            }
        }


        public static void OpCleanActorRpcBuffer(int actorNumber)
        {
            RaiseEventOptions options = new RaiseEventOptions() { CachingOption = EventCaching.RemoveFromRoomCache, TargetActors = new int[] { actorNumber } };
            NDG_Network.RaiseEventInternal(NetEvent.RPC, null, options, SendOptions.SendReliable);
        }

        public static void OpRemoveCompleteCacheOfPlayer(int actorNumber)
        {
            RaiseEventOptions options = new RaiseEventOptions() { CachingOption = EventCaching.RemoveFromRoomCache, TargetActors = new int[] { actorNumber } };
            NDG_Network.RaiseEventInternal(0, null, options, SendOptions.SendReliable);
        }

        public static void OpRemoveCompleteCache()
        {
            RaiseEventOptions options = new RaiseEventOptions() { CachingOption = EventCaching.RemoveFromRoomCache, Receivers = ReceiverGroup.MasterClient };
            NDG_Network.RaiseEventInternal(0, null, options, SendOptions.SendReliable);
        }


        public static void CleanRpcBufferIfMine(NetworkView view)
        {
            if (view.OwnerActorNumber != NetworkingClient.LocalPlayer.ActorNumber && !NetworkingClient.LocalPlayer.IsMasterClient)
            {
                Debug.LogError("RPC 제거에 실패했습니다 로컬플레이어의 소유가 아닙니다. Owner: " + view.Owner + " scene: " + view.IsSceneView);
                return;
            }

            OpCleanRpcBuffer(view);
        }

        private static readonly Hashtable rpcFilterByViewId = new Hashtable();
        private static readonly RaiseEventOptions OpCleanRpcBufferOptions = new RaiseEventOptions() { CachingOption = EventCaching.RemoveFromRoomCache };

        public static void OpCleanRpcBuffer(NetworkView view)
        {
            rpcFilterByViewId[(byte)0] = view.ViewID;
            NDG_Network.RaiseEventInternal(NetEvent.RPC, rpcFilterByViewId, OpCleanRpcBufferOptions, SendOptions.SendReliable);
        }



        public static void SetInterestGroups(byte[] disableGroups, byte[] enableGroups)
        {

            if (disableGroups != null)
            {
                if (disableGroups.Length == 0)
                {
                    allowedReceivingGroups.Clear();
                }
                else
                {
                    for (int index = 0; index < disableGroups.Length; index++)
                    {
                        byte g = disableGroups[index];
                        if (g <= 0)
                        {
                            Debug.LogError("Error: NDG_Network.SetInterestGroups 실패 그룹번호가 잘못되었습니다: " + g + ". 그룹 번호는 최소 1입니다.");
                            continue;
                        }

                        if (allowedReceivingGroups.Contains(g))
                        {
                            allowedReceivingGroups.Remove(g);
                        }
                    }
                }
            }

            if (enableGroups != null)
            {
                if (enableGroups.Length == 0)
                {
                    for (byte index = 0; index < byte.MaxValue; index++)
                    {
                        allowedReceivingGroups.Add(index);
                    }

                    allowedReceivingGroups.Add(byte.MaxValue);
                }
                else
                {
                    for (int index = 0; index < enableGroups.Length; index++)
                    {
                        byte g = enableGroups[index];
                        if (g <= 0)
                        {
                            Debug.LogError("Error: NDG_Network.SetInterestGroups 실패 그룹번호가 잘못되었습니다: " + g + ". 그룹 번호는 최소 1입니다.");
                            continue;
                        }

                        allowedReceivingGroups.Add(g);
                    }
                }
            }

            if (!NDG_Network.offlineMode)
            {
                NetworkingClient.OpChangeGroups(disableGroups, enableGroups);
            }
        }


        internal static void NewSceneLoaded()
        {
            if (loadingLevelAndPausedNetwork)
            {
                _AsyncLevelLoadingOperation = null;
                loadingLevelAndPausedNetwork = false;
                NDG_Network.IsMessageQueueRunning = true;
            }
            else
            {
                NDG_Network.SetLevelInPropsIfSynced(SceneManagerHelper.ActiveSceneName);
            }

            List<int> removeKeys = new List<int>();
            foreach (KeyValuePair<int, NetworkView> kvp in networkViewList)
            {
                NetworkView view = kvp.Value;
                if (view == null)
                {
                    removeKeys.Add(kvp.Key);
                }
            }

            for (int index = 0; index < removeKeys.Count; index++)
            {

                int key = removeKeys[index];
                Debug.LogError("NewScene Clean " + key);
                networkViewList.Remove(key);
            }

            if (removeKeys.Count > 0)
            {
                if (NDG_Network.LogLevel >= NetLogLevel.Informational)
                    Debug.Log("New level loaded. Removed " + removeKeys.Count);
            }
        }


        /// <summary>
        /// 한 번 업데이트 때 업데이트 될 오브젝트의 갯수를 정합니다.
        /// 숫자가 작으면 오버헤드가 증가하고 숫자가 크면 메시지가 조각날 수 있습니다.
        /// </summary>
        public static int ObjectsInOneUpdate = 20;


        private static readonly NetworkStream serializeStreamOut = new NetworkStream(true, null);
        private static readonly NetworkStream serializeStreamIn = new NetworkStream(false, null);

        private static RaiseEventOptions serializeRaiseEvOptions = new RaiseEventOptions();

        private struct RaiseEventBatch : IEquatable<RaiseEventBatch>
        {
            public byte Group;
            public bool Reliable;

            public override int GetHashCode()
            {
                return (this.Group << 1) + (this.Reliable ? 1 : 0);
            }
            public bool Equals(RaiseEventBatch other)
            {
                return this.Reliable == other.Reliable && this.Group == other.Group;
            }
        }

        private class SerializeViewBatch : IEquatable<SerializeViewBatch>, IEquatable<RaiseEventBatch>
        {
            public readonly RaiseEventBatch Batch;
            public List<object> ObjectUpdates;
            private int defaultSize = NDG_Network.ObjectsInOneUpdate;
            private int offset;

            public SerializeViewBatch(RaiseEventBatch batch, int offset)
            {
                this.Batch = batch;
                this.ObjectUpdates = new List<object>(this.defaultSize);
                this.offset = offset;
                for (int i = 0; i < offset; i++) this.ObjectUpdates.Add(null);
            }

            public override int GetHashCode()
            {
                return (this.Batch.Group << 1) + (this.Batch.Reliable ? 1 : 0);
            }

            public bool Equals(SerializeViewBatch other)
            {
                return this.Equals(other.Batch);
            }

            public bool Equals(RaiseEventBatch other)
            {
                return this.Batch.Reliable == other.Reliable && this.Batch.Group == other.Group;
            }

            public override bool Equals(object obj)
            {
                SerializeViewBatch other = obj as SerializeViewBatch;
                return other != null && this.Batch.Equals(other.Batch);
            }

            public void Clear()
            {
                this.ObjectUpdates.Clear();
                for (int i = 0; i < offset; i++) this.ObjectUpdates.Add(null);
            }

            public void Add(List<object> viewData)
            {
                if (this.ObjectUpdates.Count >= this.ObjectUpdates.Capacity)
                {
                    throw new Exception("크기가 초과되어 추가할 수 없습니다.");
                }

                this.ObjectUpdates.Add(viewData);
            }
        }



        private static readonly Dictionary<RaiseEventBatch, SerializeViewBatch> serializeViewBatches = new Dictionary<RaiseEventBatch, SerializeViewBatch>();


        /// <summary>
        /// NetworkHandler에서 호출되며
        /// 모든 NetworkView에 대해 OnNetworkSerializeView() 업데이트를 처리합니다.
        /// </summary>
        internal static void RunViewUpdate()
        {
            if (CurrentRoom == null || CurrentRoom.Players == null || CurrentRoom.Players.Count <= 1)
            {
                return;
            }

            var enumerator = networkViewList.GetEnumerator();
            while (enumerator.MoveNext())
            {
                NetworkView view = enumerator.Current.Value;

                //클라이언트는 활성화 상태인 NetworkView와 자신의 NetworkView만 업데이트 합니다.
                if (view.Synchronization == ViewSynchronization.Off || view.IsMine == false || view.isActiveAndEnabled == false)
                {
                    continue;
                }

                if (blockedSendingGroups.Contains(view.Group))
                {
                    continue;
                }

                List<object> evData = OnSerializeWrite(view);
                if (evData == null)
                {
                    continue;
                }

                RaiseEventBatch eventBatch = new RaiseEventBatch();
                eventBatch.Reliable = view.Synchronization == ViewSynchronization.ReliableDeltaCompressed;
                eventBatch.Group = view.Group;

                SerializeViewBatch svBatch = null;
                bool found = serializeViewBatches.TryGetValue(eventBatch, out svBatch);
                if (!found)
                {
                    svBatch = new SerializeViewBatch(eventBatch, 2);
                    serializeViewBatches.Add(eventBatch, svBatch);
                }

                svBatch.Add(evData);
                if (svBatch.ObjectUpdates.Count == svBatch.ObjectUpdates.Capacity)
                {
                    SendSerializeViewBatch(svBatch);
                }
            }

            var enumberatorB = serializeViewBatches.GetEnumerator();
            while (enumberatorB.MoveNext())
            {
                SendSerializeViewBatch(enumberatorB.Current.Value);
            }
        }

        private static void SendSerializeViewBatch(SerializeViewBatch batch)
        {
            if (batch == null || batch.ObjectUpdates.Count <= 2)
            {
                return;
            }

            serializeRaiseEvOptions.InterestGroup = batch.Batch.Group;
            batch.ObjectUpdates[0] = NDG_Network.ServerTimestamp;
            batch.ObjectUpdates[1] = (currentLevelPrefix != 0) ? (object)currentLevelPrefix : null;
            byte code = batch.Batch.Reliable ? NetworkEvent.SendSerializeReliable : NetworkEvent.SendSerialize;

            NDG_Network.RaiseEventInternal(code, batch.ObjectUpdates, serializeRaiseEvOptions, batch.Batch.Reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
            batch.Clear();
        }

        //ExecuteOnSerialize를 통해 OnNetSerializeView호출
        private static List<object> OnSerializeWrite(NetworkView view)
        {
            if (view.Synchronization == ViewSynchronization.Off)
            {
                return null;
            }

            NetworkMessageInfo info = new NetworkMessageInfo(NetworkingClient.LocalPlayer, NDG_Network.ServerTimestamp, view);

            if (view.syncValues == null)
            {
                view.syncValues = new List<object>();
            }

            view.syncValues.Clear();
            serializeStreamOut.SetWriteStream(view.syncValues);
            serializeStreamOut.SendNext(null); // View ID
            serializeStreamOut.SendNext(null); // Compressed
            serializeStreamOut.SendNext(null); // null values

            view.SerializeView(serializeStreamOut, info);

            //전송할 값이 실제 있는지 확인
            if (serializeStreamOut.Count <= SyncFirstValue)
            {
                return null;
            }

            List<object> currentValues = serializeStreamOut.GetWriteStream();
            currentValues[SyncViewId] = view.ViewID;
            currentValues[SyncCompressed] = false;
            currentValues[SyncNullValues] = null;

            if (view.Synchronization == ViewSynchronization.Unreliable)
            {
                return currentValues;
            }

            if (view.Synchronization == ViewSynchronization.UnreliableOnChange)
            {
                if (AlmostEquals(currentValues, view.lastOnSerializeDataSent))
                {
                    if (view.mixedModeIsReliable)
                    {
                        return null;
                    }

                    view.mixedModeIsReliable = true;
                    List<object> temp = view.lastOnSerializeDataSent;   // TODO: extract "exchange" into method in PV
                    view.lastOnSerializeDataSent = currentValues;
                    view.syncValues = temp;
                }
                else
                {
                    view.mixedModeIsReliable = false;
                    List<object> temp = view.lastOnSerializeDataSent;   // TODO: extract "exchange" into method in PV
                    view.lastOnSerializeDataSent = currentValues;
                    view.syncValues = temp;
                }


                return currentValues;
            }

            if (view.Synchronization == ViewSynchronization.ReliableDeltaCompressed)
            {

                List<object> dataToSend = DeltaCompressionWrite(view.lastOnSerializeDataSent, currentValues);

                List<object> temp = view.lastOnSerializeDataSent;
                view.lastOnSerializeDataSent = currentValues;
                view.syncValues = temp;

                return dataToSend;
            }

            return null;
        }

        /// <summary>
        /// OnSerializeWrite에서 생성한 업데이트를 읽어옵니다.
        /// </summary>
        private static void OnSerializeRead(object[] data, Player sender, int networkTime, short correctPrefix)
        {
            int viewID = (int)data[SyncViewId];
            NetworkView view = GetNetworkView(viewID);
            if (view == null)
            {
                Debug.LogWarning("Received OnSerialization for view ID " + viewID + ". 해당 View가 Null상태입니다 상태를 확인하세요. State: " + NetworkingClient.State);
                return;
            }

            if (view.Prefix > 0 && correctPrefix != view.Prefix)
            {
                Debug.LogError("Received OnSerialization for view ID " + viewID + " 의 prefix " + correctPrefix + ". 현재 prefix : " + view.Prefix);
                return;
            }

            // 그룹 필터링
            if (view.Group != 0 && !allowedReceivingGroups.Contains(view.Group))
            {
                return; // Ignore group
            }




            if (view.Synchronization == ViewSynchronization.ReliableDeltaCompressed)
            {
                object[] uncompressed = DeltaCompressionRead(view.lastOnSerializeDataReceived, data);

                if (uncompressed == null)
                {
                    // 현재 View의 전체본을 아직 받지 못했으므로 건너뜁니다.
                    if (NDG_Network.LogLevel >= NetLogLevel.Informational)
                    {
                        Debug.Log("Skipping packet  " + view.name + " [" + view.ViewID +
                                  "] 의 패킷 전체를 아직 다 받지 못하였습니다.");
                    }
                    return;
                }

                view.lastOnSerializeDataReceived = uncompressed;
                data = uncompressed;
            }


            serializeStreamIn.SetReadStream(data, 3);
            NetworkMessageInfo info = new NetworkMessageInfo(sender, networkTime, view);

            view.DeserializeView(serializeStreamIn, info);
        }


        //currentContent가 previousContent와 같은 경우 NULL을 값으로 사용하여 currentContent를 압축합니다
        // SyncFirstValue에서 정의한 대로 초기 인덱스를 건너뜁니다
        // SyncFirstValue는 첫 번째 실제 데이터 값의 인덱스여야 합니다
        public const int SyncViewId = 0;
        public const int SyncCompressed = 1;
        public const int SyncNullValues = 2;
        public const int SyncFirstValue = 3;

        private static List<object> DeltaCompressionWrite(List<object> previousContent, List<object> currentContent)
        {
            if (currentContent == null || previousContent == null || previousContent.Count != currentContent.Count)
            {
                return currentContent;
            }

            if (currentContent.Count <= SyncFirstValue)
            {
                return null;
            }

            List<object> compressedContent = previousContent;
            compressedContent[SyncCompressed] = false;
            int compressedValues = 0;

            Queue<int> valuesThatAreChangedToNull = null;
            for (int index = SyncFirstValue; index < currentContent.Count; index++)
            {
                object newObj = currentContent[index];
                object oldObj = previousContent[index];
                if (AlmostEquals(newObj, oldObj))
                {
                    compressedValues++;
                    compressedContent[index] = null;
                }
                else
                {
                    compressedContent[index] = newObj;

                    if (newObj == null)
                    {
                        if (valuesThatAreChangedToNull == null)
                        {
                            valuesThatAreChangedToNull = new Queue<int>(currentContent.Count);
                        }
                        valuesThatAreChangedToNull.Enqueue(index);
                    }
                }
            }


            if (compressedValues > 0)
            {
                if (compressedValues == currentContent.Count - SyncFirstValue)
                {
                    return null;
                }

                compressedContent[SyncCompressed] = true;
                if (valuesThatAreChangedToNull != null)
                {
                    compressedContent[SyncNullValues] = valuesThatAreChangedToNull.ToArray();
                }
            }

            compressedContent[SyncViewId] = currentContent[SyncViewId];
            return compressedContent;
        }


        private static object[] DeltaCompressionRead(object[] lastOnSerializeDataReceived, object[] incomingData)
        {
            if ((bool)incomingData[SyncCompressed] == false)
            {
                return incomingData;
            }

            if (lastOnSerializeDataReceived == null)
            {
                return null;
            }


            int[] indexesThatAreChangedToNull = incomingData[(byte)2] as int[];
            for (int index = SyncFirstValue; index < incomingData.Length; index++)
            {
                if (indexesThatAreChangedToNull != null && indexesThatAreChangedToNull.Contains(index))
                {
                    continue;
                }
                if (incomingData[index] == null)
                {
                    object lastValue = lastOnSerializeDataReceived[index];
                    incomingData[index] = lastValue;
                }
            }

            return incomingData;
        }

        /// <summary>
        /// 두 개체가 거의 동일한 경우 true 반환
        /// 두 개체가 업데이트를 건너뛸 수 있을 정도로 유사한지 확인하는데 사용
        /// <returns></returns>
        private static bool AlmostEquals(IList<object> lastData, IList<object> currentContent)
        {
            if (lastData == null && currentContent == null)
            {
                return true;
            }

            if (lastData == null || currentContent == null || (lastData.Count != currentContent.Count))
            {
                return false;
            }

            for (int index = 0; index < currentContent.Count; index++)
            {
                object newObj = currentContent[index];
                object oldObj = lastData[index];
                if (!AlmostEquals(newObj, oldObj))
                {
                    return false;
                }
            }

            return true;
        }

        static bool AlmostEquals(object one, object two)
        {
            if (one == null || two == null)
            {
                return one == null && two == null;
            }

            if (!one.Equals(two))
            {
                // if A is not B, lets check if A is almost B
                if (one is Vector3)
                {
                    Vector3 a = (Vector3)one;
                    Vector3 b = (Vector3)two;
                    if (a.AlmostEquals(b, NDG_Network.PrecisionForVectorSynchronization))
                    {
                        return true;
                    }
                }
                else if (one is Vector2)
                {
                    Vector2 a = (Vector2)one;
                    Vector2 b = (Vector2)two;
                    if (a.AlmostEquals(b, NDG_Network.PrecisionForVectorSynchronization))
                    {
                        return true;
                    }
                }
                else if (one is Quaternion)
                {
                    Quaternion a = (Quaternion)one;
                    Quaternion b = (Quaternion)two;
                    if (a.AlmostEquals(b, NDG_Network.PrecisionForQuaternionSynchronization))
                    {
                        return true;
                    }
                }
                else if (one is float)
                {
                    float a = (float)one;
                    float b = (float)two;
                    if (a.AlmostEquals(b, NDG_Network.PrecisionForFloatSynchronization))
                    {
                        return true;
                    }
                }

                return false;
            }

            return true;
        }


        /// <summary>
        /// NDG_Network.AutomaticallySyncScene가 활성화된 경우 현재 Scene을 확인하는데 사용합니다.
        /// </summary>
        internal static void LoadLevelIfSynced()
        {
            if (!NDG_Network.AutomaticallySyncScene || NDG_Network.IsMasterClient || NDG_Network.CurrentRoom == null)
            {
                return;
            }

            // 현재 룸의 Scene을 확인합니다.
            if (!NDG_Network.CurrentRoom.CustomProperties.ContainsKey(CurrentSceneProperty))
            {
                return;
            }

            //현재 로드된 Scene이 마스터 클라이언트의 Scene과 다를경우 Scene을 로드합니다.
            object sceneId = NDG_Network.CurrentRoom.CustomProperties[CurrentSceneProperty];
            if (sceneId is int)
            {
                if (SceneManagerHelper.ActiveSceneBuildIndex != (int)sceneId)
                {
                    NDG_Network.LoadLevel((int)sceneId);
                }
            }
            else if (sceneId is string)
            {
                if (SceneManagerHelper.ActiveSceneName != (string)sceneId)
                {
                    NDG_Network.LoadLevel((string)sceneId);
                }
            }
        }



        internal static void SetLevelInPropsIfSynced(object levelId)
        {
            if (!NDG_Network.AutomaticallySyncScene || !NDG_Network.IsMasterClient || NDG_Network.CurrentRoom == null)
            {
                return;
            }
            if (levelId == null)
            {
                Debug.LogError("levelId는 Null일수 없습니다.");
                return;
            }


            // 현재 Scene과 룸의 Scene을 비교홥니다
            if (NDG_Network.CurrentRoom.CustomProperties.ContainsKey(CurrentSceneProperty))
            {
                object levelIdInProps = NDG_Network.CurrentRoom.CustomProperties[CurrentSceneProperty];

                if (levelId.Equals(levelIdInProps))
                {
                    return;
                }
                else
                {
                    int scnIndex = SceneManagerHelper.ActiveSceneBuildIndex;
                    string scnName = SceneManagerHelper.ActiveSceneName;

                    if ((levelId.Equals(scnIndex) && levelIdInProps.Equals(scnName)) || (levelId.Equals(scnName) && levelIdInProps.Equals(scnIndex)))
                    {
                        return;
                    }
                }
            }


            // 새 levelId가 현재 룸 속성과 일치하지 않으면 기존 로드를 취소할 수 있습니다.
            if (_AsyncLevelLoadingOperation != null)
            {
                if (!_AsyncLevelLoadingOperation.isDone)
                {
                    Debug.LogWarning("다른 Scene이 로드 되어야 하므로 진행중인 비동기 Scene Load를 취소합니다. 다음 로드할 Scene:: " + levelId);
                }

                _AsyncLevelLoadingOperation.allowSceneActivation = false;
                _AsyncLevelLoadingOperation = null;
            }



            //현재 Scene level이 아직 Propertie에 없거나 다르므로 클라이언트를 다시 설정해야 합니다.
            Hashtable setScene = new Hashtable();
            if (levelId is int) setScene[CurrentSceneProperty] = (int)levelId;
            else if (levelId is string) setScene[CurrentSceneProperty] = (string)levelId;
            else Debug.LogError("levelID는 int형 또는 string형식이어야 합니다.");

            NDG_Network.CurrentRoom.SetCustomProperties(setScene);

            //클라이언트는 로드를 시작하고 잠시 동안 통신을 중지하므로 즉시 전송합니다.
            SendAllOutgoingCommands();
        }

        private static void OnEvent(EventData netEvent)
        {
            int actorNr = netEvent.Sender;
            Player originatingPlayer = null;

            if (actorNr > 0 && NetworkingClient.CurrentRoom != null)
            {
                originatingPlayer = NetworkingClient.CurrentRoom.GetPlayer(actorNr);
            }

            switch (netEvent.Code)
            {
                case EventCode.Join:
                    ResetNetworkViewsOnSerialize();
                    break;

                case NetEvent.RPC:
                    ExecuteRpc(netEvent.CustomData as Hashtable, originatingPlayer);
                    break;

                case NetEvent.SendSerialize:
                case NetEvent.SendSerializeReliable:

                    /* 이 경우에는 RunViewUpdate() 와 OnSerializeWrite()가 일치해야 합니다..
                     * Format of the event's data object[]:
                     *  [0] =NDG_Network.ServerTimestamp;
                     *  [1] = currentLevelPrefix;  OPTIONAL!
                     *  [2] = object[] of NetworkView x
                     *  [3] = object[] of NetworkView y or NULL
                     *  [...]
                     */

                    object[] pvUpdates = (object[])netEvent[ParameterCode.Data];
                    int remoteUpdateServerTimestamp = (int)pvUpdates[0];
                    short remoteLevelPrefix = (pvUpdates[1] != null) ? (byte)pvUpdates[1] : (short)0;

                    object[] viewUpdate = null;
                    for (int i = 2; i < pvUpdates.Length; i++)
                    {
                        viewUpdate = pvUpdates[i] as object[];
                        if (viewUpdate == null)
                        {
                            break;
                        }
                        OnSerializeRead(viewUpdate, originatingPlayer, remoteUpdateServerTimestamp, remoteLevelPrefix);
                    }
                    break;

                case NetEvent.Instantiation:
                    Debug.Log("Network Instatiate : Actor Number " + originatingPlayer.NickName + " " +originatingPlayer.ActorNumber );
                    NetworkInstantiate((Hashtable)netEvent.CustomData, originatingPlayer);
                    break;

                case NetEvent.CloseConnection:

                    if (originatingPlayer == null || !originatingPlayer.IsMasterClient)
                    {
                        Debug.LogError("Error: 마스터 클라이언트가 아닌 (" + originatingPlayer + ")가 연결 해제를 요청합니다.");
                    }
                    else
                    {
                        NDG_Network.LeaveRoom(false);
                    }

                    break;

                case NetEvent.DestroyPlayer:
                    Hashtable evData = (Hashtable)netEvent.CustomData;
                    int targetPlayerId = (int)evData[(byte)0];
                    if (targetPlayerId >= 0)
                    {
                        DestroyPlayerObjects(targetPlayerId, true);
                    }
                    else
                    {
                        DestroyAll(true);
                    }
                    break;

                case EventCode.Leave:

                    // 객체 및 버퍼링된 메시지를 삭제합니다
                    if (CurrentRoom != null && CurrentRoom.AutoCleanUp && (originatingPlayer == null || !originatingPlayer.IsInactive))
                    {
                        DestroyPlayerObjects(actorNr, true);
                    }
                    break;

                case NetEvent.Destroy:
                    evData = (Hashtable)netEvent.CustomData;
                    int instantiationId = (int)evData[(byte)0];

                    NetworkView pvToDestroy = null;
                    if (networkViewList.TryGetValue(instantiationId, out pvToDestroy))
                    {
                        RemoveInstantiatedGO(pvToDestroy.gameObject, true);
                    }
                    else
                    {
                        Debug.LogError("Destroy Event 실패. 생성자 ID의 NetworkView를 찾을 수 없습니다. 생성자: " + instantiationId + ". 보낸 actorNumber: " + actorNr);
                    }

                    break;

                case NetEvent.OwnershipRequest:
                    {
                        int[] requestValues = (int[])netEvent.CustomData;
                        int requestedViewId = requestValues[0];
                        int requestedFromOwnerId = requestValues[1];


                        NetworkView requestedView = GetNetworkView(requestedViewId);
                        if (requestedView == null)
                        {
                            Debug.LogWarning("소유권 요청을 받은 NetworkView를 찾을 수 없습니다. 찾지 못한 ViewID : " + requestedViewId);
                            break;
                        }

                        if (NDG_Network.LogLevel == NetLogLevel.Informational)
                        {
                            Debug.Log(string.Format("OwnershipRequest:: actorNumber: {0} requests view {1} 에서 {2}로. 현재 netView소유자: {3}  {4}. isMine: {6} master client: {5}", actorNr, requestedViewId, requestedFromOwnerId, requestedView.OwnerActorNumber, requestedView.IsOwnerActive ? "active" : "inactive", MasterClient.ActorNumber, requestedView.IsMine));
                        }

                        switch (requestedView.OwnershipTransfer)
                        {
                            case OwnershipOption.Takeover:
                                int currentPvOwnerId = requestedView.OwnerActorNumber;
                                if (requestedFromOwnerId == currentPvOwnerId || (requestedFromOwnerId == 0 && currentPvOwnerId == MasterClient.ActorNumber) || currentPvOwnerId == 0)
                                {
                                    //Takeover 옵션일 경우 소유권 이전이 자동으로 성공합니다.
                                    Player prevOwner = requestedView.Owner;
                                    Player newOwner = CurrentRoom.GetPlayer(actorNr);

                                    requestedView.SetOwnerInternal(newOwner, actorNr);

                                    if (NDG_Network.OnOwnershipTransferedEv != null)
                                    {
                                        NDG_Network.OnOwnershipTransferedEv(requestedView, prevOwner);
                                    }

                                }
                                else
                                {
                                    Debug.LogWarning("requestedView.OwnershipTransfer가 생략되었습니다. ");
                                }
                                break;

                            case OwnershipOption.Request:
                                if (NDG_Network.OnOwnershipRequestEv != null)
                                {
                                    NDG_Network.OnOwnershipRequestEv(requestedView, originatingPlayer);
                                }
                                break;

                            default:
                                Debug.LogWarning("현재 Ownership mode == " + (requestedView.OwnershipTransfer) + ". 요청이 생략되었습니다.");
                                break;
                        }
                    }
                    break;

                case NetEvent.OwnershipTransfer:
                    {
                        int[] transferViewToUserID = (int[])netEvent.CustomData;
                        int requestedViewId = transferViewToUserID[0];
                        int newOwnerId = transferViewToUserID[1];

                        if (NDG_Network.LogLevel >= NetLogLevel.Informational)
                        {
                            Debug.Log("OwnershipTransfer 이벤트. ViewID " + requestedViewId + " to: " + newOwnerId + " Time: " + Environment.TickCount % 1000);
                        }

                        NetworkView requestedView = GetNetworkView(requestedViewId);
                        if (requestedView != null)
                        {
                            // OwnershipOption이 Takeover이거나 Request일 경우만
                            if (requestedView.OwnershipTransfer == OwnershipOption.Takeover ||
                                (requestedView.OwnershipTransfer == OwnershipOption.Request && (originatingPlayer == requestedView.Controller || originatingPlayer == requestedView.Owner)))
                            {
                                Player prevOwner = requestedView.Owner;
                                Player newOwner = CurrentRoom.GetPlayer(newOwnerId);

                                requestedView.SetOwnerInternal(newOwner, newOwnerId);

                                if (NDG_Network.OnOwnershipTransferedEv != null)
                                {
                                    NDG_Network.OnOwnershipTransferedEv(requestedView, prevOwner);
                                }
                            }
                            else if (NDG_Network.LogLevel >= NetLogLevel.Informational)
                            {
                                if (requestedView.OwnershipTransfer == OwnershipOption.Request)
                                    Debug.Log("소유권 이전 실패.  요청자:'" + requestedView.name + "; " + requestedViewId +
                                              " NetworkView의 OwnershipTransfer가 Request로 설정되어 있지만 현재 소유자를 변경하려는 플레이어가 현재 소유자가 아닙니다.");
                                else
                                    Debug.Log("소유권 이전 실패.  요청자:' '" + requestedView.name + "; " + requestedViewId +
                                              " NetworkVIew의 OwnershipTransfer가 Fixed로 설정되어 있습니다.");
                            }
                        }
                        else if (NDG_Network.LogLevel >= NetLogLevel.ErrorsOnly)
                        {
                            Debug.LogErrorFormat("소유권 이전 실패. NetworkView를 찾는데 실패했습니다. ID={0} , newOwnerActorNumber={1}, sender={2}",
                                                 requestedViewId, newOwnerId, actorNr);
                        }

                        break;
                    }

                case NetEvent.OwnershipUpdate:
                    {
                        reusableNVHashset.Clear();

                        int[] viewOwnerPair = (int[])netEvent.CustomData;

                        for (int i = 0, cnt = viewOwnerPair.Length; i < cnt; i++)
                        {
                            int viewId = viewOwnerPair[i];
                            i++;
                            int newOwnerId = viewOwnerPair[i];

                            NetworkView view = GetNetworkView(viewId);
                            Player prevOwner = view.Owner;
                            Player newOwner = CurrentRoom.GetPlayer(newOwnerId);

                            view.SetOwnerInternal(newOwner, newOwnerId);

                            reusableNVHashset.Add(view);

                            if (NDG_Network.OnOwnershipTransferedEv != null && newOwner != prevOwner)
                            {
                                NDG_Network.OnOwnershipTransferedEv(view, prevOwner);
                            }
                        }

                        // Initialize all views. Typically this is just fired on a new client after it joins a room and gets the first OwnershipUpdate from the Master.
                        // This was moved from PhotonHandler OnJoinedRoom to here, to allow objects to retain controller = -1 until an controller is actually knownn.
                        //모든 View를 initialize합니다.
                        //일반적으로 새로운 클라이언트가 룸에 들어왔을때나 마스터클라이언트로부터 소유권 업데이트를 받은 후에만 실행됩니다.
                        foreach (var view in NetworkViewCollection)
                        {
                            if (!reusableNVHashset.Contains(view))
                                view.RebuildControllerCache();
                        }

                        break;
                    }


            }



        }

        private static void OnOperation(OperationResponse opResponse)
        {
            switch (opResponse.OperationCode)
            {
                //case OperationCode.GetRegions:

                case OperationCode.JoinGame:
                    if (Server == ServerConnection.GameServer)
                    {
                        NDG_Network.LoadLevelIfSynced();
                    }
                    break;
            }
        }

        private static void OnClientStateChanged(ClientState previousState, ClientState state)
        {
            if (
                (previousState == ClientState.Joined && state == ClientState.Disconnected) ||
                (Server == ServerConnection.GameServer && (state == ClientState.Disconnecting || state == ClientState.DisconnectingFromGameServer))
                )
            {
                LeftRoomCleanup();
            }

            if (state == ClientState.ConnectedToMasterServer && _cachedRegionHandler != null)
            {
                BestRegionSummaryInPreferences = _cachedRegionHandler.SummaryToCache;
                _cachedRegionHandler = null;
            }
        }

        private static RegionHandler _cachedRegionHandler;


    }



    internal class NetworkEvent
    {
        public const byte RPC = 200;
        public const byte SendSerialize = 201;
        public const byte Instantiation = 202;
        public const byte CloseConnection = 203;
        public const byte Destroy = 204;
        public const byte RemoveCachedRPCs = 205;
        public const byte SendSerializeReliable = 206;
        public const byte DestroyPlayer = 207;
        public const byte OwnershipRequest = 209;
        public const byte OwnershipTransfer = 210;
        public const byte VacantViewIds = 211;
        public const byte OwnershipUpdate = 212;
    }
}

