namespace NDG.UnityNet
{
    using System.Collections.Generic;
    using UnityEngine;

    public class AnimatorView : MonoBehaviourNet, INetObservable
    {
        #region Enums
        public enum ParameterType
        {
            Float = 1,
            Int = 3,
            Bool = 4,
            Trigger = 9,
        }

        public enum SynchronizeType
        {
            Disabled = 0,
            Discrete = 1,
            Continuous = 2,
        }

        [System.Serializable]
        public class SynchronizedParameter
        {
            public ParameterType Type;
            public SynchronizeType SynchronizeType;
            public string Name;

        }

        [System.Serializable]
        public class SynchronizedLayer
        {
            public SynchronizeType SynchronizeType;
            public int LayerIndex;
        }

        #endregion

        private bool TriggerUsageWarningDone;
        private Animator animator;

        private NetworkStreamQueue streamQueue = new NetworkStreamQueue(120);

        [HideInInspector]
        [SerializeField]
        private bool ShowLayerWeightsInspector = true;

        [HideInInspector]
        [SerializeField]
        private bool ShowParameterInspector = true;

        //[HideInInspector]
        [SerializeField]
        private List<SynchronizedParameter> synchronizeParameters = new List<SynchronizedParameter>();

        //[HideInInspector]
        [SerializeField]
        private List<SynchronizedLayer> synchronizeLayers = new List<SynchronizedLayer>();
        List<string> raisedDiscreteTriggersCache = new List<string>();
        private bool wasSynchronizeTypeChanged = true;

        #region Unity
        private void Awake()
        {
            this.animator = GetComponent<Animator>();
        }

        private void Update()
        {

            if (this.animator.applyRootMotion && this.networkView.IsMine == false && NDG_Network.IsConnected == true)
            {
                this.animator.applyRootMotion = false;
            }

            if (NDG_Network.InRoom == false || NDG_Network.CurrentRoom.PlayerCount <= 1)
            {
                this.streamQueue.Reset();
                return;
            }

            if (this.networkView.IsMine == true)
            {
                this.SerializeDataContinuously();

                this.CacheDiscreteTriggers();
            }
            else
            {
                this.DeserializeDataContinuously();
            }

        }

        #endregion

        #region Setup Synchronizeing Methods

