using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NDG
{
    public class Protocol18 : IProtocol
    {
        private readonly byte[] versionBytes = new byte[2]
        {
            (byte)1,
            (byte)8
        };
        private static readonly byte[] boolMasks = new byte[8]
        {
            (byte) 1,
            (byte) 2,
            (byte) 4,
            (byte) 8,
            (byte) 16,
            (byte) 32,
            (byte) 64,
            (byte) 128
        };

        private readonly double[] memDoubleBlock = new double[1];
        private readonly float[] memFloatBlock = new float[1];
        private readonly byte[] memCustomTypeBodyLengthSerialized = new byte[5];
        private readonly byte[] memCompressedUInt32 = new byte[5];
        private byte[] memCompressedUInt64 = new byte[10];

        public override string ProtocolType => "GpBinaryV18";

        public override byte[] VersionBytes => this.versionBytes;

        public override void Serialize(StreamBuffer dout, object serObject, bool setType)
        {
            this.Write(dout, serObject, setType);
        }

        public override void SerializeShort(StreamBuffer dout, short serObject, bool setType)
        {
            this.WriteInt16(dout, serObject, setType);
        }

        public override void SerializeString(StreamBuffer dout, string serObject, bool setType)
        {
            this.WriteString(dout, serObject, setType);
        }

        public override object Deserialize(StreamBuffer din, byte type)
        {
            return this.Read(din, type);
        }

        public override short DeserializeShort(StreamBuffer din)
        {
            return this.ReadInt16(din);
        }

        public override byte DeserializeByte(StreamBuffer din)
        {
            return this.ReadByte(din);
        }

        private static Type GetAllowedDictionaryKeyTypes(Protocol18.GpType gpType)
        {
            switch (gpType)
            {
                case Protocol18.GpType.Byte:
                case Protocol18.GpType.ByteZero:
                    return typeof(byte);
                case Protocol18.GpType.Short:
                case Protocol18.GpType.ShortZero:
                    return typeof(short);
                case Protocol18.GpType.Float:
                case Protocol18.GpType.FloatZero:
                    return typeof(float);
                case Protocol18.GpType.Double:
                case Protocol18.GpType.DoubleZero:
                    return typeof(double);
                case Protocol18.GpType.String:
                    return typeof(string);
                case Protocol18.GpType.CompressedInt:
                case Protocol18.GpType.Int1:
                case Protocol18.GpType.Int1_:
                case Protocol18.GpType.Int2:
                case Protocol18.GpType.Int2_:
                case Protocol18.GpType.IntZero:
                    return typeof(int);
                case Protocol18.GpType.CompressedLong:
                case Protocol18.GpType.L1:
                case Protocol18.GpType.L1_:
                case Protocol18.GpType.L2:
                case Protocol18.GpType.L2_:
                case Protocol18.GpType.LongZero:
                    return typeof(long);
                default:
                    throw new Exception(string.Format("{0} is not a valid value type.", (object)gpType));
            }
        }


        private static Type GetClrArrayType(Protocol18.GpType gpType)
        {
            switch (gpType)
            {
                case Protocol18.GpType.Boolean:
                case Protocol18.GpType.BooleanFalse:
                case Protocol18.GpType.BooleanTrue:
                    return typeof(bool);
                case Protocol18.GpType.Byte:
                case Protocol18.GpType.ByteZero:
                    return typeof(byte);
                case Protocol18.GpType.Short:
                case Protocol18.GpType.ShortZero:
                    return typeof(short);
                case Protocol18.GpType.Float:
                case Protocol18.GpType.FloatZero:
                    return typeof(float);
                case Protocol18.GpType.Double:
                case Protocol18.GpType.DoubleZero:
                    return typeof(double);
                case Protocol18.GpType.String:
                    return typeof(string);
                case Protocol18.GpType.CompressedInt:
                case Protocol18.GpType.Int1:
                case Protocol18.GpType.Int1_:
                case Protocol18.GpType.Int2:
                case Protocol18.GpType.Int2_:
                case Protocol18.GpType.IntZero:
                    return typeof(int);
                case Protocol18.GpType.CompressedLong:
                case Protocol18.GpType.L1:
                case Protocol18.GpType.L1_:
                case Protocol18.GpType.L2:
                case Protocol18.GpType.L2_:
                case Protocol18.GpType.LongZero:
                    return typeof(long);
                case Protocol18.GpType.Hashtable:
                    return typeof(Hashtable);
                case Protocol18.GpType.OperationRequest:
                    return typeof(OperationRequest);
                case Protocol18.GpType.OperationResponse:
                    return typeof(OperationResponse);
                case Protocol18.GpType.EventData:
                    return typeof(EventData);
                case Protocol18.GpType.BooleanArray:
                    return typeof(bool[]);
                case Protocol18.GpType.ByteArray:
                    return typeof(byte[]);
                case Protocol18.GpType.ShortArray:
                    return typeof(short[]);
                case Protocol18.GpType.FloatArray:
                    return typeof(float[]);
                case Protocol18.GpType.DoubleArray:
                    return typeof(double[]);
                case Protocol18.GpType.StringArray:
                    return typeof(string[]);
                case Protocol18.GpType.CompressedIntArray:
                    return typeof(int[]);
                case Protocol18.GpType.CompressedLongArray:
                    return typeof(long[]);
                case Protocol18.GpType.HashtableArray:
                    return typeof(Hashtable[]);
                default:
                    return (Type)null;
            }
        }


        private Protocol18.GpType GetCodeOfType(Type type)
        {
            if (type == null)
                return Protocol18.GpType.Null;
            if (type.IsPrimitive || type.IsEnum)
                return this.GetCodeOfTypeCode(Type.GetTypeCode(type));
            if (type == typeof(string))
                return Protocol18.GpType.String;
            if (type.IsArray)
            {
                Type elementType = type.GetElementType();
                if (elementType == null)
                    throw new InvalidDataException(string.Format("Arrays of type {0} are not supported", (object)type));
                if (elementType.IsPrimitive)
                {
                    switch (Type.GetTypeCode(elementType))
                    {
                        case TypeCode.Boolean:
                            return Protocol18.GpType.BooleanArray;
                        case TypeCode.Byte:
                            return Protocol18.GpType.ByteArray;
                        case TypeCode.Int16:
                            return Protocol18.GpType.ShortArray;
                        case TypeCode.Int32:
                            return Protocol18.GpType.CompressedIntArray;
                        case TypeCode.Int64:
                            return Protocol18.GpType.CompressedLongArray;
                        case TypeCode.Single:
                            return Protocol18.GpType.FloatArray;
                        case TypeCode.Double:
                            return Protocol18.GpType.DoubleArray;
                    }
                }
                if (elementType.IsArray)
                    return Protocol18.GpType.Array;
                if (elementType == typeof(string))
                    return Protocol18.GpType.StringArray;
                if (elementType == typeof(object))
                    return Protocol18.GpType.ObjectArray;
                if (elementType == typeof(Hashtable))
                    return Protocol18.GpType.HashtableArray;
                return elementType.IsGenericType ? Protocol18.GpType.DictionaryArray : Protocol18.GpType.CustomTypeArray;
            }
            if (type == typeof(Hashtable))
                return Protocol18.GpType.Hashtable;
            if (type == typeof(List<object>))
                return Protocol18.GpType.ObjectArray;
            if (type.IsGenericType && typeof(Dictionary<,>) == type.GetGenericTypeDefinition())
                return Protocol18.GpType.Dictionary;
            if (type == typeof(EventData))
                return Protocol18.GpType.EventData;
            if (type == typeof(OperationRequest))
                return Protocol18.GpType.OperationRequest;
            return type == typeof(OperationResponse) ? Protocol18.GpType.OperationResponse : Protocol18.GpType.Unknown;
        }


        private Protocol18.GpType GetCodeOfTypeCode(TypeCode type)
        {
            switch (type)
            {
                case TypeCode.Boolean:
                    return Protocol18.GpType.Boolean;
                case TypeCode.Byte:
                    return Protocol18.GpType.Byte;
                case TypeCode.Int16:
                    return Protocol18.GpType.Short;
                case TypeCode.Int32:
                    return Protocol18.GpType.CompressedInt;
                case TypeCode.Int64:
                    return Protocol18.GpType.CompressedLong;
                case TypeCode.Single:
                    return Protocol18.GpType.Float;
                case TypeCode.Double:
                    return Protocol18.GpType.Double;
                case TypeCode.String:
                    return Protocol18.GpType.String;
                default:
                    return Protocol18.GpType.Unknown;
            }
        }


        private object Read(StreamBuffer stream)
        {
            return this.Read(stream, this.ReadByte(stream));
        }

        private object Read(StreamBuffer stream, byte gpType)
        {
            if (gpType >= (byte)128 && gpType <= (byte)228)
                return this.ReadCustomType(stream, gpType);
            switch (gpType)
            {
                case 2:
                    return (object)this.ReadBoolean(stream);
                case 3:
                    return (object)this.ReadByte(stream);
                case 4:
                    return (object)this.ReadInt16(stream);
                case 5:
                    return (object)this.ReadSingle(stream);
                case 6:
                    return (object)this.ReadDouble(stream);
                case 7:
                    return (object)this.ReadString(stream);
                case 9:
                    return (object)this.ReadCompressedInt32(stream);
                case 10:
                    return (object)this.ReadCompressedInt64(stream);
                case 11:
                    return (object)this.ReadInt1(stream, false);
                case 12:
                    return (object)this.ReadInt1(stream, true);
                case 13:
                    return (object)this.ReadInt2(stream, false);
                case 14:
                    return (object)this.ReadInt2(stream, true);
                case 15:
                    return (object)(long)this.ReadInt1(stream, false); //
                case 16:
                    return (object)(long)this.ReadInt1(stream, true); //
                case 17:
                    return (object)(long)this.ReadInt2(stream, false); //
                case 18:
                    return (object)(long)this.ReadInt2(stream, true); //
                case 19:
                    return this.ReadCustomType(stream); //
                case 20:
                    return (object)this.ReadDictionary(stream);
                case 21:
                    return (object)this.ReadHashtable(stream);
                case 23:
                    return (object)this.ReadObjectArray(stream); //
                case 24:
                    return (object)this.DeserializeOperationRequest(stream);
                case 25:
                    return (object)this.DeserializeOperationResponse(stream);
                case 26:
                    return (object)this.DeserializeEventData(stream, (EventData)null, IProtocol.DeserializationFlags.None);
                case 27:
                    return (object)false;
                case 28:
                    return (object)true;
                case 29:
                    return (object)(short)0;
                case 30:
                    return (object)0;
                case 31:
                    return (object)0L;
                case 32:
                    return (object)0.0f;
                case 33:
                    return (object)0.0;
                case 34:
                    return (object)(byte)0;
                case 64:
                    return (object)this.ReadArrayInArray(stream);
                case 66:
                    return (object)this.ReadBooleanArray(stream);
                case 67:
                    return (object)this.ReadByteArray(stream);
                case 68:
                    return (object)this.ReadInt16Array(stream);
                case 69:
                    return (object)this.ReadSingleArray(stream);
                case 70:
                    return (object)this.ReadDoubleArray(stream);
                case 71:
                    return (object)this.ReadStringArray(stream);
                case 73:
                    return (object)this.ReadCompressedInt32Array(stream);
                case 74:
                    return (object)this.ReadCompressedInt64Array(stream);
                case 83:
                    return this.ReadCustomTypeArray(stream);
                case 84:
                    return (object)this.ReadDictionaryArray(stream);
                case 85:
                    return (object)this.ReadHashtableArray(stream);
                default:
                    return (object)null;
            }
        }


        internal bool ReadBoolean(StreamBuffer stream) => stream.ReadByte() > (byte)0;

        internal byte ReadByte(StreamBuffer stream) => stream.ReadByte();

        internal short ReadInt16(StreamBuffer stream)
        {
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(2, out offset);
            byte[] numArray = bufferAndAdvance;
            int index1 = offset;
            int index2 = index1 + 1;
            return (short)((int)numArray[index1] | (int)bufferAndAdvance[index2] << 8);
        }

        internal ushort ReadUShort(StreamBuffer stream)
        {
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(2, out offset);
            byte[] numArray = bufferAndAdvance;
            int index1 = offset;
            int index2 = index1 + 1;
            return (ushort)((uint)numArray[index1] | (uint)bufferAndAdvance[index2] << 8);
        }

        internal int ReadInt32(StreamBuffer stream)
        {
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(4, out offset);
            byte[] numArray1 = bufferAndAdvance;
            int index1 = offset;
            int num1 = index1 + 1;
            int num2 = (int)numArray1[index1] << 24;
            byte[] numArray2 = bufferAndAdvance;
            int index2 = num1;
            int num3 = index2 + 1;
            int num4 = (int)numArray2[index2] << 16;
            int num5 = num2 | num4;
            byte[] numArray3 = bufferAndAdvance;
            int index3 = num3;
            int index4 = index3 + 1;
            int num6 = (int)numArray3[index3] << 8;
            return num5 | num6 | (int)bufferAndAdvance[index4];
        }



        internal long ReadInt64(StreamBuffer stream)
        {
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(4, out offset);
            byte[] numArray1 = bufferAndAdvance;
            int index1 = offset;
            int num1 = index1 + 1;
            long num2 = (long)numArray1[index1] << 56;
            byte[] numArray2 = bufferAndAdvance;
            int index2 = num1;
            int num3 = index2 + 1;
            long num4 = (long)numArray2[index2] << 48;
            long num5 = num2 | num4;
            byte[] numArray3 = bufferAndAdvance;
            int index3 = num3;
            int num6 = index3 + 1;
            long num7 = (long)numArray3[index3] << 40;
            long num8 = num5 | num7;
            byte[] numArray4 = bufferAndAdvance;
            int index4 = num6;
            int num9 = index4 + 1;
            long num10 = (long)numArray4[index4] << 32;
            long num11 = num8 | num10;
            byte[] numArray5 = bufferAndAdvance;
            int index5 = num9;
            int num12 = index5 + 1;
            long num13 = (long)numArray5[index5] << 24;
            long num14 = num11 | num13;
            byte[] numArray6 = bufferAndAdvance;
            int index6 = num12;
            int num15 = index6 + 1;
            long num16 = (long)numArray6[index6] << 16;
            long num17 = num14 | num16;
            byte[] numArray7 = bufferAndAdvance;
            int index7 = num15;
            int index8 = index7 + 1;
            long num18 = (long)numArray7[index7] << 8;
            return num17 | num18 | (long)bufferAndAdvance[index8];
        }


        internal float ReadSingle(StreamBuffer stream)
        {
            int offset;
            return BitConverter.ToSingle(stream.GetBufferAndAdvance(4, out offset), offset);
        }

        internal double ReadDouble(StreamBuffer stream)
        {
            int offset;
            return BitConverter.ToDouble(stream.GetBufferAndAdvance(8, out offset), offset);
        }

        internal ByteArraySlice ReadNonAllocByteArray(StreamBuffer stream)
        {
            uint num = this.ReadCompressedUInt32(stream);
            ByteArraySlice byteArraySlice = this.ByteArraySlicePool.Acquire((int)num);
            stream.Read(byteArraySlice.Buffer, 0, (int)num);
            byteArraySlice.Count = (int)num;
            return byteArraySlice;
        }

        internal byte[] ReadByteArray(StreamBuffer stream)
        {
            uint num = this.ReadCompressedUInt32(stream);
            byte[] buffer = new byte[(int)num];
            stream.Read(buffer, 0, (int)num);
            return buffer;
        }

        public object ReadCustomType(StreamBuffer stream, byte gpType = 0)
        {
            byte key = gpType != (byte)0 ? (byte)((uint)gpType - 128U) : stream.ReadByte();
            CustomType customType;
            if (!Protocol.CodeDict.TryGetValue(key, out customType))
                throw new Exception("Read failed, customType not found :" + key.ToString());

            int count = (int)this.ReadCompressedUInt32(stream);
            if (customType.SerializeStreamFunction != null)
                return customType.DeserializeStreamFunction(stream, (short)count);
            byte[] numArray = new byte[count];
            stream.Read(numArray, 0, count);
            return customType.DeserializeFunction(numArray);
        }

        public override EventData DeserializeEventData(StreamBuffer din, EventData target = null, DeserializationFlags flags = DeserializationFlags.None)
        {
            EventData eventData;
            if (target != null)
            {
                target.Reset();
                eventData = target;
            }
            else
                eventData = new EventData();
            eventData.Code = this.ReadByte(din);
            eventData.Parameters = this.ReadParameterTable(din, eventData.Parameters, flags);
            return eventData;
        }

        private Dictionary<byte,object> ReadParameterTable(StreamBuffer stream, Dictionary<byte,object> target = null, IProtocol.DeserializationFlags flags = IProtocol.DeserializationFlags.None)
        {
            short num = (short) this.ReadByte(stream);
            Dictionary<byte, object> dictionary = target != null ? target : new Dictionary<byte, object>((int)num);
            for(int i = 0; i < (int)num; ++i)
            {
                byte key = stream.ReadByte();
                byte gpType = stream.ReadByte();
                object obj = gpType != (byte)67 || (flags & IProtocol.DeserializationFlags.AllowPooledByteArray) != IProtocol.DeserializationFlags.AllowPooledByteArray ? this.Read(stream, gpType) : (object)this.ReadNonAllocByteArray(stream);
                dictionary[key] = obj;
            }
            return dictionary;
        }


        public Hashtable ReadHashtable(StreamBuffer stream)
        {
            int x = (int)this.ReadCompressedUInt32(stream);
            Hashtable hashtable = new Hashtable(x);
            for(int i = 0; i < x; ++i)
            {
                object key = this.Read(stream);
                object obj = this.Read(stream);
                if (key != null)
                    hashtable[key] = obj;
            }
            return hashtable;
        }

        public int[] ReadIntArray(StreamBuffer stream)
        {
            int length = this.ReadInt32(stream);
            int[] numArray = new int[length];
            for (int index = 0; index < length; ++index)
                numArray[index] = this.ReadInt32(stream);
            return numArray;
        }

        public override OperationRequest DeserializeOperationRequest(StreamBuffer din)
        {
            return new OperationRequest()
            {
                OperationCode = this.ReadByte(din),
                Parameters = this.ReadParameterTable(din)
            };
        }

        public override OperationResponse DeserializeOperationResponse(StreamBuffer stream)
        {
            return new OperationResponse
            {
                OperationCode = this.ReadByte(stream),
                ReturnCode = this.ReadInt16(stream),
                DebugMessage = this.Read(stream, this.ReadByte(stream)) as string,
                Parameters = this.ReadParameterTable(stream)
            };
        }


        internal string ReadString(StreamBuffer stream)
        {
            int num = (int)this.ReadCompressedUInt32(stream);
            if (num == 0)
                return string.Empty;
            int offset = 0;
            return Encoding.UTF8.GetString(stream.GetBufferAndAdvance(num, out offset), offset, num);
        }


        private object ReadCustomTypeArray(StreamBuffer stream)
        {
            uint num1 = this.ReadCompressedUInt32(stream);
            byte key = stream.ReadByte();
            CustomType customType;
            if (!Protocol.CodeDict.TryGetValue(key, out customType))
                throw new Exception("Serialization failed. Custom type not found: " + key.ToString());
            Array instance = Array.CreateInstance(customType.Type, (int)num1);
            for (short index = 0; (long)index < (long)num1; ++index)
            {
                uint num2 = this.ReadCompressedUInt32(stream);
                object obj;
                if (customType.SerializeStreamFunction == null)
                {
                    byte[] numArray = new byte[(int)num2];
                    stream.Read(numArray, 0, (int)num2);
                    obj = customType.DeserializeFunction(numArray);
                }
                else
                    obj = customType.DeserializeStreamFunction(stream, (short)num2);
                instance.SetValue(obj, (int)index);
            }
            return (object)instance;
        }


        private Type ReadDictionaryType(StreamBuffer stream, out Protocol18.GpType keyReadType,out Protocol18.GpType valueReadType)
        {
            keyReadType = (Protocol18.GpType)stream.ReadByte();
            Protocol18.GpType gpType = (Protocol18.GpType)stream.ReadByte();
            valueReadType = gpType;

            Type type1 = keyReadType != Protocol18.GpType.Unknown ? Protocol18.GetAllowedDictionaryKeyTypes(keyReadType) : typeof(object);
            Type type2;
            switch (gpType)
            {
                case Protocol18.GpType.Unknown:
                    type2 = typeof(object);
                    break;
                case Protocol18.GpType.Dictionary:
                    type2 = this.ReadDictionaryType(stream);
                    break;
                case Protocol18.GpType.ObjectArray:
                    type2 = typeof(object[]);
                    break;
                case Protocol18.GpType.Array:
                    type2 = this.GetDictArrayType(stream);
                    valueReadType = Protocol18.GpType.Unknown;
                    break;
                case Protocol18.GpType.HashtableArray:
                    type2 = typeof(Hashtable[]);
                    break;
                default:
                    type2 = Protocol18.GetClrArrayType(gpType);
                    break;
            }
            return typeof(Dictionary<,>).MakeGenericType(type1, type2);
        }


        private Type ReadDictionaryType(StreamBuffer stream)
        {
            Protocol18.GpType gpType1 = (Protocol18.GpType)stream.ReadByte();
            Protocol18.GpType gpType2 = (Protocol18.GpType)stream.ReadByte();
            Type type1 = gpType1 != Protocol18.GpType.Unknown ? Protocol18.GetAllowedDictionaryKeyTypes(gpType1) : typeof(object);
            Type type2;
            switch (gpType2)
            {
                case Protocol18.GpType.Unknown:
                    type2 = typeof(object);
                    break;
                case Protocol18.GpType.Dictionary:
                    type2 = this.ReadDictionaryType(stream);
                    break;
                case Protocol18.GpType.Array:
                    type2 = this.GetDictArrayType(stream);
                    break;
                default:
                    type2 = Protocol18.GetClrArrayType(gpType2);
                    break;
            }
            return typeof(Dictionary<,>).MakeGenericType(type1, type2);
        }


        private Type GetDictArrayType(StreamBuffer stream)
        {
            Protocol18.GpType gpType = (Protocol18.GpType)stream.ReadByte();
            int num = 0;
            for (; gpType == Protocol18.GpType.Array; gpType = (Protocol18.GpType)stream.ReadByte())
                ++num;
            Type type = Protocol18.GetClrArrayType(gpType).MakeArrayType();
            for (int index = 0; index < num; ++index)
                type = type.MakeArrayType();
            return type;
        }


        private IDictionary ReadDictionary(StreamBuffer stream)
        {
            Protocol18.GpType keyReadType;
            Protocol18.GpType valueReadType;
            Type type = this.ReadDictionaryType(stream, out keyReadType, out valueReadType);
            if (type == null || !(Activator.CreateInstance(type) is IDictionary instance))
                return (IDictionary)null;
            this.ReadDictionaryElements(stream, keyReadType, valueReadType, instance);
            return instance;
        }


        private bool ReadDictionaryElements(
            StreamBuffer stream, 
            Protocol18.GpType keyReadType,
            Protocol18.GpType valueReadType,
            IDictionary dictionary)
        {
            uint num = this.ReadCompressedUInt32(stream);
            for(int index = 0; (long) index < (long) num; ++index)
            {
                object key = keyReadType == Protocol18.GpType.Unknown ? this.Read(stream) : this.Read(stream, (byte)keyReadType);
                object obj = valueReadType == Protocol18.GpType.Unknown ? this.Read(stream) : this.Read(stream, (byte)valueReadType);
                if (key != null)
                    dictionary.Add(key, obj);
            }
            return true;
        }


        private object[] ReadObjectArray(StreamBuffer stream)
        {
            uint num = this.ReadCompressedUInt32(stream);
            object[] objArray = new object[(int)num];
            for(short index = 0; (long)index < (long) num; ++index)
            {
                object obj = this.Read(stream);
                objArray[(int)index] = obj;
            }
            return objArray;
        }


        private bool[] ReadBooleanArray(StreamBuffer stream)
        {
            uint num1 = this.ReadCompressedUInt32(stream);
            bool[] flagArray1 = new bool[(int)num1];
            int num2 = (int)num1 / 8;
            int num3 = 0;
            for (; num2 > 0; --num2)
            {
                byte num4 = stream.ReadByte();
                bool[] flagArray2 = flagArray1;
                int index1 = num3;
                int num5 = index1 + 1;
                int num6 = ((int)num4 & 1) == 1 ? 1 : 0;
                flagArray2[index1] = num6 != 0;
                bool[] flagArray3 = flagArray1;
                int index2 = num5;
                int num7 = index2 + 1;
                int num8 = ((int)num4 & 2) == 2 ? 1 : 0;
                flagArray3[index2] = num8 != 0;
                bool[] flagArray4 = flagArray1;
                int index3 = num7;
                int num9 = index3 + 1;
                int num10 = ((int)num4 & 4) == 4 ? 1 : 0;
                flagArray4[index3] = num10 != 0;
                bool[] flagArray5 = flagArray1;
                int index4 = num9;
                int num11 = index4 + 1;
                int num12 = ((int)num4 & 8) == 8 ? 1 : 0;
                flagArray5[index4] = num12 != 0;
                bool[] flagArray6 = flagArray1;
                int index5 = num11;
                int num13 = index5 + 1;
                int num14 = ((int)num4 & 16) == 16 ? 1 : 0;
                flagArray6[index5] = num14 != 0;
                bool[] flagArray7 = flagArray1;
                int index6 = num13;
                int num15 = index6 + 1;
                int num16 = ((int)num4 & 32) == 32 ? 1 : 0;
                flagArray7[index6] = num16 != 0;
                bool[] flagArray8 = flagArray1;
                int index7 = num15;
                int num17 = index7 + 1;
                int num18 = ((int)num4 & 64) == 64 ? 1 : 0;
                flagArray8[index7] = num18 != 0;
                bool[] flagArray9 = flagArray1;
                int index8 = num17;
                num3 = index8 + 1;
                int num19 = ((int)num4 & 128) == 128 ? 1 : 0;
                flagArray9[index8] = num19 != 0;
            }
            if ((long)num3 < (long)num1)
            {
                byte num20 = stream.ReadByte();
                int index = 0;
                while ((long)num3 < (long)num1)
                {
                    flagArray1[num3++] = ((int)num20 & (int)Protocol18.boolMasks[index]) == (int)Protocol18.boolMasks[index];
                    ++index;
                }
            }
            return flagArray1;
        }

        
        internal short[] ReadInt16Array(StreamBuffer stream)
        {
            short[] numArray = new short[(int)this.ReadCompressedUInt32(stream)];
            for (int index = 0; index < numArray.Length; ++index)
                numArray[index] = this.ReadInt16(stream);
            return numArray;
        }


        private float[] ReadSingleArray(StreamBuffer stream)
        {
            int length = (int)this.ReadCompressedUInt32(stream);
            int num = length * 4;
            float[] numArray = new float[length];
            int offset;
            Buffer.BlockCopy((Array)stream.GetBufferAndAdvance(num, out offset), offset, (Array)numArray, 0, num);
            return numArray;
        }


        private double[] ReadDoubleArray(StreamBuffer stream)
        {
            int length = (int)this.ReadCompressedUInt32(stream);
            int num = length * 8;
            double[] numArray = new double[length];
            int offset;
            Buffer.BlockCopy((Array)stream.GetBufferAndAdvance(num, out offset), offset, (Array)numArray, 0, num);
            return numArray;
        }


        internal string[] ReadStringArray(StreamBuffer stream)
        {
            string[] strArray = new string[(int)this.ReadCompressedUInt32(stream)];
            for (int index = 0; index < strArray.Length; ++index)
                strArray[index] = this.ReadString(stream);
            return strArray;
        }



        private Hashtable[] ReadHashtableArray(StreamBuffer stream)
        {
            uint num = this.ReadCompressedUInt32(stream);
            Hashtable[] hashtableArray = new Hashtable[(int)num];
            for (int index = 0; (long)index < (long)num; ++index)
                hashtableArray[index] = this.ReadHashtable(stream);
            return hashtableArray;
        }


        private IDictionary[] ReadDictionaryArray(StreamBuffer stream)
        {
            Protocol18.GpType keyReadType;
            Protocol18.GpType valueReadType;
            Type type = this.ReadDictionaryType(stream, out keyReadType, out valueReadType);
            uint num = this.ReadCompressedUInt32(stream);
            IDictionary[] instance = (IDictionary[])Array.CreateInstance(type, (int)num);
            for (int index = 0; (long)index < (long)num; ++index)
            {
                instance[index] = (IDictionary)Activator.CreateInstance(type);
                this.ReadDictionaryElements(stream, keyReadType, valueReadType, instance[index]);
            }
            return instance;
        }


        private Array ReadArrayInArray(StreamBuffer stream)
        {
            uint num = this.ReadCompressedUInt32(stream);
            if (!(this.Read(stream) is Array array1))
                return (Array)null;
            Array instance = Array.CreateInstance(array1.GetType(), (int)num);
            instance.SetValue((object)array1, 0);
            for (short index = 1; (long)index < (long)num; ++index)
            {
                Array array2 = (Array)this.Read(stream);
                instance.SetValue((object)array2, (int)index);
            }
            return instance;
        }


        internal int ReadInt1(StreamBuffer stream, bool signNegative) => signNegative ? (int)-stream.ReadByte() : (int)stream.ReadByte();

        internal int ReadInt2(StreamBuffer stream, bool signNegative) => signNegative ? (int)-this.ReadUShort(stream) : (int)this.ReadUShort(stream);

        internal int ReadCompressedInt32(StreamBuffer stream) => this.DecodeZigZag32(this.ReadCompressedUInt32(stream));

        private uint ReadCompressedUInt32(StreamBuffer stream)
        {
            uint num1 = 0;
            int num2 = 0;
            byte[] buffer = stream.GetBuffer();
            int position = stream.Position;
            while (num2 != 35)
            {
                if (position >= buffer.Length)
                    throw new EndOfStreamException("Failed to read full uint.");
                byte num3 = buffer[position];
                ++position;
                num1 |= (uint)(((int)num3 & (int)sbyte.MaxValue) << num2);
                num2 += 7;
                if (((int)num3 & 128) == 0)
                    break;
            }
            stream.Position = position;
            return num1;
        }

        internal long ReadCompressedInt64(StreamBuffer stream) => this.DecodeZigZag64(this.ReadCompressedUInt64(stream));

        private ulong ReadCompressedUInt64(StreamBuffer stream)
        {
            ulong num1 = 0;
            int num2 = 0;
            byte[] buffer = stream.GetBuffer();
            int position = stream.Position;
            while (num2 != 70)
            {
                if (position >= buffer.Length)
                    throw new EndOfStreamException("Failed to read full ulong.");
                byte num3 = buffer[position];
                ++position;
                num1 |= (ulong)((int)num3 & (int)sbyte.MaxValue) << num2;
                num2 += 7;
                if (((int)num3 & 128) == 0)
                    break;
            }
            stream.Position = position;
            return num1;
        }


        internal int[] ReadCompressedInt32Array(StreamBuffer stream)
        {
            int[] numArray = new int[(int)this.ReadCompressedUInt32(stream)];
            for (int index = 0; index < numArray.Length; ++index)
                numArray[index] = this.ReadCompressedInt32(stream);
            return numArray;
        }

        internal long[] ReadCompressedInt64Array(StreamBuffer stream)
        {
            long[] numArray = new long[(int)this.ReadCompressedUInt32(stream)];
            for (int index = 0; index < numArray.Length; ++index)
                numArray[index] = this.ReadCompressedInt64(stream);
            return numArray;
        }

        private int DecodeZigZag32(uint value) => (int)( (long)(value >> 1) ^ (long)-(value & 1U) );

        private long DecodeZigZag64(ulong value) => (long)(value >> 1) ^ -((long)value & 1L);


        internal void Write(StreamBuffer stream, object value, bool writeType)
        {
            if (value == null)
                this.Write(stream, value, Protocol18.GpType.Null, writeType);
            else
                this.Write(stream, value, this.GetCodeOfType(value.GetType()), writeType);
        }


        private void Write(StreamBuffer stream,
            object value,
            Protocol18.GpType gpType,
            bool writeType)
        { 
            switch (gpType)
            {
                case Protocol18.GpType.Unknown:
                    UnityEngine.Debug.Log("Write Unkown Type : " + value.GetType().ToString());
                    switch (value)
                    {
                        case ByteArraySlice _:
                            ByteArraySlice buffer = (ByteArraySlice)value;
                            this.WriteByteArraySlice(stream, buffer, writeType);
                            return;
                        case ArraySegment<byte> seg2:
                            this.WriteArraySegmentByte(stream, seg2, writeType);
                            return;
                        default:
                            goto label_7;
                    }
                case Protocol18.GpType.Boolean:
                    this.WriteBoolean(stream, (bool)value, writeType);
                    break;
                case Protocol18.GpType.Byte:
                    this.WriteByte(stream, (byte)value, writeType);
                    break;
                case Protocol18.GpType.Short:
                    this.WriteInt16(stream, (short)value, writeType);
                    break;
                case Protocol18.GpType.Float:
                    this.WriteSingle(stream, (float)value, writeType);
                    break;
                case Protocol18.GpType.Double:
                    this.WriteDouble(stream, (double)value, writeType);
                    break;
                case Protocol18.GpType.String:
                    this.WriteString(stream, (string)value, writeType);
                    break;
                case Protocol18.GpType.Null:
                    if (!writeType)
                        break;
                    stream.WriteByte((byte)8);
                    break;
                case Protocol18.GpType.CompressedInt:
                    this.WriteCompressedInt32(stream, (int)value, writeType);
                    break;
                case Protocol18.GpType.CompressedLong:
                    this.WriteCompressedInt64(stream, (long)value, writeType);
                    break;
                case Protocol18.GpType.Custom:
                    label_7:
                    this.WriteCustomType(stream, value, writeType);
                    break;
                case Protocol18.GpType.Dictionary:
                    this.WriteDictionary(stream, (object)(IDictionary)value, writeType);
                    break;
                case Protocol18.GpType.Hashtable:
                    this.WriteHashtable(stream, (object)(Hashtable)value, writeType);
                    break;
                case Protocol18.GpType.ObjectArray:
                    this.WriteObjectArray(stream, (IList)value, writeType);
                    break;
                case Protocol18.GpType.OperationRequest:
                    this.SerializeOperationRequest(stream, (OperationRequest)value, writeType);
                    break;
                case Protocol18.GpType.OperationResponse:
                    this.SerializeOperationResponse(stream, (OperationResponse)value, writeType);
                    break;
                case Protocol18.GpType.EventData:
                    this.SerializeEventData(stream, (EventData)value, writeType);
                    break;
                case Protocol18.GpType.Array:
                    this.WriteArrayInArray(stream, value, writeType);
                    break;
                case Protocol18.GpType.BooleanArray:
                    this.WriteBoolArray(stream, (bool[])value, writeType);
                    break;
                case Protocol18.GpType.ByteArray:
                    this.WriteByteArray(stream, (byte[])value, writeType);
                    break;
                case Protocol18.GpType.ShortArray:
                    this.WriteInt16Array(stream, (short[])value, writeType);
                    break;
                case Protocol18.GpType.FloatArray:
                    this.WriteSingleArray(stream, (float[])value, writeType);
                    break;
                case Protocol18.GpType.DoubleArray:
                    this.WriteDoubleArray(stream, (double[])value, writeType);
                    break;
                case Protocol18.GpType.StringArray:
                    this.WriteStringArray(stream, value, writeType);
                    break;
                case Protocol18.GpType.CompressedIntArray:
                    this.WriteInt32ArrayCompressed(stream, (int[])value, writeType);
                    break;
                case Protocol18.GpType.CompressedLongArray:
                    this.WriteInt64ArrayCompressed(stream, (long[])value, writeType);
                    break;
                case Protocol18.GpType.CustomTypeArray:
                    this.WriteCustomTypeArray(stream, value, writeType);
                    break;
                case Protocol18.GpType.DictionaryArray:
                    this.WriteDictionaryArray(stream, (IDictionary[])value, writeType);
                    break;
                case Protocol18.GpType.HashtableArray:
                    this.WriteHashtableArray(stream, value, writeType);
                    break;
            }
        }


        public override void SerializeEventData(StreamBuffer stream, EventData serObject, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)26);
            stream.WriteByte(serObject.Code);
            this.WriteParameterTable(stream, serObject.Parameters);
        }

        private void WriteParameterTable(StreamBuffer stream, Dictionary<byte, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                this.WriteByte(stream, (byte)0, false);
            }
            else
            {
                this.WriteByte(stream, (byte)parameters.Count, false);
                foreach (KeyValuePair<byte, object> parameter in parameters)
                {
                    stream.WriteByte(parameter.Key);
                    this.Write(stream, parameter.Value, true);
                }
            }
        }

        private void SerializeOperationRequest(
          StreamBuffer stream,
          OperationRequest serObject,
          bool setType)
        {
            this.SerializeOperationRequest(stream, serObject.OperationCode, serObject.Parameters, setType);
        }

        public override void SerializeOperationRequest(
          StreamBuffer stream,
          byte operationCode,
          Dictionary<byte, object> parameters,
          bool setType)
        {
            if (setType)
                stream.WriteByte((byte)24);
            stream.WriteByte(operationCode);
            this.WriteParameterTable(stream, parameters);
        }

        public override void SerializeOperationResponse(
          StreamBuffer stream,
          OperationResponse serObject,
          bool setType)
        {
            if (setType)
                stream.WriteByte((byte)25);
            stream.WriteByte(serObject.OperationCode);
            this.WriteInt16(stream, serObject.ReturnCode, false);
            if (string.IsNullOrEmpty(serObject.DebugMessage))
            {
                stream.WriteByte((byte)8);
            }
            else
            {
                stream.WriteByte((byte)7);
                this.WriteString(stream, serObject.DebugMessage, false);
            }
            this.WriteParameterTable(stream, serObject.Parameters);
        }

        internal void WriteByte(StreamBuffer stream, byte value, bool writeType)
        {
            if (writeType)
            {
                if (value == (byte)0)
                {
                    stream.WriteByte((byte)34);
                    return;
                }
                stream.WriteByte((byte)3);
            }
            stream.WriteByte(value);
        }

        internal void WriteBoolean(StreamBuffer stream, bool value, bool writeType)
        {
            if (writeType)
            {
                if (value)
                    stream.WriteByte((byte)28);
                else
                    stream.WriteByte((byte)27);
            }
            else
                stream.WriteByte(value ? (byte)1 : (byte)0);
        }

        internal void WriteUShort(StreamBuffer stream, ushort value) => stream.WriteBytes((byte)value, (byte)((uint)value >> 8));

        internal void WriteInt16(StreamBuffer stream, short value, bool writeType)
        {
            if (writeType)
            {
                if (value == (short)0)
                {
                    stream.WriteByte((byte)29);
                    return;
                }
                stream.WriteByte((byte)4);
            }
            stream.WriteBytes((byte)value, (byte)((uint)value >> 8));
        }

        internal void WriteDouble(StreamBuffer stream, double value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)6);
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(8, out offset);
            lock (this.memDoubleBlock)
            {
                this.memDoubleBlock[0] = value;
                Buffer.BlockCopy((Array)this.memDoubleBlock, 0, (Array)bufferAndAdvance, offset, 8);
            }
        }

        internal void WriteSingle(StreamBuffer stream, float value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)5);
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(4, out offset);
            lock (this.memFloatBlock)
            {
                this.memFloatBlock[0] = value;
                Buffer.BlockCopy((Array)this.memFloatBlock, 0, (Array)bufferAndAdvance, offset, 4);
            }
        }

        internal void WriteString(StreamBuffer stream, string value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)7);
            int byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > (int)short.MaxValue)
                throw new NotSupportedException("Strings that exceed a UTF8-encoded byte-length of 32767 (short.MaxValue) are not supported. Yours is: " + byteCount.ToString());
            this.WriteIntLength(stream, byteCount);
            int offset = 0;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(byteCount, out offset);
            Encoding.UTF8.GetBytes(value, 0, value.Length, bufferAndAdvance, offset);
        }

        private void WriteHashtable(StreamBuffer stream, object value, bool writeType)
        {
            Hashtable hashtable = (Hashtable)value;
            if (writeType)
                stream.WriteByte((byte)21);
            this.WriteIntLength(stream, hashtable.Count);
            foreach (object key in hashtable.Keys)
            {
                this.Write(stream, key, true);
                this.Write(stream, hashtable[key], true);
            }
        }

        internal void WriteByteArray(StreamBuffer stream, byte[] value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)67);
            this.WriteIntLength(stream, value.Length);
            stream.Write(value, 0, value.Length);
        }

        private void WriteArraySegmentByte(StreamBuffer stream, ArraySegment<byte> seg, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)67);
            int count = seg.Count;
            this.WriteIntLength(stream, count);
            stream.Write(seg.Array, seg.Offset, count);
        }

        private void WriteByteArraySlice(StreamBuffer stream, ByteArraySlice buffer, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)67);
            int count = buffer.Count;
            this.WriteIntLength(stream, count);
            stream.Write(buffer.Buffer, buffer.Offset, count);
            buffer.Release();
        }

        internal void WriteInt32ArrayCompressed(StreamBuffer stream, int[] value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)73);
            this.WriteIntLength(stream, value.Length);
            for (int index = 0; index < value.Length; ++index)
                this.WriteCompressedInt32(stream, value[index], false);
        }

        private void WriteInt64ArrayCompressed(StreamBuffer stream, long[] values, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)74);
            this.WriteIntLength(stream, values.Length);
            for (int index = 0; index < values.Length; ++index)
                this.WriteCompressedInt64(stream, values[index], false);
        }

        internal void WriteBoolArray(StreamBuffer stream, bool[] value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)66);
            this.WriteIntLength(stream, value.Length);
            int num1 = value.Length >> 3;
            byte[] buffer = new byte[num1 + 1];
            int count = 0;
            int index1 = 0;
            while (num1 > 0)
            {
                byte num2 = 0;
                bool[] flagArray1 = value;
                int index2 = index1;
                int num3 = index2 + 1;
                if (flagArray1[index2])
                    num2 |= (byte)1;
                bool[] flagArray2 = value;
                int index3 = num3;
                int num4 = index3 + 1;
                if (flagArray2[index3])
                    num2 |= (byte)2;
                bool[] flagArray3 = value;
                int index4 = num4;
                int num5 = index4 + 1;
                if (flagArray3[index4])
                    num2 |= (byte)4;
                bool[] flagArray4 = value;
                int index5 = num5;
                int num6 = index5 + 1;
                if (flagArray4[index5])
                    num2 |= (byte)8;
                bool[] flagArray5 = value;
                int index6 = num6;
                int num7 = index6 + 1;
                if (flagArray5[index6])
                    num2 |= (byte)16;
                bool[] flagArray6 = value;
                int index7 = num7;
                int num8 = index7 + 1;
                if (flagArray6[index7])
                    num2 |= (byte)32;
                bool[] flagArray7 = value;
                int index8 = num8;
                int num9 = index8 + 1;
                if (flagArray7[index8])
                    num2 |= (byte)64;
                bool[] flagArray8 = value;
                int index9 = num9;
                index1 = index9 + 1;
                if (flagArray8[index9])
                    num2 |= (byte)128;
                buffer[count] = num2;
                --num1;
                ++count;
            }
            if (index1 < value.Length)
            {
                byte num10 = 0;
                int num11 = 0;
                for (; index1 < value.Length; ++index1)
                {
                    if (value[index1])
                        num10 |= (byte)(1 << num11);
                    ++num11;
                }
                buffer[count] = num10;
                ++count;
            }
            stream.Write(buffer, 0, count);
        }

        internal void WriteInt16Array(StreamBuffer stream, short[] value, bool writeType)
        {
            if (writeType)
                stream.WriteByte((byte)68);
            this.WriteIntLength(stream, value.Length);
            for (int index = 0; index < value.Length; ++index)
                this.WriteInt16(stream, value[index], false);
        }

        internal void WriteSingleArray(StreamBuffer stream, float[] values, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)69);
            this.WriteIntLength(stream, values.Length);
            int num = values.Length * 4;
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(num, out offset);
            Buffer.BlockCopy((Array)values, 0, (Array)bufferAndAdvance, offset, num);
        }

        internal void WriteDoubleArray(StreamBuffer stream, double[] values, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)70);
            this.WriteIntLength(stream, values.Length);
            int num = values.Length * 8;
            int offset;
            byte[] bufferAndAdvance = stream.GetBufferAndAdvance(num, out offset);
            Buffer.BlockCopy((Array)values, 0, (Array)bufferAndAdvance, offset, num);
        }

        internal void WriteStringArray(StreamBuffer stream, object value0, bool writeType)
        {
            string[] strArray = (string[])value0;
            if (writeType)
                stream.WriteByte((byte)71);
            this.WriteIntLength(stream, strArray.Length);
            for (int index = 0; index < strArray.Length; ++index)
            {
                if (strArray[index] == null)
                    throw new InvalidDataException("Unexpected - cannot serialize string array with null element " + index.ToString());
                this.WriteString(stream, strArray[index], false);
            }
        }

        private void WriteObjectArray(StreamBuffer stream, object array, bool writeType) => this.WriteObjectArray(stream, (IList)array, writeType);

        private void WriteObjectArray(StreamBuffer stream, IList array, bool writeType)
        {
                        UnityEngine.Debug.Log("WriteObjectArray");

            if (writeType)
                stream.WriteByte((byte)23);
            this.WriteIntLength(stream, array.Count);
            for (int index = 0; index < array.Count; ++index)
            {
                object obj = array[index];

                if (obj != null)
                UnityEngine.Debug.Log("Write Object Array Element ="+obj.GetType().Name);

                this.Write(stream, obj, true);
            }
        }

        private void WriteArrayInArray(StreamBuffer stream, object value, bool writeType)
        {
            UnityEngine.Debug.Log("WriteArrayInArray");
            object[] objArray = (object[])value;
            stream.WriteByte((byte)64);
            this.WriteIntLength(stream, objArray.Length);
            foreach (object obj in objArray)
            {
                UnityEngine.Debug.Log("Write Array In Array Element ="+ obj.GetType().Name);

                this.Write(stream, obj, true);
            }
        }

        private void WriteCustomTypeBody(CustomType customType, StreamBuffer stream, object value)
        {
            if (customType.SerializeStreamFunction == null)
            {
                byte[] buffer = customType.SerializeFunction(value);
                this.WriteIntLength(stream, buffer.Length);
                stream.Write(buffer, 0, buffer.Length);
            }
            else
            {

                int position = stream.Position;
                ++stream.Position;
                uint num = (uint)customType.SerializeStreamFunction(stream, value);
                int count = this.WriteCompressedUInt32(this.memCustomTypeBodyLengthSerialized, num);
                if (count == 1)
                {
                    stream.GetBuffer()[position] = this.memCustomTypeBodyLengthSerialized[0];
                }
                else
                {
                    for (int index = 0; index < count - 1; ++index)
                        stream.WriteByte((byte)0);
                    Buffer.BlockCopy((Array)stream.GetBuffer(), position + 1, (Array)stream.GetBuffer(), position + count, (int)num);
                    Buffer.BlockCopy((Array)this.memCustomTypeBodyLengthSerialized, 0, (Array)stream.GetBuffer(), position, count);
                    stream.Position = (int)((long)(position + count) + (long)num);
                }
            }
        }

        private void WriteCustomType(StreamBuffer stream, object value, bool writeType)
        {
            Type type = value.GetType();
            CustomType customType;
            if (!Protocol.TypeDict.TryGetValue(type, out customType))
                throw new Exception("Write failed. Custom type not found: " + type?.ToString());
            if (writeType)
            {
                if (customType.Code < (byte)100)
                {
                    stream.WriteByte((byte)(128U + (uint)customType.Code));
                }
                else
                {
                    stream.WriteByte((byte)19);
                    stream.WriteByte(customType.Code);
                }
            }
            else
                stream.WriteByte(customType.Code);
            this.WriteCustomTypeBody(customType, stream, value);
        }

        private void WriteCustomTypeArray(StreamBuffer stream, object value, bool writeType)
        {
            IList list = (IList)value;
            Type elementType = value.GetType().GetElementType();
            CustomType customType;
            if (!Protocol.TypeDict.TryGetValue(elementType, out customType))
                throw new Exception("Write failed. Custom type of element not found: " + elementType?.ToString());
            if (writeType)
                stream.WriteByte((byte)83);
            this.WriteIntLength(stream, list.Count);
            stream.WriteByte(customType.Code);
            foreach (object obj in (IEnumerable)list)
                this.WriteCustomTypeBody(customType, stream, obj);
        }

        private bool WriteArrayHeader(StreamBuffer stream, Type type)
        {
            Type elementType;
            for (elementType = type.GetElementType(); elementType.IsArray; elementType = elementType.GetElementType())
                stream.WriteByte((byte)64);
            Protocol18.GpType codeOfType = this.GetCodeOfType(elementType);
            if (codeOfType == Protocol18.GpType.Unknown)
                return false;
            stream.WriteByte((byte)(codeOfType | Protocol18.GpType.CustomTypeSlim));
            return true;
        }

        private void WriteDictionaryElements(
          StreamBuffer stream,
          IDictionary dictionary,
          Protocol18.GpType keyWriteType,
          Protocol18.GpType valueWriteType)
        {
            this.WriteIntLength(stream, dictionary.Count);
            foreach (DictionaryEntry dictionaryEntry in dictionary)
            {
                this.Write(stream, dictionaryEntry.Key, keyWriteType == Protocol18.GpType.Unknown);
                this.Write(stream, dictionaryEntry.Value, valueWriteType == Protocol18.GpType.Unknown);
            }
        }

        private void WriteDictionary(StreamBuffer stream, object dict, bool setType)
        {
            if (setType)
                stream.WriteByte((byte)20);
            Protocol18.GpType keyWriteType;
            Protocol18.GpType valueWriteType;
            this.WriteDictionaryHeader(stream, dict.GetType(), out keyWriteType, out valueWriteType);
            IDictionary dictionary = (IDictionary)dict;
            this.WriteDictionaryElements(stream, dictionary, keyWriteType, valueWriteType);
        }

        private void WriteDictionaryHeader(
          StreamBuffer stream,
          Type type,
          out Protocol18.GpType keyWriteType,
          out Protocol18.GpType valueWriteType)
        {
            Type[] genericArguments = type.GetGenericArguments();
            if (genericArguments[0] == typeof(object))
            {
                stream.WriteByte((byte)0);
                keyWriteType = Protocol18.GpType.Unknown;
            }
            else
            {
                keyWriteType = genericArguments[0].IsPrimitive || genericArguments[0] == typeof(string) ? this.GetCodeOfType(genericArguments[0]) : throw new InvalidDataException("Unexpected - cannot serialize Dictionary with key type: " + genericArguments[0]?.ToString());
                if (keyWriteType == Protocol18.GpType.Unknown)
                    throw new InvalidDataException("Unexpected - cannot serialize Dictionary with key type: " + genericArguments[0]?.ToString());
                stream.WriteByte((byte)keyWriteType);
            }

            
            if (genericArguments[1] == typeof(object))
            {
                stream.WriteByte((byte)0);
                valueWriteType = Protocol18.GpType.Unknown;
            }
            else if (genericArguments[1].IsArray)
            {
                if (!this.WriteArrayType(stream, genericArguments[1], out valueWriteType))
                    throw new InvalidDataException("Unexpected - cannot serialize Dictionary with value type: " + genericArguments[1]?.ToString());
            }
            else
            {
                valueWriteType = this.GetCodeOfType(genericArguments[1]);
                if (valueWriteType == Protocol18.GpType.Unknown)
                    throw new InvalidDataException("Unexpected - cannot serialize Dictionary with value type: " + genericArguments[1]?.ToString());
                if (valueWriteType == Protocol18.GpType.Array)
                {
                    if (!this.WriteArrayHeader(stream, genericArguments[1]))
                        throw new InvalidDataException("Unexpected - cannot serialize Dictionary with value type: " + genericArguments[1]?.ToString());
                }
                else if (valueWriteType == Protocol18.GpType.Dictionary)
                {
                    stream.WriteByte((byte)valueWriteType);
                    this.WriteDictionaryHeader(stream, genericArguments[1], out Protocol18.GpType _, out Protocol18.GpType _);
                }
                else
                    stream.WriteByte((byte)valueWriteType);
            }
        }

        private bool WriteArrayType(StreamBuffer stream, Type type, out Protocol18.GpType writeType)
        {
            Type elementType = type.GetElementType();
            if (elementType == null)
                throw new InvalidDataException("Unexpected - cannot serialize array with type: " + type?.ToString());
            if (elementType.IsArray)
            {
                for (; elementType != null && elementType.IsArray; elementType = elementType.GetElementType())
                    stream.WriteByte((byte)64);
                byte num = (byte)(this.GetCodeOfType(elementType) | Protocol18.GpType.Array);
                stream.WriteByte(num);
                writeType = Protocol18.GpType.Array;
                return true;
            }
            if (elementType.IsPrimitive)
            {
                byte num = (byte)(this.GetCodeOfType(elementType) | Protocol18.GpType.Array);
                if (num == (byte)226)
                    num = (byte)67;
                stream.WriteByte(num);
                if (Enum.IsDefined(typeof(Protocol18.GpType), (object)num))
                {
                    writeType = (Protocol18.GpType)num;
                    return true;
                }
                writeType = Protocol18.GpType.Unknown;
                return false;
            }
            if (elementType == typeof(string))
            {
                stream.WriteByte((byte)71);
                writeType = Protocol18.GpType.StringArray;
                return true;
            }
            if (elementType == typeof(object))
            {
                stream.WriteByte((byte)23);
                writeType = Protocol18.GpType.ObjectArray;
                return true;
            }
            if (elementType == typeof(Hashtable))
            {
                stream.WriteByte((byte)85);
                writeType = Protocol18.GpType.HashtableArray;
                return true;
            }
            writeType = Protocol18.GpType.Unknown;
            return false;
        }

        private void WriteHashtableArray(StreamBuffer stream, object value, bool writeType)
        {
            Hashtable[] hashtableArray = (Hashtable[])value;
            if (writeType)
                stream.WriteByte((byte)85);
            this.WriteIntLength(stream, hashtableArray.Length);
            foreach (Hashtable hashtable in hashtableArray)
                this.WriteHashtable(stream, (object)hashtable, false);
        }

        private void WriteDictionaryArray(StreamBuffer stream, IDictionary[] dictArray, bool writeType)
        {
            stream.WriteByte((byte)84);
            Protocol18.GpType keyWriteType;
            Protocol18.GpType valueWriteType;
            this.WriteDictionaryHeader(stream, dictArray.GetType().GetElementType(), out keyWriteType, out valueWriteType);
            this.WriteIntLength(stream, dictArray.Length);
            foreach (IDictionary dict in dictArray)
                this.WriteDictionaryElements(stream, dict, keyWriteType, valueWriteType);
        }

        private void WriteIntLength(StreamBuffer stream, int value) => this.WriteCompressedUInt32(stream, (uint)value);

        private void WriteVarInt32(StreamBuffer stream, int value, bool writeType) => this.WriteCompressedInt32(stream, value, writeType);

        /// <summary>
        /// 정수를 압축된 상태로 Write합니다.
        /// </summary>
        private void WriteCompressedInt32(StreamBuffer stream, int value, bool writeType)
        {
            if (writeType)
            {
                if (value == 0)
                {
                    stream.WriteByte((byte)30);
                    return;
                }
                if (value > 0)
                {
                    if (value <= (int)byte.MaxValue)
                    {
                        stream.WriteByte((byte)11);
                        stream.WriteByte((byte)value);
                        return;
                    }
                    if (value <= (int)ushort.MaxValue)
                    {
                        stream.WriteByte((byte)13);
                        this.WriteUShort(stream, (ushort)value);
                        return;
                    }
                }
                else if (value >= -65535)
                {
                    if (value >= -255)
                    {
                        stream.WriteByte((byte)12);
                        stream.WriteByte((byte)-value);
                        return;
                    }
                    if (value >= -65535)
                    {
                        stream.WriteByte((byte)14);
                        this.WriteUShort(stream, (ushort)-value);
                        return;
                    }
                }
            }
            if (writeType)
                stream.WriteByte((byte)9);
            uint num = this.EncodeZigZag32(value);
            this.WriteCompressedUInt32(stream, num);
        }

        private void WriteCompressedInt64(StreamBuffer stream, long value, bool writeType)
        {
            if (writeType)
            {
                if (value == 0L)
                {
                    stream.WriteByte((byte)31);
                    return;
                }
                if (value > 0L)
                {
                    if (value <= (long)byte.MaxValue)
                    {
                        stream.WriteByte((byte)15);
                        stream.WriteByte((byte)value);
                        return;
                    }
                    if (value <= (long)ushort.MaxValue)
                    {
                        stream.WriteByte((byte)17);
                        this.WriteUShort(stream, (ushort)value);
                        return;
                    }
                }
                else if (value >= -65535L)
                {
                    if (value >= -255L)
                    {
                        stream.WriteByte((byte)16);
                        stream.WriteByte((byte)-value);
                        return;
                    }
                    if (value >= -65535L)
                    {
                        stream.WriteByte((byte)18);
                        this.WriteUShort(stream, (ushort)-value);
                        return;
                    }
                }
            }
            if (writeType)
                stream.WriteByte((byte)10);
            ulong num = this.EncodeZigZag64(value);
            this.WriteCompressedUInt64(stream, num);
        }

        private void WriteCompressedUInt32(StreamBuffer stream, uint value)
        {
            lock (this.memCompressedUInt32)
                stream.Write(this.memCompressedUInt32, 0, this.WriteCompressedUInt32(this.memCompressedUInt32, value));
        }

        private int WriteCompressedUInt32(byte[] buffer, uint value)
        {
            int index = 0;
            buffer[index] = (byte)(value & (uint)sbyte.MaxValue);
            for (value >>= 7; value > 0U; value >>= 7)
            {
                buffer[index] |= (byte)128;
                buffer[++index] = (byte)(value & (uint)sbyte.MaxValue);
            }
            return index + 1;
        }

        private void WriteCompressedUInt64(StreamBuffer stream, ulong value)
        {
            int index = 0;
            lock (this.memCompressedUInt64)
            {
                this.memCompressedUInt64[index] = (byte)(value & (ulong)sbyte.MaxValue);
                for (value >>= 7; value > 0UL; value >>= 7)
                {
                    this.memCompressedUInt64[index] |= (byte)128;
                    this.memCompressedUInt64[++index] = (byte)(value & (ulong)sbyte.MaxValue);
                }
                int count = index + 1;
                stream.Write(this.memCompressedUInt64, 0, count);
            }
        }

        private uint EncodeZigZag32(int value) => (uint)(value << 1 ^ value >> 31);

        private ulong EncodeZigZag64(long value) => (ulong)(value << 1 ^ value >> 63);

        public enum GpType : byte
        {
            /// <summary>Unkown. GpType: 0.</summary>
            Unknown = 0,
            /// <summary>Boolean. GpType: 2. See: BooleanFalse, BooleanTrue.</summary>
            Boolean = 2,
            /// <summary>Byte. GpType: 3.</summary>
            Byte = 3,
            /// <summary>Short. GpType: 4.</summary>
            Short = 4,
            /// <summary>32-bit floating-point value. GpType: 5.</summary>
            Float = 5,
            /// <summary>64-bit floating-point value. GpType: 6.</summary>
            Double = 6,
            /// <summary>String. GpType: 7.</summary>
            String = 7,
            /// <summary>Null value don't have types. GpType: 8.</summary>
            Null = 8,
            /// <summary>CompressedInt. GpType: 9.</summary>
            CompressedInt = 9,
            /// <summary>CompressedLong. GpType: 10.</summary>
            CompressedLong = 10, // 0x0A
            /// <summary>Int1. GpType: 11.</summary>
            Int1 = 11, // 0x0B
            /// <summary>Int1_. GpType: 12.</summary>
            Int1_ = 12, // 0x0C
            /// <summary>Int2. GpType: 13.</summary>
            Int2 = 13, // 0x0D
            /// <summary>Int2_. GpType: 14.</summary>
            Int2_ = 14, // 0x0E
            /// <summary>L1. GpType: 15.</summary>
            L1 = 15, // 0x0F
            /// <summary>L1_. GpType: 16.</summary>
            L1_ = 16, // 0x10
            /// <summary>L2. GpType: 17.</summary>
            L2 = 17, // 0x11
            /// <summary>L2_. GpType: 18.</summary>
            L2_ = 18, // 0x12
            /// <summary>Custom Type. GpType: 19.</summary>
            Custom = 19, // 0x13
            /// <summary>Dictionary. GpType: 20.</summary>
            Dictionary = 20, // 0x14
            /// <summary>Hashtable. GpType: 21.</summary>
            Hashtable = 21, // 0x15
            /// <summary>ObjectArray. GpType: 23.</summary>
            ObjectArray = 23, // 0x17
            /// <summary>OperationRequest. GpType: 24.</summary>
            OperationRequest = 24, // 0x18
            /// <summary>OperationResponse. GpType: 25.</summary>
            OperationResponse = 25, // 0x19
            /// <summary>EventData. GpType: 26.</summary>
            EventData = 26, // 0x1A
            /// <summary>Boolean False. GpType: 27.</summary>
            BooleanFalse = 27, // 0x1B
            /// <summary>Boolean True. GpType: 28.</summary>
            BooleanTrue = 28, // 0x1C
            /// <summary>ShortZero. GpType: 29.</summary>
            ShortZero = 29, // 0x1D
            /// <summary>IntZero. GpType: 30.</summary>
            IntZero = 30, // 0x1E
            /// <summary>LongZero. GpType: 3.</summary>
            LongZero = 31, // 0x1F
            /// <summary>FloatZero. GpType: 32.</summary>
            FloatZero = 32, // 0x20
            /// <summary>DoubleZero. GpType: 33.</summary>
            DoubleZero = 33, // 0x21
            /// <summary>ByteZero. GpType: 34.</summary>
            ByteZero = 34, // 0x22
            /// <summary>Array for nested Arrays. GpType: 64 (0x40). Element count and type follows.</summary>
            Array = 64, // 0x40
            BooleanArray = 66, // 0x42
            ByteArray = 67, // 0x43
            ShortArray = 68, // 0x44
            FloatArray = 69, // 0x45
            DoubleArray = 70, // 0x46
            StringArray = 71, // 0x47
            CompressedIntArray = 73, // 0x49
            CompressedLongArray = 74, // 0x4A
            CustomTypeArray = 83, // 0x53
            DictionaryArray = 84, // 0x54
            HashtableArray = 85, // 0x55
            /// <summary>Custom Type Slim. GpType: 128 (0x80) and up.</summary>
            CustomTypeSlim = 128, // 0x80
        }
    }
}
