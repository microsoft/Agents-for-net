
namespace Microsoft.Agents.Core.Models.Activities
{
    public class EventActivity : Activity, IEventActivity
    {
        public EventActivity(string name) : base(ActivityTypes.Event)
        { 
            Name = name;
        }

        public string Name { get; set; }
        //!!!public ConversationReference RelatesTo { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
