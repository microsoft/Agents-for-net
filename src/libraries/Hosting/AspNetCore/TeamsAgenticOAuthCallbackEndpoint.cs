// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.UserAuth.TeamsAgentic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    /// <summary>
    /// Thin ASP.NET Core adapter for the Teams agentic OAuth callback.
    /// Extracts HTTP request parameters and delegates to <see cref="TeamsAgenticCallbackHandler"/>
    /// for all auth logic.
    /// </summary>
    internal static class TeamsAgenticOAuthCallbackEndpoint
    {
        private const string SuccessHtml = @"<!DOCTYPE html>
<html>
<head><title>Sign-in Complete</title></head>
<body>
<h2>Sign-in successful!</h2>
<p>You can close this window and return to the conversation.</p>
<script>window.close();</script>
</body>
</html>";

        private const string ErrorHtml = @"<!DOCTYPE html>
<html>
<head><title>Sign-in Failed</title></head>
<body>
<h2>Sign-in failed</h2>
<p>{0}</p>
</body>
</html>";

        public static async Task HandleCallbackAsync(HttpContext httpContext)
        {
            var handler = httpContext.RequestServices.GetRequiredService<TeamsAgenticCallbackHandler>();

            var input = new OAuthCallbackInput
            {
                Code = httpContext.Request.Query["code"].ToString(),
                State = httpContext.Request.Query["state"].ToString(),
                Error = httpContext.Request.Query["error"].ToString(),
                ErrorDescription = httpContext.Request.Query["error_description"].ToString()
            };

            var result = await handler.HandleAsync(input, httpContext.RequestAborted).ConfigureAwait(false);

            httpContext.Response.ContentType = "text/html";
            httpContext.Response.StatusCode = result.StatusCode;

            if (result.Success)
            {
                await httpContext.Response.WriteAsync(SuccessHtml, httpContext.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                await httpContext.Response.WriteAsync(
                    string.Format(ErrorHtml, WebUtility.HtmlEncode(result.ErrorMessage)),
                    httpContext.RequestAborted).ConfigureAwait(false);
            }
        }
    }
}
