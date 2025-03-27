using System;

namespace Microsoft.Agents.Core.Models
{
    public class ChannelId
    {
        public string Channel { get; set; }
        public string SubChannel { get; set; }

        public ChannelId(string channelId)
        {
            var split = channelId.Split(':');
            Channel = split[0];
            SubChannel = split.Length == 2 ? split[1] : null;
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
            return Channel.GetHashCode();
        }

        public static implicit operator ChannelId(string value)
        {
            return new ChannelId(value);
        }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(SubChannel))
            {
                return $"{Channel}:{SubChannel}";
            }
            return Channel;
        }
    }
}
