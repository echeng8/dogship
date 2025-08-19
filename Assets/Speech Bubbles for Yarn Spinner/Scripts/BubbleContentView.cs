namespace Yarn.Unity.Addons.SpeechBubbles
{
    using UnityEngine;
    using TMPro;
    using Yarn.Unity;

#nullable enable

    /// <summary>
    /// Represents the content inside of a bubble.
    /// </summary>
    /// <remarks>
    /// Exists so that the <see cref="Bubble"/> class, which is responsible for tracking, doesn't also have to do display.
    /// This gives an easy subclassing entry point without needing to also risk breaking the tracking code.
    /// </remarks>
    public abstract class BubbleContentView : MonoBehaviour
    {
        /// <summary>
        /// Calculates and returns the size of the bubble for the provided text.
        /// </summary>
        /// <remarks>
        /// This is to be used so that the dialogue view will know how big to make the view for the upcoming text provided in <paramref name="line"/>.
        /// Is necessary as we can't lerp between sizes until we know the final size, but if we just set the bubble to have its upcoming line content
        /// it will snap to its final size, as such we use to get the size so we can move to the new size without disrupting the current content.
        /// </remarks>
        /// <param name="line">The new text soon to be displayed.</param>
        /// <param name="isOption">Controls if the upcoming content is to be an option or not.
        /// As options generally will have other elements than just the text the content will need to know this get the size correct.</param>
        /// <returns>The size of this bubble based on the upcoming line content</returns>
        public abstract Vector2 GetContentSize(Bubble.BubbleContent content);

        /// <summary>
        /// Used to configure what option navigation elements are shown and if they are active or not.
        /// </summary>
        /// <remarks>
        /// This is all (I think) the info your custom views will need to determine their own navigation elements.
        /// </remarks>
        /// <param name="currentOptionIndex">What index in the option group is the current option.</param>
        /// <param name="totalAvailableOptions"The total number of options in the option group.</param>
        /// <param name="decrementNavigationAvailable">Based on dialogue view configuration and the <paramref name="currentOptionIndex"/> can the user increment the current option index?</param>
        /// <param name="incrementNavigationAvailable">Based on dialogue view configuration and the <paramref name="currentOptionIndex"/> can the user decrement the current option index?</param>
        public abstract void SetOptionNavigation(int currentOptionIndex, int totalAvailableOptions, bool decrementNavigationAvailable, bool incrementNavigationAvailable);

        /// <summary>
        /// Informs the content that the next content change will be to present a line.
        /// </summary>
        /// <remarks>
        /// This method is used so that the content can then enable or disable any UI elements so they are ready for this.
        /// </remarks>
        public abstract void PrepareForLine();

        /// <summary>
        /// Informs the content that the next content change will be to present an option.
        /// </summary>
        /// <remarks>
        /// This method is used so that the content can enable or disable any UI elements so that it is ready to show an option.
        /// </remarks>
        public abstract void PrepareForOptions();

        /// <summary>
        /// Informs the content of the name of the speaking character for this upcoming line.
        /// </summary>
        /// <remarks>
        /// Intended to be used to set up any nameplates or similar setup needed to unqiuely modify each bubble.
        /// </remarks>
        /// <param name="characterBubbleData">
        /// A <see cref="CharacterBubbleData"/> containing information on the character who is speaking.
        /// </param>
        public abstract void SetCharacter(CharacterBubbleData? characterBubbleData);

        public abstract void PrepareForContent(Bubble.BubbleContent content);

        public abstract YarnTask PresentContent(Bubble.BubbleContent content, System.Threading.CancellationToken cancellationToken);
        public abstract YarnTask DismissContent(System.Threading.CancellationToken cancellationToken);

        internal abstract void SetContentWithoutAnimation(Bubble.BubbleContent content);

        /// <summary>
        /// The label for the line text.
        /// </summary>
        /// <remarks>
        /// We provide direct access to this so we can use various text effects on it, among other things.
        /// </remarks>
        public TMP_Text? textField;

        /// <summary>
        /// Represents the presentation factor for animation purposes - that is,
        /// how "much" has the bubble appeared?
        /// </summary>
        /// <remarks>
        /// Will be set by the dialogue view during presentation and dismissal
        /// moments. This value is always normalised to be between 0 and 1.
        /// </remarks>
        public virtual float PresentationFactor { get; set; } = 0;
    }
}
