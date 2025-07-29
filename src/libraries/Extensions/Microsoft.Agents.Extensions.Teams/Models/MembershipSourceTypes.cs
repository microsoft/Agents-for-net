namespace Microsoft.Agents.Extensions.Teams.Models
{
    public enum MembershipSourceTypes
    {
        /// <summary>
        /// Indicates that the source is that of a channel and the user is a member of that channel.
        /// </summary>
        Team,

        /// <summary>
        /// Indicates that the source is that of a team and the user is a member of that team.
        /// </summary>
        Channel
    }
}