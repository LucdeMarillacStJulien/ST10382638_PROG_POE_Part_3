using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using ST10382638_PROG_POE.Models;

namespace ST10382638_PROG_POE.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ILogger<AccountController> logger, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(Login login)
        {
            if (!ModelState.IsValid)
            {
                return View(login);
            }

            var user = await _userManager.FindByEmailAsync(login.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(login);
            }

            var result = await _signInManager.PasswordSignInAsync(user, login.Password, login.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // Otherwise redirect by role to the correct dashboard.
                var roles = await _userManager.GetRolesAsync(user);
                var email = user.Email; // used by your controllers' Index(email) signatures

                if (roles.Contains("Lecturer"))
                {
                    // Lecturer dashboard requires email parameter.
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
                    // Placeholder: HR dashboard not implemented as a separate controller.
                    return RedirectToAction("Index", "HR");
                }

                // Fallback if user has no mapped role.
                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out for {Email}.", login.Email);
                ModelState.AddModelError(string.Empty, "User account locked out.");
                return View(login);
            }

            // Generic failure message.
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(login);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
