using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams
{
    public abstract class TurnContextWrapper : ITurnContext
    {

        protected readonly ITurnContext _turnContext;

        public IChannelAdapter Adapter
        {
            get { return _turnContext.Adapter; }
        }

        public TurnContextStateCollection Services
        {
            get { return _turnContext.Services; }
        }

        public TurnContextStateCollection StackState
        {
            get { return _turnContext.StackState; }
        }

        public IActivity Activity
        {
            get { return _turnContext.Activity; }
        }

        public IStreamingResponse StreamingResponse
        {
            get { return _turnContext.StreamingResponse; }
        }

        public bool Responded
        {
            get { return _turnContext.Responded; }
        }

        public ClaimsIdentity Identity
        {
            get { return _turnContext.Identity; }
        }

        public TurnContextWrapper(ITurnContext turnContext)
        {
            this._turnContext = turnContext;
        }

        public Task<ResourceResponse> SendActivityAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(text, speak, inputHint, cancellationToken);
        }

        public Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(activity, cancellationToken);
        }

        public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivitiesAsync(activities, cancellationToken);
        }

        public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.UpdateActivityAsync(activity, cancellationToken);
        }

        public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(activityId, cancellationToken);
        }

        public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(conversationReference, cancellationToken);
        }

        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            _turnContext.OnSendActivities(handler);
            return this;
        }

        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            _turnContext.OnUpdateActivity(handler);
            return this;
        }

        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            _turnContext.OnDeleteActivity(handler);
            return this;
        }

        public Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
        {
            return _turnContext.TraceActivityAsync(name, value, valueType, label, cancellationToken);
        }
    }
}
