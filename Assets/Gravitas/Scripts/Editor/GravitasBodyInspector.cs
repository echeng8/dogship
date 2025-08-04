using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace Gravitas.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(GravitasBody))]
    internal sealed class GravitasBodyInspector : UnityEditor.Editor
    {
        private const string
            ERROR_ICON_CLASS_NAME = "warning-icon_error",
            WARNING_ICON_CLASS_NAME = "warning-icon_warning";
        private const long WARNING_REFRESH_DELAY = 100;

        [SerializeField] private VisualTreeAsset visualTreeAsset;

        public override VisualElement CreateInspectorGUI()
        {
            if (visualTreeAsset != null && serializedObject != null)
            {
                VisualElement rootVisualElement = visualTreeAsset.CloneTree();

                Button autoFindSubjectCollidersButton = rootVisualElement.Q<Button>("auto-find-colliders-button");
                if (autoFindSubjectCollidersButton != null)
                {
                    autoFindSubjectCollidersButton.clicked += () =>
                    {
                        ((GravitasBody)serializedObject.targetObject).AutoFindBodyColliders();

                        RefreshWarnings();
                    };
                }

                ListView subjectCollidersListView = rootVisualElement.Q<ListView>("body-colliders-list-view");
                if (subjectCollidersListView != null)
                {
                    subjectCollidersListView.Q<Foldout>().viewDataKey = "gravitas-body-colliders-list-view-foldout";
                    subjectCollidersListView.itemsAdded += (_) => RefreshWarningsDelay();
                    subjectCollidersListView.itemsRemoved += (_) => RefreshWarningsDelay();
                    subjectCollidersListView.itemsSourceChanged += RefreshWarningsDelay;

                    void RefreshWarningsDelay()
                    {
                        subjectCollidersListView.schedule.Execute(RefreshWarnings).StartingIn(WARNING_REFRESH_DELAY);
                    }
                }

                ObjectField objectField = rootVisualElement.Q<ObjectField>("body-rigidbody-field");
                objectField?.RegisterValueChangedCallback((_) => RefreshWarnings());

                RefreshWarnings();

                return rootVisualElement;

                void RefreshWarnings()
                {
                    if (serializedObject.targetObject is GravitasBody gravitasBody)
                    {
                        int errorCode = gravitasBody.CheckBodyErrors(out string errorString);

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

                            warningFoldout.MarkDirtyRepaint();
                        }
                    }
                }
            }

            return base.CreateInspectorGUI();
        }
    }
}
