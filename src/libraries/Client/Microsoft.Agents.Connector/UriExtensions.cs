using System;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// Extensions for URI's that are used by the Agents Connector Namespace.. 
    /// </summary>
    internal static class UriExtensions
    {
        internal static Uri EnsureTrailingSlash(this Uri uri)
        {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            string uriString = uri.ToString();
            if (!uriString.EndsWith("/"))
            {
                uriString += "/";
            }
            return new Uri(uriString);
        }
    }
}
