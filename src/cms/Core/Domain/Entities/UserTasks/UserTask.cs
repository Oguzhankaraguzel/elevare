using Domain.Entities.Abstractions;
using Domain.Entities.Users;

namespace Domain.Entities.UserTasks;

public class UserTask : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    /// <summary>
    /// Null while nobody owns the task yet. A task often exists before it is clear
    /// who should pick it up, and forcing a name at creation time means people park
    /// it on themselves and forget — an unassigned task is at least visibly ownerless.
    /// </summary>
    public Guid? AssignedToUserId { get; set; }

    public Guid? AssignedByUserId { get; set; }

    public UserTaskStatus Status { get; set; } = UserTaskStatus.New;
    public UserTaskPriority Priority { get; set; } = UserTaskPriority.Medium;

    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool IsRead { get; set; }
    public bool IsArchived { get; set; }

    public int? ParentTaskId { get; set; }
    public UserTask? ParentTask { get; set; }
    public ICollection<UserTask> SubTasks { get; set; } = new List<UserTask>();

    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }

    public AppUser? AssignedToUser { get; set; }
    public AppUser? AssignedByUser { get; set; }

    public ICollection<UserTaskComment> Comments { get; set; } = new List<UserTaskComment>();
}
