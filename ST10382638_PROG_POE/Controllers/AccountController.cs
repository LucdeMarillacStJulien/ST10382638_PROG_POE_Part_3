// Luc de Marillac St Julien
// ST10382638
// Group 1

// References:
// https://chatgpt.com/c/691e5672-00c8-8328-b4da-a2c0b7ea1c63
// https://www.w3schools.com/cs/index.php
// https://www.w3schools.com/css/default.asp

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using ST10382638_PROG_POE.Models;

namespace ST10382638_PROG_POE.Controllers
{
    // Controller responsible for handling the Identity-based login experience:
    //  - Shows the login page for unauthenticated users.
    //  - Validates credentials using ASP.NET Core Identity.
    //  - Redirects authenticated users to role-specific dashboards (Lecturer / Coordinator / Manager / HR).
    //  - Logs users out and shows an Access Denied page when authorization fails.
    [AllowAnonymous]
    public class AccountController : Controller
    {
        // Identity services used to authenticate and manage users
        private readonly SignInManager<ApplicationUser> _signInManager; // Handles sign-in/out logic
        private readonly UserManager<ApplicationUser> _userManager;     // Handles user lookups and role retrieval
        private readonly ILogger<AccountController> _logger;            // Used to log important authentication events

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Build an AccountController with all necessary Identity services.
        //          - ILogger<AccountController>   : for logging login success/failure and logout events.
        //          - SignInManager<ApplicationUser>: for handling password sign-in and sign-out flows.
        //          - UserManager<ApplicationUser> : for finding users by email and reading assigned roles.
        public AccountController(ILogger<AccountController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Render the login view so the user can enter email, password and remember-me.
        //          - No data loading or processing happens here.
        //          - The view is bound to the Login model which captures the user’s credentials.
        public IActionResult Login()
        {
            return View();
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Handle the posted login form and authenticate the user.
        //          Processing steps:
        //          1) Validate the incoming Login model using ModelState.
        //          2) Look up the ApplicationUser using the provided email address.
        //          3) Use SignInManager.PasswordSignInAsync to check the password and sign the user in.
        //          4) If login succeeds:
        //              - Read the user’s roles from Identity.
        //              - Redirect to the correct dashboard based on role:
        //                  * Lecturer   → Lecturer/Index
        //                  * Coordinator→ Coordinator/Index
        //                  * Manager    → Manager/Index
        //                  * HR         → HR/Index
        //              - If no known role matches, fall back to Home/Index.
        //          5) If the account is locked out, show a specific message and log a warning.
        //          6) For any other failure, show a generic “Invalid login attempt.” message.
        [HttpPost]
        public async Task<IActionResult> Login(Login login)
        {
            // Ensure form validation before authentication
            if (!ModelState.IsValid)
            {
                return View(login);
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(login.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(login);
            }

            // Attempt sign-in through Identity
            var result = await _signInManager.PasswordSignInAsync(user, login.Password, login.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // Retrieve Identity role to determine redirection
                var roles = await _userManager.GetRolesAsync(user);
                var email = user.Email; // Controllers use email for navigation

                if (roles.Contains("Lecturer"))
                {
                    return RedirectToAction("Index", "Lecturer");
                }

                if (roles.Contains("Coordinator"))
                {
                    return RedirectToAction("Index", "Coordinator");
                }

                if (roles.Contains("Manager"))
                {
                    return RedirectToAction("Index", "Manager");
                }

                if (roles.Contains("HR"))
                {
                    return RedirectToAction("Index", "HR");
                }

                // Default target when no matching role is found
                return RedirectToAction("Index", "Home");
            }

            // Handle locked-out accounts
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out for {Email}.", login.Email);
                ModelState.AddModelError(string.Empty, "User account locked out.");
                return View(login);
            }

            // Generic login failure
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(login);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Log the current user out of the system and send them back to the Login page.
        //          - Requires the user to be authenticated (Authorize attribute).
        //          - Calls SignInManager.SignOutAsync() to clear the authentication cookie.
        //          - Writes a log entry so logout events are traceable.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login", "Account");
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Display a simple Access Denied screen when a user is authenticated
        //          but tries to access a resource they do not have permission to view.
        //          - Typically used together with [Authorize(Roles = "...")] on other controllers.
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
