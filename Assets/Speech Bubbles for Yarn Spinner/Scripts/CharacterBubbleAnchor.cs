namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using Yarn.Unity.Attributes;

#nullable enable

    /// <summary>
    /// Represents the target gameobject we want the speech bubbles to follow around.
    /// </summary>
    /// <remarks>
    /// Is an annotated game object, performs no calculations or tracking.
    /// Just provides an association between a character name and a game object.
    /// All the work is done in other classes.
    /// </remarks>
    public class CharacterBubbleAnchor : MonoBehaviour
    {
        /// <summary>
        /// The name of this game object character as Yarn knows it.
        /// </summary>
        /// <remarks>
        /// For example if you had a yarn line "player: ah-ha! I knew it was
        /// you!", the character name will need to be "player", even if you
        /// later intend to display it as something different (or not display it
        /// at all). Otherwise, the Dialogue Presenter won't be able to
        /// associate the character and the <see cref="GameObject"/>.
        /// </remarks>
        public string? CharacterName;

        /// <summary>
        /// The <see cref="Transform"/> that the end of the bubble stem should
        /// be connected to.
        /// </summary>
        /// <remarks>
        /// It is quite uncommon to want to have the bubble stem directly hit
        /// the game object itself. You more often want it floating off to the
        /// top or side a little bit. This transform represents the position for
        /// this character you want the bubble offset to exist at. This is the
        /// backing value that <see cref="BubblePosition"/> uses. If this is
        /// <see langword="null"/>, that is otherwise this objects transform.
        /// </remarks>
        [UnityEngine.Serialization.FormerlySerializedAs("offset")]
        [SerializeField] private Transform? bubblePosition;

        [Header("Overrides")]
        public CharacterBubbleData? characterBubbleData = null;

        /// <summary>
        /// A <see cref="Bubble"/> prefab to use as the bubble for this
        /// character.
        /// </summary>
        /// <remarks>
        /// If this is null, the <see cref="BubbleDialogueView"/> will use the
        /// default prefab.
        /// </remarks>
        [Label("Bubble Override")]
        public Bubble? characterBubblePrefab;


        /// <summary>
        /// It is quite uncommon to want to have the bubble stem directly hit the game object itself.
        /// </summary>
        /// <remarks>
        /// You more often want it floating off to the top or side a little bit.
        /// This transform represents the position for this character you want the bubble offset to exist at.
        /// </remarks>
        public Transform BubblePosition
        {
            get
            {
                if (bubblePosition == null)
                {
                    return this.transform;
                }
                return bubblePosition;
            }
        }

        // just drawing a little green ball to show the position the bubble will reach to
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(BubblePosition.position, 0.1f);
        }
    }
}
