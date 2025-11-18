// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ST10382638_PROG_POE.Models
{
    /// <summary>
    /// Represents a system user. Can be a lecturer, coordinator, or manager.
    /// Stores identity details, contact info, and role designation.
    /// Links to one or more LecturerProfiles when the user is a lecturer.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// User's first/given name.
        /// Required for identification.
        /// </summary>
        [Required]
        public string FirstName { get; set; }

        /// <summary>
        /// User's surname/last name.
        /// Required for identification.
        /// </summary>
        [Required]
        public string Surname { get; set; }

        /// <summary>
        /// Navigation property: a user may have one or more lecturer profiles
        /// (especially relevant if they teach across multiple contexts).
        /// Initializes to an empty list to avoid null references.
        /// </summary>
        public List<LecturerProfile> LecturerProfile { get; set; } = new();
    }
}
