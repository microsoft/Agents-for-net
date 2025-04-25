﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    public class UserTokenClientWrapper
    {
        public static Task<SignInResource> GetSignInResourceAsync(ITurnContext context, string connectionName, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return userTokenClient.GetSignInResourceAsync(connectionName, context.Activity, null, cancellationToken);
        }

        public static Task<TokenResponse> GetUserTokenAsync(ITurnContext context, string connectionName, string magicCode, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return userTokenClient.GetUserTokenAsync(context.Activity.From.Id, connectionName, context.Activity.ChannelId, magicCode, cancellationToken);
        }

        public static Task<TokenOrSignInResourceResponse> GetTokenOrSignInResourceAsync(ITurnContext context, string connectionName, string magicCode = null, CancellationToken cancellationToken = default)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return userTokenClient.GetTokenOrSignInResourceAsync(connectionName, context.Activity, magicCode, null, null, cancellationToken);
        }

        public static Task<TokenResponse> ExchangeTokenAsync(ITurnContext context, string connectionName, TokenExchangeRequest tokenExchangeRequest, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return userTokenClient.ExchangeTokenAsync(context.Activity.From.Id, connectionName, context.Activity.ChannelId, tokenExchangeRequest, cancellationToken);
        }

        public static Task SignOutUserAsync(ITurnContext context, string connectionName, CancellationToken cancellationToken)
        {
            IUserTokenClient userTokenClient = GetUserTokenClient(context);
            return userTokenClient.SignOutUserAsync(context.Activity.From.Id, connectionName, context.Activity.ChannelId, cancellationToken);
        }

        private static IUserTokenClient GetUserTokenClient(ITurnContext context)
        {
            IUserTokenClient userTokenClient = context.Services.Get<IUserTokenClient>();
            return userTokenClient ?? throw new NotSupportedException("IUserTokenClient is not supported by the current adapter");
        }
    }
}
