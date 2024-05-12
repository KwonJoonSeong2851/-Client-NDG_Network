namespace NDG.UnityNet
{
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.Animations;
    using UnityEngine;

    [CustomEditor(typeof(AnimatorView))]

    public class AnimatorViewEditor : MonoBehaviourNetEditor
    {
        private Animator animator;
        private AnimatorView targetView;
        private AnimatorController controller;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (this.animator == null)
            {
                EditorGUILayout.HelpBox("해당 게임오브젝트가 animator 컴포넌트를 갖고있지 않습니다", MessageType.Warning);
                return;
            }

            this.DrawWeightInspector();

            if (this.GetLayerCount() == 0)
            {
                EditorGUILayout.HelpBox("Animator에 동기화할 layer가 설정되어 있지 않습니다.", MessageType.Warning);
            }

            this.DrawParameterInspector();

            if (this.GetParameterCount() == 0)
            {
                EditorGUILayout.HelpBox("Animator에 동기화할 Parameter가 설정되어 있지 않습니다.", MessageType.Warning);
            }

            this.serializedObject.ApplyModifiedProperties();

        }

        private int GetLayerCount()
        {
            return (this.controller == null) ? 0 : this.controller.layers.Length;
        }

        private int GetParameterCount()
        {
            return (this.controller == null) ? 0 : this.controller.parameters.Length;
        }

        private AnimatorControllerParameter GetAnimatorControllerParameter(int i)
        {
            return this.controller.parameters[i];
        }


        private RuntimeAnimatorController GetEffectiveController(Animator animator)
        {
            RuntimeAnimatorController runTimeController = animator.runtimeAnimatorController;

            AnimatorOverrideController overrideController = runTimeController as AnimatorOverrideController;
            while (overrideController != null)
            {
                runTimeController = overrideController.runtimeAnimatorController;
                overrideController = runTimeController as AnimatorOverrideController;
            }

            return runTimeController;
        }


        private void OnEnable()
        {
            this.targetView = (AnimatorView)this.target;
            this.animator = this.targetView.GetComponent<Animator>();

            if (animator)
            {
                this.controller = this.GetEffectiveController(this.animator) as AnimatorController;
                this.CheckIfStoredParametersExist();
            }
        }

        private bool DoesParameterExist(string name)
        {
            for (int i = 0; i < this.GetParameterCount(); ++i)
            {
                if (this.GetAnimatorControllerParameter(i).name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private void CheckIfStoredParametersExist()
        {
            var syncedParams = this.targetView.GetSynchronizedParameters();
            List<string> paramsToRemove = new List<string>();

            for (int i = 0; i < syncedParams.Count; ++i)
            {
                string parameterName = syncedParams[i].Name;
                if (this.DoesParameterExist(parameterName) == false)
                {
                    Debug.LogWarning("Parameter '" + this.targetView.GetSynchronizedParameters()[i].Name + "' doesn't exist anymore. Removing it from the list of synchronized parameters");
                    paramsToRemove.Add(parameterName);
                }
            }

            if (paramsToRemove.Count > 0)
            {
                foreach (string param in paramsToRemove)
                {
                    this.targetView.GetSynchronizedParameters().RemoveAll(item => item.Name == param);
                }
            }
        }

        private void DrawWeightInspector()
        {
            for (int i = 0; i < this.GetLayerCount(); ++i)
            {
                if (this.targetView.DoesLayerSynchronizeTypeExist(i) == false)
                {
                    this.targetView.SetLayerSynchronized(i, AnimatorView.SynchronizeType.Disabled);
                }

            }

        }

        private void DrawParameterInspector()
        {
            //bool isUsingTriggers = false;

            for (int i = 0; i < this.GetParameterCount(); i++)
            {
                AnimatorControllerParameter parameter = null;
                parameter = this.GetAnimatorControllerParameter(i);

                string defaultValue = "";

                if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    if (Application.isPlaying && this.animator.gameObject.activeInHierarchy)
                    {
                        defaultValue += this.animator.GetBool(parameter.name);
                    }
                    else
                    {
                        defaultValue += parameter.defaultBool.ToString();
                    }
                }
                else if (parameter.type == AnimatorControllerParameterType.Float)
                {
                    if (Application.isPlaying && this.animator.gameObject.activeInHierarchy)
                    {
                        defaultValue += this.animator.GetFloat(parameter.name).ToString("0.00");
                    }
                    else
                    {
                        defaultValue += parameter.defaultFloat.ToString();
                    }
                }
                else if (parameter.type == AnimatorControllerParameterType.Int)
                {
                    if (Application.isPlaying && this.animator.gameObject.activeInHierarchy)
                    {
                        defaultValue += this.animator.GetInteger(parameter.name);
                    }
                    else
                    {
                        defaultValue += parameter.defaultInt.ToString();
                    }
                }
                else if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    if (Application.isPlaying && this.animator.gameObject.activeInHierarchy)
                    {
                        defaultValue += this.animator.GetBool(parameter.name);
                    }
                    else
                    {
                        defaultValue += parameter.defaultBool.ToString();
                    }
                }

                if (this.targetView.DoesParameterSynchronizeTypeExist(parameter.name) == false)
                {
                    this.targetView.SetParameterSynchronized(parameter.name, (AnimatorView.ParameterType)parameter.type, AnimatorView.SynchronizeType.Disabled);
                }

                // AnimatorView.SynchronizeType value = this.targetView.GetParameterSynchronizeType(parameter.name);

                // if (value != AnimatorView.SynchronizeType.Disabled && parameter.type == AnimatorControllerParameterType.Trigger)
                // {
                //     isUsingTriggers = true;
                // }

            }

        }




    }

}
