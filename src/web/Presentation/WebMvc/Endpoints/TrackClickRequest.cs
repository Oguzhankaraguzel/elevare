namespace WebMvc.Endpoints;

/// <param name="Consent">The visitor allowed analytics cookies — see TrackingEndpoints.</param>
public sealed record TrackClickRequest(string Path, string ElementLabel, bool Consent = false);
