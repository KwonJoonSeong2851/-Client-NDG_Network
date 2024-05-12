



namespace NDG.UnityNet
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 네트워크 상에서 GameObject 인스턴스를 생성하고 삭제하는 리소스 풀입니다.
    /// </summary>
    public class DefaultPool : INetPrefabPool
    {
        //prefabId당 GameObject를 할당하여 인스턴스화 속도를 높입니다.
        public readonly Dictionary<string, GameObject> ResourceCache = new Dictionary<string, GameObject>();

        /// <summary>
        /// 네트워크 상에서 사용될 GameObject 인스턴스를 생성합니다.
        /// </summary>
        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            GameObject res = null;
            bool cached = this.ResourceCache.TryGetValue(prefabId, out res);
            if (!cached)
            {
                res = (GameObject)Resources.Load(prefabId, typeof(GameObject));
                if (res == null)
                {
                    Debug.LogError("DefaultPool을 로드하는데 실패하였습니다. \"" + prefabId + "\". 해당 Prefab이 \"Resources\" 폴더에 있는지 확인하세요. ");
                }
                else
                {
                    this.ResourceCache.Add(prefabId, res);
                }
            }

            bool wasActive = res.activeSelf;
            if (wasActive) res.SetActive(false);

            GameObject instance = GameObject.Instantiate(res, position, rotation) as GameObject;

            if (wasActive) res.SetActive(true);
            return instance;
        }
        public void Destroy(GameObject gameObject)
        {
            GameObject.Destroy(gameObject);
        }
    }
}
