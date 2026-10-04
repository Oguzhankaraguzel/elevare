using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Users.GetMyProfile;

/// <summary>
/// Deliberately carries no permission requirement: the subject is always the caller,
/// so there is nothing here that the caller is not already entitled to see.
/// </summary>
public sealed record GetMyProfileQuery : IQuery<MyProfileResponse>;
