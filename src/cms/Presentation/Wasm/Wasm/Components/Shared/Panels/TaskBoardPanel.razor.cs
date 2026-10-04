using Application.Features.Queries.UserTasks.GetUserTaskById;
using Domain.Entities.UserTasks;

namespace Wasm.Components.Shared.Panels;

public partial class TaskBoardPanel
{
    private enum WeekFilter { All, ThisWeek }

    private sealed record BoardColumn(UserTaskStatus Status, string Label, string Icon, string Color);

    private sealed class TaskFormModel
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string AssignedToUserId { get; set; } = "";
        public UserTaskPriority Priority { get; set; } = UserTaskPriority.Medium;
        public UserTaskStatus Status { get; set; } = UserTaskStatus.New;
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsArchived { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // Comment state
    private List<UserTaskCommentResponse> _comments = [];
    private bool _loadingComments;
    private string _newComment = "";
    private bool _savingComment;
}
