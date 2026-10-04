namespace WebMvc.Endpoints;

/// <param name="Consent">The visitor allowed analytics cookies — see TrackingEndpoints.</param>
public sealed record TrackViewRequest(string Path, string? Title, bool Consent = false);
