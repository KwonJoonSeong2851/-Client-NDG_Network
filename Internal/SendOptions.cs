

namespace NDG
{
    /// <summary>
    /// operation 및 message 전송에 필요한 DeliveryMode,Encryption 및 채널 값을 설정합니다.
    /// </summary>
    public struct SendOptions
    {
        /// <summary>기본 Reliable 전송 인스턴스</summary>
        public static readonly SendOptions SendReliable = new SendOptions()
        {
            Reliability = true
        };
        /// <summary>기본 UnReliable 전송 인스턴스</summary>
        public static readonly SendOptions SendUnreliable = new SendOptions()
        {
            Reliability = false
        };
        /// <summary>전송모드를 설정합니다. 기본: Unreliable</summary>
        public DeliveryMode DeliveryMode;
        /// <summary>이 값이 true면 operation/message가 전송되기전에 암호화 됩니다. 기본값은 false입니다.</summary>
        /// <remarks>암호화를 사용하려면 먼저  Peer의 IsEncryptionAvailable를 true로 설정해야합니다</remarks>
        public bool Encrypt;
        /// <summary>전송할 Enet 채널을 설정합니다. 기본값은 0입니다.</summary>
        public byte Channel;

        /// <summary>이 값을 true로 설정하면 전송 모드가 Reliable, false로 설정하면 Unreliable로 설정됩니다.</summary>
        public bool Reliability
        {
            get => this.DeliveryMode == DeliveryMode.Reliable;
            set => this.DeliveryMode = value ? DeliveryMode.Reliable : DeliveryMode.Unreliable;
        }
    }
}