// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A Hero card (card with a single, large image). </summary>
    public class HeroCard : Card
    {
        /// <summary> Initializes a new instance of HeroCard. </summary>
        public HeroCard()
        {
            Images = [];
            Buttons = [];
        }

        /// <summary> Initializes a new instance of HeroCard. </summary>
        /// <param name="title"> Title of the card. </param>
        /// <param name="subtitle"> Subtitle of the card. </param>
        /// <param name="text"> Text for the card. </param>
        /// <param name="images"> Array of images for the card. </param>
        /// <param name="buttons"> Set of actions applicable to the current card. </param>
        /// <param name="tap"> A clickable action. </param>
        public HeroCard(string title = default, string subtitle = default, string text = default, IList<CardImage> images = default, IList<CardAction> buttons = default, CardAction tap = default)
        {
            Title = title;
            Subtitle = subtitle;
            Text = text;
            Images = images ?? [];
            Buttons = buttons ?? [];
            Tap = tap;
        }

        /// <summary>
        /// The content type value of a <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.
        /// </summary>
        public const string ContentType = Models.ContentTypes.HeroCard;

        /// <summary>
        /// Creates a new attachment from <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.
        /// </summary>
        /// <param name="card"> The instance of <see cref="Microsoft.Agents.Core.Models.HeroCard"/>.</param>
        /// <returns> The generated attachment.</returns>
        public override Attachment ToAttachment()
        {
            return new Attachment
            {
                Content = this,
                ContentType = ContentType
            };
        }

        /// <summary> Title of the card. </summary>
        public string Title { get; set; }
        /// <summary> Subtitle of the card. </summary>
        public string Subtitle { get; set; }
        /// <summary> Text for the card. </summary>
        public string Text { get; set; }
        /// <summary> Array of images for the card. </summary>
        public IList<CardImage> Images { get; set; }
        /// <summary> Set of actions applicable to the current card. </summary>
        public IList<CardAction> Buttons { get; set; }
        /// <summary> A clickable action. </summary>
        public CardAction Tap { get; set; }

        /// <summary> Adds an image and returns this card. </summary>
        public HeroCard AddImage(CardImage image) { Images ??= []; Images.Add(image); return this; }
        /// <summary> Adds an image by URL and returns this card. </summary>
        public HeroCard AddImage(string url, string alt = null) { Images ??= []; Images.Add(new CardImage(url, alt)); return this; }
        /// <summary> Adds a button and returns this card. </summary>
        public HeroCard AddButton(CardAction button) { Buttons ??= []; Buttons.Add(button); return this; }
        /// <summary> Adds a button by title/type/value and returns this card. </summary>
        public HeroCard AddButton(string title, string type = ActionTypes.ImBack, object value = null) { Buttons ??= []; Buttons.Add(new CardAction(type: type, title: title, value: value ?? title)); return this; }
        /// <summary> Adds one or more buttons and returns this card. </summary>
        public HeroCard AddButtons(params CardAction[] buttons) { if (buttons == null) { return this; } Buttons ??= []; foreach (var button in buttons) { Buttons.Add(button); } return this; }
    }
}
