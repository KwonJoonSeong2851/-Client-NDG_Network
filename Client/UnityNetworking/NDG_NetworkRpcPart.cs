



namespace NDG.UnityNet
{
    using System.Collections.Generic;
    using System;
    using System.Reflection;
    using System.Collections;
    using UnityEngine;

    using NDG.Realtime;


    public class RPC : Attribute { }

    public static partial class NDG_Network
    {
        //활성화 되지않은 MonoBehaviour의 RPC호출을 방지합니다.
        public static bool UseRpcMonoBehaviourCache;

        private static readonly Dictionary<Type, List<MethodInfo>> monoRPCMethodsCache = new Dictionary<Type, List<MethodInfo>>();

        private static Dictionary<string, int> rpcShortcuts;

        public static bool RunRpcCoroutines = true;


        private static readonly Type typeNetRPC = typeof(RPC);
        private static readonly Type typeNetworkMessageInfo = typeof(NetworkMessageInfo);
        private static readonly object keyByteZero = (byte)0; //View ID
        private static readonly object keyByteOne = (byte)1; //Prefix
        private static readonly object keyByteTwo = (byte)2; //Send Time
        private static readonly object keyByteThree = (byte)3; //Method name
        private static readonly object keyByteFour = (byte)4; // Arguments
        private static readonly object keyByteFive = (byte)5; //RPC Index
        private static readonly object keyByteSix = (byte)6;
        private static readonly object keyByteSeven = (byte)7;
        private static readonly object keyByteEight = (byte)8;
        private static readonly object[] emptyObjectArray = new object[0];
        private static readonly Type[] emptyTypeArray = new Type[0];