        /// <summary>
        /// 발생된 Trigger를 추적하고 캐시목록에 등록합니다.
        /// </summary>
        public void CacheDiscreteTriggers()
        {
            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {
                SynchronizedParameter parameter = this.synchronizeParameters[i];

                if (parameter.SynchronizeType == SynchronizeType.Discrete && parameter.Type == ParameterType.Trigger && this.animator.GetBool(parameter.Name))
                {
                    if (parameter.Type == ParameterType.Trigger)
                    {
                        this.raisedDiscreteTriggersCache.Add(parameter.Name);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 특정 레이어만 동기화되도록 구성되어있는지 확인합니다.
        /// </summary>
        /// <param name="layerIndex"></param>
        /// <returns></returns>
        public bool DoesLayerSynchronizeTypeExist(int layerIndex)
        {
            return this.synchronizeLayers.FindIndex(item => item.LayerIndex == layerIndex) != -1;
        }

        /// <summary>
        /// 지정된 string 매개변수가 동기화 되도록 설정되었는지 확인합니다.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool DoesParameterSynchronizeTypeExist(string name)
        {
            return this.synchronizeParameters.FindIndex(item => item.Name == name) != -1;
        }

        public List<SynchronizedLayer> GetSynchronizedLayers()
        {
            return this.synchronizeLayers;
        }

        public List<SynchronizedParameter> GetSynchronizedParameters()
        {
            return this.synchronizeParameters;
        }

        /// <summary>
        /// 레이어 동기화 방식을 가져옵니다.
        /// </summary>
        /// <param name="layerIndex"></param>
        /// <returns></returns>
        public SynchronizeType GetLayerSynchronizeType(int layerIndex)
        {
            int index = this.synchronizeLayers.FindIndex(item => item.LayerIndex == layerIndex);

            if (index == -1)
            {
                return SynchronizeType.Disabled;
            }

            return this.synchronizeLayers[index].SynchronizeType;
        }

        /// <summary>
        /// 매개변수가 동기화 되는 방식을 가져옵니다.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public SynchronizeType GetParameterSynchronizeType(string name)
        {
            int index = this.synchronizeParameters.FindIndex(item => item.Name == name);

            if (index == -1)
            {
                return SynchronizeType.Disabled;
            }

            return this.synchronizeParameters[index].SynchronizeType;
        }

        /// <summary>
        /// 레이어 동기화 방식을 설정합니다.
        /// </summary>
        /// <param name="layerIndex"></param>
        /// <param name="synchronizeType"></param>
        public void SetLayerSynchronized(int layerIndex, SynchronizeType synchronizeType)
        {
            if (Application.isPlaying == true)
            {
                this.wasSynchronizeTypeChanged = true;
            }

            int index = this.synchronizeLayers.FindIndex(item => item.LayerIndex == layerIndex);

            if (index == -1)
            {
                this.synchronizeLayers.Add(new SynchronizedLayer { LayerIndex = layerIndex, SynchronizeType = synchronizeType });
            }
            else
            {
                this.synchronizeLayers[index].SynchronizeType = synchronizeType;
            }
        }

        public void SetParameterSynchronized(string name, ParameterType type, SynchronizeType synchronizeType)
        {
            if (Application.isPlaying == true)
            {
                this.wasSynchronizeTypeChanged = true;
            }

            int index = this.synchronizeParameters.FindIndex(item => item.Name == name);

            if (index == -1)
            {
                this.synchronizeParameters.Add(new SynchronizedParameter { Name = name, Type = type, SynchronizeType = synchronizeType });
            }
            else
            {
                this.synchronizeParameters[index].SynchronizeType = synchronizeType;
            }
        }

        #endregion


        #region Serialization

        private void SerializeDataContinuously()
        {
            if (this.animator == null)
                return;

            for (int i = 0; i < this.synchronizeLayers.Count; ++i)
            {
                if (this.synchronizeLayers[i].SynchronizeType == SynchronizeType.Continuous)
                {
                    this.streamQueue.SendNext(this.animator.GetLayerWeight(this.synchronizeLayers[i].LayerIndex));
                }
            }

            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {
                SynchronizedParameter parameter = this.synchronizeParameters[i];

                if (parameter.SynchronizeType == SynchronizeType.Continuous)
                {
                    switch (parameter.Type)
                    {
                        case ParameterType.Bool:
                            this.streamQueue.SendNext(this.animator.GetBool(parameter.Name));
                            break;
                        case ParameterType.Float:
                            this.streamQueue.SendNext(this.animator.GetFloat(parameter.Name));
                            break;
                        case ParameterType.Int:
                            this.streamQueue.SendNext(this.animator.GetInteger(parameter.Name));
                            break;
                        case ParameterType.Trigger:
                            if (!TriggerUsageWarningDone)
                            {
                                TriggerUsageWarningDone = true;
                            }
                            this.streamQueue.SendNext(this.animator.GetBool(parameter.Name));
                            break;
                    }
                }
            }
        }


        private void DeserializeDataContinuously()
        {
            if (this.streamQueue.HasQueuedObjects() == false)
            {
                return;
            }

            for (int i = 0; i < this.synchronizeLayers.Count; ++i)
            {
                if (this.synchronizeLayers[i].SynchronizeType == SynchronizeType.Continuous)
                {
                    this.animator.SetLayerWeight(this.synchronizeLayers[i].LayerIndex, (float)this.streamQueue.ReceiveNext());
                }
            }

            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {
                SynchronizedParameter parameter = this.synchronizeParameters[i];

                if (parameter.SynchronizeType == SynchronizeType.Continuous)
                {
                    switch (parameter.Type)
                    {
                        case ParameterType.Bool:
                            this.animator.SetBool(parameter.Name, (bool)this.streamQueue.ReceiveNext());
                            break;
                        case ParameterType.Float:
                            this.animator.SetFloat(parameter.Name, (float)this.streamQueue.ReceiveNext());
                            break;
                        case ParameterType.Int:
                            this.animator.SetInteger(parameter.Name, (int)this.streamQueue.ReceiveNext());
                            break;
                        case ParameterType.Trigger:
                            this.animator.SetBool(parameter.Name, (bool)this.streamQueue.ReceiveNext());
                            break;
                    }
                }
            }
        }



        private void SerializeDataDiscretly(NetworkStream stream)
        {
            for (int i = 0; i < this.synchronizeLayers.Count; ++i)
            {
                if (this.synchronizeLayers[i].SynchronizeType == SynchronizeType.Discrete)
                {
                    stream.SendNext(this.animator.GetLayerWeight(this.synchronizeLayers[i].LayerIndex));
                }
            }

            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {

                SynchronizedParameter parameter = this.synchronizeParameters[i];

                if (parameter.SynchronizeType == SynchronizeType.Discrete)
                {
                    switch (parameter.Type)
                    {
                        case ParameterType.Bool:
                            stream.SendNext(this.animator.GetBool(parameter.Name));
                            break;
                        case ParameterType.Float:
                            stream.SendNext(this.animator.GetFloat(parameter.Name));
                            break;
                        case ParameterType.Int:
                            stream.SendNext(this.animator.GetInteger(parameter.Name));
                            break;
                        case ParameterType.Trigger:
                            if (!TriggerUsageWarningDone)
                            {
                                TriggerUsageWarningDone = true;
                            }
                            stream.SendNext(this.raisedDiscreteTriggersCache.Contains(parameter.Name));
                            break;
                    }
                }
            }

            this.raisedDiscreteTriggersCache.Clear();
        }


        private void DeserializeDataDiscretly(NetworkStream stream)
        {
            for (int i = 0; i < this.synchronizeLayers.Count; ++i)
            {
                if (this.synchronizeLayers[i].SynchronizeType == SynchronizeType.Discrete)
                {
                    this.animator.SetLayerWeight(this.synchronizeLayers[i].LayerIndex, (float)stream.ReceiveNext());
                }
            }

            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {
                SynchronizedParameter parameter = this.synchronizeParameters[i];

                if (parameter.SynchronizeType == SynchronizeType.Discrete)
                {
                    switch (parameter.Type)
                    {
                        case ParameterType.Bool:
                            if (stream.PeekNext() is bool == false)
                            {
                                return;
                            }
                            this.animator.SetBool(parameter.Name, (bool)stream.ReceiveNext());
                            break;
                        case ParameterType.Float:
                            if (stream.PeekNext() is float == false)
                            {
                                return;
                            }

                            this.animator.SetFloat(parameter.Name, (float)stream.ReceiveNext());
                            break;
                        case ParameterType.Int:
                            if (stream.PeekNext() is int == false)
                            {
                                return;
                            }

                            this.animator.SetInteger(parameter.Name, (int)stream.ReceiveNext());
                            break;
                        case ParameterType.Trigger:
                            if (stream.PeekNext() is bool == false)
                            {
                                return;
                            }

                            if ((bool)stream.ReceiveNext())
                            {
                                this.animator.SetTrigger(parameter.Name);
                            }
                            break;
                    }
                }
            }
        }

        private void SerializeSynchronizationTypeState(NetworkStream stream)
        {
            byte[] states = new byte[this.synchronizeLayers.Count + this.synchronizeParameters.Count];

            for (int i = 0; i < this.synchronizeLayers.Count; ++i)
            {
                states[i] = (byte)this.synchronizeLayers[i].SynchronizeType;
            }

            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {
                states[this.synchronizeLayers.Count + i] = (byte)this.synchronizeParameters[i].SynchronizeType;
            }

            stream.SendNext(states);
        }

        private void DeserializeSynchronizationTypeState(NetworkStream stream)
        {
            byte[] state = (byte[])stream.ReceiveNext();

            for (int i = 0; i < this.synchronizeLayers.Count; ++i)
            {
                this.synchronizeLayers[i].SynchronizeType = (SynchronizeType)state[i];
            }

            for (int i = 0; i < this.synchronizeParameters.Count; ++i)
            {
                this.synchronizeParameters[i].SynchronizeType = (SynchronizeType)state[this.synchronizeLayers.Count + i];
            }
        }


        public void OnNetSerializeView(NetworkStream stream, NetworkMessageInfo info)
        {
            if (this.animator == null)
            {
                return;
            }

            if (stream.IsWriting == true)
            {
                if (this.wasSynchronizeTypeChanged == true)
                {
                    this.streamQueue.Reset();
                    this.SerializeSynchronizationTypeState(stream);

                    this.wasSynchronizeTypeChanged = false;
                }

                this.streamQueue.Serialize(stream);
                this.SerializeDataDiscretly(stream);
            }
            else
            {

                if (stream.PeekNext() is byte[])
                {
                    this.DeserializeSynchronizationTypeState(stream);
                }

                this.streamQueue.Deserialize(stream);
                this.DeserializeDataDiscretly(stream);

            }
        }
        #endregion
    }
}