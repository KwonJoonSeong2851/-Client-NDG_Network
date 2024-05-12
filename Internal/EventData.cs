using System.Collections;
using System.Collections.Generic;



namespace NDG
{
    public class EventData
    {

        public byte Code;
        public Dictionary<byte, object> Parameters;

        private int sender = -1;
        public byte SenderKey = 254;

        public byte CustomDataKey = 245;
        private object customData;

        public int Sender
        {
            get
            {
                if(this.sender == -1)
                {
                    object obj = this[this.SenderKey];
                    this.sender = obj != null ? (int)obj : -1;
                }
                return this.sender;
            }
            internal set => this.sender = value;
        }

        public object this[byte key] 
        {
            get
            {
                if(this.Parameters == null)
                {
                    return (object)null;
                }
                object obj;
                this.Parameters.TryGetValue(key, out obj);
                return obj;
            }

            internal set
            {
                if(this.Parameters == null)
                {
                    this.Parameters = new Dictionary<byte, object>();
                }
                this.Parameters[key] = value;
            }
        }

        public object CustomData
        {
            get
            {
                if (this.customData == null)
                    this.customData = this[this.CustomDataKey];
                return this.customData;
            }
            internal set => this.customData = value;
        }


        internal void Reset()
        {
            this.Code = (byte)0;
            this.Parameters.Clear();
            this.sender = -1;
        }
    }
}
