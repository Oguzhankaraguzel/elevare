namespace Wasm.Models;

/// <summary>
/// One copied (unlinked) template on the canvas whose source has changed since —
/// as reported by checkStaleSnapshotTemplates in grapes-editor.js.
/// </summary>
/// <param name="Id">The template's id.</param>
/// <param name="Name">The template's name.</param>
/// <param name="CopiedAt">When the copy was taken, as stamped on it (ISO 8601).</param>
/// <param name="ChangedAt">When the template's own content last changed (ISO 8601);
/// also the version a dismissal is remembered for.</param>
public sealed record StaleTemplateInfo(int Id, string Name, string CopiedAt, string ChangedAt);
