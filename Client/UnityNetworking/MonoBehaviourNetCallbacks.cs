using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NDG;
using NDG.UnityNet;
using NetworkView = NDG.UnityNet.NetworkView;
using NDG.Realtime;

public class MonoBehaviourNet : MonoBehaviour
{
    private NetworkView nvCache;
        
    

    public NetworkView networkView
    {
        get
        {
            #if UNITY_EDITOR
            if(!Application.isPlaying || this.nvCache == null)
            {
                this.nvCache = NetworkView.Get(this);
            }
            #else
            if(this.nvCache == null)
            {
                this.nvCache = NetworkView.Get(this);
            }
            #endif
            return this.nvCache;
        }
    }
}

public class MonoBehaviourNetCallbacks : MonoBehaviourNet,IConnectionCallbacks,IMatchmakingCallbacks,IInRoomCallbacks,ILobbyCallbacks,IErrorInfoCallback
{
    public virtual void OnEnable()
    {
        NDG_Network.AddCallbackTarget(this);
    }
    public virtual void OnDisable()
    {
        NDG_Network.RemoveCallbackTarget(this);
    }

    //ConnectionCallbacks
    public virtual void OnConnected()
    {
    }

    public virtual void OnConnectedToMaster()
    {
    }

    public virtual void OnDisconnected(DisconnectCause cause)
    {
    }

    public virtual void OnRegionListReceived(RegionHandler regionHandler)
    {
    }

    public virtual void OnCustomAuthenticationResponse(Dictionary<string, object> data)
    {
    }

    public virtual void OnCustomAuthenticationFailed(string debugMessage)
    {
    }

    //MatchmakingCallbacks

    public virtual void OnCreatedRoom()
    {
    }

    public virtual void OnCreateRoomFailed(short returnCode,string message)
    {
    }

    public virtual void OnJoinedRoom()
    {
    }

    public virtual void OnJoinRoomFailed(short returnCode, string message)
    {
    }

    public virtual void OnJoinRandomFailed(short returnCode, string message)
    {
    }

    public virtual void OnLeftRoom()
    {
    }

    //InRoomCallbacks

    public virtual void OnPlayerEnteredRoom(Player newPlayer)
    {
    }

    public virtual void OnPlayerLeftRoom(Player otherPlayer)
    {
    }

    public virtual void OnRoomPropertiesUpdate(Hashtable propertiesChanged)
    {
    }

    public virtual void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
    }

    public virtual void OnMasterClientSwitched(Player newMasterClient)
    {
    }

    //LobbyCallbacks

    public virtual void OnJoinedLobby()
    {
    }

    public virtual void OnLeftLobby()
    {
    }

    public virtual void OnRoomListUpdate(List<RoomInfo> roomList)
    {
    }

    public virtual void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
    {
    }

    //ErrorInfoCallbacks

    public virtual void OnErrorInfo(ErrorInfo errorInfo)
    {
    }
}
