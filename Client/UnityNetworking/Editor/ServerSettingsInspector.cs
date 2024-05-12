using System;
using UnityEditor;
using UnityEngine;

using NDG;
using NDG.UnityNet;

namespace NDG
{
    // [CustomEditor(typeof(ServerSettings))]
    // public class ServerSettingsInspector : Editor
    // {
    //     private string version;
    //     private string[] regionsPrefsList;
    //     private string prefLabel;
    //     private const string notAvailableLabel = "n/a";
    //     private string rpcCrc;
    //     private bool showRpcs;
    //     private GUIStyle vertboxStyle;

    //     public void Awake()
    //     {
    //         this.version = System.Reflection.Assembly.GetAssembly(typeof(Peer)).GetName().Version.ToString();
    //     }

    //     public override void OnInspectorGUI()
    //     {
    //         if(vertboxStyle == null)
    //           vertboxStyle = new GUIStyle("HelpBox") {padding = new RectOffset(6,6,6,6)};

    //         SerializedObject serializedObject = new SerializedObject(this.target);
    //         ServerSettings settings = this.target as ServerSettings;

    //         EditorGUI.BeginChangeCheck();
            
    //     }



    // }

}
