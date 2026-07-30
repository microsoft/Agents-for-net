// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.IO;
using System.Text;

namespace Microsoft.Agents.Core.Models
{
    /// <summary> A card that carries a raw Adaptive Card JSON payload. </summary>
    /// <remarks>
    /// The JSON is stored verbatim in <see cref="Content"/> and unpacked into the attachment as nested
    /// JSON when the activity is serialized.
    /// </remarks>
    public class AdaptiveCardCard : Card
    {
        /// <summary>
        /// The content type value of an <see cref="Microsoft.Agents.Core.Models.AdaptiveCardCard"/>.
        /// </summary>
        public const string ContentType = Models.ContentTypes.AdaptiveCard;

        /// <summary> The Adaptive Card content as a JSON string. </summary>
        public string Content;

        /// <summary> Initializes a new instance of <see cref="Microsoft.Agents.Core.Models.AdaptiveCardCard"/> from an Adaptive Card JSON string. </summary>
        /// <param name="json"> The Adaptive Card content as a JSON string. </param>
        public AdaptiveCardCard(string json)
        {
            Content = json;
        }

        /// <summary> Initializes a new instance of <see cref="Microsoft.Agents.Core.Models.AdaptiveCardCard"/> from a stream containing Adaptive Card JSON. </summary>
        /// <param name="stream"> A stream containing the Adaptive Card JSON. The stream is left open. </param>
        public AdaptiveCardCard(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            Content = reader.ReadToEnd();
        }

        /// <inheritdoc/>
        public override Attachment ToAttachment()
        {
            return new Attachment
            {
                ContentType = ContentType,
                Content = Content
            };
        }
    }
}
