// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.Connector
{
    public static class HttpClientExtensions
    {
        /// <summary>
        /// The product info header value for the SDK. Stored as Static for performance.
        /// </summary>
        private static ProductInfoHeaderValue _frameworkProductInfo = null;
        /// <summary>
        /// SDK Version String. Stored as Static for performance.
        /// </summary>
        private static ProductInfoHeaderValue _versionString = null;

        public static void AddDefaultUserAgent(this HttpClient httpClient, ProductInfoHeaderValue[] additionalProductInfo = null)
        {
            if (httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                return;    
            }

            var userAgent = new List<ProductInfoHeaderValue>();

            userAgent.Add(new ProductInfoHeaderValue("Microsoft-Agents", "1.0"));
            userAgent.Add(GetClientProductInfo());

            var framework = GetFrameworkProductInfo();
            if (framework != null)
            {
                userAgent.Add(framework);
            }

            if (additionalProductInfo != null)
            {
                userAgent.AddRange(additionalProductInfo);
            }

            var userAgentValue = string.Empty;
            foreach (var productInfo in userAgent)
            {
                if (string.IsNullOrEmpty(userAgentValue))
                {
                    userAgentValue = productInfo.ToString();
                }
                else
                {
                    userAgentValue += " " + productInfo.ToString();
                }
            }

            httpClient.DefaultRequestHeaders.Add("User-Agent", userAgentValue);
        }

        private static ProductInfoHeaderValue GetClientProductInfo()
        {
            if (_versionString != null)
            {
                return _versionString;
            }

            var type = typeof(RestConnectorClient);
            var assembly = type.GetTypeInfo().Assembly;

            var version = assembly.GetName().Version.ToString();

            _versionString = new ProductInfoHeaderValue("Microsoft-Agent-SDK", version);
            return _versionString;
        }

        private static readonly Regex FrameworkRegEx = new Regex(@"(?:(\d+)\.)?(?:(\d+)\.)?(?:(\d+)\.\d+)", RegexOptions.Compiled);

        private static ProductInfoHeaderValue GetFrameworkProductInfo()
        {
            if (_frameworkProductInfo != null)
                return _frameworkProductInfo;

            var frameworkName = Assembly
                    .GetEntryAssembly()?
                    .GetCustomAttribute<TargetFrameworkAttribute>()?
                    .FrameworkName ?? RuntimeInformation.FrameworkDescription;

            if (!string.IsNullOrWhiteSpace(frameworkName) && frameworkName.Length > 0)
            {
                // from:
                // .NETCoreApp,Version=v3.1
                // to:
                // .NETCoreAppVersion/v3.1

                // from:
                // .NET Framework 4.8.4250.0
                // to:
                // .NETFramework/4.8.4250.0

                var splitFramework = frameworkName.Replace(",", string.Empty).Replace(" ", string.Empty).Split('=');
                if (splitFramework.Length > 1)
                {
                    ProductInfoHeaderValue.TryParse($"{splitFramework[0]}/{splitFramework[1]}", out _frameworkProductInfo);
                }
                else if (splitFramework.Length > 0)
                {
                    frameworkName = splitFramework[0];

                    // Parse the version from the framework string.
                    var version = FrameworkRegEx.Match(frameworkName);

                    if (version.Success)
                    {
                        frameworkName = frameworkName.Replace(version.Value, string.Empty).Trim();
                        ProductInfoHeaderValue.TryParse($"{frameworkName}/{version.Value}", out _frameworkProductInfo);
                    }
                }
            }

            return _frameworkProductInfo;
        }
    }
}
