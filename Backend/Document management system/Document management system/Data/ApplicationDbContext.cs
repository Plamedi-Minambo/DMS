using DocumentManagement.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DocumentManagement.API.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DocumentManagement.API.Models.Document> Documents { get; set; }

        public DbSet<Approval> Approvals { get; set; }

        public DbSet<InvoiceData> InvoiceData { get; set; }
    }
}