        /// <summary>
        /// 수신된 RPC를 실행합니다.
        /// </summary>
        internal static void ExecuteRpc(Hashtable rpcData, Player sender)
        {
            if (rpcData == null || !rpcData.ContainsKey(keyByteZero))
            {
                Debug.LogError("잘못된 형식의 RPC입니다 . Data: " + rpcData.ToString());
                return;
            }

            int netViewID = (int)rpcData[keyByteZero]; 
            int otherSidePrefix = 0;    // 기본적으로 Prefix는 0이며 이 Prefix는 전송되지 않습니다.
            if (rpcData.ContainsKey(keyByteOne))
            {
                otherSidePrefix = (short)rpcData[keyByteOne];
            }


            string inMethodName;
            if (rpcData.ContainsKey(keyByteFive))
            {
                int rpcIndex = (byte)rpcData[keyByteFive];  // LIMITS RPC COUNT
                if (rpcIndex > NDG_Network.ServerSetting.RpcList.Count - 1)
                {
                    Debug.LogError("현재 Index의 RPC를 찾을 수 없습니다.: " + rpcIndex );
                    return;
                }
                else
                {
                    inMethodName = NDG_Network.ServerSetting.RpcList[rpcIndex];
                }
            }
            else
            {
                inMethodName = (string)rpcData[keyByteThree];
            }

            object[] arguments = null;
            if (rpcData.ContainsKey(keyByteFour))
            {
                arguments = (object[])rpcData[keyByteFour];
            }

            NetworkView netView = GetNetworkView(netViewID);
            if (netView == null)
            {
                int viewOwnerId = netViewID / NDG_Network.MAX_VIEW_IDS;
                bool owningNv = (viewOwnerId == NetworkingClient.LocalPlayer.ActorNumber);
                bool ownerSent = sender != null && viewOwnerId == sender.ActorNumber;

                if (owningNv)
                {
                    Debug.LogWarning("Received RPC \"" + inMethodName + "\" for viewID " + netViewID + " 하지만 해당 View가 존재하지 않습니다." + (ownerSent ? " Owner called." : " Remote called.") + " By: " + sender);
                }
                else
                {
                    Debug.LogWarning("Received RPC \"" + inMethodName + "\" for viewID " + netViewID + " 하지만 해당 View가 존재하지 않습니다." + (ownerSent ? " Owner called." : " Remote called.") + " By: " + sender + " GO는 삭제되었지만 RPC는 제거되지 않았습니다.");
                }
                return;
            }

            //Prefix가 같지않을경우 Return
            if (netView.Prefix != otherSidePrefix)
            {
                Debug.LogError("Received RPC \"" + inMethodName + "\" viewID " + netViewID + " prefix" + otherSidePrefix + "  " + netView.Prefix + ". 해당 RPC가 생략되었습니다.");
                return;
            }

            //Method Name이 공백일경우
            if (string.IsNullOrEmpty(inMethodName))
            {
                Debug.LogError("잘못된 형식의 RPC입니다. Data: " + rpcData.ToString());
                return;
            }

            if (NDG_Network.LogLevel >= NetLogLevel.Full)
            {
                Debug.Log("Received RPC: " + inMethodName);
            }


            //그룹 필터링
            if (netView.Group != 0 && !allowedReceivingGroups.Contains(netView.Group))
            {
                return;
            }

            Type[] argumentsTypes = null;
            if (arguments != null && arguments.Length > 0)
            {
                argumentsTypes = new Type[arguments.Length];
                int i = 0;
                for (int index = 0; index < arguments.Length; index++)
                {
                    object objX = arguments[index];
                    if (objX == null)
                    {
                        argumentsTypes[i] = null;
                    }
                    else
                    {
                        argumentsTypes[i] = objX.GetType();
                    }

                    i++;
                }
            }


            int receivers = 0;
            int foundMethods = 0;
            if (!NDG_Network.UseRpcMonoBehaviourCache || netView.RpcMonoBehaviours == null || netView.RpcMonoBehaviours.Length == 0)
            {
                netView.RefreshRpcMonoBehaviourCache();
            }

            for (int componentsIndex = 0; componentsIndex < netView.RpcMonoBehaviours.Length; componentsIndex++)
            {
                MonoBehaviour monob = netView.RpcMonoBehaviours[componentsIndex];
                if (monob == null)
                {
                    Debug.LogError("ERROR: GameObject에 MonoBehavours가 없습니다.");
                    continue;
                }

                Type type = monob.GetType();

                //  캐시에서 [RPC] method들을 가져옵니다.
                List<MethodInfo> cachedRPCMethods = null;
                bool methodsOfTypeInCache = monoRPCMethodsCache.TryGetValue(type, out cachedRPCMethods);

                if (!methodsOfTypeInCache)
                {
                    List<MethodInfo> entries = SupportClass.GetMethods(type, typeNetRPC);

                    monoRPCMethodsCache[type] = entries;
                    cachedRPCMethods = entries;
                }

                if (cachedRPCMethods == null)
                {
                    continue;
                }

                // 캐시에서 메서드 이름과 인수가 올바른지 확인합니다.
                for (int index = 0; index < cachedRPCMethods.Count; index++)
                {
                    MethodInfo mInfo = cachedRPCMethods[index];
                    if (!mInfo.Name.Equals(inMethodName))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = mInfo.GetCachedParemeters();
                    foundMethods++;


                    // 인수가 없다면
                    if (arguments == null)
                    {
                        if (parameters.Length == 0)
                        {
                            receivers++;
                            object o = mInfo.Invoke((object)monob, null);
                            if (NDG_Network.RunRpcCoroutines)
                            {
                                IEnumerator ie = null;
                                if ((ie = o as IEnumerator) != null)
                                {
                                    NetworkHandler.Instance.StartCoroutine(ie);
                                }
                            }
                        }
                        else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(NetworkMessageInfo))
                        {
                            int sendTime = (int)rpcData[keyByteTwo];

                            receivers++;
                            object o = mInfo.Invoke((object)monob, new object[] { new NetworkMessageInfo(sender, sendTime, netView) });
                            if (NDG_Network.RunRpcCoroutines)
                            {
                                IEnumerator ie = null;//o as IEnumerator;
                                if ((ie = o as IEnumerator) != null)
                                {
                                    NetworkHandler.Instance.StartCoroutine(ie);
                                }
                            }
                        }
                        continue;
                    }


                    //인수가 있다면 메서드가 호환되는지 확인.
                    if (parameters.Length == arguments.Length)
                    {
                        if (CheckTypeMatch(parameters, argumentsTypes))
                        {
                            receivers++;
                            object o = mInfo.Invoke((object)monob, arguments);
                            if (NDG_Network.RunRpcCoroutines)
                            {
                                IEnumerator ie = null;
                                if ((ie = o as IEnumerator) != null)
                                {
                                    NetworkHandler.Instance.StartCoroutine(ie);
                                }
                            }
                        }
                        continue;
                    }

                    if (parameters.Length == arguments.Length + 1)
                    {
                        // 마지막 메세지인지 확인합니다.
                        if (parameters[parameters.Length - 1].ParameterType == typeof(NetworkMessageInfo) && CheckTypeMatch(parameters, argumentsTypes))
                        {
                            int sendTime = (int)rpcData[(byte)2];
                            object[] argumentsWithInfo = new object[arguments.Length + 1];
                            arguments.CopyTo(argumentsWithInfo, 0);
                            argumentsWithInfo[argumentsWithInfo.Length - 1] = new NetworkMessageInfo(sender, sendTime, netView);

                            receivers++;
                            object o = mInfo.Invoke((object)monob, argumentsWithInfo);
                            if (NDG_Network.RunRpcCoroutines)
                            {
                                IEnumerator ie = null;
                                if ((ie = o as IEnumerator) != null)
                                {
                                    NetworkHandler.Instance.StartCoroutine(ie);
                                }
                            }
                        }
                        continue;
                    }

                    if (parameters.Length == 1 && parameters[0].ParameterType.IsArray)
                    {
                        receivers++;
                        object o = mInfo.Invoke((object)monob, new object[] { arguments });
                        if (NDG_Network.RunRpcCoroutines)
                        {
                            IEnumerator ie = null;
                            if ((ie = o as IEnumerator) != null)
                            {
                                NetworkHandler.Instance.StartCoroutine(ie);
                            }
                        }
                        continue;
                    }
                }
            }

