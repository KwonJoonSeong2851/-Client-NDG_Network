using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    public abstract class IProtocol
    {
        public readonly ByteArraySlicePool ByteArraySlicePool = new ByteArraySlicePool();

        public abstract string ProtocolType { get; }

        public abstract byte[] VersionBytes { get; }

        public abstract void Serialize(StreamBuffer dout, object serObject, bool setType);

        public abstract void SerializeShort(StreamBuffer dout, short serObject, bool setType);

        public abstract void SerializeString(StreamBuffer dout, string serObject, bool setType);

        public abstract void SerializeEventData(StreamBuffer stream, EventData serObject, bool setType);

        public abstract void SerializeOperationRequest(
          StreamBuffer stream,
          byte operationCode,
          Dictionary<byte, object> parameters,
          bool setType);

        public abstract void SerializeOperationResponse(
          StreamBuffer stream,
          OperationResponse serObject,
          bool setType);

        public abstract object Deserialize(StreamBuffer din, byte type);

        public abstract short DeserializeShort(StreamBuffer din);

        public abstract byte DeserializeByte(StreamBuffer din);

        public abstract EventData DeserializeEventData(
          StreamBuffer din,
          EventData target = null,
          IProtocol.DeserializationFlags flags = IProtocol.DeserializationFlags.None);

        public abstract OperationRequest DeserializeOperationRequest(StreamBuffer din);

        public abstract OperationResponse DeserializeOperationResponse(StreamBuffer stream);

        /// <summary>
        /// 지정된 개체에서 바이트 배열을 생성하고 반환합니다.
        /// </summary>
        public byte[] Serialize(object obj)
        {
            StreamBuffer dout = new StreamBuffer(64);
            this.Serialize(dout, obj, true);
            return dout.ToArray();
        }

        /// <summary>
        /// 지정된 바이트 배열에서 재구성된 개체를 반환합니다.
        /// </summary>
        public object Deserialize(StreamBuffer stream) => this.Deserialize(stream, stream.ReadByte());

        public object Deserialize(byte[] serializedData)
        {
            StreamBuffer din = new StreamBuffer(serializedData);
            return this.Deserialize(din, din.ReadByte());
        }

        public object DeserializeMessage(StreamBuffer stream) => this.Deserialize(stream, stream.ReadByte());

        internal void SerializeMessage(StreamBuffer ms, object msg) => this.Serialize(ms, msg, true);

        public enum DeserializationFlags
        {
            None,
            AllowPooledByteArray,
        }
    }
}
