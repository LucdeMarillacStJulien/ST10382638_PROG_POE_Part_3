// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Models;

namespace ST10382638_PROG_POE.Data
{
    /// <summary>
    /// Central EF Core <see cref="DbContext"/> for the application.
    /// Manages entity sets for Users, LecturerProfiles, Claims, and SupportingDocs,
    /// enabling LINQ queries, change tracking, and database persistence.
    /// </summary>
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        /// <summary>
        /// Initializes the context with specific options (e.g., connection string, provider).
        /// </summary>
        /// <param name="options">The EF Core DbContext configuration options.</param>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ---------- Entity Sets (map to database tables) ----------

        /// <summary>
        /// Gets or sets the set of LecturerProfile entities.
        /// Links users with lecturer-specific metadata and claims.
        /// </summary>
        public DbSet<LecturerProfile> LecturerProfile { get; set; }

        /// <summary>
        /// Gets or sets the set of Claim entities.
        /// Represents lecturer claims submitted for approval and payment.
        /// </summary>
        public DbSet<Claim> Claim { get; set; }

        /// <summary>
        /// Gets or sets the set of SupportingDoc entities.
        /// Stores metadata for claim-related uploaded documents (encrypted).
        /// </summary>
        public DbSet<SupportingDoc> SupportingDoc { get; set; }
    }
}
