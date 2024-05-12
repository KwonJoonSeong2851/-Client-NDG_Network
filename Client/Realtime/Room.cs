

namespace NDG.Realtime
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    public class Room : RoomInfo
    {
        public LoadBalancingClient LoadBalancingClient { get; set; }

        public new string Name
        {
            get
            {
                return this.name;
            }

            internal set
            {
                this.name = value;
            }
        }

        private bool isOffline;

        public bool IsOffline
        {
            get
            {
                return isOffline;
            }

            private set
            {
                isOffline = value;
            }
        }

        public new bool IsOpen
        {
            get
            {
                return this.isOpen;
            }

            set
            {
                if (value != this.isOpen)
                {
                    this.LoadBalancingClient.OpSetPropertiesOfRoom(new Hashtable() { { GamePropertyKey.IsOpen, value } });
                }

                this.isOpen = value;
            }
        }

        public new bool IsVisible
        {
            get
            {
                return this.isVisible;
            }

            set
            {
                if (value != this.isVisible)
                {
                    this.LoadBalancingClient.OpSetPropertiesOfRoom(new Hashtable() { { GamePropertyKey.IsVisible, value } });
                }
                this.isVisible = value;
            }
        }

        public new byte MaxPlayers
        {
            get
            {
                return this.maxPlayers;
            }

            set
            {
                if (value != this.maxPlayers)
                {
                    this.LoadBalancingClient.OpSetPropertiesOfRoom(new Hashtable() { { GamePropertyKey.MaxPlayers, value } });
                }
                this.maxPlayers = value;
            }
        }

        public new byte PlayerCount
        {
            get
            {
                if (this.Players == null)
                {
                    return 0;
                }

                return (byte)this.Players.Count;
            }
        }

        private Dictionary<int, Player> players = new Dictionary<int, Player>();

        public Dictionary<int, Player> Players
        {
            get
            {
                return this.players;
            }

            private set
            {
                this.players = value;
            }
        }

        public string[] ExpectedUsers
        {
            get { return this.expectedUsers; }
        }

        public int PlayerTtl
        {
            get { return this.playerTtl; }

            set
            {
                if (value != this.playerTtl)
                {
                    if (!this.isOffline)
                    {
                        this.LoadBalancingClient.OpSetPropertyOfRoom(GamePropertyKey.PlayerTtl, value);
                    }
                }

                this.playerTtl = value;
            }
        }

        public int EmptyRoomTtl
        {
            get { return this.emptyRoomTtl; }

            set
            {
                if (value != this.emptyRoomTtl)
                {
                    if (!this.isOffline)
                    {
                        this.LoadBalancingClient.OpSetPropertyOfRoom(GamePropertyKey.EmptyRoomTtl, value);
                    }
                }

                this.emptyRoomTtl = value;
            }
        }

        public int MasterClientId { get { return this.masterClientId; } }

        public string[] PropertiesListedInLobby
        {
            get { return this.propertiesListedInLobby; }

            private set
            {
                this.propertiesListedInLobby = value;
            }
        }


        public bool AutoCleanUp
        {
            get
            {
                return this.autoCleanUp;
            }
        }

        private bool broadcastPropertiesChangeToAll = true;

        public bool BroadcastPropertiesChangeToAll
        {
            get
            {
                return broadcastPropertiesChangeToAll;
            }
            private set
            {
                this.broadcastPropertiesChangeToAll = value;
            }
        }

        public bool SuppressRoomEvents { get; private set; }

        public Room(string roomName, RoomOptions options, bool isOffline = false) : base(roomName, options != null ? options.CustomRoomProperties : null)
        {
            if (options != null)
            {
                this.isVisible = options.IsVisible;
                this.isOpen = options.IsOpen;
                this.maxPlayers = options.MaxPlayers;
                this.propertiesListedInLobby = options.CustomRoomPropertiesForLobby;
            }
        }

        internal void InternalCacheRoomFlags(int roomFlags)
        {
            this.BroadcastPropertiesChangeToAll = (roomFlags & (int)RoomOptionBit.BroadcastPropsChangeToAll) != 0;
            this.SuppressRoomEvents = (roomFlags & (int)RoomOptionBit.SuppressRoomEvents) != 0;
        }

        protected internal override void InternalCacheProperties(Hashtable propertiesToCache)
        {
            int oldMasterId = this.masterClientId;

            base.InternalCacheProperties(propertiesToCache);

            if (oldMasterId != 0 && this.masterClientId != oldMasterId)
            {
                this.LoadBalancingClient.InRoomCallbackTargets.OnMasterClientSwitched(this.GetPlayer(this.masterClientId));
            }
        }

        public virtual bool SetCustomProperties(Hashtable propertiesToSet, Hashtable expectedProperties = null)
        {
            if (propertiesToSet == null || propertiesToSet.Count == 0)
            {
                return false;
            }
            Hashtable customProps = propertiesToSet.StripToStringKeys() as Hashtable;

            UnityEngine.Debug.Log("customProps Count :::::;; " + customProps.Count + "befor count " + propertiesToSet.Count);

            if (this.isOffline)
            {
                if (customProps.Count == 0)
                {
                    return false;
                }
                this.CustomProperties.MergeStringKeys(customProps);
                this.CustomProperties.StripKeysWithNullValues();

                this.LoadBalancingClient.InRoomCallbackTargets.OnRoomPropertiesUpdate(propertiesToSet);

            }
            else
            {
                return this.LoadBalancingClient.OpSetPropertiesOfRoom(customProps, expectedProperties);
            }

            return true;
        }

        public bool SetPropertiesListedInLobby(string[] lobbyProps)
        {
            if (this.isOffline)
            {
                return false;
            }
            Hashtable customProps = new Hashtable();
            customProps[GamePropertyKey.PropsListedInLobby] = lobbyProps;
            return this.LoadBalancingClient.OpSetPropertiesOfRoom(customProps);
        }

        protected internal virtual void RemovePlayer(Player player)
        {
            this.Players.Remove(player.ActorNumber);
            player.RoomReference = null;
        }

        protected internal virtual void RemovePlayer(int id)
        {
            this.RemovePlayer(this.GetPlayer(id));
        }

        public bool SetMasterClient(Player masterClientPlayer)
        {
            if (this.isOffline)
            {
                return false;
            }
            Hashtable newProps = new Hashtable() { { GamePropertyKey.MasterClientId, masterClientPlayer.ActorNumber } };
            Hashtable prevProps = new Hashtable() { { GamePropertyKey.MasterClientId, this.MasterClientId } };
            return this.LoadBalancingClient.OpSetPropertiesOfRoom(newProps, prevProps);
        }

        public virtual bool AddPlayer(Player player)
        {
            if (!this.Players.ContainsKey(player.ActorNumber))
            {
                this.StorePlayer(player);
                return true;
            }

            return false;
        }

        public virtual Player StorePlayer(Player player)
        {
            this.Players[player.ActorNumber] = player;
            player.RoomReference = this;

            if (this.MasterClientId == 0 || player.ActorNumber < this.MasterClientId)
            {
                UnityEngine.Debug.LogWarning("STORE PLAYER NUMBER " + player.ActorNumber);
                this.masterClientId = player.ActorNumber;
            }

            return player;
        }

        public virtual Player GetPlayer(int id)
        {
            Player result = null;
            this.Players.TryGetValue(id, out result);

            return result;
        }

        public bool ClearExpectedUsers()
        {
            if (this.isOffline)
            {
                return false;
            }
            Hashtable props = new Hashtable();
            props[GamePropertyKey.ExpectedUsers] = new string[0];
            Hashtable expected = new Hashtable();
            expected[GamePropertyKey.ExpectedUsers] = this.ExpectedUsers;
            return this.LoadBalancingClient.OpSetPropertiesOfRoom(props, expected);
        }

        public override string ToString()
        {
            return string.Format("Room: '{0}' {1},{2} {4}/{3} players.", this.name, this.isVisible ? "visible" : "hidden", this.isOpen ? "open" : "closed", this.maxPlayers, this.PlayerCount);
        }
    }


}