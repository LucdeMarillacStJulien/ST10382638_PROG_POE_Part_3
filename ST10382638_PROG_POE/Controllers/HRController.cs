using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;

namespace ST10382638_PROG_POE.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public HRController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

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


        // GET: HR/Create
        public IActionResult Create()
        {
            return View();
        }

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

            var result = await _userManager.CreateAsync(user, input.PasswordHash);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);
                return View(input);
            }

            await _userManager.AddToRoleAsync(user, role);

            // If lecturer selected, also create lecturer profile
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

            if (role == "Lecturer")
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);
                ViewBag.LecturerProfile = profile;
            }

            return View(user);
        }

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

            // ---------- lecturer hourly rate only ----------
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



        // GET: HR/Details
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            // Load user
            var user = await _userManager.Users
                .Include(u => u.LecturerProfile)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
                return NotFound();

            // Single role string (for display)
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? string.Empty;
            ViewBag.Role = role;

            // Lecturer-specific data + claim summary
            LecturerProfile? lecturerProfile = null;
            object? claimSummary = null;
            bool hasClaims = false;

            if (role == "Lecturer")
            {
                lecturerProfile = await _context.LecturerProfile
                    .Include(p => p.Claim)
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (lecturerProfile != null && lecturerProfile.Claim != null && lecturerProfile.Claim.Any())
                {
                    var claims = lecturerProfile.Claim;

                    // LINQ summary for the view / report
                    var totalClaims = claims.Count;
                    var totalHours = claims.Sum(c => c.HoursWorked);
                    var totalAmount = claims.Sum(c => c.CalculatedAmount);

                    // Group by status so the view can show a breakdown if desired
                    var statusBreakdown = claims
                        .GroupBy(c => c.Status)
                        .Select(g => new
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                        .ToList();

                    claimSummary = new
                    {
                        TotalClaims = totalClaims,
                        TotalHours = totalHours,
                        TotalAmount = totalAmount,
                        StatusBreakdown = statusBreakdown
                    };

                    hasClaims = true;
                }
            }

            ViewBag.LecturerProfile = lecturerProfile;
            ViewBag.ClaimSummary = claimSummary;
            ViewBag.HasClaims = hasClaims;

            // Strongly-typed model is just ApplicationUser; extras are in ViewBag
            return View(user);
        }


        // GET: HR/Delete
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // POST: HR/Delete
        [HttpPost, ActionName("Delete")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            // delete lecturer profile if the user is lecturer
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
