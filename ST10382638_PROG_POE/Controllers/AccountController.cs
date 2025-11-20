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
    // Allows unauthenticated users to access this controller (used for login)
    [AllowAnonymous]
    public class AccountController : Controller
    {
        // Declaring Identity services for authentication and user management
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;

        //------------------------------------------------------------------------------------------------------------------------//
        // Constructor injecting logger, sign-in manager and user manager
        public AccountController(ILogger<AccountController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // Display the Login page
        public IActionResult Login()
        {
            return View();
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // Authenticate user credentials and redirect to dashboard based on user role
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
        // Logout the authenticated user and return to Login screen
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
        // Display the Access Denied page when user lacks required permissions
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
