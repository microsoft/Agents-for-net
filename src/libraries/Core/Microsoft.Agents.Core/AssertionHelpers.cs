using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Agents.Core
{
    public static class AssertionHelpers
    {

        public static void ThrowIfNull(object obj, string name)
        {
#if !NETSTANDARD
            ArgumentNullException.ThrowIfNull(obj, name);
#else
            if (obj == null)
            {
                throw new ArgumentNullException(name);
            }
#endif
        }
        public static void ThrowIfNullOrWhiteSpace(string str, string name)
        {
#if !NETSTANDARD    
            ArgumentException.ThrowIfNullOrWhiteSpace(str, name);
#else
            if (str == null)
                throw new ArgumentNullException(name);

            if (string.IsNullOrWhiteSpace(str))
            {
                throw new ArgumentException(name);
            }
#endif
        }
        public static void ThrowIfNullOrEmpty(string str, string name)
        {
#if !NETSTANDARD    
            ArgumentException.ThrowIfNullOrEmpty(str, name);
#else
            if (str == null)
                throw new ArgumentNullException(name);

            if (string.IsNullOrWhiteSpace(str))
            {
                throw new ArgumentException(name);
            }
#endif
        }

        public static void ThrowIfObjectDisposed(bool obj, string name)
        {
#if !NETSTANDARD
            ObjectDisposedException.ThrowIf(obj, name);
#else
            if (obj)
            {
                throw new ObjectDisposedException(name);
            }
#endif
        }
    }
}
