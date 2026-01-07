// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Net;

namespace Microsoft.Agents.Builder.App.AdaptiveCards
{
    /// <summary>
    /// Contains utility methods for creating various types of <see cref="AdaptiveCardInvokeResponse"/>.
    /// </summary>
    public static class AdaptiveCardInvokeResponseFactory
    {
        /// <summary>
        /// Returns response with type <see cref="ContentTypes.AdaptiveCard"/>.
        /// </summary>
        /// <param name="adaptiveCardJson">An AdaptiveCard JSON value.</param>
        /// <returns>The response that includes an Adaptive Card that the client should display.</returns>
        public static AdaptiveCardInvokeResponse AdaptiveCard(string adaptiveCardJson)
        {
            return new AdaptiveCardInvokeResponse
            {
                StatusCode = 200,
                Type = ContentTypes.AdaptiveCard,
                Value = adaptiveCardJson
            };
        }

        public static AdaptiveCardInvokeResponse SearchResponse(object result)
        {
            return new AdaptiveCardInvokeResponse
            {
                StatusCode = 200,
                Type = "application/vnd.microsoft.search.searchResponse",
                Value = result
            };
        }

        /// <summary>
        /// Returns response with type <see cref="ContentTypes.Message"/>.
        /// </summary>
        /// <param name="message">A message.</param>
        /// <returns>The response that includes a message that the client should display.</returns>
        public static AdaptiveCardInvokeResponse Message(string message)
        {
            return new AdaptiveCardInvokeResponse
            {
                StatusCode = 200,
                Type = ContentTypes.Message,
                Value = message
            };
        }

        /// <summary>
        /// Returns response with type <see cref="ContentTypes.LoginRequest"/>.
        /// </summary>
        /// <param name="card">An OAuthCard</param>
        /// <returns>The response that includes a response that the client should display.</returns>
        public static AdaptiveCardInvokeResponse Login(OAuthCard card)
        {
            return new AdaptiveCardInvokeResponse
            {
                StatusCode = 401,
                Type = ContentTypes.LoginRequest,
                Value = card
            };
        }

        /// <summary>
        /// Returns response with type <see cref="ContentTypes.IncorrectAuthCode"/>.
        /// </summary>
        /// <returns>The response that includes a response that the client should display.</returns>
        public static AdaptiveCardInvokeResponse IncorrectAuthCode()
        {
            return new AdaptiveCardInvokeResponse
            {
                StatusCode = 401,
                Type = ContentTypes.IncorrectAuthCode,
            };
        }

        /// <summary>
        /// Returns response with type <see cref="ContentTypes.PreConditionFailed"/>.
        /// </summary>
        /// <returns>The response that includes a response that the client should display.</returns>
        public static AdaptiveCardInvokeResponse PreConditionFailed(string message, string code = null)
        {
            return new AdaptiveCardInvokeResponse
            {
                StatusCode = 412,
                Type = ContentTypes.PreConditionFailed,
                Value = new Error()
                {
                    Code = code ?? "412",
                    Message = message
                }
            };
        }

        /// <summary>
        /// Creates an Error of type "BadRequest" AdaptiveCardInvokeResponse.
        /// </summary>
        /// <param name="message"></param>
        public static AdaptiveCardInvokeResponse BadRequest(string message)
        {
            return Error(HttpStatusCode.BadRequest, "BadRequest", message);
        }

        /// <summary>
        /// Creates an Error of type "NotSupported" AdaptiveCardInvokeResponse.
        /// </summary>
        /// <param name="message"></param>
        public static AdaptiveCardInvokeResponse NotSupported(string message)
        {
            return Error(HttpStatusCode.BadRequest, "NotSupported", message);
        }

        /// <summary>
        /// Creates an Error of type InternalError AdaptiveCardInvokeResponse.
        /// </summary>
        /// <param name="message"></param>
        public static AdaptiveCardInvokeResponse InternalError(string message)
        {
            return Error(HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError.ToString(), message);
        }

        /// <summary>
        /// Creates an Error AdaptiveCardInvokeResponse.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="statusCode">Defaults to HttpStatusCode.BadRequest.</param>
        /// <param name="code">Defaults to HttpStatusCode.ToString()</param>
        public static AdaptiveCardInvokeResponse Error(HttpStatusCode statusCode, string code, string message)
        {
            return new AdaptiveCardInvokeResponse()
            {
                StatusCode = (int)statusCode,
                Type = ContentTypes.Error,
                Value = new Error()
                {
                    Code = code ?? statusCode.ToString(),
                    Message = message
                }
            };
        }

        public static bool TryValidateSearchInvokeValue(IActivity activity, out AdaptiveCardSearchInvokeValue searchInvokeValue, out AdaptiveCardInvokeResponse errorResponse)
        {
            searchInvokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardSearchInvokeValue>(activity.Value);

            if (searchInvokeValue == null)
            {
                errorResponse = BadRequest("Missing value property for search");
                return false;
            }

            string missingField = null;

            if (string.IsNullOrEmpty(searchInvokeValue.Kind))
            {
                // Teams does not always send the 'kind' field. Default to 'search'.
                if (activity.ChannelId.IsParentChannel(Channels.Msteams))
                {
                    searchInvokeValue.Kind = SearchInvokeTypes.Search;
                }
                else
                {
                    missingField = "kind";
                }
            }

            if (string.IsNullOrEmpty(searchInvokeValue.QueryText))
            {
                missingField = missingField == null ? "queryText" : $"{missingField}, queryText";
            }

            if (missingField != null)
            {
                errorResponse = BadRequest($"Missing '{missingField}' property for search");
                return false;
            }

            errorResponse = null;
            return true;
        }

        public static bool TryValidateActionInvokeValue(IActivity activity, string expectedAction, out AdaptiveCardInvokeValue actionInvokeValue, out AdaptiveCardInvokeResponse errorResponse)
        {
            actionInvokeValue = null;

            if (activity.Value == null)
            {
                errorResponse = BadRequest("Missing value property for Invoke Action");
                return false;
            }

            try
            {
                actionInvokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardInvokeValue>(activity.Value);
            }
            catch
            {
                errorResponse = BadRequest("Value property is not a properly formed Invoke Action");
                return false;
            }

            if (actionInvokeValue.Action == null)
            {
                errorResponse = BadRequest("Missing action property");
                return false;
            }

            if (actionInvokeValue.Action.Type != expectedAction)
            {
                errorResponse = NotSupported($"The Invoke Action '{actionInvokeValue.Action.Type}' was not expected.");
                return false;
            }

            errorResponse = null;
            return true;
        }
    }
}