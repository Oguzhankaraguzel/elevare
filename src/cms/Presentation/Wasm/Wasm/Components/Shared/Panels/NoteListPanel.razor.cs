using Domain.Entities.UserNotes;

namespace Wasm.Components.Shared.Panels;

public partial class NoteListPanel
{
    private sealed class NoteFormModel
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public NoteType Type { get; set; } = NoteType.Personal;
        public NoteVisibility Visibility { get; set; } = NoteVisibility.Private;
        public string Color { get; set; } = "";
        public bool IsPinned { get; set; }
        public bool IsArchived { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
