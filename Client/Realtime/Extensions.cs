

namespace NDG.Realtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    public static class Extensions
    {
        /// <summary>
        /// 대상 해시테이블에 문자열 유형의 키를 병합
        /// </summary>
        public static void MergeStringKeys(this IDictionary target, IDictionary addHash)
        {
            if (addHash == null || target.Equals(addHash))
            {
                return;
            }

            foreach (object key in addHash.Keys)
            {
                if (key is string)
                {
                    target[key] = addHash[key];
                }
            }
        }

        /// <summary>
        ///  모든 문자열 형식 키를 새 해시테이블에 복사
        /// </summary>
        public static Hashtable StripToStringKeys(this IDictionary original)
        {
            Hashtable target = new Hashtable();
            if (original != null)
            {
                foreach (object key in original.Keys)
                {
                    if (key is string)
                    {
                        target[key] = original[key];
                    }
                }
            }

            return target;
        }


        //NULL값을 가진 키 리스트
        private static readonly List<object> keysWithNullValue = new List<object>();

        /// <summary>
        ///         NULL값을 가진 모든 키 제거
        /// </summary>      
        public static void StripKeysWithNullValues(this IDictionary original)
        {
            lock (keysWithNullValue)
            {
                keysWithNullValue.Clear();

                foreach (DictionaryEntry entry in original)
                {
                    if (entry.Value == null)
                    {
                        keysWithNullValue.Add(entry.Key);
                    }
                }

                for (int i = 0; i < keysWithNullValue.Count; i++)
                {
                    var key = keysWithNullValue[i];
                    original.Remove(key);
                }
            }
        }

        /// <summary>
        /// 특정 정수 값이 배열 내에 있는지 확인합니다.
        /// </summary>
        public static bool Contains(this int[] target, int nr)
        {
            if (target == null)
            {
                return false;
            }

            for (int index = 0; index < target.Length; index++)
            {
                if (target[index] == nr)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
