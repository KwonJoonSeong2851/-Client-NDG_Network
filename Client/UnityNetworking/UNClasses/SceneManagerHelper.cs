
namespace NDG.UnityNet
{
    using UnityEngine;
    using UnityEngine.SceneManagement;
    public class SceneManagerHelper
    {
        public static string ActiveSceneName
        {
            get
            {
                Scene s = SceneManager.GetActiveScene();
                return s.name;
            }
        }

        public static int ActiveSceneBuildIndex
        {
            get { return SceneManager.GetActiveScene().buildIndex; }
        }


    }
}
