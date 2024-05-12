
namespace NDG.Realtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    

    using UnityEngine;

    public class LoadBalancingPeer : Peer
    {

        private readonly Pool<Dictionary<byte, object>> paramDictionaryPool = new Pool<Dictionary<byte, object>>(() => new Dictionary<byte, object>(), x => x.Clear(), 1);

        /// <summary>
        /// 지정된 프로토콜로 peer를 생성합니다.
        /// </summary>
        /// <param name="protocolType"></param>
        public LoadBalancingPeer(ConnectionProtocol protocolType) : base(protocolType)
        {
            this.ConfigUnitySockets();
        }

        /// <summary>
        /// 지정된 프로토콜을 사용하는 peer와 콜백용 listener를 만듭니다.
        /// </summary>
        public LoadBalancingPeer(INetPeerListener listener, ConnectionProtocol protocolType) : this(protocolType)
        {
            this.Listener = listener;
        }

        private void ConfigUnitySockets()
        {
            this.SocketImplementationConfig[ConnectionProtocol.Tcp] = typeof(SocketTcpAsync);
        }

        public virtual bool OpGetRegions(string appId)
        {
            Dictionary<byte, object> parameters = new Dictionary<byte, object>(1);
            parameters[(byte)ParameterCode.ApplicationId] = appId;

            return this.SendOperation(OperationCode.GetRegions, parameters, new SendOptions() { Reliability = true, Encrypt = true });
        }

        /// <summary>
        /// 마스터 서버 로비에 참여합니다.
        /// </summary>
        /// <param name="lobby"></param>
        /// <returns></returns>
        public virtual bool OpJoinLobby(TypedLobby lobby = null)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpJoinLobby()");
            }

            Dictionary<byte, object> parameters = null;
            if (lobby != null && !lobby.IsDefault)
            {
                parameters = new Dictionary<byte, object>();
                parameters[(byte)ParameterCode.LobbyName] = lobby.Name;
                parameters[(byte)ParameterCode.LobbyType] = (byte)lobby.Type;
            }

            return this.SendOperation(OperationCode.JoinLobby, parameters, SendOptions.SendReliable);
        }

        /// <summary>
        /// 마스터서버 로비를 떠납니다.
        /// </summary>
        /// <returns></returns>
        public virtual bool OpLeaveLobby()
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpLeaveLobby()");
            }

            return this.SendOperation(OperationCode.LeaveLobby, null, SendOptions.SendReliable);
        }

        /// <summary>
        /// OnJoinRoom 및 OpCreateRoom에서 사용됩니다.
        /// </summary>
        private void RoomOptionsToOpParameters(Dictionary<byte, object> op, RoomOptions roomOptions, bool usePropertiesKey = false)
        {
            if (roomOptions == null)
            {
                roomOptions = new RoomOptions();
            }

            Hashtable gameProperties = new Hashtable();
            gameProperties[GamePropertyKey.IsOpen] = roomOptions.IsOpen;
            gameProperties[GamePropertyKey.IsVisible] = roomOptions.IsVisible;
            gameProperties[GamePropertyKey.PropsListedInLobby] = (roomOptions.CustomRoomPropertiesForLobby == null) ? new string[0] : roomOptions.CustomRoomPropertiesForLobby;
            gameProperties.MergeStringKeys(roomOptions.CustomRoomProperties);
            if (roomOptions.MaxPlayers > 0)
            {
                gameProperties[GamePropertyKey.MaxPlayers] = roomOptions.MaxPlayers;
            }

            if (!usePropertiesKey)
            {
                op[ParameterCode.GameProperties] = gameProperties;  
            }
            else
            {
                op[ParameterCode.Properties] = gameProperties;      
            }


            // int flags = 0;  

            // if (roomOptions.CleanupCacheOnLeave)
            // {
            //     op[ParameterCode.CleanupCacheOnLeave] = true;	                
            //     flags = flags | (int)RoomOptionBit.DeleteCacheOnLeave;        
            // }
            // else
            // {
            //     op[ParameterCode.CleanupCacheOnLeave] = false;	               
            //     gameProperties[GamePropertyKey.CleanupCacheOnLeave] = false;    
            // }

            // flags = flags | (int)RoomOptionBit.CheckUserOnJoin;
            // op[ParameterCode.CheckUserOnJoin] = true;

            // if (roomOptions.PlayerTtl > 0 || roomOptions.PlayerTtl == -1)
            // {
            //     op[ParameterCode.PlayerTTL] = roomOptions.PlayerTtl;    
            // }

            // if (roomOptions.EmptyRoomTtl > 0)
            // {
            //     op[ParameterCode.EmptyRoomTTL] = roomOptions.EmptyRoomTtl;  
            // }

            // if (roomOptions.SuppressRoomEvents)
            // {
            //     flags = flags | (int)RoomOptionBit.SuppressRoomEvents;
            //     op[ParameterCode.SuppressRoomEvents] = true;
            // }
            // if (roomOptions.Plugins != null)
            // {
            //     op[ParameterCode.Plugins] = roomOptions.Plugins;
            // }
            // if (roomOptions.PublishUserId)
            // {
            //     flags = flags | (int)RoomOptionBit.PublishUserId;
            //     op[ParameterCode.PublishUserId] = true;
            // }
            // if (roomOptions.DeleteNullProperties)
            // {
            //     flags = flags | (int)RoomOptionBit.DeleteNullProps; 
            // }
            // if (roomOptions.BroadcastPropsChangeToAll)
            // {
            //     flags = flags | (int)RoomOptionBit.BroadcastPropsChangeToAll; 
            // }

            // op[ParameterCode.RoomOptionFlags] = flags;
        }


        /// <summary>
        /// 마스터에 룸 생성을 요청합니다.
        /// 마스터가 연결할 게임 서버 및 룸의 데이터를 반환합니다.
        /// OnOperationResponse()를 트리거하는 비동기 요청입니다.
        /// </summary>
        public virtual bool OpCreateRoom(EnterRoomParams opParams)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpCreateRoom()");
            }

            Dictionary<byte, object> op = new Dictionary<byte, object>();

            if (!string.IsNullOrEmpty(opParams.RoomName))
            {
                op[ParameterCode.RoomName] = opParams.RoomName;
            }
            if (opParams.Lobby != null && !opParams.Lobby.IsDefault)
            {
                op[ParameterCode.LobbyName] = opParams.Lobby.Name;
                op[ParameterCode.LobbyType] = (byte)opParams.Lobby.Type;
            }

            if (opParams.ExpectedUsers != null && opParams.ExpectedUsers.Length > 0)
            {
                op[ParameterCode.Add] = opParams.ExpectedUsers;
            }
            if (opParams.OnGameServer)
            {
                if (opParams.PlayerProperties != null && opParams.PlayerProperties.Count > 0)
                {
                    op[ParameterCode.PlayerProperties] = opParams.PlayerProperties;
                    op[ParameterCode.Broadcast] = true; 
                }

                this.RoomOptionsToOpParameters(op, opParams.RoomOptions);
            }

            return this.SendOperation(OperationCode.CreateGame, op, SendOptions.SendReliable);
        }


        public virtual bool OpJoinRoom(EnterRoomParams opParams)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpJoinRoom()");
            }
            Dictionary<byte, object> op = new Dictionary<byte, object>();

            if (!string.IsNullOrEmpty(opParams.RoomName))
            {
                op[ParameterCode.RoomName] = opParams.RoomName;
            }

            //필수로 로비 name과 type이 포함되도록 수정
            op[ParameterCode.LobbyName] = opParams.Lobby.Name;
            op[ParameterCode.LobbyType] = (byte)opParams.Lobby.Type;

            // if (opParams.CreateIfNotExists)
            // {
            //     op[ParameterCode.JoinMode] = (byte)JoinMode.CreateIfNotExists;
            //     if (opParams.Lobby != null && !opParams.Lobby.IsDefault)
            //     {
            //         op[ParameterCode.LobbyName] = opParams.Lobby.Name;
            //         op[ParameterCode.LobbyType] = (byte)opParams.Lobby.Type;
            //     }
            // }

            if (opParams.RejoinOnly)
            {
                op[ParameterCode.JoinMode] = (byte)JoinMode.RejoinOnly; 
            }

            if (opParams.ExpectedUsers != null && opParams.ExpectedUsers.Length > 0)
            {
                op[ParameterCode.Add] = opParams.ExpectedUsers;
            }

            if (opParams.OnGameServer)
            {
                if (opParams.PlayerProperties != null && opParams.PlayerProperties.Count > 0)
                {
                    op[ParameterCode.PlayerProperties] = opParams.PlayerProperties;
                    op[ParameterCode.Broadcast] = true; 
                }

                if (opParams.CreateIfNotExists)
                {
                    this.RoomOptionsToOpParameters(op, opParams.RoomOptions);
                }
            }
            return this.SendOperation(OperationCode.JoinGame, op, SendOptions.SendReliable);
        }


        /// <summary>
        /// 임의의 사용 가능한 룸에 참가를 요청합니다.
        /// OnOperationResponse() 호출을 트리거하는 비동기 요청입니다.
        /// 모든 룸이 닫혀있거나 꽉찬 경우, 응답에 오류코드가 반환됩니다.
        /// </summary>
        public virtual bool OpJoinRandomRoom(OpJoinRandomRoomParams opJoinRandomRoomParams)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpJoinRandomRoom()");
            }

            Hashtable expectedRoomProperties = new Hashtable();
            expectedRoomProperties.MergeStringKeys(opJoinRandomRoomParams.ExpectedCustomRoomProperties);
            if (opJoinRandomRoomParams.ExpectedMaxPlayers > 0)
            {
                expectedRoomProperties[GamePropertyKey.MaxPlayers] = opJoinRandomRoomParams.ExpectedMaxPlayers;
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            if (expectedRoomProperties.Count > 0)
            {
                opParameters[ParameterCode.GameProperties] = expectedRoomProperties;
            }

            if (opJoinRandomRoomParams.MatchingType != MatchmakingMode.FillRoom)
            {
                opParameters[ParameterCode.MatchMakingType] = (byte)opJoinRandomRoomParams.MatchingType;
            }

            if (opJoinRandomRoomParams.TypedLobby != null && !opJoinRandomRoomParams.TypedLobby.IsDefault)
            {
                opParameters[ParameterCode.LobbyName] = opJoinRandomRoomParams.TypedLobby.Name;
                opParameters[ParameterCode.LobbyType] = (byte)opJoinRandomRoomParams.TypedLobby.Type;
            }

            if (!string.IsNullOrEmpty(opJoinRandomRoomParams.SqlLobbyFilter))
            {
                opParameters[ParameterCode.Data] = opJoinRandomRoomParams.SqlLobbyFilter;
            }

            if (opJoinRandomRoomParams.ExpectedUsers != null && opJoinRandomRoomParams.ExpectedUsers.Length > 0)
            {
                opParameters[ParameterCode.Add] = opJoinRandomRoomParams.ExpectedUsers;
            }
            return this.SendOperation(OperationCode.JoinRandomGame, opParameters, SendOptions.SendReliable);
        }

        /// <summary>
        /// 참여가능한 임의의 룸이 없으면 룸을 생성합니다
        /// </summary>
        public virtual bool OpJoinRandomOrCreateRoom(OpJoinRandomRoomParams opJoinRandomRoomParams, EnterRoomParams createRoomParams)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpJoinRandomOrCreateRoom()");
            }

            // join random room parameters:

            Hashtable expectedRoomProperties = new Hashtable();
            expectedRoomProperties.MergeStringKeys(opJoinRandomRoomParams.ExpectedCustomRoomProperties);
            if (opJoinRandomRoomParams.ExpectedMaxPlayers > 0)
            {
                expectedRoomProperties[GamePropertyKey.MaxPlayers] = opJoinRandomRoomParams.ExpectedMaxPlayers;
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            if (expectedRoomProperties.Count > 0)
            {
                opParameters[ParameterCode.GameProperties] = expectedRoomProperties;   
            }

            if (opJoinRandomRoomParams.MatchingType != MatchmakingMode.FillRoom)
            {
                opParameters[ParameterCode.MatchMakingType] = (byte)opJoinRandomRoomParams.MatchingType;
            }

            if (opJoinRandomRoomParams.TypedLobby != null && !opJoinRandomRoomParams.TypedLobby.IsDefault)
            {
                opParameters[ParameterCode.LobbyName] = opJoinRandomRoomParams.TypedLobby.Name;
                opParameters[ParameterCode.LobbyType] = (byte)opJoinRandomRoomParams.TypedLobby.Type;
            }

            if (!string.IsNullOrEmpty(opJoinRandomRoomParams.SqlLobbyFilter))
            {
                opParameters[ParameterCode.Data] = opJoinRandomRoomParams.SqlLobbyFilter;
            }

            if (opJoinRandomRoomParams.ExpectedUsers != null && opJoinRandomRoomParams.ExpectedUsers.Length > 0)
            {
                opParameters[ParameterCode.Add] = opJoinRandomRoomParams.ExpectedUsers;
            }


            opParameters[ParameterCode.JoinMode] = (byte)JoinMode.CreateIfNotExists;

            if (createRoomParams != null)
            {
                if (!string.IsNullOrEmpty(createRoomParams.RoomName))
                {
                    opParameters[ParameterCode.RoomName] = createRoomParams.RoomName;
                }

                this.RoomOptionsToOpParameters(opParameters, createRoomParams.RoomOptions, true);
            }

            return this.SendOperation(OperationCode.JoinRandomGame, opParameters, SendOptions.SendReliable);
        }


        public virtual bool OpLeaveRoom(bool becomeInactive, bool sendAuthCookie = false)
        {
            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            if (becomeInactive)
            {
                opParameters[ParameterCode.IsInactive] = true;
            }

            return this.SendOperation(OperationCode.Leave, opParameters, SendOptions.SendReliable);
        }


        /// <summary>
        /// sqlLobbyFilter에 해당하는 GameList를 가져오도록 요청합니다.
        /// </summary>
        public virtual bool OpGetGameList(TypedLobby lobby, string queryData)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpGetGameList()");
            }

            if (lobby == null)
            {
                if (this.DebugOut >= DebugLevel.INFO)
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "OpGetGameList 전송을 실패했습니다. Lobby상태가 null입니다.");
                }
                return false;
            }

            if (lobby.Type != LobbyType.SqlLobby)
            {
                if (this.DebugOut >= DebugLevel.INFO)
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "OpGetGameList 전송을 실패했습니다. LobbyType은 SqlLobby타입이어야 합니다.");
                }
                return false;
            }

            if (lobby.IsDefault)
            {
                if (this.DebugOut >= DebugLevel.INFO)
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "OpGetGameList 전송을 실패했습니다.  Lobby Name이 null이거나 empty 상태입니다.");
                }
                return false;
            }

            if (string.IsNullOrEmpty(queryData))
            {
                if (this.DebugOut >= DebugLevel.INFO)
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "OpGetGameList 전송을 실패했습니다. queryData가 null이거나 empty 상태입니다.");
                }
                return false;
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            opParameters[(byte)ParameterCode.LobbyName] = lobby.Name;
            opParameters[(byte)ParameterCode.LobbyType] = (byte)lobby.Type;
            opParameters[(byte)ParameterCode.Data] = queryData;

            return this.SendOperation(OperationCode.GetGameList, opParameters, SendOptions.SendReliable);
        }


        public bool OpSetCustomPropertiesOfActor(int actorNr, Hashtable actorProperties)
        {
            return this.OpSetPropertiesOfActor(actorNr, actorProperties.StripToStringKeys(), null);
        }


        /// <summary>
        /// player나 actor의 Propertie값 설정을 요청합니다..
        /// </summary>
        protected internal bool OpSetPropertiesOfActor(int actorNr, Hashtable actorProperties, Hashtable expectedProperties = null)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpSetPropertiesOfActor()");
            }

            if (actorNr <= 0 || actorProperties == null || actorProperties.Count == 0)
            {
                if (this.DebugOut >= DebugLevel.INFO)
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "OpSetPropertiesOfActor 전송에 실패했습니다. actorNumber가 0보다 커야하며 actorProperties가 null이거나 empty 상태이면 안됩니다.");
                }
                return false;
            }

            Debug.Log("SetPropertiesOfActor  :" + actorNr);

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            opParameters.Add(ParameterCode.Properties, actorProperties);
            opParameters.Add(ParameterCode.ActorNr, actorNr);
            opParameters.Add(ParameterCode.Broadcast, true);
            if (expectedProperties != null && expectedProperties.Count != 0)
            {
                opParameters.Add(ParameterCode.ExpectedValues, expectedProperties);
            }


            return this.SendOperation(OperationCode.SetProperties, opParameters, SendOptions.SendReliable);
        }


        protected bool OpSetPropertyOfRoom(byte propCode, object value)
        {
            Hashtable properties = new Hashtable();
            properties[propCode] = value;
            return this.OpSetPropertiesOfRoom(properties);
        }

        public bool OpSetCustomPropertiesOfRoom(Hashtable gameProperties)
        {
            return this.OpSetPropertiesOfRoom(gameProperties.StripToStringKeys());
        }

        /// <summary>
        /// Room의 Propertie 설정을 요청합니다.
        /// </summary>
        protected internal bool OpSetPropertiesOfRoom(Hashtable gameProperties, Hashtable expectedProperties = null)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpSetPropertiesOfRoom()");
            }
            if (gameProperties == null || gameProperties.Count == 0)
            {
                if (this.DebugOut >= DebugLevel.INFO)
                {
                    this.Listener.DebugReturn(DebugLevel.INFO, "OpSetPropertiesOfRoom 전송해 실패했습니다. gameProperties는 null이거나 비어있으면 안됩니다.");
                }
                return false;
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            opParameters.Add(ParameterCode.Properties, gameProperties);
            opParameters.Add(ParameterCode.Broadcast, true);
            if (expectedProperties != null && expectedProperties.Count != 0)
            {
                opParameters.Add(ParameterCode.ExpectedValues, expectedProperties);
            }


            return this.SendOperation(OperationCode.SetProperties, opParameters, SendOptions.SendReliable);
        }


        /// <summary>
        ///  현재 프로그램에 설정된 appID 및 appversion을 전송하여 이 프로그램을 서버측에서 인증합니다.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appVersion"></param>
        /// <param name="authValues"></param>
        /// <param name="regionCode"></param>
        /// <param name="getLobbyStatistics"></param>
        /// <returns></returns>
        public virtual bool OpAuthenticate(string appId, string appVersion, AuthenticationValues authValues, string regionCode, bool getLobbyStatistics)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpAuthenticate()");
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            if (getLobbyStatistics)
            {
                opParameters[ParameterCode.LobbyStats] = true;
            }

            if (authValues != null && authValues.Token != null)
            {
                opParameters[ParameterCode.Secret] = authValues.Token;
                return this.SendOperation(OperationCode.Authenticate, opParameters, SendOptions.SendReliable); 
            }


            opParameters[ParameterCode.AppVersion] = appVersion;
            opParameters[ParameterCode.ApplicationId] = appId;

            if (!string.IsNullOrEmpty(regionCode))
            {
                opParameters[ParameterCode.Region] = regionCode;
            }

            if (authValues != null)
            {

                if (!string.IsNullOrEmpty(authValues.UserId))
                {
                    opParameters[ParameterCode.UserId] = authValues.UserId;
                }

                if (authValues.AuthType != CustomAuthenticationType.None)
                {
                    opParameters[ParameterCode.ClientAuthenticationType] = (byte)authValues.AuthType;
                    if (!string.IsNullOrEmpty(authValues.AuthGetParameters))
                    {
                        opParameters[ParameterCode.ClientAuthenticationParams] = authValues.AuthGetParameters;
                    }
                    if (authValues.AuthPostData != null)
                    {
                        opParameters[ParameterCode.ClientAuthenticationData] = authValues.AuthPostData;
                    }
                }
            }

            return this.SendOperation(OperationCode.Authenticate, opParameters, new SendOptions() { Reliability = true, Encrypt = true });
        }

        /// <summary>
        /// 이 앱의 appId 및 appVersion을 전송하여 이 응용프로그램을 서버에서 인증합니다.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appVersion"></param>
        /// <param name="authValues"></param>
        /// <param name="regionCode"></param>
        /// <param name="encryptionMode"></param>
        /// <param name="expectedProtocol"></param>
        /// <returns></returns>
        public virtual bool OpAuthenticateOnce(string appId, string appVersion, AuthenticationValues authValues, string regionCode, EncryptionMode encryptionMode, ConnectionProtocol expectedProtocol)
        {
            if (this.DebugOut >= DebugLevel.INFO)
            {
                this.Listener.DebugReturn(DebugLevel.INFO, "OpAuthenticateOnce(): authValues = " + authValues + ", region = " + regionCode + ", encryption = " + encryptionMode);
            }

            var opParameters = new Dictionary<byte, object>();

            if (authValues != null && authValues.Token != null)
            {
                opParameters[ParameterCode.Secret] = authValues.Token;
                return this.SendOperation(OperationCode.AuthenticateOnce, opParameters, SendOptions.SendReliable); 
            }

            if (encryptionMode == EncryptionMode.DatagramEncryption && expectedProtocol != ConnectionProtocol.Udp)
            {
                throw new NotSupportedException("Expected protocol set to UDP, due to encryption mode DatagramEncryption.");  
            }

            opParameters[ParameterCode.ExpectedProtocol] = (byte)expectedProtocol;
            opParameters[ParameterCode.EncryptionMode] = (byte)encryptionMode;

            opParameters[ParameterCode.AppVersion] = appVersion;
            opParameters[ParameterCode.ApplicationId] = appId;

            if (!string.IsNullOrEmpty(regionCode))
            {
                opParameters[ParameterCode.Region] = regionCode;
            }

            if (authValues != null)
            {
                if (!string.IsNullOrEmpty(authValues.UserId))
                {
                    opParameters[ParameterCode.UserId] = authValues.UserId;
                }

                if (authValues.AuthType != CustomAuthenticationType.None)
                {
                    opParameters[ParameterCode.ClientAuthenticationType] = (byte)authValues.AuthType;
                    if (!string.IsNullOrEmpty(authValues.Token))
                    {
                        opParameters[ParameterCode.Secret] = authValues.Token;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(authValues.AuthGetParameters))
                        {
                            opParameters[ParameterCode.ClientAuthenticationParams] = authValues.AuthGetParameters;
                        }
                        if (authValues.AuthPostData != null)
                        {
                            opParameters[ParameterCode.ClientAuthenticationData] = authValues.AuthPostData;
                        }
                    }
                }
            }

            return this.SendOperation(OperationCode.AuthenticateOnce, opParameters, new SendOptions() { Reliability = true, Encrypt = true });
        }

        public virtual bool OpChangeGroups(byte[] groupsToRemove, byte[] groupsToAdd)
        {
            if (this.DebugOut >= DebugLevel.ALL)
            {
                this.Listener.DebugReturn(DebugLevel.ALL, "OpChangeGroups()");
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();
            if (groupsToRemove != null)
            {
                opParameters[(byte)ParameterCode.Remove] = groupsToRemove;
            }
            if (groupsToAdd != null)
            {
                opParameters[(byte)ParameterCode.Add] = groupsToAdd;
            }

            return this.SendOperation(OperationCode.ChangeGroups, opParameters, SendOptions.SendReliable);
        }

        /// <summary>
        /// 같은 룸안에 있는 플레이어에게 이벤트 컨텐츠들을 전송합니다.
        /// </summary>
        /// <param name="eventCode"></param> 이벤트 유형을 식별합니다. 0부터 시작합니다.
        /// <param name="customEventContent"></param> 직렬화가 된 이벤트 컨텐츠
        /// <param name="raiseEventOptions"></param> null을 전달하면 기본 옵션이 사용됩니다.
        /// <param name="sendOptions"></param> reliable 및 암호화 전송 옵션입니다.
        /// <returns></returns>
        public virtual bool OpRaiseEvent(byte eventCode, object customEventContent, RaiseEventOptions raiseEventOptions, SendOptions sendOptions)
        {
            var paramDict = this.paramDictionaryPool.Acquire();
            try
            {
                if (raiseEventOptions != null)
                {
                    if (raiseEventOptions.CachingOption != EventCaching.DoNotCache)
                    {
                        paramDict[(byte)ParameterCode.Cache] = (byte)raiseEventOptions.CachingOption;
                    }
                    switch (raiseEventOptions.CachingOption)
                    {
                        case EventCaching.SliceSetIndex:
                        case EventCaching.SlicePurgeIndex:
                        case EventCaching.SlicePurgeUpToIndex:

                            return this.SendOperation(OperationCode.RaiseEvent, paramDict, sendOptions);
                        case EventCaching.SliceIncreaseIndex:
                        case EventCaching.RemoveFromRoomCacheForActorsLeft:
                            return this.SendOperation(OperationCode.RaiseEvent, paramDict, sendOptions);
                        case EventCaching.RemoveFromRoomCache:
                            if (raiseEventOptions.TargetActors != null)
                            {
                                paramDict[(byte)ParameterCode.ActorList] = raiseEventOptions.TargetActors;
                            }
                            break;
                        default:
                            if (raiseEventOptions.TargetActors != null)
                            {
                                paramDict[(byte)ParameterCode.ActorList] = raiseEventOptions.TargetActors;
                            }
                            else if (raiseEventOptions.InterestGroup != 0)
                            {
                                paramDict[(byte)ParameterCode.Group] = raiseEventOptions.InterestGroup;
                            }
                            else if (raiseEventOptions.Receivers != ReceiverGroup.Others)
                            {
                                paramDict[(byte)ParameterCode.ReceiverGroup] = (byte)raiseEventOptions.Receivers;
                            }
                            break;
                    }
                }
                paramDict[(byte)ParameterCode.Code] = (byte)eventCode;
                if (customEventContent != null)
                {
                    paramDict[(byte)ParameterCode.Data] = customEventContent;
                }
                return this.SendOperation(OperationCode.RaiseEvent, paramDict, sendOptions);
            }
            finally
            {
                this.paramDictionaryPool.Release(paramDict);
            }
        }

        public virtual bool OpSettings(bool receiveLobbyStats)
        {
            if (this.DebugOut >= DebugLevel.ALL)
            {
                this.Listener.DebugReturn(DebugLevel.ALL, "OpSettings()");
            }

            Dictionary<byte, object> opParameters = new Dictionary<byte, object>();

            if (receiveLobbyStats)
            {
                opParameters[(byte)0] = receiveLobbyStats;
            }

            if (opParameters.Count == 0)
            {
                return true;
            }

            return this.SendOperation(OperationCode.ServerSettings, opParameters, SendOptions.SendReliable);
        }

    }

    /// <summary>
    /// RoomOptionFlags 매개변수에서 사용되는 이 비트마스크는 룸의 옵션을 설정합니다.
    /// </summary>
    internal enum RoomOptionBit : int
    {
        CheckUserOnJoin = 0x01,  // 룸에 플레이어가 참가시 UserId 체크
        DeleteCacheOnLeave = 0x02,  // 룸에서 나갔을시 캐시 삭제
        SuppressRoomEvents = 0x04,  // 모든 room 이벤트 통제
        PublishUserId = 0x08,  // UserId를 publish 해야함
        DeleteNullProps = 0x10,  // 속성이 null값인 경우 속성을 삭제해야한다는 플래그입니다.
        BroadcastPropsChangeToAll = 0x20,  //모든 플레이어들에게 Proppertie 변경 이벤트를 전송해야한다는 플래그입니다.
    }

    /// <summary>
    /// JoinRandomRoom 과 JoinRandomOrCreateRoom에서 매치메이킹시 매개 변수
    /// </summary>
    public class OpJoinRandomRoomParams
    {
        /// <summary>룸을 찾는데에 사용할 커스텀 프로퍼티를 기준을 저장합니다..</summary>
        public Hashtable ExpectedCustomRoomProperties;
        /// <summary>룸을 찾는데에 사용할 MaxPlayer 기준을 설정합니다.</summary>
        public byte ExpectedMaxPlayers;
        /// <summary>매치메이킹 타입 설정.</summary>
        public MatchmakingMode MatchingType;
        /// <summary>로비 타입 설정.</summary>
        public TypedLobby TypedLobby;
        /// <summary>SQL query to filter room matches. For default-typed lobbies, use ExpectedCustomRoomProperties instead.</summary>
        public string SqlLobbyFilter;

        public string[] ExpectedUsers;
    }

    /// <summary>
    /// 룸에 생성할때 사용하는 Parameter입니다.
    /// </summary>
    public class EnterRoomParams
    {
        /// <summary>생성할 룸의 이름입니다 null인 경우 서버에서 고유한 이름을 생성하며, null이 아닐 경우 중복되지 않아야 하며 중복되면 오류를 보냅니다.</summary>
        public string RoomName;
        /// <summary>룸 옵션.</summary>
        public RoomOptions RoomOptions;
        /// <summary>룸을 생성할 로비</summary>
        public TypedLobby Lobby;
        /// <summary>현재 플레이어에 정의된 CustomProperties</summary>
        public Hashtable PlayerProperties;
        /// <summary>마스터 서버에 보낼때 일부 작업을 건너 뛸때 사용됩니다. </summary>
        protected internal bool OnGameServer = true; 
        /// <summary>매치메이킹 실패시 룸을 생성할 수 있습니다..</summary>
        public bool CreateIfNotExists;

        public bool RejoinOnly;
        /// <summary>같이 참여할 것으로 예상되는 플레이어 목록.</summary>
        public string[] ExpectedUsers;
    }

    public class ErrorCode
    {
        /// <summary>0일 경우 정상 진행입니다. </summary>
        public const int Ok = 0;

        /// <summary> 현재 상태에 맞지 않는 작업은 실행할 수 없습니다. </summary>
        public const int OperationNotAllowedInCurrentState = -3;

        /// <summary>operation을 서버에서 실행할 수 없습니다.</summary>
        public const int InvalidOperation = -2;

        /// <summary>서버 내부에서 오류가 발생하였습니다.</summary>
        public const int InternalServerError = -1;

        /// <summary>(32767) 인증에 실패하였습니다. 서버에서 AppId를 알 수 없습니다.</summary>
        public const int InvalidAuthentication = 0x7FFF;

        /// <summary>(32766) GameId가 이미 사용중 입니다. 이름을 바꾸어야합니다.</summary>
        public const int GameIdAlreadyExists = 0x7FFF - 1;

        /// <summary>(32765) 플레이어가 꽉 찼습니다.</summary>
        public const int GameFull = 0x7FFF - 2;

        /// <summary>(32764) 게임이 이미 종료되어 참여할 수 없습니다.</summary>
        public const int GameClosed = 0x7FFF - 3;

        /// <summary>(32762) 서버에 인원이 모두 찼습니다.</summary>
        public const int ServerFull = 0x7FFF - 5;

        /// <summary>(32760) 랜덤 매치메이킹은 닫히지도 않고 아직 꽉 차지도 않은 룸이 존재하는 경우에만 성공합니다./summary>
        public const int NoRandomMatchFound = 0x7FFF - 7;

        /// <summary>(32758) 룸이 존재하지않는 경우 참여에 실패할 수 있습니다. 사용자가 참여하는동안 기존 플레이어가 나갈때 발생할 수 있습니다.</summary>
        public const int GameDoesNotExist = 0x7FFF - 9;

        /// <summary>(32757) 서버에서 할당된 최대 동시 접속자에 도달하여 인증이 실패했습니다..</summary>
        public const int MaxCcuReached = 0x7FFF - 10;

        /// <summary>(32756) 서버에서 설정한 특정 지역의 서버 인증 실패.</summary>
        public const int InvalidRegion = 0x7FFF - 11;

        /// <summary>
        /// (32755) 
        /// </summary>
        public const int CustomAuthenticationFailed = 0x7FFF - 12;

        /// <summary>(32753) 인증 티켓이 만료되었습니다. </summary>
        public const int AuthenticationTicketExpired = 0x7FF1;

        /// <summary>
        /// (32752) 
        /// </summary>
        public const int PluginReportedError = 0x7FFF - 15;

        /// <summary>
        /// (32751) 플러그인이 로드된 플러그인과 일치하지 않으면 CreateGame/JoinGame/Join작업이 실패합니다.
        /// </summary>
        public const int PluginMismatch = 0x7FFF - 16;

        /// <summary>
        /// (32750) 현재 피어가 이미 Join을 호출했거나 룸에 참여되있을경우입니다.
        /// </summary>
        public const int JoinFailedPeerAlreadyJoined = 32750; // 0x7FFF - 17,

        /// <summary>
        /// (32749)  비활성 플레이어 목록에 포함되어있습니다.
        /// </summary>
        public const int JoinFailedFoundInactiveJoiner = 32749; // 0x7FFF - 18,

        /// <summary>
        /// (32748) Actor목록에서 요청한 ActorNumber 또는 UserId를 가진 Actor가 포함되어 있지않습니다.
        /// </summary>
        public const int JoinFailedWithRejoinerNotFound = 32748; // 0x7FFF - 19,

        /// <summary>
        /// (32747)
        /// </summary>
        public const int JoinFailedFoundExcludedUserId = 32747; // 0x7FFF - 20,

        /// <summary>
        /// (32746 ActiveActors목록에 이미 요청한 ActorNr or UserId가 포함되어있습니다..
        /// </summary>
        public const int JoinFailedFoundActiveJoiner = 32746; // 0x7FFF - 21,

        /// <summary>
        /// (32743) 
        /// </summary>
        public const int OperationLimitReached = 32743; // 0x7FFF - 24,

        /// <summary>
        /// (32742) Slot예약 실패. 슬롯은 MaxPlayer를 초과할수 없음.
        /// </summary>
        public const int SlotError = 32742; // 0x7FFF - 25,

        /// <summary>
        /// (32741) 토큰에서 제공한 함호화 매개 변수가 옳바르지 않음.
        /// </summary>
        public const int InvalidEncryptionParameters = 32741; // 0x7FFF - 24,
    }


    public class ActorProperties
    {
        public const byte PlayerName = 255; 

       /// <summary>현재 게임에 참여하고 있는지 여부를 알려줍니다.</summary>
        public const byte IsInactive = 254;

        public const byte UserId = 253;
    }

    public class GamePropertyKey
    {
        public const byte MaxPlayers = 255;

        /// <summary>로비에서 룸을 숨길지 나타낼지 결정합니다..</summary>
        public const byte IsVisible = 254;

        public const byte IsOpen = 253;

        public const byte PlayerCount = 252;

        /// <summary>삭제되거나 삭제될 룸일경우 true</summary>
        public const byte Removed = 251;

        /// <summary>로비에서 보여지는 Custom Properties 목록/summary>
        public const byte PropsListedInLobby = 250;

        public const byte CleanupCacheOnLeave = 249;

        public const byte MasterClientId = (byte)248;

        public const byte ExpectedUsers = (byte)247;

        public const byte PlayerTtl = (byte)246;

        ///<summary>마지막 플레이어가 나갔을 시 룸이 유지되는 시간.</summary>
        public const byte EmptyRoomTtl = (byte)245;
    }

    public class EventCode
    {
        public const byte GameList = 230;

        public const byte GameListUpdate = 229;

        public const byte AppStats = 226;

        public const byte LobbyStats = 224;

        public const byte Join = (byte)255;

        public const byte Leave = (byte)254;

        public const byte PropertiesChanged = (byte)253;

        public const byte ErrorInfo = 251;

        public const byte CacheSliceChanged = 250;

        public const byte AuthEvent = 223;
    }

    public class ParameterCode
    {
        public const byte SuppressRoomEvents = 237;

        public const byte EmptyRoomTTL = 236;

        public const byte PlayerTTL = 235;

        public const byte EventForward = 234;

        public const byte IsInactive = (byte)233;

        public const byte CheckUserOnJoin = (byte)232;

        public const byte ExpectedValues = (byte)231;

        public const byte Address = 230;

        public const byte PeerCount = 229;

        public const byte GameCount = 228;

        public const byte MasterPeerCount = 227;

        public const byte UserId = 225;

        public const byte ApplicationId = 224;

        public const byte Position = 223;

        public const byte MatchMakingType = 223;

        public const byte GameList = 222;

        public const byte Secret = 221;

        public const byte AppVersion = 220;

        public const byte RoomName = (byte)255;

        public const byte Broadcast = (byte)250;

        public const byte ActorList = (byte)252;

        public const byte ActorNr = (byte)254;

        public const byte PlayerProperties = (byte)249;

        public const byte CustomEventContent = (byte)245;

        public const byte Data = (byte)245;

        public const byte Code = (byte)244;

        public const byte GameProperties = (byte)248;

        public const byte Properties = (byte)251;

        public const byte TargetActorNr = (byte)253;

        public const byte ReceiverGroup = (byte)246;

        public const byte Cache = (byte)247;

        public const byte CleanupCacheOnLeave = (byte)241;

        public const byte Group = 240;

        public const byte Remove = 239;

        public const byte PublishUserId = 239;

        public const byte Add = 238;

        public const byte Info = 218;

        public const byte ClientAuthenticationType = 217;

        public const byte ClientAuthenticationParams = 216;

        public const byte JoinMode = 215;

        public const byte ClientAuthenticationData = 214;

        public const byte MasterClientId = (byte)203;

        public const byte LobbyName = (byte)213;

        public const byte LobbyType = (byte)212;

        public const byte LobbyStats = (byte)211;

        public const byte Region = (byte)210;

        public const byte CacheSliceIndex = 205;

        public const byte Plugins = 204;

        public const byte NickName = 202;

        public const byte PluginName = 201;

        public const byte PluginVersion = 200;

        public const byte Cluster = 196;

        public const byte ExpectedProtocol = 195;

        public const byte CustomInitData = 194;

        public const byte EncryptionMode = 193;

        public const byte EncryptionData = 192;

        public const byte RoomOptionFlags = 191;
    }

    public class OperationCode
    {
        public const byte Join = 255;

        public const byte AuthenticateOnce = 231;

        public const byte Authenticate = 230;

        public const byte JoinLobby = 229;

        public const byte LeaveLobby = 228;

        public const byte CreateGame = 227;

        public const byte JoinGame = 226;

        public const byte JoinRandomGame = 225;

        public const byte Leave = (byte)254;

        public const byte RaiseEvent = (byte)253;

        public const byte SetProperties = (byte)252;

        public const byte GetProperties = (byte)251;

        public const byte ChangeGroups = (byte)248;

        public const byte GetLobbyStats = 221;

        public const byte GetRegions = 220;

        public const byte ServerSettings = 218;

        public const byte GetGameList = 217;
    }

    public enum JoinMode : byte
    {
        Default = 0,

        CreateIfNotExists = 1,

        JoinOrRejoin = 2,

        RejoinOnly = 3,
    }

    public enum MatchmakingMode : byte
    {
        /// <summary>가능한 빠르게 방을 채울 수 있게</summary>
        FillRoom = 0,

        /// <summary>고르게 분배</summary>
        SerialMatching = 1,

        RandomMatching = 2
    }

    public enum ReceiverGroup : byte
    {
        Others = 0,

        All = 1,

        MasterClient = 2,
    }

    public enum EventCaching : byte
    {
        DoNotCache = 0,

        MergeCache = 1,

        ReplaceCache = 2,

        RemoveCache = 3,

        AddToRoomCache = 4,

        AddToRoomCacheGlobal = 5,

        RemoveFromRoomCache = 6,

        RemoveFromRoomCacheForActorsLeft = 7,

        SliceIncreaseIndex = 10,

        SliceSetIndex = 11,

        SlicePurgeIndex = 12,

        SlicePurgeUpToIndex = 13,
    }

    /// <summary>
    /// 룸 생성시 사용하는 공통적인 옵션입니다.
    /// </summary>
    public class RoomOptions
    {
        /// <summary>
        /// 로비 룸목록에 보여질지 설정합니다.
        /// </summary>
        public bool IsVisible { get { return this.isVisible; } set { this.isVisible = value; } }
        private bool isVisible = true;

        /// <summary>새로운 플레이어가 참여 가능한지 설정합니다.</summary>
        public bool IsOpen { get { return this.isOpen; } set { this.isOpen = value; } }
        private bool isOpen = true;

        public byte MaxPlayers;

        public int PlayerTtl;

        public int EmptyRoomTtl;

        /// <summary>
        /// 플레이어가 나갈때 룸에서 사용자의 이벤트 및 속성을 제거합니다.
        /// </summary>
        public bool CleanupCacheOnLeave { get { return this.cleanupCacheOnLeave; } set { this.cleanupCacheOnLeave = value; } }
        private bool cleanupCacheOnLeave = true;

        public Hashtable CustomRoomProperties;

        public string[] CustomRoomPropertiesForLobby = new string[0];

        public string[] Plugins;
        public bool SuppressRoomEvents { get; set; }

        public bool PublishUserId { get; set; }

        /// <summary>
        /// Properties가 null로 설정될시 삭제할지 여부를 설정합니다.
        /// </summary>
        public bool DeleteNullProperties { get; set; }

        public bool BroadcastPropsChangeToAll { get { return this.broadcastPropsChangeToAll; } set { this.broadcastPropsChangeToAll = value; } }
        private bool broadcastPropsChangeToAll = true;


    }


    public class RaiseEventOptions
    {
        public readonly static RaiseEventOptions Default = new RaiseEventOptions();

        public EventCaching CachingOption;

        public byte InterestGroup;

        public int[] TargetActors;

        public ReceiverGroup Receivers;
    }

    public enum LobbyType : byte
    {
        Default = 0,
        SqlLobby = 2,
        AsyncRandomLobby = 3
    }

    /// <summary>
    /// 서버상의 로비입니다.
    /// </summary>
    public class TypedLobby
    {

        public string Name;

        public LobbyType Type;

        public static readonly TypedLobby Default = new TypedLobby();

        public bool IsDefault { get { return string.IsNullOrEmpty(this.Name); } }

        internal TypedLobby()
        {
        }

        public TypedLobby(string name, LobbyType type)
        {
            this.Name = name;
            this.Type = type;
        }

        public override string ToString()
        {
            return string.Format("lobby '{0}'[{1}]", this.Name, this.Type);
        }
    }

    /// <summary>
    /// 서버상의 로비 정보입니다.
    /// </summary>
    public class TypedLobbyInfo : TypedLobby
    {
        public int PlayerCount;

        public int RoomCount;

        public override string ToString()
        {
            return string.Format("TypedLobbyInfo '{0}'[{1}] rooms: {2} players: {3}", this.Name, this.Type, this.RoomCount, this.PlayerCount);
        }
    }

    public enum AuthModeOption { Auth, AuthOnce, AuthOnceWss }

    public enum CustomAuthenticationType : byte
    {
        Custom = 0,
        None = byte.MaxValue
    }


    /// <summary>
    /// 서버에서 사용자 인증을 위한 컨테이너 입니다.
    /// 연결하기전에 AuthValues를 설정하세요.
    /// </summary>
    public class AuthenticationValues
    {
        private CustomAuthenticationType authType = CustomAuthenticationType.None;

        //커스텀 사용자 인증
        public CustomAuthenticationType AuthType
        {
            get { return authType; }
            set { authType = value; }
        }

        public string AuthGetParameters { get; set; }

        public object AuthPostData { get; private set; }

        public string Token { get; protected internal set; }

        public string UserId { get; set; }

        public AuthenticationValues()
        {
        }

        public AuthenticationValues(string userId)
        {
            this.UserId = userId;
        }

        public virtual void SetAuthPostData(string stringData)
        {
            this.AuthPostData = (string.IsNullOrEmpty(stringData)) ? null : stringData;
        }

        public virtual void SetAuthPostData(byte[] byteData)
        {
            this.AuthPostData = byteData;
        }

        public virtual void SetAuthPostData(Dictionary<string, object> dictData)
        {
            this.AuthPostData = dictData;
        }


        public virtual void AddAuthParameter(string key, string value)
        {
            string ampersand = string.IsNullOrEmpty(this.AuthGetParameters) ? "" : "&";
            this.AuthGetParameters = string.Format("{0}{1}{2}={3}", this.AuthGetParameters, ampersand, System.Uri.EscapeDataString(key), System.Uri.EscapeDataString(value));
        }


        public override string ToString()
        {
            return string.Format("AuthenticationValues Type: {3} UserId: {0}, GetParameters: {1} Token available: {2}", this.UserId, this.AuthGetParameters, !string.IsNullOrEmpty(this.Token), this.AuthType);
        }
    }

}