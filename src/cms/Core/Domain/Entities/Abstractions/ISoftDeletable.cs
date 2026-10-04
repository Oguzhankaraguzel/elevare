using Domain.Entities.Users;

namespace Domain.Entities.Abstractions;

/// <summary>Marks an entity as soft-deletable — who deleted it, and when — separate from <see cref="IAuditable"/>'s create/update tracking.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeleteDate { get; set; }
    Guid? DeleteUserId { get; set; }
    AppUser? DeleteUser { get; set; }
}
