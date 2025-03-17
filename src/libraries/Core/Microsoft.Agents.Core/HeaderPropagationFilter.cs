
using System.Collections.Generic;

namespace Microsoft.Agents.Core
{
    public class HeaderPropagationFilter(IList<string> headers = null, string userAgentAddition = null) : IHeaderPropagationFilter
    {
        public IList<string> Headers => headers ?? [];

        public string UserAgent => userAgentAddition;
    }
}
