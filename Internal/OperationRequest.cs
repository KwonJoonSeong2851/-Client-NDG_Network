

namespace NDG
{
    using System.Collections.Generic;


    public class OperationRequest
    {
        public byte OperationCode;
        public Dictionary<byte, object> Parameters;
    }

    public class OperationResponse
    {
        public byte OperationCode;
        public short ReturnCode;
        public string DebugMessage;

        public Dictionary<byte, object> Parameters;

        public object this[byte parameterCode]
        {
            get
            {
                object obj;
                this.Parameters.TryGetValue(parameterCode, out obj);
                return obj;
            }

            set => this.Parameters[parameterCode] = value;
        }

    }
}