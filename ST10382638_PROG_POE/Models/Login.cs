// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/691e5672-00c8-8328-b4da-a2c0b7ea1c63
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using System.ComponentModel.DataAnnotations;

namespace ST10382638_PROG_POE.Models
{
    /// <summary>
    /// ViewModel used for user authentication during login.
    /// Captures the email, password and optional RememberMe preference.
    /// </summary>
    public class Login
    {
        // Login email (required format must be a valid email address)
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        // Login password (masked input during authentication)
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        // Option allowing the session to persist across browser restarts
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; } = false;
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
