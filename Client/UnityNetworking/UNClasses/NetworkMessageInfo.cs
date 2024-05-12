


namespace NDG.UnityNet
{
    using NDG.Realtime;

    public struct NetworkMessageInfo
    {
        private readonly int timeInt;
        public readonly Player Sender;
        public readonly NetworkView networkView;

        public NetworkMessageInfo(Player player, int timestamp, NetworkView view)
        {
            this.Sender = player;
            this.timeInt = timestamp;
            this.networkView = view;
        }


        public double SentServerTime
        {
            get
            {
                uint u = (uint)this.timeInt;
                double t = u;
                return t / 1000.0d;
            }
        }

        public int SentServerTimestamp
        {
            get { return this.timeInt; }
        }

        public override string ToString()
        {
            return string.Format("[NetworkMessageInfo: Sender='{1}' Senttime={0}]", this.SentServerTime, this.Sender);
        }



    }
}