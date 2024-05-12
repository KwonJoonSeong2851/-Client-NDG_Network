using System.Collections;
using System.Collections.Generic;

namespace NDG.Realtime
{

    internal class LobbyCallbacksContainer : List<ILobbyCallbacks>,ILobbyCallbacks
    {
        private readonly LoadBalancingClient client;

        public LobbyCallbacksContainer(LoadBalancingClient client)
        {
            this.client = client;
        }

        public void OnJoinedLobby()
        {
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnJoinedLobby();
            }
        }

        public void OnLeftLobby()
        {
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnLeftLobby();
            }
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnRoomListUpdate(roomList);
            }
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
            this.client.UpdateCallbackTargets();

            foreach (ILobbyCallbacks target in this)
            {
                target.OnLobbyStatisticsUpdate(lobbyStatistics);
            }
        }

    }
}
