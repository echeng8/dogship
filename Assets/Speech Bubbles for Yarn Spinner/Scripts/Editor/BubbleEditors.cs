
#if UNITY_EDITOR
namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEditor;
    using Yarn.Unity.Addons.SpeechBubbles.Editor;

    [CustomEditor(typeof(Bubble), true)]
    [CanEditMultipleObjects]
    public class BubbleEditor : Yarn.Unity.Editor.YarnEditor { }

    [CustomEditor(typeof(Bubble.PresentationState), true)]
    [CanEditMultipleObjects]
    public class BubblePresentationStateEditor : Yarn.Unity.Editor.YarnEditor { }

    [CustomEditor(typeof(Bubble.BubbleContent), true)]
    [CanEditMultipleObjects]
    public class BubbleContentEditor : Yarn.Unity.Editor.YarnEditor { }

    [CustomEditor(typeof(BubbleDialogueView), true)]
    [CanEditMultipleObjects]
    public class BubbleDialogueViewEditor : Yarn.Unity.Editor.YarnEditor { }

    [CustomEditor(typeof(CharacterBubbleAnchor), true)]
    [CanEditMultipleObjects]
    public class CharacterBubbleAnchorEditor : Yarn.Unity.Editor.YarnEditor { }
}
#endif