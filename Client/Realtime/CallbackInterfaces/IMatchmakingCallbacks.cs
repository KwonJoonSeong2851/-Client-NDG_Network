using System.Collections;
using System.Collections.Generic;


public interface IMatchmakingCallbacks
{
    void OnCreatedRoom();

    void OnCreateRoomFailed(short returnCode, string message);

    void OnJoinedRoom();

    void OnJoinRoomFailed(short returnCode, string message);

    void OnJoinRandomFailed(short returnCode, string message);

    void OnLeftRoom();
}
