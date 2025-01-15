// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> Media card. </summary>
    public class MediaCard
    {
        /// <summary> Initializes a new instance of MediaCard. </summary>
        public MediaCard()
        {
            Media = new List<MediaUrl>();
            Buttons = new List<CardAction>();
        }

        /// <summary> Initializes a new instance of MediaCard. </summary>
        /// <param name="title"> Title of this card. </param>
        /// <param name="subtitle"> Subtitle of this card. </param>
        /// <param name="text"> Text of this card. </param>
        /// <param name="image"> Thumbnail URL. </param>
        /// <param name="media"> Media URLs for this card. When this field contains more than one URL, each URL is an alternative format of the same content. </param>
        /// <param name="buttons"> Actions on this card. </param>
        /// <param name="shareable"> This content may be shared with others (default:true). </param>
        /// <param name="autoloop"> Should the client loop playback at end of content (default:true). </param>
        /// <param name="autostart"> Should the client automatically start playback of media in this card (default:true). </param>
        /// <param name="aspect"> Aspect ratio of thumbnail/media placeholder. Allowed values are "16:9" and "4:3". </param>
        /// <param name="duration"> Describes the length of the media content without requiring a receiver to open the content. Formatted as an ISO 8601 Duration field. </param>
        /// <param name="value"> Supplementary parameter for this card. </param>
        public MediaCard(string title = default, string subtitle = default, string text = default, ThumbnailUrl image = default, IList<MediaUrl> media = default, IList<CardAction> buttons = default, bool? shareable = default, bool? autoloop = default, bool? autostart = default, string aspect = default, object value = default, string duration = default)
        {
            Title = title;
            Subtitle = subtitle;
            Text = text;
            Image = image;
            Media = media ?? new List<MediaUrl>();
            Buttons = buttons ?? new List<CardAction>();
            Shareable = shareable;
            Autoloop = autoloop;
            Autostart = autostart;
            Aspect = aspect;
            Duration = duration;
            Value = value;
        }

        /// <summary> Title of this card. </summary>
        public string Title { get; set; }
        /// <summary> Subtitle of this card. </summary>
        public string Subtitle { get; set; }
        /// <summary> Text of this card. </summary>
        public string Text { get; set; }
        /// <summary> Thumbnail URL. </summary>
        public ThumbnailUrl Image { get; set; }
        /// <summary> Media URLs for this card. When this field contains more than one URL, each URL is an alternative format of the same content. </summary>
        public IList<MediaUrl> Media { get; set; }
        /// <summary> Actions on this card. </summary>
        public IList<CardAction> Buttons { get; set; }
        /// <summary> This content may be shared with others (default:true). </summary>
        public bool? Shareable { get; set; }
        /// <summary> Should the client loop playback at end of content (default:true). </summary>
        public bool? Autoloop { get; set; }
        /// <summary> Should the client automatically start playback of media in this card (default:true). </summary>
        public bool? Autostart { get; set; }
        /// <summary> Aspect ratio of thumbnail/media placeholder. Allowed values are "16:9" and "4:3". </summary>
        public string Aspect { get; set; }
        /// <summary> Describes the length of the media content without requiring a receiver to open the content. Formatted as an ISO 8601 Duration field. </summary>
        public string Duration { get; set; }
        /// <summary> Supplementary parameter for this card. </summary>
        public object Value { get; set; }
    }
}
