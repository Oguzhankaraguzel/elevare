using Domain.Entities.Users;

namespace Domain.Entities.Abstractions;

/// <summary>Who created/last-updated a row, and when — split out from <see cref="ISoftDeletable"/> so each concern can be reasoned about independently.</summary>
public interface IAuditable
{
    DateTime CreateDate { get; }
    Guid CreateUserId { get; }
    AppUser CreateUser { get; }

    DateTime? UpdateDate { get; set; }
    Guid? UpdateUserId { get; set; }
    AppUser? UpdateUser { get; set; }
}
