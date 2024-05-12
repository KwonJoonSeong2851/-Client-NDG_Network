using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    /// <summary>
    /// customType 직렬화 메서드 유형으로 커스텀 타입 지원을 추가합니다.
    /// 새 타입을 추가하려면 Peer.RegisterType()을 사용합니다.
    /// </summary>
    public delegate byte[] SerializeMethod(object customObject);

    /// <summary>
    /// customType 역직렬화 메서드 유형으로 커스텀 타입 지원을 추가합니다.
    /// 새 타입을 추가하려면 Peer.RegisterType()을 사용합니다.
    /// </summary>
    public delegate object DeserializeMethod(byte[] serializedCustomObject);

    /// <summary>
    /// 직렬화 메서드 델리게이트. 커스텀 타입 직렬화 메서드는 이 형식을 사용해야합니다.
    /// </summary>
    public delegate short SerializeStreamMethod(StreamBuffer outStream, object customObject);

    /// <summary>
    /// 역직렬화 메서드 델리게이트. 커스텀 타입 역직렬화 메서드는 이 형식을 사용해야합니다.
    /// </summary>
    public delegate object DeserializeStreamMethod(StreamBuffer inStream, short length);


    internal class CustomType
    {
        public readonly byte Code;
        public readonly Type Type;
        public readonly SerializeMethod SerializeFunction;
        public readonly DeserializeMethod DeserializeFunction;
        public readonly SerializeStreamMethod SerializeStreamFunction;
        public readonly DeserializeStreamMethod DeserializeStreamFunction;

        public CustomType(
          Type type,
          byte code,
          SerializeMethod serializeFunction,
          DeserializeMethod deserializeFunction)
        {
            this.Type = type;
            this.Code = code;
            this.SerializeFunction = serializeFunction;
            this.DeserializeFunction = deserializeFunction;
        }

        public CustomType(
          Type type,
          byte code,
          SerializeStreamMethod serializeFunction,
          DeserializeStreamMethod deserializeFunction)
        {
            this.Type = type;
            this.Code = code;
            this.SerializeStreamFunction = serializeFunction;
            this.DeserializeStreamFunction = deserializeFunction;
        }
    }
}
