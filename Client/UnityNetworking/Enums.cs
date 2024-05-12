using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG.UnityNet
{
    public enum ConnectMethod { NotCalled, ConnectToMaster, ConnectToRegion, ConnectToBest }

    public enum NetLogLevel
    {
        ErrorsOnly,
        Informational,
        Full,
    }

    public enum RpcTarget
    {
        All,

        Others,

        MasterClient,

        AllBuffered,

        OthersBuffered,

    }

    public enum ViewSynchronization
    {
        Off,
        ReliableDeltaCompressed,
        Unreliable,
        UnreliableOnChange
    }


    public enum OwnershipOption
    {
        /// <summary>
        /// 소유권이 고정되어있고 인스터화 된 객체는 생성자로 고정되며, Scene 객체는 항상 마스터 클라이언트가 소유권을 가집니다.
        /// </summary>
        Fixed,

        /// <summary>
        /// 소유권을 갖고있는 소유자로 부터 소유권을 가져올 수 있습니다.
        /// </summary>
        Takeover,

        /// <summary>
        /// 소유권을 요청할 수 있고 현재 소유권을 소유하고있는 플레이어가 동의 해야 합니다.
        /// </summary>
        Request
    }
}
