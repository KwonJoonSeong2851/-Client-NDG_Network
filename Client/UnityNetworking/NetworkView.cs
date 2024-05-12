

namespace NDG.UnityNet
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Serialization;

    using NDG.Realtime;

#if UNITY_EDITOR
    using UnityEditor;
#endif



    [AddComponentMenu("NDG Networking/NetworkView")]
    public class NetworkView : MonoBehaviour
    {
        [NonSerialized]
        private int ownerActorNumber;

        public byte Group = 0;

        protected internal bool mixedModeIsReliable = false;

        /// <summary>
        /// prefabs는 -1의 prefixField를 가지며 런타임에서는 현재 LevelPrefix로 대체됨
        /// </summary>
        public int Prefix
        {
            get
            {
                if (this.prefixField == -1 && NDG_Network.NetworkingClient != null)
                {
                    this.prefixField = NDG_Network.currentLevelPrefix;
                }
                return this.prefixField;
            }
            set { this.prefixField = value; }
        }

        [FormerlySerializedAs("prefixBackup")]
        public int prefixField = -1;


        public enum ObservableSearch
        {
            Manual,
            AutoFindActive,
            AutoFindAll,
        }

        public ObservableSearch observableSearch = ObservableSearch.Manual;

        /// <summary>
        /// NDG_Network.Instantiate에서 호출시 전달된 데이터
        /// </summary>
        public object[] InstantiationData
        {
            get
            {
                if(!this.didAwake)
                {
                    Debug.LogError("FetchInstantiationData() was removed");
                }
                return this.instantiationDataField;
            }
            set
            {
                this.instantiationDataField = value;
            }
        }

        internal object[] instantiationDataField;

        protected internal List<object> lastOnSerializeDataSent = null;
        protected internal List<object> syncValues;

        protected internal object[] lastOnSerializeDataReceived = null;


        public ViewSynchronization Synchronization = ViewSynchronization.UnreliableOnChange;


        /// <summary>
        /// NetworkView의 소유권이 고정되어 있는지, 요청할수 있는지 또는 단순히 취할수 있는지 설정
        /// </summary>
        public OwnershipOption OwnershipTransfer = OwnershipOption.Fixed;

        public List<Component> ObservedComponents;

       /// <summary>
       /// Callback Interface 처리
       /// </summary>
        #region Callback Interfaces
        private struct CallbackTargetChange
        {
            public INetworkViewCallback obj;
            public Type type;
            public bool add;

            public CallbackTargetChange(INetworkViewCallback obj,Type type,bool add)
            {
                this.obj = obj;
                this.type = type;
                this.add = add;
            }
        }

        private Queue<CallbackTargetChange> CallbackChangeQueue = new Queue<CallbackTargetChange>();

        private List<IOnNetworkViewPreNetDestroy> OnPreNetDestroyCallbacks;
        private List<IOnNetworkViewOwnerChange> OnOwnerChangeCallbacks;
        private List<IOnNetworkViewControllerChange> OnControllerChangeCallbacks;

        public void AddCallbackTarget(INetworkViewCallback obj)
        {
            CallbackChangeQueue.Enqueue(new CallbackTargetChange(obj, null, true));
        }

        public void RemoveCallbackTarget(INetworkViewCallback obj)
        {
            CallbackChangeQueue.Enqueue(new CallbackTargetChange(obj, null, false));
        }

        public void AddCallback<T>(INetworkViewCallback obj) where T : class,INetworkViewCallback
        {
            CallbackChangeQueue.Enqueue(new CallbackTargetChange(obj, typeof(T), true));
        }

        public void RemoveCallback<T>(INetworkViewCallback obj) where T : class, INetworkViewCallback
        {
            CallbackChangeQueue.Enqueue(new CallbackTargetChange(obj, typeof(T), false));
        }
        
        private void UpdateCallbackLists()
        {
            while (CallbackChangeQueue.Count > 0)
            {
                var item = CallbackChangeQueue.Dequeue();
                var obj = item.obj;
                var type = item.type;
                var add = item.add;

                if (type == null)
                {
                    TryRegisterCallback(obj, ref OnPreNetDestroyCallbacks, add);
                    TryRegisterCallback(obj, ref OnOwnerChangeCallbacks, add);
                    TryRegisterCallback(obj, ref OnControllerChangeCallbacks, add);
                }
                else if (type == typeof(IOnNetworkViewPreNetDestroy))
                    RegisterCallback(obj as IOnNetworkViewPreNetDestroy, ref OnPreNetDestroyCallbacks, add);

                else if (type == typeof(IOnNetworkViewOwnerChange))
                    RegisterCallback(obj as IOnNetworkViewOwnerChange, ref OnOwnerChangeCallbacks, add);

                else if (type == typeof(IOnNetworkViewControllerChange))
                    RegisterCallback(obj as IOnNetworkViewControllerChange, ref OnControllerChangeCallbacks, add);
            }
        }

        private void TryRegisterCallback<T>(INetworkViewCallback obj,ref List<T> list, bool add) where T : class,INetworkViewCallback
        {
            T iobj = obj as T;
            if(iobj != null)
            {
                RegisterCallback(iobj, ref list, add);
            }
        }

        private void RegisterCallback<T>(T obj,ref List<T> list,bool add) where T : class,INetworkViewCallback
        {
            if(ReferenceEquals(list,null))
            {
                list = new List<T>();
            }

            if(add)
            {
                if (!list.Contains(obj))
                    list.Add(obj);
            }
            else
            {
                if (list.Contains(obj))
                    list.Remove(obj);
            }
        }

        #endregion Callback Interfaces

        [SerializeField]
        private int viewIdField = 0;

        /// <summary>
        /// 방별로 NetworkView ID소유  
        /// </summary>
        public int ViewID
        {
            get
            {
                return this.viewIdField;
            }
            set
            {
                bool viewMustRegister = this.viewIdField == 0 && value != 0;

                this.viewIdField = value;
                this.ownerActorNumber = value / NDG_Network.MAX_VIEW_IDS;

                if(viewMustRegister)
                {
                    NDG_Network.RegisterNetworkView(this);
                }
            }
        }

        //view가 게임오브젝트로 인스턴스화될 경우 InstatiationId를 가짐 
        public int InstantiationId;


        public bool IsSceneView
        {
            get
            {
                return this.CreatorActorNumber == 0;
            }
        }

        #region Ownership
        /// <summary>
        /// NetworkView를 재설정합니다. 플레이어가 룸에 참여할때 사용
        /// </summary>
        internal void ResetNetworkView(bool resetOwner)
        {
            //다시 룸에 참여할 경우 소유권 캐시를 creator로 재설정
            if(resetOwner)
            {
                ResetOwnership();
            }

            ownershipCacheIsValid = OwnershipCacheState.ControllerValid;

            lastOnSerializeDataSent = null;
        }

        /// <summary>
        /// 소유자 및 컨트롤러를 Creator로 재설정
        /// </summary>
        internal void ResetOwnership()
        {
            if(this.CreatorActorNumber == 0)
            {
                this.SetOwnerInternal(null, 0);
            }
            else
            {
                if(ReferenceEquals(NDG_Network.CurrentRoom,null))
                {
                    this.SetOwnerInternal(null, this.CreatorActorNumber);
                }
                else
                {
                    this.SetOwnerInternal(NDG_Network.CurrentRoom.GetPlayer(this.CreatorActorNumber), this.CreatorActorNumber);
                }
            }
        }

        /// <summary>
        /// NetworkView의 소유자를 수동으로 설정
        /// </summary>
        public void SetOwnerInternal(Player newOwner,int newOwnerId)
        {
            if((ownershipCacheIsValid & OwnershipCacheState.OwnerValid) != 0)
            {
                if(ownerActorNumber == newOwnerId)
                {
                    RebuildControllerCache(false);
                    return;
                }
            }
            else
            {
                ownershipCacheIsValid = OwnershipCacheState.OwnerValid;
            }

            Player prevOwner = this.owner;
            this.ownerActorNumber = newOwnerId;
            this.owner = newOwner;
            this.AmOwner = newOwner == NDG_Network.LocalPlayer;

            if(newOwner != prevOwner)
            {
                if(!ReferenceEquals(OnOwnerChangeCallbacks,null))
                {
                    for(int i = 0, count = OnOwnerChangeCallbacks.Count; i < count; ++i)
                    {
                        OnOwnerChangeCallbacks[i].OnOwnerChage(newOwner, prevOwner);
                    }
                }
            }

            RebuildControllerCache(true);
        }

        public void SetControllerInternal(int newControllerId)
        {
            SetControllerInternal(NDG_Network.CurrentRoom.GetPlayer(newControllerId), newControllerId);
        }

        public void SetControllerInternal(Player newController,int newControllerId)
        {
            Player prevController = this.controller;

            this.controller = newController;
            this.controllerActorNumber = newControllerId;
            this.amController = newController == NDG_Network.LocalPlayer;

            this.ownershipCacheIsValid |= OwnershipCacheState.ControllerValid;

            UpdateCallbackLists();

            if (controller != prevController)
                if (!ReferenceEquals(OnControllerChangeCallbacks, null))
                    for (int i = 0, cnt = OnControllerChangeCallbacks.Count; i < cnt; ++i)
                        OnControllerChangeCallbacks[i].OnControllerChange(newController, prevController);
        }

        internal void RebuildControllerCache(bool ownerHasChanged = false)
        {
            var prevController = controller;

            //Scene Object들은 컨트롤러를 변경해야됨
            if(owner == null || this.ownerActorNumber == 0 || this.owner.IsInactive)
            {
                var masterclient = NDG_Network.MasterClient;
                this.controller = masterclient;
                this.controllerActorNumber = masterclient == null ? -1 : masterclient.ActorNumber;
            }
            else
            {
                this.controller = this.owner;
                this.controllerActorNumber = this.ownerActorNumber;
            }

            ownershipCacheIsValid |= OwnershipCacheState.ControllerValid;

            this.amController = this.controllerActorNumber != -1 && this.controllerActorNumber == NDG_Network.LocalPlayer.ActorNumber;

            UpdateCallbackLists();

            if (controller != prevController)
                if (!ReferenceEquals(OnControllerChangeCallbacks, null))
                    for (int i = 0, cnt = OnControllerChangeCallbacks.Count; i < cnt; ++i)
                        OnControllerChangeCallbacks[i].OnControllerChange(this.controller, prevController);
        }

        internal enum OwnershipCacheState
        {
            Invalid = 0,
            OwnerValid = 1,
            ControllerValid = 2,
            AllValid = 3,
        }

        internal OwnershipCacheState ownershipCacheIsValid;

        private Player owner;

        public Player Owner
        {
            get
            {
                if((ownershipCacheIsValid & OwnershipCacheState.OwnerValid) == 0)
                {
                    ownerActorNumber = this.didAwake ? this.ownerActorNumber : this.ViewID;
                    owner = NDG_Network.CurrentRoom == null ? null : NDG_Network.CurrentRoom.GetPlayer(this.ownerActorNumber);
                    ownershipCacheIsValid |= OwnershipCacheState.OwnerValid;
                }
                return owner;
            }
        }


        public int OwnerActorNumber
        {
            get
            {
                if((ownershipCacheIsValid & OwnershipCacheState.OwnerValid) == 0)
                {
                    ownerActorNumber = this.didAwake ? this.ownerActorNumber : this.ViewID;
                    owner = NDG_Network.CurrentRoom == null ? null : NDG_Network.CurrentRoom.GetPlayer(this.ownerActorNumber);
                    ownershipCacheIsValid |= OwnershipCacheState.OwnerValid;
                }

                return ownerActorNumber;
            }
        }

        public Player Controller
        {
            get
            {
                if((ownershipCacheIsValid & OwnershipCacheState.ControllerValid) == 0)
                {
                    controllerActorNumber = this.IsOwnerActive ? this.OwnerActorNumber : (NDG_Network.MasterClient != null ? NDG_Network.MasterClient.ActorNumber : -1);
                    controller =
                        (NDG_Network.CurrentRoom == null) ? NDG_Network.LocalPlayer :
                        (!this.IsOwnerActive) ? NDG_Network.MasterClient :
                        owner;

                    ownershipCacheIsValid |= OwnershipCacheState.ControllerValid;
                }

                return controller;
            }
        }

        private Player controller;

        public int ControllerActorNumber
        {
            get
            {
                if((ownershipCacheIsValid & OwnershipCacheState.ControllerValid) == 0)
                {
                    controllerActorNumber = this.IsOwnerActive ? this.OwnerActorNumber : (NDG_Network.MasterClient != null ? NDG_Network.MasterClient.ActorNumber : -1);
                    controller =
                        (NDG_Network.CurrentRoom == null) ? NDG_Network.LocalPlayer :
                        (!this.IsOwnerActive) ? NDG_Network.MasterClient :
                        owner;

                    ownershipCacheIsValid |= OwnershipCacheState.ControllerValid;
                }

                return controllerActorNumber;
            }
        }

        private int controllerActorNumber;

        public bool IsOwnerActive
        {
            get { return this.Owner != null && !this.Owner.IsInactive; }
        }

        public int CreatorActorNumber
        {
            get { return this.viewIdField / NDG_Network.MAX_VIEW_IDS; }
        }

        //NetworkView가 자신의 것이고 클라이언트가 제어할 수 있는 경우 true
        private bool amController;

        public bool IsMine
        {
            get
            {
                return
                    (ownershipCacheIsValid & OwnershipCacheState.ControllerValid) == 0 ?
                    (this.OwnerActorNumber == NDG_Network.LocalPlayer.ActorNumber) || (NDG_Network.IsMasterClient && !this.IsOwnerActive) :
                    amController;
            }
        }

        public bool AmOwner
        {
            get;
            private set;
        }
        #endregion Ownership

        protected internal bool didAwake;

        [SerializeField]
        [HideInInspector]
        public bool isRuntimeInstantiated;

        protected internal bool removedFromLocalViewList;

        internal MonoBehaviour[] RpcMonoBehaviours;

#if UNITY_EDITOR
        private void Reset()
        {
            observableSearch = ObservableSearch.AutoFindAll;
        }
#endif

        protected internal void Awake()
        {
            if(this.ViewID != 0)
            {
                int ownerId = this.ViewID / NDG_Network.MAX_VIEW_IDS;
                var room = NDG_Network.CurrentRoom;
                if(room != null)
                {
                    var owner = NDG_Network.CurrentRoom.GetPlayer(ownerId);
 
                    SetOwnerInternal(owner, ownerId);
                }

                NDG_Network.RegisterNetworkView(this);
            }

            this.didAwake = true;

            FindObservables();
        }

        public void FindObservables(bool force = false)
        {
            if(!force && observableSearch == ObservableSearch.Manual)
            {
                return;
            }

            if(ObservedComponents == null)
            {
                ObservedComponents = new List<Component>();
            }

            ObservedComponents.Clear();

            transform.GetNestedComponentsInChildren<Component, INetObservable, NetworkView>
                (force || observableSearch == ObservableSearch.AutoFindAll, ObservedComponents);
        }

        public void OnPreNetDestroy(NetworkView rootView)
        {
            UpdateCallbackLists();

            if(!ReferenceEquals(OnPreNetDestroyCallbacks,null))
            {
                for(int i = 0,count = OnPreNetDestroyCallbacks.Count; i < count; ++i)
                {
                    OnPreNetDestroyCallbacks[i].OnPreNetDestroy(rootView);
                }
            }
        }

        protected internal void OnDestroy()
        {
            if(!this.removedFromLocalViewList)
            {
                NDG_Network.LocalCleanNetworkView(this);
            }
        }
        
        /// <summary>
        /// NetworView의 OwnerShipTransfer 설정에 따라 소유권을 요청할수 있음.
        /// </summary>
        public void RequestOwnsership()
        {
            if(OwnershipTransfer != OwnershipOption.Fixed)
            {
                NDG_Network.RequestOwnership(this.ViewID, this.ownerActorNumber);
            }
            else
            {
                if(NDG_Network.LogLevel >= NetLogLevel.Informational)
                {
                    Debug.LogWarning(name + "viewID:" + ViewID + "가 소유권을 요청중이지만 소유권 이전이 Fixed로 설정되어있습니다.");
                }
            }
        }

        /// <summary>
        /// Networkview의 소유권을 다른 플레이어에게 양도합니다.
        /// </summary>
        public void TransferOwnership(Player newOwner)
        {
            if(newOwner != null)
            {
                TransferOwnership(newOwner.ActorNumber);
            }
            else
            {
                if(NDG_Network.LogLevel >= NetLogLevel.Informational)
                {
                    Debug.LogWarning(name + " viewID:" + ViewID + "가 소유권을 양도하려하지만 새로운 플레이어가 null상태입니다.");
                }
            }
        }

        /// <summary>
        /// Networkview의 소유권을 다른 플레이어에게 양도합니다.
        /// </summary>
        public void TransferOwnership(int newOwnerId)
        {
            if(OwnershipTransfer == OwnershipOption.Takeover || (OwnershipTransfer == OwnershipOption.Request && amController))
            {
                NDG_Network.TransferOwnership(this.ViewID, newOwnerId);
            } else
            {
                if(NDG_Network.LogLevel >= NetLogLevel.Informational)
                {
                    if (OwnershipTransfer == OwnershipOption.Fixed)
                        Debug.LogWarning(name + " viewID:" + ViewID + "가 소유권을 양도하려하지만 소유권 이전이 고정으로 설정되어있습니다.");
                    else if (OwnershipTransfer == OwnershipOption.Request)
                        Debug.LogWarning(name + " viewID" + ViewID + "가 소유권을 양도하려하지만 이 개체의 컨트롤러만 소유권을 양도할수 있게 설정되어있습니다.");
                }
            }
        }

        public void SerializeView(NetworkStream stream,NetworkMessageInfo info)
        {
            if(this.ObservedComponents != null && this.ObservedComponents.Count > 0)
            {
                for(int i = 0; i < this.ObservedComponents.Count; ++i)
                {
                    var component = this.ObservedComponents[i];
                    if(component != null)
                    {
                        SerializeComponent(this.ObservedComponents[i], stream, info);
                    }
                }
            }
        }
        public void DeserializeView(NetworkStream stream, NetworkMessageInfo info)
        {
            if (this.ObservedComponents != null && this.ObservedComponents.Count > 0)
            {
                for (int i = 0; i < this.ObservedComponents.Count; ++i)
                {
                    var component = this.ObservedComponents[i];
                    if (component != null)
                        DeserializeComponent(component, stream, info);
                }
            }
        }

        protected internal void DeserializeComponent(Component component,NetworkStream stream,NetworkMessageInfo info)
        {
            INetObservable observable = component as INetObservable;
            if(observable != null)
            {
                observable.OnNetSerializeView(stream, info);
            }
            else
            {
                Debug.LogError(component + "컴포넌트가 INetObservable 인터페이스를 구현하지 않았습니다");
            }
        }

        protected internal void SerializeComponent(Component component,NetworkStream stream,NetworkMessageInfo info)
        {
            INetObservable observable = component as INetObservable;
            if(observable != null)
            {
                observable.OnNetSerializeView(stream, info);
            }
            else
            {
                Debug.LogError(component + "컴포넌트가 INetObservable 인터페이스를 구현하지 않았습니다");
            }
        }

        public void RefreshRpcMonoBehaviourCache()
        {
            this.RpcMonoBehaviours = this.GetComponents<MonoBehaviour>();
        }

        /// <summary>
        /// 로컬 플레이어나 원격 플레이어에서 이 GameObject의 RPC메서드를 호출합니다.
        /// </summary>
        public void RPC(string methodName, RpcTarget target, params object[] parameters)
        {
            NDG_Network.RPC(this, methodName, target, false, parameters);
        }

        public static NetworkView Get(Component component)
        {
            return component.transform.GetParentComponent<NetworkView>();
        }

        public static NetworkView Find(int viewID)
        {
            return NDG_Network.GetNetworkView(viewID);
        }



    }
}