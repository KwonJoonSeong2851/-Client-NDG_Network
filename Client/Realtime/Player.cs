using System.Collections;
using System.Collections.Generic;


namespace NDG.Realtime
{
    [System.Serializable]
    public class Player
    {
        protected internal Room RoomReference { get; set; }

        private int actorNumber = -1;

        public int ActorNumber
        {
            get { return this.actorNumber; }
        }

        public readonly bool IsLocal;

        public bool HasRejoined
        {
            get; internal set;
        }

        private string nickName = string.Empty;

        public string NickName
        {
            get
            {
                return this.nickName;
            }
            set
            {
                if(!string.IsNullOrEmpty(this.nickName) && this.nickName.Equals(value))
                {
                    return;
                }
                this.nickName = value;

                if(this.IsLocal)
                {

                    this.SetPlayerNameProperty();
                }
            }
        }

        public string UserId { get; internal set; }

        public bool IsMasterClient
        {
            get
            {
                if(this.RoomReference == null)
                {
                    return false;
                }

                return this.ActorNumber == this.RoomReference.MasterClientId;
            }
        }


        public bool IsInactive { get; protected internal set; }

        public Hashtable CustomProperties { get; set; }

        protected internal Player(string nickName, int actorNumber, bool isLocal, Hashtable playerProperties)
        {
            this.IsLocal = isLocal;
            this.actorNumber = actorNumber;
            this.NickName = nickName;

            this.CustomProperties = new Hashtable();
            this.InternalCacheProperties(playerProperties);
        }

        public Player Get(int id)
        {
            if(this.RoomReference == null)
            {
                return null;
            }

            return this.RoomReference.GetPlayer(id);
        }

        public Player GetNext()
        {
            return GetNextFor(this.ActorNumber);
        }

        /// <summary>
        /// 정렬된 플레이어 목록에서 다음 플레이어를 가져옵니다.
        /// </summary>
        public Player GetNextFor(Player currentPlayer)
        {
            if (currentPlayer == null)
            {
                return null;
            }
            return GetNextFor(currentPlayer.ActorNumber);
        }

        /// <summary>
        /// 정렬된 플레이어 목록에서 다음 플레이어를 가져옵니다.
        /// </summary>
        public Player GetNextFor(int currentPlayerId)
        {
            if (this.RoomReference == null || this.RoomReference.Players == null || this.RoomReference.Players.Count < 2)
            {
                return null;
            }

            Dictionary<int, Player> players = this.RoomReference.Players;
            int nextHigherId = int.MaxValue;   
            int lowestId = currentPlayerId;   

            foreach (int playerid in players.Keys)
            {
                if (playerid < lowestId)
                {
                    lowestId = playerid;       
                }
                else if (playerid > currentPlayerId && playerid < nextHigherId)
                {
                    nextHigherId = playerid;    
                }
            }

            return (nextHigherId != int.MaxValue) ? players[nextHigherId] : players[lowestId];
        }

        /// <summary>
        /// 새 플레이어의 정보를 수신하여 업데이트합니다.
        /// </summary>
        protected internal virtual void InternalCacheProperties(Hashtable properties)
        {
            if (properties == null || properties.Count == 0 || this.CustomProperties.Equals(properties))
            {
                return;
            }

            if (properties.ContainsKey(ActorProperties.PlayerName))
            {
                string nameInServersProperties = (string)properties[ActorProperties.PlayerName];
                if (nameInServersProperties != null)
                {
                    if (this.IsLocal)
                    {
                        if (!nameInServersProperties.Equals(this.nickName))
                        {
                            this.SetPlayerNameProperty();
                        }
                    }
                    else
                    {
                        this.NickName = nameInServersProperties;
                    }
                }
            }
            if (properties.ContainsKey(ActorProperties.UserId))
            {
                this.UserId = (string)properties[ActorProperties.UserId];
            }
            if (properties.ContainsKey(ActorProperties.IsInactive))
            {
                this.IsInactive = (bool)properties[ActorProperties.IsInactive]; 
            }

            this.CustomProperties.MergeStringKeys(properties);
            this.CustomProperties.StripKeysWithNullValues();
        }

        public override string ToString()
        {
            return string.Format("#{0:00} '{1}'", this.ActorNumber, this.NickName);
        }

        public override bool Equals(object obj)
        {
            Player p = obj as Player;
            return (p != null && this.GetHashCode() == p.GetHashCode());
        }

        public override int GetHashCode()
        {
            return this.ActorNumber;
        }

        protected internal void ChangeLocalID(int newID)
        {
            if (!this.IsLocal)
            {
                return;
            }

            this.actorNumber = newID;
        }

        /// <summary>
        /// 플레이어의 CustomPropertie를 업데이트하고 동기화 합니다.
        /// </summary>
        public bool SetCustomProperties(Hashtable propertiesToSet, Hashtable expectedValues = null)
        {
            if (propertiesToSet == null || propertiesToSet.Count == 0)
            {
                return false;
            }

            Hashtable customProps = propertiesToSet.StripToStringKeys() as Hashtable;

            if (this.RoomReference != null)
            {
                if (this.RoomReference.IsOffline)
                {
                    if (customProps.Count == 0)
                    {
                        return false;
                    }
                    this.CustomProperties.MergeStringKeys(customProps);
                    this.CustomProperties.StripKeysWithNullValues();
                    // 콜백 호출
                    this.RoomReference.LoadBalancingClient.InRoomCallbackTargets.OnPlayerPropertiesUpdate(this, customProps);
                    return true;
                }
                else
                {
                    Hashtable customPropsToCheck = expectedValues.StripToStringKeys() as Hashtable;

                    // Online room일 경우
                    return this.RoomReference.LoadBalancingClient.OpSetPropertiesOfActor(this.actorNumber, customProps, customPropsToCheck);
                }
            }
            if (this.IsLocal)
            {
                if (customProps.Count == 0)
                {
                    return false;
                }
                if (expectedValues == null)
                {
                    this.CustomProperties.MergeStringKeys(customProps);
                    this.CustomProperties.StripKeysWithNullValues();
                    return true;
                }
            }

            return false;
        }

        private bool SetPlayerNameProperty()
        {
            if (this.RoomReference != null && !this.RoomReference.IsOffline)
            {
                Hashtable properties = new Hashtable();
                properties[ActorProperties.PlayerName] = this.nickName;
                return this.RoomReference.LoadBalancingClient.OpSetPropertiesOfActor(this.ActorNumber, properties);
            }
            return false;
        }

    }
}