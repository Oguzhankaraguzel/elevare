namespace WebMvc.Endpoints;

public sealed record FormSubmitRequest(int PageId, string? FormName, Dictionary<string, string>? Fields, string? CaptchaToken);
