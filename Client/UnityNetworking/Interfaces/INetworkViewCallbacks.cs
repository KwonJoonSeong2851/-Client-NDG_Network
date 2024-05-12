namespace NDG.UnityNet
{
    using NDG.Realtime;



    public interface INetworkViewCallback
    {

    }

    public interface IOnNetworkViewPreNetDestroy : INetworkViewCallback
    {
        void OnPreNetDestroy(NetworkView rootView);
    }

    public interface IOnNetworkViewOwnerChange : INetworkViewCallback
    {
        void OnOwnerChage(Player newOwner, Player previousOwner);
    }

    public interface IOnNetworkViewControllerChange : INetworkViewCallback
    {
        void OnControllerChange(Player newController, Player previousController);
    }
}