using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Gravitas.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(GravitasManager))]
    internal sealed class GravitasManagerInspector : UnityEditor.Editor
    {
        [SerializeField] private VisualTreeAsset visualTreeAsset;

        public override VisualElement CreateInspectorGUI()
        {
            if (visualTreeAsset)
            {
                return visualTreeAsset.CloneTree();
            }

            return base.CreateInspectorGUI();
        }
    }
}
