namespace WebMvc.RateLimiting;

/// <summary>
/// Body returned with an HTTP 429, so a caller (or a developer looking at the
/// network tab) sees why the request was refused instead of an empty response.
/// </summary>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="RetryAfterSeconds">How long to wait before retrying; mirrors the <c>Retry-After</c> header.</param>
public sealed record RateLimitRejectionResponse(string Message, int RetryAfterSeconds);
