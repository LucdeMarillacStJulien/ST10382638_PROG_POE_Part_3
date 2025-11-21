// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ST10382638_PROG_POE.Models
{
    /// <summary>
    /// Represents lecturer-specific profile information.
    /// Contains hourly rate, availability status, and a reference
    /// to the associated <see cref="User"/> account. 
    /// Serves as the anchor entity for lecturer claims.
    /// </summary>
    public class LecturerProfile
    {
        /// <summary>
        /// Primary key identifier for the LecturerProfile.
        /// </summary>
        [Key]
        public int LecturerProfileId { get; set; }

        /// <summary>
        /// Lecturer's hourly rate at the time of claim submission.
        /// Used as the basis for calculating claim amounts.
        /// </summary>
        [Required]
        [Range(10, 750, ErrorMessage = "Hourly rate must be between 0 and 750.")]
        public double HourlyRate { get; set; }

        /// <summary>
        /// Availability flag indicating if the lecturer is currently active
        /// and eligible to submit claims.
        /// </summary>
        [Required]
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Foreign key linking the LecturerProfile to the <see cref="User"/> entity.
        /// </summary>
        [Required]
        public string UserId { get; set; }

        /// <summary>
        /// Navigation property to the associated <see cref="User"/>.
        /// Provides lecturer account details (e.g., email, name).
        /// </summary>
        public ApplicationUser User { get; set; }

        /// <summary>
        /// Navigation property for all claims submitted by this lecturer.
        /// Initializes to an empty list to avoid null reference exceptions.
        /// </summary>
        public List<Claim> Claim { get; set; } = new();
    }
}
