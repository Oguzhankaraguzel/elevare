using Domain.Entities.UserReminders;

namespace Wasm.Components.Shared.Panels;

public partial class ReminderListPanel
{
    private sealed class ReminderFormModel
    {
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime RemindAt { get; set; } = DateTime.UtcNow.AddHours(1);
        public ReminderChannel Channel { get; set; } = ReminderChannel.InApp;
        public bool IsCompleted { get; set; }
        public bool IsDismissed { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
