using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

//삭제 예정

namespace NDG.AA
{
    public class Protocol
    {
        private readonly byte[] memShort = new byte[2];
        private readonly long[] memLongBlock = new long[1];
        private readonly byte[] memLongBlockBytes = new byte[8];
        private static readonly float[] memFloatBlock = new float[1];
        private static readonly byte[] memFloatBlockBytes = new byte[4];
        private readonly double[] memDoubleBlock = new double[1];
        private readonly byte[] memDoubleBlockBytes = new byte[8];
        private readonly byte[] memInteger = new byte[4];
        private readonly byte[] memLong = new byte[8];
        private readonly byte[] memFloat = new byte[4];
        private readonly byte[] memDouble = new byte[8];
        private byte[] memString;

        private Type GetTypeOfCode(byte typeCode)
        {
            switch (typeCode)
            {
                case 0:
                case 42:
                    return typeof(object);
                case 68:
                    return typeof(IDictionary);
                case 97:
                    return typeof(string[]);
                case 98:
                    return typeof(byte);
                case 100:
                    return typeof(double);
                case 101:
                    return typeof(EventData);
                case 102:
                    return typeof(float);
                case 104:
                    return typeof(Hashtable);
                case 105:
                    return typeof(int);
                case 107:
                    return typeof(short);
                case 108:
                    return typeof(long);
                case 110:
                    return typeof(int[]);
                case 111:
                    return typeof(bool);
                case 112:
                    return typeof(OperationResponse);
                case 113:
                    return typeof(OperationRequest);
                case 115:
                    return typeof(string);
                case 120:
                    return typeof(byte[]);
                case 121:
                    return typeof(Array);
                case 122:
                    return typeof(object[]);
                default:
                    Debug.LogError("missing type: " + typeCode.ToString());
                    return null;
            }
        }


