
namespace Microsoft.Agents.Core.Models.Activities
{
    public class TypingActivity : Activity, ITypingActivity
    {
        public TypingActivity() : base(ActivityTypes.Typing)
        {
        }

        public string Text { get; set; }
        public string TextFormat { get; set; }
    }
}
