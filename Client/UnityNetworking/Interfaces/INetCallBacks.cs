

namespace NDG.UnityNet
{
    using UnityEngine;
    using NDG.Realtime;
    public interface INetObservable
    {
        void OnNetSerializeView(NetworkStream stream, NetworkMessageInfo info);
    }
    public interface INetOwnershipCallbacks
    {
        void OnOwnershipRequest(NetworkView targetView, Player requestingPlayer);

        void OnOwnershipTransfered(NetworkView targetView, Player previousOwner);

    }

    public interface INetInstantiateMagicCallback
    {
        void OnNetInstantiate(NetworkMessageInfo info);
    }

    public interface INetPrefabPool
    {
        /// <summary>
        /// Prefab 인스턴스를 생성하기 위해 호출됩니다.
        /// NetworkView가 있는 GameObject이어야 합니다.
        /// </summary>
        GameObject Instantiate(string frefabId, Vector3 position, Quaternion rotation);

        /// <summary>
        /// Prefab 인스턴스를 삭제하기 위해 호출됩니다.
        /// </summary>
        void Destroy(GameObject gameObject);

    }
}