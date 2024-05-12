using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;


namespace NDG
{
    public class SupportClass
    {
        private static List<Thread> threadList;
        private static readonly object ThreadListLock = new object();
        protected internal static SupportClass.IntegerMillisecondsDelegate IntegerMilliseconds = (SupportClass.IntegerMillisecondsDelegate)(() => Environment.TickCount);
        private static uint[] crcLookupTable;
        public static List<MethodInfo> GetMethods(Type type, Type attribute)
        {
            List<MethodInfo> methodInfoList = new List<MethodInfo>();
            if (type == null)
                return methodInfoList;
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (attribute == null || method.IsDefined(attribute, false))
                    methodInfoList.Add(method);
            }
            return methodInfoList;
        }

        /// <summary>
        /// 로컬 컴퓨터의 시작후 밀리초 값을 가져옵니다.
        /// </summary>
        public static int GetTickCount() => SupportClass.IntegerMilliseconds();


        /// <summary>
        /// true를 반환하는 동안에 전달된 함수를 호출하는 스레드를 생성합니다.
        /// </summary>
        public static byte StartBackgroundCalls(Func<bool> myThread, int millisecondsInterval = 100, string taskName = "")
        {
            lock (SupportClass.ThreadListLock)
            {
                if (SupportClass.threadList == null)
                    SupportClass.threadList = new List<Thread>();
                Thread thread = new Thread((ThreadStart)(() =>
                {
                    try
                    {
                        while (myThread())
                            Thread.Sleep(millisecondsInterval);
                    }
                    catch (ThreadAbortException ex)
                    {
                    }
                }));
                if (!string.IsNullOrEmpty(taskName))
                    thread.Name = taskName;
                thread.IsBackground = true;
                thread.Start();
                for (int index = 0; index < SupportClass.threadList.Count; ++index)
                {
                    if (SupportClass.threadList[index] == null)
                    {
                        SupportClass.threadList[index] = thread;
                        return (byte)index;
                    }
                }
                if (SupportClass.threadList.Count >= (int)byte.MaxValue)
                    throw new NotSupportedException("StartBackgroundCalls()는 최대 255개의 Thread를 생성할 수 있습니다.");
                SupportClass.threadList.Add(thread);
                return (byte)(SupportClass.threadList.Count - 1);
            }
        }

        /// <summary>
        /// 해당 Thread를 종료합니다.
        /// </summary>
        public static bool StopBackgroundCalls(byte id)
        {
            lock (SupportClass.ThreadListLock)
            {
                if (SupportClass.threadList == null || (int)id >= SupportClass.threadList.Count || SupportClass.threadList[(int)id] == null)
                    return false;
                SupportClass.threadList[(int)id].Abort();
                SupportClass.threadList[(int)id] = (Thread)null;
                return true;
            }
        }

        public static bool StopAllBackgroundCalls()
        {
            lock (SupportClass.ThreadListLock)
            {
                if (SupportClass.threadList == null)
                    return false;
                foreach (Thread thread in SupportClass.threadList)
                    thread?.Abort();
                SupportClass.threadList.Clear();
            }
            return true;
        }

        /// <summary>
        /// 수신된 exception을 stream에 씁니다.
        /// </summary>
        public static void WriteStackTrace(Exception throwable, TextWriter stream)
        {
            if (stream != null)
            {
                stream.WriteLine(throwable.ToString());
                stream.WriteLine(throwable.StackTrace);
                stream.Flush();
            }
            else
            {
                Debug.WriteLine(throwable.ToString());
                Debug.WriteLine(throwable.StackTrace);
            }
        }

        /// <summary>
        /// 수신된 exception을 stream에 씁니다.
        /// </summary>
        public static void WriteStackTrace(Exception throwable) => SupportClass.WriteStackTrace(throwable, (TextWriter)null);

        public static string DictionaryToString(IDictionary dictionary) => SupportClass.DictionaryToString(dictionary, true);

        public static string DictionaryToString(IDictionary dictionary, bool includeTypes)
        {
            if (dictionary == null)
                return "null";
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("{");
            foreach (object key in (IEnumerable)dictionary.Keys)
            {
                if (stringBuilder.Length > 1)
                    stringBuilder.Append(", ");
                Type type;
                string str;
                if (dictionary[key] == null)
                {
                    type = typeof(object);
                    str = "null";
                }
                else
                {
                    type = dictionary[key].GetType();
                    str = dictionary[key].ToString();
                }
                if (typeof(IDictionary) == type || typeof(Hashtable) == type)
                    str = SupportClass.DictionaryToString((IDictionary)dictionary[key]);
                if (typeof(string[]) == type)
                    str = string.Format("{{{0}}}", (object)string.Join(",", (string[])dictionary[key]));
                if (typeof(byte[]) == type)
                    str = string.Format("byte[{0}]", (object)((byte[])dictionary[key]).Length);
                if (includeTypes)
                    stringBuilder.AppendFormat("({0}){1}=({2}){3}", (object)key.GetType().Name, key, (object)type.Name, (object)str);
                else
                    stringBuilder.AppendFormat("{0}={1}", key, (object)str);
            }
            stringBuilder.Append("}");
            return stringBuilder.ToString();
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            uint[] numArray = new uint[256];
            for (int index1 = 0; index1 < 256; ++index1)
            {
                uint num = (uint)index1;
                for (int index2 = 0; index2 < 8; ++index2)
                {
                    if (((int)num & 1) == 1)
                        num = num >> 1 ^ polynomial;
                    else
                        num >>= 1;
                }
                numArray[index1] = num;
            }
            return numArray;
        }

        public static uint CalculateCrc(byte[] buffer, int length)
        {
            uint num = uint.MaxValue;
            uint polynomial = 3988292384;
            if (SupportClass.crcLookupTable == null)
                SupportClass.crcLookupTable = SupportClass.InitializeTable(polynomial);
            for (int index = 0; index < length; ++index)
                num = num >> 8 ^ SupportClass.crcLookupTable[(int)buffer[index] ^ (int)num & (int)byte.MaxValue];
            return num;
        }

        public delegate int IntegerMillisecondsDelegate();

        public class ThreadSafeRandom
        {
            private static readonly Random _r = new Random();

            public static int Next()
            {
                lock (SupportClass.ThreadSafeRandom._r)
                    return SupportClass.ThreadSafeRandom._r.Next();
            }
        }
    }
}
