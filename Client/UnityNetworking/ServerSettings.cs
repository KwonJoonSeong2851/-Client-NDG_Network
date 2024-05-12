using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace NDG.UnityNet
{
    using NDG.Realtime;

[CreateAssetMenu(fileName = "NDG_ServerSettings" , menuName = "Network/ServerSettings")]
    public class ServerSettings : ScriptableObject
    {

        public AppSettings AppSettings;
        
        private string DevRegion;

        public NetLogLevel NetLogging = NetLogLevel.ErrorsOnly;


        public bool RunInBackground = true;

        public bool StartInOfflineMode;

        public List<string> RpcList = new List<string>();   



        public static bool IsAppId(string val)
        {
            try
            {
                new Guid(val);
            }
            catch
            {
                return false;
            }
            return true;
        }

  

    }
}
