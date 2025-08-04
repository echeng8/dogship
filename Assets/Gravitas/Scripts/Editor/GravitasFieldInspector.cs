using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace Gravitas.Editor
{
    /// <summary>
    /// Custom editor for GravitasField targets.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(GravitasField))]
    internal sealed class GravitasFieldInspector : UnityEditor.Editor
    {
        private const string
            ERROR_ICON_CLASS_NAME = "warning-icon_error",
            WARNING_ICON_CLASS_NAME = "warning-icon_warning";

        private VisualElement rootVisualElement;
        [SerializeField] private VisualTreeAsset visualTreeAsset;

        public override VisualElement CreateInspectorGUI()
        {
            // Checking if a GravitasManager exists in the scene when interacting with a field component
            if (FindObjectOfType<GravitasManager>() == null)
            {
                Debug.LogWarning("Gravitas: No Gravitas Manager GameObject detected! Adding one!");
                GameObject managerObject = new GameObject("Gravitas Manager");
                managerObject.AddComponent<GravitasManager>();
            }

            if (visualTreeAsset != null && serializedObject != null)
            {
                rootVisualElement = visualTreeAsset.CloneTree();

                SerializedProperty boundaryColliderProperty = serializedObject.FindProperty("boundaryCollider");
                if (boundaryColliderProperty != null)
                    rootVisualElement.TrackPropertyValue(boundaryColliderProperty, (_) => RefreshWarnings());

                RefreshWarnings();

                return rootVisualElement;

                void RefreshWarnings()
                {
                    if (serializedObject != null && serializedObject.targetObject is GravitasField gravitasField)
                    {
                        int errorCode = gravitasField.CheckFieldErrors(out string errorString);

                        if (rootVisualElement != null)
                        {
                            Foldout warningFoldout = rootVisualElement.Q<Foldout>("inspector-warning-content-foldout");
                            if (warningFoldout != null)
                            {
                                if (errorCode != 0)
                                {
                                    Label warningTextLabel = warningFoldout.contentContainer.Q<Label>("warning-text-label");
                                    VisualElement warningIconElement = warningFoldout.contentContainer.Q<VisualElement>("warning-icon-element");

                                    if (warningTextLabel != null)
                                        warningTextLabel.text = errorString;

                                    if (warningIconElement != null)
                                    {
                                        warningIconElement.RemoveFromClassList(ERROR_ICON_CLASS_NAME);
                                        warningIconElement.RemoveFromClassList(WARNING_ICON_CLASS_NAME);

                                        if (errorCode == 1)
                                            warningIconElement.AddToClassList(WARNING_ICON_CLASS_NAME);
                                        else if (errorCode == 2)
                                            warningIconElement.AddToClassList(ERROR_ICON_CLASS_NAME);
                                    }

                                    warningFoldout.style.display = DisplayStyle.Flex;
                                }
                                else
                                {
                                    warningFoldout.style.display = DisplayStyle.None;
                                }
                            }
                        }
                    }
                }
            }

            return base.CreateInspectorGUI();
        }
    }
}
