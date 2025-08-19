namespace Yarn.Unity.Addons.SpeechBubbles.Editor
{
    using UnityEditor;

    [CanEditMultipleObjects]
    [CustomEditor(typeof(RoundedRect), true)]
    public class RoundedRectEditor : Yarn.Unity.Editor.YarnEditor
    {
        // This class is empty, but serves to indicate to Unity to use our
        // custom inspector system for the indicated class.
    }
}
