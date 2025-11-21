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
        // PURPOSE: Set up the HR controller with all required services.
        //          - UserManager: manage ApplicationUser accounts (create, update, delete, roles, passwords).
        //          - RoleManager: manage Identity roles such as "Lecturer", "Manager", "HR", etc.
        //          - AppDbContext: access domain tables (e.g. LecturerProfile, Claim).
        //          - LecturerInvoiceReport: build CSV “invoice” files for lecturer claims.
        public HRController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context, LecturerInvoiceReport invoiceReport)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _invoiceReport = invoiceReport;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Display the HR dashboard with a list of users that HR can manage.
        //          - Loads all users from Identity.
        //          - Filters out users who are in the "HR" role so HR cannot manage themselves.
        //          - Sends the remaining users to the Index view for display and further actions.
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
        // PURPOSE: Show an empty form that allows HR to capture a brand new system user.
        //          - No data is loaded from the database.
        //          - The view will bind the input fields to an ApplicationUser model on POST.
        public IActionResult Create()
        {
            return View();
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Handle the submission of the create-user form.
        //          - Validates HourlyRate (if provided) for a lecturer (max 750).
        //          - Creates a new ApplicationUser with the supplied details and password.
        //          - Assigns the selected Identity role (e.g. Lecturer, Manager, Coordinator).
        //          - If the role is "Lecturer" and an HourlyRate is supplied:
        //              * Creates a linked LecturerProfile with the chosen hourly rate.
        //          - On success, redirects back to the HR dashboard (Index).
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
        // PURPOSE: Load an existing user into the edit form so HR can update their details.
        //          - Validates that the user ID exists.
        //          - Loads the user from Identity and finds their current role.
        //          - If the user is a Lecturer, also loads their LecturerProfile (e.g. HourlyRate).
        //          - Stores role and lecturer profile in ViewBag for the Edit view to use.
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
        // PURPOSE: Apply updates submitted from the edit-user form.
        //          - Validates basic user information and (optionally) the HourlyRate for lecturers.
        //          - Updates the user’s name and email.
        //          - If a new password is provided:
        //              * Confirms the password match.
        //              * Replaces the existing password using Identity (remove + add).
        //          - Persists changes to the ApplicationUser in Identity.
        //          - If the user is a Lecturer and HourlyRate is provided:
        //              * Creates or updates the LecturerProfile with the new rate.
        //          - On success, returns to the HR dashboard; otherwise reloads the form with errors.
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
        // PURPOSE: Helper method used by the Edit actions to repopulate ViewBag data.
        //          - Retrieves the user’s current role.
        //          - If the user is a Lecturer, loads the matching LecturerProfile.
        //          - Exposes role and lecturer profile via ViewBag so the Edit view can rebuild
        //            any UI elements that depend on them (e.g. hourly rate field).
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
        // PURPOSE: Show a complete overview of a selected user for HR.
        //          - Loads the ApplicationUser and determines their role.
        //          - If the user is a Lecturer:
        //              * Loads the LecturerProfile including all related claims.
        //              * Orders claims from newest to oldest.
        //              * Calculates total approved hours and total approved amount.
        //          - Stores lecturer profile, claims list and totals in ViewBag so the Details
        //            view can display a full financial and workload summary.
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
        // PURPOSE: Allow HR to download a CSV “invoice” for a single lecturer claim.
        //          - Uses the LecturerInvoiceReport service to build the CSV for the claimId.
        //          - If no data is found, returns 404 (NotFound) with an explanation.
        //          - On success, streams the CSV file back to the browser for download.
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
        // PURPOSE: Allow HR to download a CSV “invoice” summarising a lecturer’s claims
        //          for a specific period (e.g. day, week or month).
        //          - Uses LecturerInvoiceReport.BuildPeriodInvoiceAsync with:
        //              * id          : lecturer’s user ID.
        //              * period      : the chosen period type ("day", "week", "month").
        //              * referenceDate: the date used as the anchor for the calculation.
        //          - If no invoice data is returned, sets a TempData error message and
        //            redirects back to the Details page for that user.
        //          - On success, streams the CSV file back to the browser for download.
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
        // PURPOSE: Show a confirmation view before HR permanently removes a user.
        //          - Loads the user by ID.
        //          - If no user is found, returns 404 (NotFound).
        //          - If found, passes the user to the Delete view so HR can confirm.
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // PURPOSE: Perform the actual deletion of a user once HR has confirmed.
        //          - Loads the user and determines their role.
        //          - If the user is a Lecturer:
        //              * Finds and removes the linked LecturerProfile record as well.
        //          - Deletes the ApplicationUser from Identity.
        //          - Saves changes and redirects back to the HR dashboard (Index).
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
