// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Hosting.AspNetCore.HeaderPropagation;

/// <summary>
/// A middleware to propagate incoming request headers to outgoing ones using internally a custom <see cref="IHttpClientFactory"/>.
/// </summary>
public class HeaderPropagationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HeaderPropagationContext _context;

    /// <summary>
    /// Initializes a new instance of <see cref="HeaderPropagationMiddleware"/>.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="context">The <see cref="HeaderPropagationContext"/> that stores the request headers to be propagated.</param>
    public HeaderPropagationMiddleware(RequestDelegate next, HeaderPropagationContext context)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(context);

        _next = next;
        _context = context;
    }

    /// <summary>
    /// Executes the middleware to set the request headers in <see cref="HeaderPropagationContext"/>.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    public Task Invoke(HttpContext context)
    {
        _context.Headers = context.Request.Headers;

        return _next.Invoke(context);
    }
}