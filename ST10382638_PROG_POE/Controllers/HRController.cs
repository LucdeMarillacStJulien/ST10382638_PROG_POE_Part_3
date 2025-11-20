// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/691e5672-00c8-8328-b4da-a2c0b7ea1c63
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;

namespace ST10382638_PROG_POE.Controllers
{
    // Restricts access to HR role only
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly LecturerInvoiceReport _invoiceReport;

        // -------------------------------------------------------------------------
        // Constructor: inject Identity managers, DbContext and invoice report service
        // -------------------------------------------------------------------------
        public HRController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context, LecturerInvoiceReport invoiceReport)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _invoiceReport = invoiceReport;
        }

        // -------------------------------------------------------------------------
        // HR dashboard: list all non-HR users
        // -------------------------------------------------------------------------
        // GET: HR/Index
        public async Task<IActionResult> Index()
        {
            // Get all identity users
            var allUsers = await _userManager.Users.ToListAsync();

            // Keep only non-HR users
            var users = new List<ApplicationUser>();
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("HR"))
                    users.Add(user);
            }

            // Pass non-null list to view
            return View(users);
        }

        // -------------------------------------------------------------------------
        // Display create user form (HR can create any role)
        // -------------------------------------------------------------------------
        // GET: HR/Create
        public IActionResult Create()
        {
            return View();
        }

        // -------------------------------------------------------------------------
        // Create a new ApplicationUser and, if Lecturer, create a LecturerProfile
        // -------------------------------------------------------------------------
        // POST: HR/Create
        [HttpPost]
        public async Task<IActionResult> Create(ApplicationUser input, string role, decimal? HourlyRate)
        {
            if (!ModelState.IsValid)
                return View(input);

            var user = new ApplicationUser
            {
                UserName = input.Email,
                Email = input.Email,
                FirstName = input.FirstName,
                Surname = input.Surname
            };

            // Create Identity user with provided password
            var result = await _userManager.CreateAsync(user, input.PasswordHash);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return View(input);
            }

            // Assign selected role
            await _userManager.AddToRoleAsync(user, role);

            // If lecturer selected, also create lecturer profile with hourly rate
            if (role == "Lecturer" && HourlyRate.HasValue)
            {
                var profile = new LecturerProfile
                {
                    UserId = user.Id,
                    HourlyRate = (double)HourlyRate.Value,
                    IsAvailable = true
                };
                _context.LecturerProfile.Add(profile);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // -------------------------------------------------------------------------
        // Display edit form for a specific user (HR can edit any user)
        // -------------------------------------------------------------------------
        // GET: HR/Edit
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Current role for conditional lecturer fields
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();
            ViewBag.Role = role;

            // If lecturer, load lecturer profile to populate extra fields
            if (role == "Lecturer")
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);
                ViewBag.LecturerProfile = profile;
            }

            return View(user);
        }

        // -------------------------------------------------------------------------
        // Update user details, password (optional), and lecturer hourly rate
        // -------------------------------------------------------------------------
        // POST: HR/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            ApplicationUser input,
            string? NewPassword,
            string? ConfirmPassword,
            decimal? HourlyRate)
        {
            if (string.IsNullOrWhiteSpace(input?.Id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(input.Id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await PopulateEditViewBags(user);
                return View(input);
            }

            // ---------- base user info ----------
            user.FirstName = input.FirstName;
            user.Surname = input.Surname;
            user.Email = input.Email;
            user.UserName = input.Email;

            // ---------- password change (any role) ----------
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                // Confirm new password matches confirmation
                if (NewPassword != ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Password and confirmation do not match.");
                    await PopulateEditViewBags(user);
                    return View(input);
                }

                var hasPassword = await _userManager.HasPasswordAsync(user);

                if (hasPassword)
                {
                    // Remove existing password before adding new one
                    var remove = await _userManager.RemovePasswordAsync(user);
                    if (!remove.Succeeded)
                    {
                        foreach (var err in remove.Errors)
                            ModelState.AddModelError(string.Empty, err.Description);

                        await PopulateEditViewBags(user);
                        return View(input);
                    }
                }

                // Add new password
                var add = await _userManager.AddPasswordAsync(user, NewPassword);
                if (!add.Succeeded)
                {
                    foreach (var err in add.Errors)
                        ModelState.AddModelError(string.Empty, err.Description);

                    await PopulateEditViewBags(user);
                    return View(input);
                }
            }

            // Persist user updates
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                await PopulateEditViewBags(user);
                return View(input);
            }

            // ---------- lecturer hourly rate only ----------
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == "Lecturer" && HourlyRate.HasValue)
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (profile == null)
                {
                    // Create lecturer profile if it does not exist
                    profile = new LecturerProfile
                    {
                        UserId = user.Id,
                        HourlyRate = (double)HourlyRate.Value
                    };
                    _context.LecturerProfile.Add(profile);
                }
                else
                {
                    // Update existing hourly rate
                    profile.HourlyRate = (double)HourlyRate.Value;
                    _context.LecturerProfile.Update(profile);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // -------------------------------------------------------------------------
        // Helper: populate ViewBag for Edit view (role + lecturer profile)
        // -------------------------------------------------------------------------
        private async Task PopulateEditViewBags(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();
            ViewBag.Role = role;

            if (role == "Lecturer")
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);
                ViewBag.LecturerProfile = profile;
            }
        }

        // -------------------------------------------------------------------------
        // Show full details for a user; if Lecturer, include claims summary
        // -------------------------------------------------------------------------
        // GET: HR/Details
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? string.Empty;
            ViewBag.Role = role;

            LecturerProfile? lecturerProfile = null;
            var claims = new List<Claim>();
            double totalHoursAll = 0;
            double totalAmountAll = 0;

            // If lecturer, load profile and calculate claim totals
            if (role.Equals("Lecturer", StringComparison.OrdinalIgnoreCase))
            {
                lecturerProfile = await _context.LecturerProfile
                    .Include(lp => lp.Claim)
                    .FirstOrDefaultAsync(lp => lp.UserId == user.Id);

                if (lecturerProfile?.Claim != null && lecturerProfile.Claim.Any())
                {
                    claims = lecturerProfile.Claim
                        .OrderByDescending(c => c.SubmittedOn)
                        .ToList();

                    totalHoursAll = claims.Sum(c => c.HoursWorked);
                    totalAmountAll = claims.Sum(c => c.CalculatedAmount);
                }
            }

            ViewBag.LecturerProfile = lecturerProfile;
            ViewBag.AllClaims = claims;
            ViewBag.TotalHoursAll = totalHoursAll;
            ViewBag.TotalAmountAll = totalAmountAll;

            // View model is the ApplicationUser itself (used by Details view)
            return View(user);
        }

        // -------------------------------------------------------------------------
        // Generate and download invoice for a single claim (CSV)
        // -------------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadClaimInvoice(int claimId)
        {
            var result = await _invoiceReport.BuildSingleClaimInvoiceAsync(claimId);
            if (result == null)
                return NotFound("Claim not found or no invoice data.");

            return File(result.Value.Content, "text/csv", result.Value.FileName);
        }

        // -------------------------------------------------------------------------
        // Generate and download invoice for all claims in a period for a lecturer
        // -------------------------------------------------------------------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadInvoiceByPeriod(string id, string period, DateTime referenceDate)
        {
            var result = await _invoiceReport.BuildPeriodInvoiceAsync(id, period, referenceDate);
            if (result == null)
                return NotFound("No claims found for the selected period.");

            return File(result.Value.Content, "text/csv", result.Value.FileName);
        }

        // -------------------------------------------------------------------------
        // Confirm delete view for a user
        // -------------------------------------------------------------------------
        // GET: HR/Delete
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // -------------------------------------------------------------------------
        // Permanently delete a user and, if Lecturer, remove LecturerProfile as well
        // -------------------------------------------------------------------------
        // POST: HR/Delete
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            // Delete lecturer profile if the user is a lecturer
            if (role == "Lecturer")
            {
                var profile = _context.LecturerProfile.FirstOrDefault(p => p.UserId == user.Id);
                if (profile != null)
                    _context.LecturerProfile.Remove(profile);
            }

            await _userManager.DeleteAsync(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
