using SharedKernel.Social;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>Covers <see cref="SocialProfiles"/>: account handles read from Site Settings' profile links.</summary>
public sealed class SocialProfilesTests
{
    [Fact]
    public void X_handles_come_only_from_x_or_twitter_profiles()
    {
        SocialProfiles.XHandle("https://twitter.com/abc/").ShouldBe("@abc");
        SocialProfiles.XHandle("https://www.linkedin.com/in/abc").ShouldBeNull();
        SocialProfiles.XHandle("x.com/abc").ShouldBeNull();
    }
}
