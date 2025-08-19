using UnityEngine;

#nullable enable

namespace Yarn.Unity.Addons.SpeechBubbles
{

    [CreateAssetMenu(menuName = "Yarn Spinner/Speech Bubbles/Basic Character Bubble Data")]
    public class BasicCharacterBubbleData : CharacterBubbleData
    {
        public string? characterDisplayName;
        public Color textColor = Color.white;

        public Color nameplateBackgroundColor = Color.cyan;
        public Color nameplateTextColor = Color.black;
    }

}