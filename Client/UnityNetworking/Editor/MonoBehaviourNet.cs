namespace NDG.UnityNet
{
    using UnityEditor;

    [CustomEditor(typeof(MonoBehaviourNet))]
    public class MonoBehaviourNetEditor : Editor
    {
        MonoBehaviourNet mbTarget;

        private void OnEnable()
        {
            mbTarget = target as MonoBehaviourNet;
        }

        public override void OnInspectorGUI()
        {
            mbTarget = target as MonoBehaviourNet;

            base.OnInspectorGUI();

            if (mbTarget.networkView == null)
            {
                EditorGUILayout.HelpBox("이 게임 오브젝트 또는 부모 오브젝트에서 NetworkView를 찾을 수 없습니다. ", MessageType.Warning);
            }
        }
    }
}