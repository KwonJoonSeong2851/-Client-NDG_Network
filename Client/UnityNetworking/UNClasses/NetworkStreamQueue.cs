namespace NDG.UnityNet
{
    using System.Collections.Generic;
    using UnityEngine;


    /// <summary>
    /// 오브젝트의 상태를 폴링하는데 도움을 주는 클래스입니다.
    /// NDG_Network.SendRate가 지정한것보다 더 높은 빈도로 오브젝트 상태를 폴링한다음
    /// Serialize()가 호출될 떄 한번 전송합니다.
    /// 수산 측에서 Deserialize()를 호출하면 stream이 롤아웃 됩니다.
    /// </summary>
    public class NetworkStreamQueue
    {
        private int sampleRate;
        private int sampleCount;
        private int objectsPerSample = -1;
        private float lastSampleTime = -Mathf.Infinity;

        private int lastFrameCount = -1;
        private int nextObjectIndex = -1;

        private List<object> objects = new List<object>();

        private bool isWriting;

        public NetworkStreamQueue(int sampleRate)
        {
            this.sampleRate = sampleRate;
        }

        private void BeginWritePackage()
        {
            //마지막 sample 이후 충분한 시간이 지나지 않았다면 return합니다.
            if (Time.realtimeSinceStartup < this.lastSampleTime + 1f / this.sampleRate)
            {
                this.isWriting = false;
                return;
            }

            if (this.sampleCount == 1)
            {
                this.objectsPerSample = this.objects.Count;
            }
            else if (this.sampleCount > 1)
            {
                if (this.objects.Count / this.sampleCount != this.objectsPerSample)
                {
                    Debug.LogWarning("NetworkStreamQueue를 통해 전송되는 오브젝트 수는 매 프레임마다 동일해야 합니다.");
                    Debug.LogWarning("Objects in List: " + this.objects.Count + " / Sample Count: " + this.sampleCount + " = " + this.objects.Count / this.sampleCount + " != " + this.objectsPerSample);
                }
            }

            this.isWriting = true;
            this.sampleCount++;
            this.lastSampleTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// StreamQueue를 리셋합니다.
        /// 관찰중인 오브젝트의 양이 변경될 떄마다 이 작업을 수행해야합니다.
        /// </summary>
        public void Reset()
        {
            this.sampleCount = 0;
            this.objectsPerSample = -1;
            this.lastSampleTime = -Mathf.Infinity;
            this.lastFrameCount = -1;
            this.objects.Clear();
        }


        /// <summary>
        /// 다음 오브젝트를 대기열에 추가합니다.
        /// </summary>
        /// <param name="obj"></param>
        public void SendNext(object obj)
        {
            if (Time.frameCount != this.lastFrameCount)
            {
                this.BeginWritePackage();
            }
            this.lastFrameCount = Time.frameCount;

            if (this.isWriting == false)
                return;

            this.objects.Add(obj);
        }


        /// <summary>
        /// Queue에 Object가 저장되어 있는지 확인합니다.
        /// </summary>
        /// <returns></returns>
        public bool HasQueuedObjects()
        {
            return this.nextObjectIndex != -1;
        }


        /// <summary>
        /// Queue에서 다음 Object를 수신합니다. 
        /// </summary>
        /// <returns></returns>
        public object ReceiveNext()
        {
            if (this.nextObjectIndex == -1)
            {
                return null;
            }

            if (this.nextObjectIndex >= this.objects.Count)
            {
                this.nextObjectIndex -= this.objectsPerSample;
            }

            return this.objects[this.nextObjectIndex++];
        }


        /// <summary>
        ///stream을 직렬화합니다. 
        ///녹화하고있는 stream 전체를 전송하려면 OnNetworkSerializeView 메서드에서 이 함수를 호출해야합니다. 
        /// </summary>
        /// <param name="stream"></param>
        public void Serialize(NetworkStream stream)
        {
            if (this.objects.Count > 0 && this.objectsPerSample < 0)
            {
                this.objectsPerSample = this.objects.Count;
            }

            stream.SendNext(this.sampleCount);
            stream.SendNext(this.objectsPerSample);

            for (int i = 0; i < this.objects.Count; ++i)
            {
                stream.SendNext(this.objects[i]);
            }
            this.objects.Clear();
            this.sampleCount = 0;
        }


        /// <summary>
        /// stream을 역직렬화합니다.
        /// 녹화하고있는 stream 전체를 수신하려면 OnNetworkDeserializeView 메서드에서 이 함수를 호출해야합니다.
        /// </summary>
        /// <param name="stream"></param>
        public void Deserialize(NetworkStream stream)
        {
            this.objects.Clear();

            this.sampleCount = (int)stream.ReceiveNext();
            this.objectsPerSample = (int)stream.ReceiveNext();

            for (int i = 0; i < this.sampleCount * this.objectsPerSample; ++i)
            {
                this.objects.Add(stream.ReceiveNext());
            }

            if (this.objects.Count > 0)
            {
                this.nextObjectIndex = 0;
            }
            else
            {
                this.nextObjectIndex = -1;
            }

        }

    }

}