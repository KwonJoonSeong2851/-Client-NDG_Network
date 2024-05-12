
namespace NDG.UnityNet
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.SceneManagement;


    public static class Extensions
    {
        public static Dictionary<MethodInfo, ParameterInfo[]> ParametersOfMethods = new Dictionary<MethodInfo, ParameterInfo[]>();

        public static ParameterInfo[] GetCachedParemeters(this MethodInfo mo)
        {
            ParameterInfo[] result;
            bool cached = ParametersOfMethods.TryGetValue(mo, out result);

            if (!cached)
            {
                result = mo.GetParameters();
                ParametersOfMethods[mo] = result;
            }

            return result;
        }

        /// <summary>
        /// 해당 게임오브젝트에서 NetworkView를 자식으로 갖고있는 객체를 탐색합니다.
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        public static NetworkView[] GetNetworkViewsInChildren(this UnityEngine.GameObject go)
        {
            return go.GetComponentsInChildren<NetworkView>(true) as NetworkView[];
        }

        /// <summary>
        /// 첫번째와 두번째 값이 비슷한지 확인하는 함수
        /// </summary>
        public static bool AlmostEquals(this Vector3 target, Vector3 second, float sqrMagnitudePrecision)
        {
            return (target - second).sqrMagnitude < sqrMagnitudePrecision;
        }

        public static bool AlmostEquals(this Vector2 target, Vector2 second, float sqrMagnitudePrecision)
        {
            return (target - second).sqrMagnitude < sqrMagnitudePrecision; 
        }

        public static bool AlmostEquals(this Quaternion target, Quaternion second, float maxAngle)
        {
            return Quaternion.Angle(target, second) < maxAngle;
        }

        public static bool AlmostEquals(this float target, float second, float floatDiff)
        {
            return Mathf.Abs(target - second) < floatDiff;
        }


        //public static byte[] ObjectToByteArray(this object obj)
        //{
        //    if(obj == null)
        //    {
        //        return null;
        //    }

        //    BinaryFormatter bf = new BinaryFormatter();
        //    MemoryStream ms = new MemoryStream();
        //    bf.Serialize(ms, obj);
        //    return ms.ToArray();
        //}
    }
}