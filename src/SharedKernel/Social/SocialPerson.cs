namespace SharedKernel.Social;

/// <summary>A <c>video:actor</c>: the actor's profile URL and, optionally, the role played.</summary>
public sealed class SocialPerson
{
    public string Url { get; set; } = "";
    public string? Role { get; set; }
}
