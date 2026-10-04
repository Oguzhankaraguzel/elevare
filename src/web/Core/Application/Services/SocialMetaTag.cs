namespace Application.Services;

/// <summary>One <c>&lt;meta&gt;</c> for the head: <c>property="og:…"</c> or <c>name="twitter:…"</c>.</summary>
public sealed record SocialMetaTag(string Attribute, string Key, string Content);
