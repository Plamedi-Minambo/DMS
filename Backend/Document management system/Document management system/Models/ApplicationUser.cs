using Microsoft.AspNetCore.Identity;

namespace DocumentManagement.API.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
    }
}