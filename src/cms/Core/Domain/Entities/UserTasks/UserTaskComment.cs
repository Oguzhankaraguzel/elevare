using System.ComponentModel.DataAnnotations.Schema;
using Domain.Entities.Abstractions;
using Domain.Entities.Users;

namespace Domain.Entities.UserTasks;

public class UserTaskComment : BaseEntity
{
    public int UserTaskId { get; set; }
    public Guid UserId { get; set; }
    public string Comment { get; set; } = null!;

    [ForeignKey(nameof(UserTaskId))]
    public virtual UserTask UserTask { get; set; } = null!;
    [ForeignKey(nameof(UserId))]
    public virtual AppUser User { get; set; } = null!;
}
