
namespace NDG
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public static class NestedComponentUtilities
    {
        private static Queue<Transform> nodesQueue = new Queue<Transform>();
        public static Dictionary<System.Type, ICollection> searchLists = new Dictionary<System.Type, ICollection>();
        private static Stack<Transform> nodeStack = new Stack<Transform>();

        /// <summary>
        ///  부모 객체 반환
        /// </summary>
        public static T GetParentComponent<T>(this Transform t) where T : Component
        {
            T found = t.GetComponent<T>();

            if (found)
                return found;

            var par = t.parent;
            while (par)
            {
                found = par.GetComponent<T>();
                if (found)
                    return found;
                par = par.parent;
            }
            return null;
        }
        public static void GetNestedComponentsInChildren<T, SearchT, NestedT>(this Transform t, bool includeInactive, List<T> list)
          where T : class
          where SearchT : class
        {
            list.Clear();

            if(!includeInactive && !t.gameObject.activeSelf)
            {
                return;
            }

            System.Type searchType = typeof(SearchT);

            List<SearchT> searchList;

            if(!searchLists.ContainsKey(searchType))
            {
                searchLists.Add(searchType, searchList = new List<SearchT>());
            }
            else
            {
                searchList = searchList = searchLists[searchType] as List<SearchT>;
            }

            nodesQueue.Clear();
            nodesQueue.Enqueue(t);

            while(nodesQueue.Count > 0)
            {
                var node = nodesQueue.Dequeue();

                searchList.Clear();
                node.GetComponents(searchList);
                foreach(var comp in searchList)
                {
                    var casted = comp as T;
                    if(!ReferenceEquals(casted,null))
                    {
                        list.Add(casted);
                    }
                }

                for(int i = 0, count = node.childCount; i < count; ++i)
                {
                    var child = node.GetChild(i);

                    if (!includeInactive && !child.gameObject.activeSelf)
                        continue;

                    if (!ReferenceEquals(child.GetComponent<NestedT>(), null))
                        continue;

                    nodesQueue.Enqueue(child);
                }
            }
        }
    }
}