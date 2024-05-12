using System.Collections;
using System.Collections.Generic;


namespace NDG.Realtime
{
    public interface IOnEventCallback
    {
        void OnEvent(EventData networkEvent);
    }

}