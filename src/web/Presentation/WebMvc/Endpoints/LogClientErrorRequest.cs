namespace WebMvc.Endpoints;

public sealed record LogClientErrorRequest(string Message, string? Stack, string? Path);