            // Error 처리
            if (receivers != 1)
            {
                string argsString = string.Empty;
                int argsLength = 0;
                if (argumentsTypes != null)
                {
                    argsLength = argumentsTypes.Length;
                    for (int index = 0; index < argumentsTypes.Length; index++)
                    {
                        Type ty = argumentsTypes[index];
                        if (argsString != string.Empty)
                        {
                            argsString += ", ";
                        }

                        if (ty == null)
                        {
                            argsString += "null";
                        }
                        else
                        {
                            argsString += ty.Name;
                        }
                    }
                }

                GameObject context = netView != null ? netView.gameObject : null;
                if (receivers == 0)
                {
                    if (foundMethods == 0)
                    {
                        // 일치하는 메서드를 찾을 수 없을때
                        Debug.LogErrorFormat(context, "RPC method '{0}({2})' 가 있는 NetworkView 객체를 찾을수 없습니다. NetView ID: {1}.", inMethodName, netViewID, argsString);
                    }
                    else
                    {
                        // 메서드를 찾았지만 인수가 올바르지 않을때
                        Debug.LogErrorFormat(context, "RPC method '{0}' 와 일치하는 메서드를 찾았지만 {1} 인수가 일치하지 않습니다.", inMethodName, netViewID, argsString);
                    }
                }
                else
                {
                    // 동일한 메서드가 여러개 존재할때
                    Debug.LogErrorFormat(context, "RPC method '{0}({2})' 와 {3}이 하나의 NetworkView에 존재합니다. {1}. 객체당 하나의 메서드만 갖을수 있습니다..", inMethodName, netViewID, argsString, foundMethods);
                }
            }
        }


