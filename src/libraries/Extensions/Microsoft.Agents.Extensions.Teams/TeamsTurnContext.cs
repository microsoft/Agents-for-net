using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    class TeamsTurnContext : ITurnContext
    {

        private readonly ITurnContext _turnContext;

        IChannelAdapter Adapter
        {
            get { return _turnContext.Adapter; }
        }

        TurnContextStateCollection Services
        {
            get {  return _turnContext.Services; }
        }

        TurnContextStateCollection StackState
        {
            get { return _turnContext.StackState; }
        }

        IStreamingResponse StreamingResponse
        {
            get { return _turnContext.StreamingResponse; }
        }

        bool Responses
        {
            get { return _turnContext.Responded; }
        }

        ClaimsIdentity Identity
        {
            get { return _turnContext.Identity; }
        }

        Task<ResourceResponse> SendActivityAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(text, speak, inputHint, cancellationToken);
        }

        Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(activity, cancellationToken);
        }

        Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.UpdateActivityAsync(activity, cancellationToken);
        }

        Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(activityId, cancellationToken);
        }

        Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(conversationReference, cancellationToken);
        }

        TeamsTurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            _turnContext.OnSendActivities(handler);
            return this;
        }

        TeamsTurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            _turnContext.OnUpdateActivity(handler);
            return this;
        }

        TeamsTurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            _turnContext.OnDeleteActivity(handler);
            return this;
        }

        Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
        {
            return _turnContext.TraceActivityAsync(name, value, valueType, label, cancellationToken);
        }

        public TeamsTurnContext(ITurnContext turnContext)
        {
            this._turnContext = turnContext;
        }
    }
}
