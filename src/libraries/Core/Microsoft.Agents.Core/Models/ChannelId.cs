﻿using System;

namespace Microsoft.Agents.Core.Models
{
    public class ChannelId
    {
        private readonly bool _fullNotation;

        public string Channel { get; set; }
        public string SubChannel { get; set; }

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

        public bool IsParentChannel(string channelId)
        {
            return string.Equals(Channel, channelId, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsSubChannel()
        {
            return !string.IsNullOrEmpty(SubChannel);
        }

        public static bool operator == (ChannelId obj1, ChannelId obj2)
        {
            return string.Equals(obj1?.ToString(), obj2?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool operator != (ChannelId obj1, ChannelId obj2)
        {
            return !string.Equals(obj1?.ToString(), obj2?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(ChannelId other)
        {
            return string.Equals(ToString(), other?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as ChannelId);

        public override int GetHashCode()
        {
            var channelId = ToString();
            return channelId == null ? 0 : channelId.GetHashCode();
        }

        public static implicit operator ChannelId(string value)
        {
            return new ChannelId(value);
        }

        public static implicit operator string(ChannelId channelId)
        {
            return channelId?.ToString();
        }

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