        /// <summary>
        /// 모든 메서드가 매개변수와 일치하는지 확인합니다.
        /// </summary>
        private static bool CheckTypeMatch(ParameterInfo[] methodParameters, Type[] callParameterTypes)
        {
            if (methodParameters.Length < callParameterTypes.Length)
            {
                return false;
            }

            for (int index = 0; index < callParameterTypes.Length; index++)
            {
                Type type = methodParameters[index].ParameterType;
                if (callParameterTypes[index] != null && !type.IsAssignableFrom(callParameterTypes[index]) && !(type.IsEnum && System.Enum.GetUnderlyingType(type).IsAssignableFrom(callParameterTypes[index])))
                {
                    return false;
                }
            }
            return true;
        }


        static Hashtable rpcEvent = new Hashtable();
        static RaiseEventOptions RpcOptionsToAll = new RaiseEventOptions();


        internal static void RPC(NetworkView view, string methodName, RpcTarget target, Player player, bool encrypt, params object[] parameters)
        {
            if (blockedSendingGroups.Contains(view.Group))
            {
                return; 
            }

            if (view.ViewID < 1)
            {
                Debug.LogError("Network RPC Part : 등록되지 않은 NetworkView:" + view.ViewID + " method: " + methodName + " GO:" + view.gameObject.name);
            }

            if (NDG_Network.LogLevel >= NetLogLevel.Full)
            {
                Debug.Log("Sending RPC \"" + methodName + "\" to target: " + target + " or player:" + player + ".");
            }

            rpcEvent.Clear();

            rpcEvent[keyByteZero] = (int)view.ViewID; 
            if (view.Prefix > 0)
            {
                rpcEvent[keyByteOne] = (short)view.Prefix;
            }
            rpcEvent[keyByteTwo] = NDG_Network.ServerTimestamp;


            int shortcut = 0;
            if (rpcShortcuts.TryGetValue(methodName, out shortcut))
            {
                rpcEvent[keyByteFive] = (byte)shortcut; 
            }
            else
            {
                rpcEvent[keyByteThree] = methodName;
            }

            if (parameters != null && parameters.Length > 0)
            {
                rpcEvent[keyByteFour] = (object[])parameters;
            }

            SendOptions sendOptions = SendOptions.SendReliable;

            if (player != null)
            {
                if (NetworkingClient.LocalPlayer.ActorNumber == player.ActorNumber)
                {
                    ExecuteRpc(rpcEvent, player);
                }
                else
                {
                    RaiseEventOptions options = new RaiseEventOptions() { TargetActors = new int[] { player.ActorNumber } };
                    NDG_Network.RaiseEventInternal(NetEvent.RPC, rpcEvent, options, sendOptions);
                }

                return;
            }

            switch (target)
            {
                case RpcTarget.All:
                    RpcOptionsToAll.InterestGroup = (byte)view.Group;   
                    NDG_Network.RaiseEventInternal(NetEvent.RPC, rpcEvent, RpcOptionsToAll, sendOptions);

                    // Execute local
                    ExecuteRpc(rpcEvent, NetworkingClient.LocalPlayer);
                    break;
                case RpcTarget.Others:
                    {
                        RaiseEventOptions options = new RaiseEventOptions() { InterestGroup = (byte)view.Group };
                        NDG_Network.RaiseEventInternal(NetEvent.RPC, rpcEvent, options, sendOptions);
                        break;
                    }


                case RpcTarget.MasterClient:
                    {
                        if (NetworkingClient.LocalPlayer.IsMasterClient)
                        {
                            ExecuteRpc(rpcEvent, NetworkingClient.LocalPlayer);
                        }
                        else
                        {
                            RaiseEventOptions options = new RaiseEventOptions() { Receivers = ReceiverGroup.MasterClient };
                            NDG_Network.RaiseEventInternal(NetEvent.RPC, rpcEvent, options, sendOptions);
                        }

                        break;
                    }

                default:
                    break;
            }
        }


       

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



    }
}
