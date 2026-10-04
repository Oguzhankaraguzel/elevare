using System.ComponentModel.DataAnnotations;

namespace Wasm.Components.Pages.Users;

public partial class UserCreateEdit
{
    private sealed class UserFormModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string UserName { get; set; } = "";

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Bio { get; set; }
        public string Role { get; set; } = "Author";
        public bool IsActive { get; set; } = true;
    }
}
