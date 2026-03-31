// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Teams.Api;
using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models;

/// <summary>
/// Data about the meeting participants.
/// </summary>
/// <remarks>Back-compat Teams Model that does not exist in Teams SDK</remarks>
public class MeetingParticipantsEventDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingParticipantsEventDetails"/> class.
    /// </summary>
    public MeetingParticipantsEventDetails()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingParticipantsEventDetails"/> class.
    /// </summary>
    /// <param name="members">The members involved in the meeting event.</param>
    public MeetingParticipantsEventDetails(
        IList<TeamsMeetingMember> members = default)
    {
        Members = members;
    }

    /// <summary>
    /// Gets the meeting participants info.
    /// </summary>
    /// <value>
    /// The participant accounts info.
    /// </value>
    public IList<TeamsMeetingMember> Members { get; set; } = new List<TeamsMeetingMember>();
}

/// <summary>
/// Data about the meeting participants.
/// </summary>
public class TeamsMeetingMember
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TeamsMeetingMember"/> class.
    /// </summary>
    /// <param name="user">The channel user data.</param>
    /// <param name="meeting">The user meeting details.</param>
    public TeamsMeetingMember(Account user, UserMeetingDetails meeting)
    {
        User = user;
        Meeting = meeting;
    }

    /// <summary>
    /// Gets or sets the meeting participant.
    /// </summary>
    /// <value>
    /// The joined participant account.
    /// </value>
    public Account User { get; set; }

    /// <summary>
    /// Gets or sets the user meeting details.
    /// </summary>
    /// <value>
    /// The users meeting details.
    /// </value>
    public UserMeetingDetails Meeting { get; set; }
}

/// <summary>
/// Specific details of a user in a Teams meeting.
/// </summary>
public class UserMeetingDetails
{
    /// <summary>
    /// Gets or sets a value indicating whether the user is in the meeting.
    /// </summary>
    /// <value>
    /// The user in meeting indicator.
    /// </value>
    public bool InMeeting { get; set; }

    /// <summary>
    /// Gets or sets the value of the user's role.
    /// </summary>
    /// <value>
    /// The user's role.
    /// </value>
    public string Role { get; set; }
}
