
namespace Microsoft.Agents.Core.Models.Activities
{
    public class CommandResultActivity : Activity, ICommandResultActivity
    {
        public CommandResultActivity() : base(ActivityTypes.CommandResult)
        {
        }

        public string Name { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
