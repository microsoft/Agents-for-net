using System;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Represents a channel identifier, optionally including a sub-channel.
    /// Provides parsing, equality, and conversion operations for channel IDs.
    /// </summary>
    public class ChannelId
    {
        private readonly bool _fullNotation;

        /// <summary>
        /// Gets or sets the main channel name.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Gets or sets the sub-channel name, if present.
        /// </summary>
        public string SubChannel { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChannelId"/> class by parsing the specified channel ID string.
        /// </summary>
        /// <param name="channelId">The channel ID string, optionally in the format "Channel:SubChannel".</param>
        /// <param name="fullNotation">Indicates whether to use full notation (include sub-channel in string representation).</param>
        public ChannelId(string channelId, bool fullNotation = true)
        {
            _fullNotation = fullNotation;
            if (!string.IsNullOrEmpty(channelId))
            {
                var split = channelId.Split(':');
                Channel = split[0];
                SubChannel = split.Length == 2 ? split[1] : null;
            }
        }

        /// <summary>
        /// Determines whether the specified channel ID matches the parent channel.
        /// </summary>
        /// <param name="channelId">The channel ID to compare.</param>
        /// <returns><c>true</c> if the specified channel ID matches the parent channel; otherwise, <c>false</c>.</returns>
        public bool IsParentChannel(string channelId)
        {
            return string.Equals(Channel, channelId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether this instance represents a sub-channel.
        /// </summary>
        /// <returns><c>true</c> if a sub-channel is present; otherwise, <c>false</c>.</returns>
        public bool IsSubChannel()
        {
            return !string.IsNullOrEmpty(SubChannel);
        }

        /// <summary>
        /// Determines whether two <see cref="ChannelId"/> instances are equal.
        /// </summary>
        /// <param name="obj1">The first <see cref="ChannelId"/> instance.</param>
        /// <param name="obj2">The second <see cref="ChannelId"/> instance.</param>
        /// <returns><c>true</c> if the instances are equal; otherwise, <c>false</c>.</returns>
        public static bool operator ==(ChannelId obj1, ChannelId obj2)
        {
            return string.Equals(obj1?.ToString(), obj2?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether two <see cref="ChannelId"/> instances are not equal.
        /// </summary>
        /// <param name="obj1">The first <see cref="ChannelId"/> instance.</param>
        /// <param name="obj2">The second <see cref="ChannelId"/> instance.</param>
        /// <returns><c>true</c> if the instances are not equal; otherwise, <c>false</c>.</returns>
        public static bool operator !=(ChannelId obj1, ChannelId obj2)
        {
            return !string.Equals(obj1?.ToString(), obj2?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified <see cref="ChannelId"/> is equal to the current instance.
        /// </summary>
        /// <param name="other">The <see cref="ChannelId"/> to compare with the current instance.</param>
        /// <returns><c>true</c> if the specified <see cref="ChannelId"/> is equal to the current instance; otherwise, <c>false</c>.</returns>
        public bool Equals(ChannelId other)
        {
            return string.Equals(ToString(), other?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as ChannelId);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var channelId = ToString();
            return channelId == null ? 0 : channelId.GetHashCode();
        }

        /// <summary>
        /// Implicitly converts a string to a <see cref="ChannelId"/> instance.
        /// </summary>
        /// <param name="value">The channel ID string.</param>
        public static implicit operator ChannelId(string value)
        {
            return new ChannelId(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="ChannelId"/> instance to a string.
        /// </summary>
        /// <param name="channelId">The <see cref="ChannelId"/> instance.</param>
        public static implicit operator string(ChannelId channelId)
        {
            return channelId?.ToString();
        }

        /// <summary>
        /// Returns the string representation of the channel ID, including the sub-channel if <c>fullNotation</c> is <c>true</c>.
        /// fullNotation is controlled by the property <c>ProtocolJsonSerializer.ChannelIdIncludesProduct</c>
        /// </summary>
        /// <returns>The channel ID as a string.</returns>
        public override string ToString()
        {
            if (_fullNotation && !string.IsNullOrEmpty(SubChannel))
            {
                return $"{Channel}:{SubChannel}";
            }
            return Channel;
        }
    }
}