// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Microsoft.Agents.Core.Errors
{
    /// <summary>
    /// Exception thrown for an invalid response with ErrorResponse
    /// information.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ErrorResponseException"/> class.
    /// </remarks>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">Inner exception.</param>
    public class ErrorResponseException(string message, System.Exception innerException = null) : Exception(message, innerException)
    {
        /// <summary>
        /// Gets or sets the body object.
        /// </summary>
        /// <value>The body.</value>
        public ErrorResponse Body { get; set; }

        public static ErrorResponseException CreateErrorResponseException(AgentErrorDefinition message, System.Exception innerException = null, params string[] errors) 
        {
            string errorMessageToSend = string.Empty;
            if (errors != null && errors.Length > 0)
            {
                errorMessageToSend = string.Format(message.description, errors);
            }
            else
            {
                errorMessageToSend = message.description;
            }

            var excecp = new ErrorResponseException(errorMessageToSend, innerException)
            {
                HResult = message.code,
                HelpLink = message.helplink
            };
            return excecp;
        }

        public static ErrorResponseException CreateErrorResponseException(HttpResponseMessage httpResponse, AgentErrorDefinition message, System.Exception innerException = null, CancellationToken cancellationToken = default, params string[] errors)
        {
            var ex = CreateErrorResponseException(message, innerException, errors);
            try
            {
#if !NETSTANDARD
                ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content?.ReadAsStream(cancellationToken));
#else
                ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content?.ReadAsStringAsync().Result);
#endif
                if (errorBody != null && errorBody.Error != null)
                {
                    ex.Body = errorBody;
                }
            }
            catch (JsonException)
            {
                // Ignore the exception
            }
            return ex; 
        }

    }
}