        private Protocol.GpType GetCodeOfType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    return Protocol.GpType.Boolean;
                case TypeCode.Byte:
                    return Protocol.GpType.Byte;
                case TypeCode.Int16:
                    return Protocol.GpType.Short;
                case TypeCode.Int32:
                    return Protocol.GpType.Integer;
                case TypeCode.Int64:
                    return Protocol.GpType.Long;
                case TypeCode.Single:
                    return Protocol.GpType.Float;
                case TypeCode.Double:
                    return Protocol.GpType.Double;
                case TypeCode.String:
                    return Protocol.GpType.String;
                default:
                    if (type.IsArray)
                        return type == typeof(byte[]) ? Protocol.GpType.ByteArray : Protocol.GpType.Array;
                    if (type == typeof(Hashtable))
                        return Protocol.GpType.Hashtable;
                    if (type == typeof(List<object>))
                        return Protocol.GpType.ObjectArray;
                    if (type.IsGenericType && typeof(Dictionary<,>) == type.GetGenericTypeDefinition())
                        return Protocol.GpType.Dictionary;
                    if (type == typeof(EventData))
                        return Protocol.GpType.EventData;
                    if (type == typeof(OperationRequest))
                        return Protocol.GpType.OperationRequest;
                    return type == typeof(OperationResponse) ? Protocol.GpType.OperationResponse : Protocol.GpType.Unknown;
            }
        }

        private void SerializeOperationRequest(MemoryStream stream,OperationRequest serObj,bool setType = true)
        {
            this.SerializeOperationRequest(stream, serObj.OperationCode, serObj.Parameters, setType);
        }

        public void SerializeOperationRequest(MemoryStream stream,byte operationCode,Dictionary<byte,object> parameters,bool setType = true)
        {
            if(setType)
            {
                stream.WriteByte((byte)113);
            }

            stream.WriteByte(operationCode);
            this.SerializeParameterTable(stream, parameters);
        }

        public  OperationRequest DeserializeOperationRequest(MemoryStream din) => new OperationRequest()
        {
            OperationCode = this.DeserializeByte(din),
            Parameters = this.DeserializeParameterTable(din)
        };

        public void SerializeOperationResponse(MemoryStream stream,OperationResponse serObject,bool setType)
        {
            if (setType)
                stream.WriteByte((byte)112);
            stream.WriteByte(serObject.OperationCode);
            this.SerializeShort(stream, serObject.ReturnCode, false);
            if (string.IsNullOrEmpty(serObject.DebugMessage))
                stream.WriteByte((byte)42);
            else
                this.SerializeString(stream, serObject.DebugMessage, false);
            this.SerializeParameterTable(stream, serObject.Parameters);
        }

        public OperationResponse DeserializeOperationResponse(MemoryStream stream)
        {
            return new OperationResponse()
            {
                OperationCode = this.DeserializeByte(stream),
                ReturnCode = this.DeserializeShort(stream),
                DebugMessage = this.Deserialize(stream, this.DeserializeByte(stream)) as string,
                Parameters = this.DeserializeParameterTable(stream)
            };
        }

        public void SerializeEventData(MemoryStream stream,EventData obj,bool setType = true)
        {
            if(setType)
            {
                stream.WriteByte((byte)101);
            }
            stream.WriteByte(obj.Code);
            this.SerializeParameterTable(stream, obj.Parameters);
        }

        public EventData DeserializeEventData(MemoryStream stream,EventData target = null)
        {
            EventData eventData;
            if(target != null)
            {
                target.Reset();
                eventData = target;
            }
            else
            {
                eventData = new EventData();
            }

            eventData.Code = this.DeserializeByte(stream);
            eventData.Parameters = this.DeserializeParameterTable(stream, eventData.Parameters);
            return eventData;
        }

        private void SerializeParameterTable(MemoryStream stream,Dictionary<byte,object> parameters)
        {
            if(parameters == null || parameters.Count == 0)
            {
                this.SerializeShort(stream, (short)0, false);
            }
            else
            {
                this.SerializeShort(stream, (short)parameters.Count, false);
                foreach(KeyValuePair<byte,object> parameter in parameters)
                {
                    stream.WriteByte(parameter.Key);
                    this.Serialize(stream, parameter.Value,true);
                }
            }
        }

        private Dictionary<byte,object> DeserializeParameterTable(MemoryStream stream,Dictionary<byte,object> target = null)
        {
            short num = this.DeserializeShort(stream);
            Dictionary<byte, object> dictionary = target != null ? target : new Dictionary<byte, object>((int)num);
            for (int index = 0; index < (int)num; ++index)
            {
                byte key = (byte)stream.ReadByte();
                object obj = this.Deserialize(stream, (byte)stream.ReadByte());
                dictionary[key] = obj;
            }
            return dictionary;
        }


        //-----------------------Serialize Generic Type-------------------------------------------//

        public void Serialize(MemoryStream stream, object serObject, bool setType)
        {
            if (serObject == null)
            {
                if (!setType)
                    return;
                stream.WriteByte((byte)42);
            }
            else
            {
                switch (this.GetCodeOfType(serObject.GetType()))
                {
                    case Protocol.GpType.Dictionary:
                        this.SerializeDictionary(stream, (IDictionary)serObject, setType);
                        break;
                    case Protocol.GpType.Byte:
                        this.SerializeByte(stream, (byte)serObject, setType);
                        break;
                    case Protocol.GpType.Double:
                        this.SerializeDouble(stream, (double)serObject, setType);
                        break;
                    case Protocol.GpType.EventData:
                        this.SerializeEventData(stream, (EventData)serObject, setType);
                        break;
                    case Protocol.GpType.Float:
                        this.SerializeFloat(stream, (float)serObject, setType);
                        break;
                    case Protocol.GpType.Integer:
                        this.SerializeInteger(stream, (int)serObject, setType);
                        break;
                    case Protocol.GpType.Short: 
                        this.SerializeShort(stream, (short)serObject, setType);
                        break;
                    case Protocol.GpType.Long:
                        this.SerializeLong(stream, (long)serObject, setType);
                        break;
                    case Protocol.GpType.Boolean:
                        this.SerializeBoolean(stream, (bool)serObject, setType);
                        break;
                    case Protocol.GpType.OperationResponse:
                        this.SerializeOperationResponse(stream, (OperationResponse)serObject, setType);
                        break;
                    case Protocol.GpType.OperationRequest:
                        this.SerializeOperationRequest(stream, (OperationRequest)serObject, setType);
                        break;
                    case Protocol.GpType.String:
                        this.SerializeString(stream, (string)serObject, setType);
                        break;
                    case Protocol.GpType.Array:
                        this.SerializeArray(stream, (Array)serObject, setType);
                        break;
                    case Protocol.GpType.Hashtable:
                        this.SerializeHashTable(stream, (Hashtable)serObject, setType);
                        break;

                    default:
                        throw new Exception("cannot serialize" + serObject.GetType().ToString());

                }
            }
        }


        private void SerializeByte(MemoryStream dout, byte serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)98);
            dout.WriteByte(serObject);
        }

        private void SerializeBoolean(MemoryStream dout, bool serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)111);
            dout.WriteByte(serObject ? (byte)1 : (byte)0);
        }

        public  void SerializeShort(MemoryStream dout, short serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)107);
            lock (this.memShort)
            {
                byte[] memShort = this.memShort;
                memShort[0] = (byte)((uint)serObject >> 8);
                memShort[1] = (byte)serObject;
                dout.Write(memShort, 0, 2);
            }
        }

        private void SerializeInteger(MemoryStream dout, int serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)105);
            lock (this.memInteger)
            {
                byte[] memInteger = this.memInteger;
                memInteger[0] = (byte)(serObject >> 24);
                memInteger[1] = (byte)(serObject >> 16);
                memInteger[2] = (byte)(serObject >> 8);
                memInteger[3] = (byte)serObject;
                dout.Write(memInteger, 0, 4);
            }
        }

        private void SerializeLong(MemoryStream dout, long serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)108);
            lock (this.memLongBlock)
            {
                this.memLongBlock[0] = serObject;
                Buffer.BlockCopy((Array)this.memLongBlock, 0, (Array)this.memLongBlockBytes, 0, 8);
                byte[] memLongBlockBytes = this.memLongBlockBytes;
                if (BitConverter.IsLittleEndian)
                {
                    byte num1 = memLongBlockBytes[0];
                    byte num2 = memLongBlockBytes[1];
                    byte num3 = memLongBlockBytes[2];
                    byte num4 = memLongBlockBytes[3];
                    memLongBlockBytes[0] = memLongBlockBytes[7];
                    memLongBlockBytes[1] = memLongBlockBytes[6];
                    memLongBlockBytes[2] = memLongBlockBytes[5];
                    memLongBlockBytes[3] = memLongBlockBytes[4];
                    memLongBlockBytes[4] = num4;
                    memLongBlockBytes[5] = num3;
                    memLongBlockBytes[6] = num2;
                    memLongBlockBytes[7] = num1;
                }
                dout.Write(memLongBlockBytes, 0, 8);
            }
        }

        private void SerializeFloat(MemoryStream dout, float serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)102);
            lock (Protocol.memFloatBlockBytes)
            {
                Protocol.memFloatBlock[0] = serObject;
                Buffer.BlockCopy((Array)Protocol.memFloatBlock, 0, (Array)Protocol.memFloatBlockBytes, 0, 4);
                if (BitConverter.IsLittleEndian)
                {
                    byte memFloatBlockByte1 = Protocol.memFloatBlockBytes[0];
                    byte memFloatBlockByte2 = Protocol.memFloatBlockBytes[1];
                    Protocol.memFloatBlockBytes[0] = Protocol.memFloatBlockBytes[3];
                    Protocol.memFloatBlockBytes[1] = Protocol.memFloatBlockBytes[2];
                    Protocol.memFloatBlockBytes[2] = memFloatBlockByte2;
                    Protocol.memFloatBlockBytes[3] = memFloatBlockByte1;
                }
                dout.Write(Protocol.memFloatBlockBytes, 0, 4);
            }
        }

        private void SerializeDouble(MemoryStream dout, double serObject, bool setType)
        {
            if (setType)
                dout.WriteByte((byte)100);
            lock (this.memDoubleBlockBytes)
            {
                this.memDoubleBlock[0] = serObject;
                Buffer.BlockCopy((Array)this.memDoubleBlock, 0, (Array)this.memDoubleBlockBytes, 0, 8);
                byte[] doubleBlockBytes = this.memDoubleBlockBytes;
                if (BitConverter.IsLittleEndian)
                {
                    byte num1 = doubleBlockBytes[0];
                    byte num2 = doubleBlockBytes[1];
                    byte num3 = doubleBlockBytes[2];
                    byte num4 = doubleBlockBytes[3];
                    doubleBlockBytes[0] = doubleBlockBytes[7];
                    doubleBlockBytes[1] = doubleBlockBytes[6];
                    doubleBlockBytes[2] = doubleBlockBytes[5];
                    doubleBlockBytes[3] = doubleBlockBytes[4];
                    doubleBlockBytes[4] = num4;
                    doubleBlockBytes[5] = num3;
                    doubleBlockBytes[6] = num2;
                    doubleBlockBytes[7] = num1;
                }
                dout.Write(doubleBlockBytes, 0, 8);
            }
        }

        public  void SerializeString(MemoryStream stream, string value, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)115);
            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > (int)short.MaxValue)
                throw new NotSupportedException("Strings that exceed a UTF8-encoded byte-length of 32767 (short.MaxValue) are not supported. Yours is: " + byteCount.ToString());
            this.SerializeShort(stream, (short)byteCount, false);
            int offset = 0;
            offset = (int)stream.Position;
            stream.Position += byteCount;
            byte[] bufferAndAdvance = stream.ToArray();
            Encoding.UTF8.GetBytes(value, 0, value.Length, bufferAndAdvance, offset);
        }

        private void SerializeArray(MemoryStream stream,Array serObject, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)121);

            this.SerializeShort(stream, (short)serObject.Length, false);

            Type elementType = serObject.GetType().GetElementType();
            Protocol.GpType codeOfType = this.GetCodeOfType(elementType);

            if ((uint)codeOfType > 0U)
            {
                stream.WriteByte((byte)codeOfType);

                if (codeOfType == Protocol.GpType.Dictionary)
                    throw new NotSupportedException("Dictionary array is not supported");

                for (int index = 0; index < serObject.Length; ++index)
                {
                    object serObject1 = serObject.GetValue(index);
                    this.Serialize(stream, serObject1, false);
                }
            }
            else
                throw new NotFiniteNumberException("CustomType array is not supported");
        }

        private void SerializeHashTable(MemoryStream stream, Hashtable serObject,bool setType)
        {
            if (setType)
                stream.WriteByte((byte)104);

            this.SerializeShort(stream, (short)serObject.Count, false);

            foreach(object key in serObject.Keys)
            {
                this.Serialize(stream, key, true);
                this.Serialize(stream, serObject[key], true);
            }
        }

        private void SerializeDictionary(MemoryStream stream, IDictionary serObject, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)68);
            bool setKeyType;
            bool setValueType;
            this.SerializeDictionaryHeader(stream, (object)serObject, out setKeyType, out setValueType);
            this.SerializeDictionaryElements(stream, (object)serObject, setKeyType, setValueType);
        }



        private void SerializeDictionaryHeader(MemoryStream writer,object dict,out bool setKeyType,out bool setValueType)
        {
            Type[] genericArguments = dict.GetType().GetGenericArguments();
            setKeyType = genericArguments[0] == typeof(object);
            setValueType = genericArguments[1] == typeof(object);
            if (setKeyType)
            {
                writer.WriteByte((byte)0);
            }
            else
            {
                Protocol.GpType codeOfType = this.GetCodeOfType(genericArguments[0]);
                if (codeOfType == Protocol.GpType.Unknown || codeOfType == Protocol.GpType.Dictionary)
                    throw new Exception("Unexpected - cannot serialize Dictionary with key type: " + genericArguments[0]?.ToString());
                writer.WriteByte((byte)codeOfType);
            }
            if (setValueType)
            {
                writer.WriteByte((byte)0);
            }
            else
            {
                Protocol.GpType codeOfType = this.GetCodeOfType(genericArguments[1]);
                if (codeOfType == Protocol.GpType.Unknown)
                    throw new Exception("Unexpected - cannot serialize Dictionary with value type: " + genericArguments[0]?.ToString());
                writer.WriteByte((byte)codeOfType);
                if (codeOfType == Protocol.GpType.Dictionary)
                    this.SerializeDictionaryHeader(writer, genericArguments[1]);
            }
        }

        private void SerializeDictionaryHeader(MemoryStream writer, Type dictType) => this.SerializeDictionaryHeader(writer, (object)dictType, out bool _, out bool _);

        private void SerializeDictionaryElements(MemoryStream stream,object dict,bool setKeyType,bool setValueType)
        {
            IDictionary dictionary = (IDictionary)dict;
            this.SerializeShort(stream, (short)dictionary.Count, false);
            foreach (DictionaryEntry dictionaryEntry in dictionary)
            {
                if (!setValueType && dictionaryEntry.Value == null)
                    throw new Exception("Can't serialize null in Dictionary with specific value-type.");
                if (!setKeyType && dictionaryEntry.Key == null)
                    throw new Exception("Can't serialize null in Dictionary with specific key-type.");
                this.Serialize(stream, dictionaryEntry.Key, setKeyType);
                this.Serialize(stream, dictionaryEntry.Value, setValueType);
            }
        }



        //===============================Serialize Generic Type End===================================//




        //================================Deserialize Type Start=====================================//

        public  object Deserialize(MemoryStream stream, byte type)
        {
            switch (type)
            {
                case 0:
                case 42:
                    return (object)null;
                case 68:
                    return (object)this.DeserializeDictionary(stream);
                case 98:
                    return (object)this.DeserializeByte(stream);
                case 100:
                    return (object)this.DeserializeDouble(stream);
                case 101:
                    return (object)this.DeserializeEventData(stream, (EventData)null);
                case 102:
                    return (object)this.DeserializeFloat(stream);
                case 104:
                    return (object)this.DeserializeHashtable(stream);
                case 105:
                    return (object)this.DeserializeInteger(stream);
                case 107:
                    return (object)this.DeserializeShort(stream);
                case 108:
                    return (object)this.DeserializeLong(stream);
                case 110:
                    return (object)this.DeserializeBoolean(stream);
                case 112:
                    return (object)this.DeserializeOperationResponse(stream);
                case 113:
                    return (object)this.DeserializeOperationRequest(stream);
                case 115:
                    return (object)this.DeserializeString(stream);

                case 121:
                    return (object)this.DeserializeArray(stream);


                default:
                    throw new Exception("Deserialize()");
            }
        }

        public byte DeserializeByte(MemoryStream stream) => (byte)stream.ReadByte();

        private bool DeserializeBoolean(MemoryStream stream) => (byte)stream.ReadByte() > (byte)0;

        public short DeserializeShort(MemoryStream stream)
        {
            lock(this.memShort)
            {
                byte[] memShort = this.memShort;
                stream.Read(memShort, 0, 2);
                return (short)((int)memShort[0] << 8 | (int)memShort[1]);
            }
        }


        private int DeserializeInteger(MemoryStream din)
        {
            lock (this.memInteger)
            {
                byte[] memInteger = this.memInteger;
                din.Read(memInteger, 0, 4);
                return (int)memInteger[0] << 24 | (int)memInteger[1] << 16 | (int)memInteger[2] << 8 | (int)memInteger[3];
            }
        }

        private long DeserializeLong(MemoryStream din)
        {
            lock (this.memLong)
            {
                byte[] memLong = this.memLong;
                din.Read(memLong, 0, 8);
                return BitConverter.IsLittleEndian ? (long)memLong[0] << 56 | (long)memLong[1] << 48 | (long)memLong[2] << 40 | (long)memLong[3] << 32 | (long)memLong[4] << 24 | (long)memLong[5] << 16 | (long)memLong[6] << 8 | (long)memLong[7] : BitConverter.ToInt64(memLong, 0);
            }
        }

        private float DeserializeFloat(MemoryStream din)
        {
            lock (this.memFloat)
            {
                byte[] memFloat = this.memFloat;
                din.Read(memFloat, 0, 4);
                if (BitConverter.IsLittleEndian)
                {
                    byte num1 = memFloat[0];
                    byte num2 = memFloat[1];
                    memFloat[0] = memFloat[3];
                    memFloat[1] = memFloat[2];
                    memFloat[2] = num2;
                    memFloat[3] = num1;
                }
                return BitConverter.ToSingle(memFloat, 0);
            }
        }

        private double DeserializeDouble(MemoryStream din)
        {
            lock (this.memDouble)
            {
                byte[] memDouble = this.memDouble;
                din.Read(memDouble, 0, 8);
                if (BitConverter.IsLittleEndian)
                {
                    byte num1 = memDouble[0];
                    byte num2 = memDouble[1];
                    byte num3 = memDouble[2];
                    byte num4 = memDouble[3];
                    memDouble[0] = memDouble[7];
                    memDouble[1] = memDouble[6];
                    memDouble[2] = memDouble[5];
                    memDouble[3] = memDouble[4];
                    memDouble[4] = num4;
                    memDouble[5] = num3;
                    memDouble[6] = num2;
                    memDouble[7] = num1;
                }
                return BitConverter.ToDouble(memDouble, 0);
            }
        }

        private string DeserializeString(MemoryStream din)
        {
            short num = this.DeserializeShort(din);
            if (num == (short)0)
                return string.Empty;
            if (this.memString == null || this.memString.Length < (int)num)
                this.memString = new byte[(int)num];
            din.Read(this.memString, 0, (int)num);
            return Encoding.UTF8.GetString(this.memString, 0, (int)num);
        }

        private Array DeserializeArray(MemoryStream din)
        {
            short length = this.DeserializeShort(din);
            byte elementsType =(byte)din.ReadByte();

            Array retVal;

            switch(elementsType)
            {
                default:
                    retVal = this.CreateArrayByType(elementsType, length);
                    for(short index = 0; (int) index < (int) length; ++index)
                    {
                        retVal.SetValue(this.Deserialize(din, elementsType), (int)index);
                    }
                    break;
            }

            return retVal;
        }

        private Array CreateArrayByType(byte arrayType, short length)
            => Array.CreateInstance(this.GetTypeOfCode(arrayType), (int)length);

        private IDictionary DeserializeDictionary(MemoryStream din)
        {
            byte typeCode1 = (byte)din.ReadByte();
            byte typeCode2 = (byte)din.ReadByte();
            int num = (int)this.DeserializeShort(din);
            bool flag1 = typeCode1 == (byte)0 || typeCode1 == (byte)42;
            bool flag2 = typeCode2 == (byte)0 || typeCode2 == (byte)42;
            IDictionary instance = Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(this.GetTypeOfCode(typeCode1), this.GetTypeOfCode(typeCode2))) as IDictionary;
            for (int index = 0; index < num; ++index)
            {
                object key = this.Deserialize(din, flag1 ? (byte)din.ReadByte() : typeCode1);
                object obj = this.Deserialize(din, flag2 ? (byte)din.ReadByte() : typeCode2);
                if (key != null)
                    instance.Add(key, obj);
            } 
            return instance;
        }

        private Type DeserializeDictionaryType(MemoryStream reader,out byte keyTypeCode,out byte valTypeCode)
        {
            keyTypeCode = (byte)reader.ReadByte();
            valTypeCode = (byte)reader.ReadByte();
            Protocol.GpType gpType1 = (Protocol.GpType)keyTypeCode;
            Protocol.GpType gpType2 = (Protocol.GpType)valTypeCode;
            return typeof(Dictionary<,>).MakeGenericType(gpType1 != Protocol.GpType.Unknown ? this.GetTypeOfCode(keyTypeCode) : typeof(object), gpType2 != Protocol.GpType.Unknown ? this.GetTypeOfCode(valTypeCode) : typeof(object));
        }

        private Hashtable DeserializeHashtable(MemoryStream stream)
        {
            int len = (int)this.DeserializeShort(stream);
            Hashtable hashtable = new Hashtable(len);
            for(int index = 0; index < len; ++index)
            {
                object key = this.Deserialize(stream, (byte)stream.ReadByte());
                object obj = this.Deserialize(stream, (byte)stream.ReadByte());
                if (key != null)
                    hashtable[key] = obj;
            }
            return hashtable;
        }






        public enum GpType : byte
        {
            Unknown = 0,
            Null = 42, // 0x2A
            Dictionary = 68, // 0x44
            StringArray = 97, // 0x61
            Byte = 98, // 0x62
            Custom = 99, // 0x63
            Double = 100, // 0x64
            EventData = 101, // 0x65
            Float = 102, // 0x66
            Hashtable = 104, // 0x68
            Integer = 105, // 0x69
            Short = 107, // 0x6B
            Long = 108, // 0x6C
            IntegerArray = 110, // 0x6E
            Boolean = 111, // 0x6F
            OperationResponse = 112, // 0x70
            OperationRequest = 113, // 0x71
            String = 115, // 0x73
            ByteArray = 120, // 0x78
            Array = 121, // 0x79
            ObjectArray = 122, // 0x7A
        }
    }





}
