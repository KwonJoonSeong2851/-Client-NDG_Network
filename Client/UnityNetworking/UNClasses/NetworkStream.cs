


namespace NDG.UnityNet
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    using NDG.Realtime;

    public class NetworkStream 
    {
        private List<object> writeData;
        private object[] readData;
        private int currentItem;

        public bool IsWriting { get; private set; }

        public bool IsReading { get { return !this.IsWriting; } }

        public int Count
        {
            get
            {
                return this.IsWriting ? this.writeData.Count : this.readData.Length;
            }
        }

        public NetworkStream(bool write,object[] incomingData)
        {
            this.IsWriting = write;

            if(!write && incomingData != null)
            {
                this.readData = incomingData;
            }
        }

        public void SetReadStream(object[] incomingData, int pos = 0)
        {
            this.readData = incomingData;
            this.currentItem = pos;
            this.IsWriting = false;
        }

        internal void SetWriteStream(List<object> newWriteData, int pos = 0)
        {
            if(pos != newWriteData.Count)
            {
                throw new System.Exception("SetWriteStream failed");
            }
            this.writeData = newWriteData;
            this.currentItem = pos;
            this.IsWriting = true;
        }

        internal List<object> GetWriteStream()
        {
            return this.writeData;
        }

        public object ReceiveNext()
        {
            if(this.IsWriting)
            {
                Debug.LogError("Steram Error:���� Stream�� writing�����̹Ƿ� read�� �� �����ϴ�.");
                return null;
            }

            object obj = this.readData[this.currentItem];
            this.currentItem++;
            return obj;
        }

        public object PeekNext()
        {
            if(this.IsWriting)
            {
                Debug.Log("Error: 작성 중인 스트림은 읽을 수 없습니다.");
                return null;
            }
            object obj = this.readData[this.currentItem];
            return obj;
        }

        public void SendNext(object obj)
        {
            if(!this.IsWriting)
            {
                Debug.LogError("Stream Error:���� stream�� wrtie/send ���� �̹Ƿ� reading�� �� �����ϴ�.");
                return;
            }

            this.writeData.Add(obj);
        }

        public object[] ToArray()
        {
            return this.IsWriting ? this.writeData.ToArray() : this.readData;
        }

        public void Serialize(ref bool myBool)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(myBool);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    myBool = (bool)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref int myInt)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(myInt);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    myInt = (int)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref string value)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(value);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    value = (string)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref char value)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(value);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    value = (char)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref short value)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(value);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    value = (short)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref float obj)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(obj);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    obj = (float)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref Player obj)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(obj);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    obj = (Player)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }


        public void Serialize(ref Vector3 obj)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(obj);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    obj = (Vector3)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref Vector2 obj)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(obj);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    obj = (Vector2)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }

        public void Serialize(ref Quaternion obj)
        {
            if (this.IsWriting)
            {
                this.writeData.Add(obj);
            }
            else
            {
                if (this.readData.Length > this.currentItem)
                {
                    obj = (Quaternion)this.readData[this.currentItem];
                    this.currentItem++;
                }
            }
        }
    }
}
