using System.Collections;
using System.Collections.Generic;

namespace NDG.Realtime
{
    public interface ILobbyCallbacks
    {
        void OnJoinedLobby();

        void OnLeftLobby();

        void OnRoomListUpdate(List<RoomInfo> roomList);

        void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics);

    }
}