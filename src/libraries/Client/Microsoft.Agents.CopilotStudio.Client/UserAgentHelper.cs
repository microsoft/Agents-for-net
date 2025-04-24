using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;

namespace Microsoft.Agents.CopilotStudio.Client
{
    internal static class UserAgentHelper
    {
        private static object _initLock = new object();
        public static string ClientFileVersion { get; private set; }

        public static string ClientName { get; private set; } = "CopilotStudioClient";

        public static ProductInfoHeaderValue UserAgentHeader { get; private set; }

        static UserAgentHelper()
        {
            lock (_initLock)
            {
                if (string.IsNullOrEmpty(ClientFileVersion))
                {
                    ClientFileVersion = "unknown";
                    // Get the version of the assembly

                    try
                    {
                        if (Version.TryParse(ThisAssembly.AssemblyFileVersion, out Version? fileVersion))
                        {
                            ClientFileVersion = fileVersion.ToString(3);
                        }
                        else
                        {
                            ClientFileVersion = "0.0.0.0";
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting assembly version: {ex.Message}");
                    }
                }
            }
            if (string.IsNullOrEmpty(ClientFileVersion))
            {
                ClientFileVersion = "unknown";
            }

            UserAgentHeader = new ProductInfoHeaderValue(ClientName, ClientFileVersion);
        }
    }
}
