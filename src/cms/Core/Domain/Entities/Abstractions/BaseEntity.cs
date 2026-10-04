using System.ComponentModel.DataAnnotations.Schema;
using Domain.Entities.Users;

namespace Domain.Entities.Abstractions;

public abstract class BaseEntity : IAuditable, ISoftDeletable
{
    public int Id { get; init; }
    public DateTime CreateDate { get; init; } = DateTime.UtcNow;
    public DateTime? UpdateDate { get; set; }
    public DateTime? DeleteDate { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; } = true;

    #region Foreign Keys
    public Guid CreateUserId { get; init; }
    public Guid? UpdateUserId { get; set; }
    public Guid? DeleteUserId { get; set; } 
    #endregion

    #region Navigation Props
    [ForeignKey(nameof(CreateUserId))]
    public virtual AppUser CreateUser { get; init; }
    [ForeignKey(nameof(UpdateUserId))]
    public virtual AppUser? UpdateUser { get; set; }
    [ForeignKey(nameof(DeleteUserId))]
    public virtual AppUser? DeleteUser { get; set; }
    #endregion
}
