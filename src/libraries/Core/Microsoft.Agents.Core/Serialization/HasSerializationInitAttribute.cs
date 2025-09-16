using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Serialization
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class HasSerializationInitAttribute : Attribute
    {
    }
}
