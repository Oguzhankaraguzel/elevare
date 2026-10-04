using System.ComponentModel.DataAnnotations;
using Domain.Entities.Abstractions;

namespace Domain.Entities.FormReplyTemplates;

/// <summary>
/// A canned answer to a form submission — "teklif gönderildi", "randevunuz alındı",
/// "bu konu bizim alanımız değil".
/// <para>
/// The point is consistency and speed: the same twenty questions arrive every week
/// and re-typing the answer means each visitor gets a slightly different one.
/// <see cref="Body"/> may contain <c>{{alan}}</c> placeholders that are filled from
/// the submission's own fields when the reply is composed, so a template can still
/// open with the visitor's name.
/// </para>
/// </summary>
public class FormReplyTemplate : BaseEntity
{
    /// <summary>What this template is for — the only thing shown when picking one.</summary>
    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(300)]
    public required string Subject { get; set; }

    public required string Body { get; set; }

    /// <summary>Order in the picker; BaseEntity's IsActive retires one without deleting it.</summary>
    public int SortOrder { get; set; }
}
