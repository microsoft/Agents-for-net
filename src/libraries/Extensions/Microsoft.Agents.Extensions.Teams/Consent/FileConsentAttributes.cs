// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using System;
using System.Reflection;

namespace Microsoft.Agents.Extensions.Teams.Consent;

/// <summary>
/// Attribute to define a route that handles Teams file consent accept invocations.
/// The decorated method must match the <see cref="FileConsentHandler"/> delegate signature —
/// the third parameter must be <see cref="Microsoft.Teams.Api.FileConsentCardResponse"/>.
/// </summary>
/// <remarks>
/// <code>
/// [FileConsentAcceptRoute]
/// public async Task OnFileConsentAcceptAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.FileConsentCardResponse response, CancellationToken cancellationToken)
/// {
///     using var fileStream = File.OpenRead("report.txt");
///     var fileContent = new StreamContent(fileStream);
///     fileContent.Headers.ContentLength = new FileInfo("report.txt").Length;
///     await httpClient.PutAsync(response.UploadInfo.UploadUrl, fileContent, cancellationToken);
/// }
/// </code>
/// Alternatively, <see cref="FileConsent.OnAccept"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FileConsentAcceptRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<FileConsentHandler>(app, method);
        var builder = FileConsentAcceptRouteBuilder.Create()
            .WithHandler(handler)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}

/// <summary>
/// Attribute to define a route that handles Teams file consent decline invocations.
/// The decorated method must match the <see cref="FileConsentHandler"/> delegate signature —
/// the third parameter must be <see cref="Microsoft.Teams.Api.FileConsentCardResponse"/>.
/// </summary>
/// <remarks>
/// <code>
/// [FileConsentDeclineRoute]
/// public Task OnFileConsentDeclineAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.FileConsentCardResponse response, CancellationToken cancellationToken)
/// {
///     return turnContext.SendActivityAsync("File upload was declined.", cancellationToken: cancellationToken);
/// }
/// </code>
/// Alternatively, <see cref="FileConsent.OnDecline"/> can be used to register the handler via the fluent API.
/// </remarks>
/// <param name="isAgenticOnly">When <see langword="true"/>, the route only fires for agentic turns. Defaults to <see langword="false"/>.</param>
/// <param name="rank">Route evaluation order. Lower values run first. Defaults to <see cref="RouteRank.Unspecified"/>.</param>
/// <param name="signInHandlers">A comma/space/semicolon-delimited list of OAuth sign-in handler names, or the name of an instance method on the agent class matching <c>Func&lt;ITurnContext, string[]&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public class FileConsentDeclineRouteAttribute(bool isAgenticOnly = false, ushort rank = RouteRank.Unspecified, string signInHandlers = null) : Attribute, IRouteAttribute
{
    /// <inheritdoc />
    public void AddRoute(AgentApplication app, MethodInfo method)
    {
        var handler = RouteAttributeHelper.CreateHandlerDelegate<FileConsentHandler>(app, method);
        var builder = FileConsentDeclineRouteBuilder.Create()
            .WithHandler(handler)
            .AsAgentic(isAgenticOnly)
            .WithOrderRank(rank);
        RouteAttributeHelper.ApplySignInHandlers(app, signInHandlers, s => builder.WithOAuthHandlers(s), f => builder.WithOAuthHandlers(f));
        app.AddRoute(builder.Build());
    }
}
