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
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;
        private readonly LecturerInvoiceReport _invoiceReport;

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Constructor that injects Identity managers, database context and invoice report service.
        public HRController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context, LecturerInvoiceReport invoiceReport)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _invoiceReport = invoiceReport;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: HR dashboard — lists all system users except HR staff.
        public async Task<IActionResult> Index()
        {
            var allUsers = await _userManager.Users.ToListAsync();
            var users = new List<ApplicationUser>();

            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("HR"))
                    users.Add(user);
            }
            return View(users);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Load blank create-user form for HR to add a new system user.
        public IActionResult Create()
        {
            return View();
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Creates a new ApplicationUser and assigns selected role; if Lecturer, creates LecturerProfile.
        [HttpPost]
        public async Task<IActionResult> Create(ApplicationUser input, string role, decimal? HourlyRate)
        {
            if (HourlyRate.HasValue && HourlyRate > 750)
                ModelState.AddModelError("HourlyRate", "Hourly rate cannot exceed 750.");

            if (!ModelState.IsValid)
                return View(input);

            var user = new ApplicationUser
            {
                UserName = input.Email,
                Email = input.Email,
                FirstName = input.FirstName,
                Surname = input.Surname
            };

            var result = await _userManager.CreateAsync(user, input.PasswordHash);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return View(input);
            }

            await _userManager.AddToRoleAsync(user, role);

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

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Loads user details into edit form (plus lecturer profile if role = Lecturer).
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();
            ViewBag.Role = role;

            if (role == "Lecturer")
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);
                ViewBag.LecturerProfile = profile;
            }

            return View(user);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Updates user info, optional password change, and lecturer hourly rate when applicable.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApplicationUser input, string? NewPassword, string? ConfirmPassword, decimal? HourlyRate)
        {
            if (string.IsNullOrWhiteSpace(input?.Id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(input.Id);
            if (user == null) return NotFound();

            if (HourlyRate.HasValue && HourlyRate > 750)
                ModelState.AddModelError("HourlyRate", "Hourly rate cannot exceed 750.");

            if (!ModelState.IsValid)
            {
                await PopulateEditViewBags(user);
                return View(input);
            }

            user.FirstName = input.FirstName;
            user.Surname = input.Surname;
            user.Email = input.Email;
            user.UserName = input.Email;

            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                if (NewPassword != ConfirmPassword)
                {
                    ModelState.AddModelError("ConfirmPassword", "Password and confirmation do not match.");
                    await PopulateEditViewBags(user);
                    return View(input);
                }

                var hasPassword = await _userManager.HasPasswordAsync(user);
                if (hasPassword)
                {
                    var remove = await _userManager.RemovePasswordAsync(user);
                    if (!remove.Succeeded)
                    {
                        foreach (var err in remove.Errors)
                            ModelState.AddModelError(string.Empty, err.Description);
                        await PopulateEditViewBags(user);
                        return View(input);
                    }
                }

                var add = await _userManager.AddPasswordAsync(user, NewPassword);
                if (!add.Succeeded)
                {
                    foreach (var err in add.Errors)
                        ModelState.AddModelError(string.Empty, err.Description);
                    await PopulateEditViewBags(user);
                    return View(input);
                }
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                await PopulateEditViewBags(user);
                return View(input);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            if (role == "Lecturer" && HourlyRate.HasValue)
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (profile == null)
                {
                    profile = new LecturerProfile
                    {
                        UserId = user.Id,
                        HourlyRate = (double)HourlyRate.Value
                    };
                    _context.LecturerProfile.Add(profile);
                }
                else
                {
                    profile.HourlyRate = (double)HourlyRate.Value;
                    _context.LecturerProfile.Update(profile);
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Loads supporting ViewBag information for Edit view (role + lecturer profile).
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

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Shows full details of a selected user; if Lecturer, also includes claims summary.
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

                    var approvedClaims = claims
                        .Where(c => string.Equals(c.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    totalHoursAll = approvedClaims.Sum(c => c.HoursWorked);
                    totalAmountAll = approvedClaims.Sum(c => c.CalculatedAmount);
                }
            }

            ViewBag.LecturerProfile = lecturerProfile;
            ViewBag.AllClaims = claims;
            ViewBag.TotalHoursAll = totalHoursAll;
            ViewBag.TotalAmountAll = totalAmountAll;

            return View(user);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Generates and returns a CSV invoice for a single lecturer claim.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadClaimInvoice(int claimId)
        {
            var result = await _invoiceReport.BuildSingleClaimInvoiceAsync(claimId);
            if (result == null)
                return NotFound("Claim not found or no invoice data.");

            return File(result.Value.Content, "text/csv", result.Value.FileName);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Generates and returns a CSV invoice for all claims in a given period (day/week/month).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DownloadInvoiceByPeriod(string id, string period, DateTime referenceDate)
        {
            var result = await _invoiceReport.BuildPeriodInvoiceAsync(id, period, referenceDate);
            if (result == null)
            {
                TempData["PeriodInvoiceError"] = "No invoices found for the selected period.";
                return RedirectToAction(nameof(Details), new { id });
            }
            return File(result.Value.Content, "text/csv", result.Value.FileName);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Shows confirmation page before deleting a user.
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Permanently deletes a user; if role = Lecturer, also removes LecturerProfile.
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

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
