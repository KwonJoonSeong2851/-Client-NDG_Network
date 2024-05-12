using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    internal static class SerializationProtocolFactory
    {
        internal static IProtocol Create(SerializationProtocol serializationProtocol)
        {
            //  return serializationProtocol == SerializationProtocol.GpBinaryV18 ? (IProtocol)new Protocol18() : (IProtocol)new Protocol16();
            return (IProtocol)new Protocol18();
        }
    }
}
