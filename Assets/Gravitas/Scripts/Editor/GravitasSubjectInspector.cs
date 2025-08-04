using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace Gravitas.Editor
{
    /// <summary>
    /// Custom editor for GravitasSubject targets.
    /// </summary>
    [CanEditMultipleObjects]
    [CustomEditor(typeof(GravitasSubject))]
    internal sealed class GravitasSubjectInspector : UnityEditor.Editor
    {
        private const string
            ERROR_ICON_CLASS_NAME = "warning-icon_error",
            WARNING_ICON_CLASS_NAME = "warning-icon_warning";

        [SerializeField] private VisualTreeAsset visualTreeAsset;

        public override VisualElement CreateInspectorGUI()
        {
            if (visualTreeAsset != null && serializedObject != null)
            {
                VisualElement rootVisualElement = visualTreeAsset.CloneTree();

                SerializedProperty
                    autoOrientProperty = serializedObject.FindProperty("autoOrient"),
                    subjectRigidbodyProperty = serializedObject.FindProperty("subjectRigidbody"),
                    willReorientProperty = serializedObject.FindProperty("willReorient");

                if (autoOrientProperty != null)
                    rootVisualElement.TrackPropertyValue(autoOrientProperty, (_) => SetOrientSpeedFieldActive());
                if (willReorientProperty != null)
                {
                    rootVisualElement.TrackPropertyValue(willReorientProperty, (_) => SetReorientDelayFieldActive());
                    rootVisualElement.TrackPropertyValue(willReorientProperty, (_) => SetOrientSpeedFieldActive());
                }

                SetOrientSpeedFieldActive();
                SetReorientDelayFieldActive();

                return rootVisualElement;

                void SetOrientSpeedFieldActive()
                {
                    FloatField orientSpeedField = rootVisualElement.Q<FloatField>("orient-speed-field");
                    orientSpeedField?.SetEnabled
                    (
                        (autoOrientProperty != null && autoOrientProperty.boolValue) ||
                        (willReorientProperty != null && willReorientProperty.boolValue)
                    );
                }

                void SetReorientDelayFieldActive()
                {
                    FloatField reorientDelayField = rootVisualElement.Q<FloatField>("reorient-delay-field");
                    reorientDelayField?.SetEnabled(willReorientProperty.boolValue);
                }
            }

            return base.CreateInspectorGUI();
        }
    }
}
