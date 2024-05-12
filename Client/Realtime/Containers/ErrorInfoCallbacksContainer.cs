using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG.Realtime
{
    internal class ErrorInfoCallbacksContainer : List<IErrorInfoCallback>, IErrorInfoCallback
    {
        private LoadBalancingClient client;

        public ErrorInfoCallbacksContainer(LoadBalancingClient client)
        {
            this.client = client;
        }

        public void OnErrorInfo(ErrorInfo errorInfo)
        {
            this.client.UpdateCallbackTargets();
            foreach (IErrorInfoCallback target in this)
            {
                target.OnErrorInfo(errorInfo);
            }
        }
    }
}
