// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    public class AgenticAuthorization
    {
        /*
        Demo Activity.Recipient

        "recipient":
        {
          "id":"34bde265-6abe-4392-9f2a-90063f156f4a", // AA
          "name":"saapp1user1@projectkairoentra.onmicrosoft.com", //AU UPN
          "aadObjectId":"cc8beb3e-8e7a-4f33-91da-08c612099a58", // AU Oid
          "aadClientId":"52fb5abc-26cb-4ede-b26c-0aa4c1f2154c", // AAI
          "role":"agentuser"
        } 
        */

        /*
        Example 1 
        "recipient": { 
            "id": "ts-smartcard-agent@testcsaaa.onmicrosoft.com", 
            "name": "Smart Card Agent", 
            "agenticUserId": "ac28ca57-8b82-4ff3-9f91-3309c824d67f",  // AAU 
            "agenticAppId": "34d5cf6a-b842-4df1-84bb-1b035b58b833", // AAI 
            "tenantId": "5369a35c-46a5-4677-8ff9-2e65587654e7", 
            "role": "agenticUser" 
        }, 
        "from": {  
            "id": "29:1sH5NArUwkWAX-VmfHH3cfem2S89f2nB0N6aJ5zEjBoxT17fhSMdlYu_55ZyR8_OKFxS3BMnaGldHH3wdf_9K4Q",  
            "name": "MattB",  
            "aadObjectId": "2e278178-80f8-4263-a9b2-761ce462a197",  
            "role": "user"  
        } 

        Example 2: 
        "recipient": { 
            "id": "ts-smartcard-agent@testcsaaa.onmicrosoft.com", 
            "name": "Smart Card Agent", 
            "agenticUserId": "ac28ca57-8b82-4ff3-9f91-3309c824d67f",  // AAU 
            "agenticAppId": "34d5cf6a-b842-4df1-84bb-1b035b58b833", // AAI 
            "tenantId": "5369a35c-46a5-4677-8ff9-2e65587654e7", 
            "role": " agenticAppInstance" 
        }, 
        "from": {  
            "id": "29:1sH5NArUwkWAX-VmfHH3cfem2S89f2nB0N6aJ5zEjBoxT17fhSMdlYu_55ZyR8_OKFxS3BMnaGldHH3wdf_9K4Q",  
            "name": "MattB",  
            "aadObjectId": "2e278178-80f8-4263-a9b2-761ce462a197",  
            "role": "user"  
        } 
        */

        private readonly IConnections _connections;

        public static bool IsAgenticRequest(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            return IsAgenticRequest(turnContext.Activity);
        }
        public static bool IsAgenticRequest(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            return activity?.Recipient?.Role == RoleTypes.AgenticIdentity
                || activity?.Recipient?.Role == RoleTypes.AgenticUser;
        }

        public static string GetAgentInstanceId(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.AgenticAppId;
        }

        public static string GetAgenticUser(ITurnContext turnContext)
        {
            AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
            AssertionHelpers.ThrowIfNull(turnContext.Activity, nameof(turnContext.Activity));

            if (!IsAgenticRequest(turnContext)) return null;
            return turnContext?.Activity?.Recipient?.Id;  // What the demo is using for AU UserUpn
        }

        public AgenticAuthorization(IConnections connections)
        {
            AssertionHelpers.ThrowIfNull(connections, nameof(connections));
            _connections = connections;
        }

        public async Task<string> GetAgenticInstanceTokenAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticInstanceTokenAsync(GetAgentInstanceId(turnContext), cancellationToken);
            }

            throw new InvalidOperationException("Connection doesn't support IAgenticTokenProvider");
        }

        public async Task<string> GetAgenticUserTokenAsync(ITurnContext turnContext, IList<string> scopes, CancellationToken cancellationToken = default)
        {
            if (!IsAgenticRequest(turnContext) || string.IsNullOrEmpty(GetAgenticUser(turnContext)))
            {
                return null;
            }

            var connection = _connections.GetTokenProvider(turnContext.Identity, "agentic");
            if (connection is IAgenticTokenProvider agenticTokenProvider)
            {
                return await agenticTokenProvider.GetAgenticUserTokenAsync(GetAgentInstanceId(turnContext), GetAgenticUser(turnContext), scopes, cancellationToken);
            }

            throw new InvalidOperationException("Connection doesn't support IAgenticTokenProvider");
        }
    }
}
