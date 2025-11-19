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
        // GET: HR/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // current role
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();
            ViewBag.Role = role;

            // if lecturer, load profile so we can show hourly rate etc.
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
        public async Task<IActionResult> Edit(ApplicationUser input, decimal? HourlyRate, bool? IsAvailable)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(input.Id);
            if (user == null) return NotFound();

            // base fields for any role
            user.FirstName = input.FirstName;
            user.Surname = input.Surname;
            user.Email = input.Email;
            user.UserName = input.Email;

            await _userManager.UpdateAsync(user);

            // get current role
            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            // lecturer-only extra fields
            if (role == "Lecturer")
            {
                var profile = await _context.LecturerProfile
                    .FirstOrDefaultAsync(p => p.UserId == user.Id);

                if (profile != null)
                {
                    if (HourlyRate.HasValue)
                        profile.HourlyRate = (double)HourlyRate.Value;

                    if (IsAvailable.HasValue)
                        profile.IsAvailable = IsAvailable.Value;

                    _context.LecturerProfile.Update(profile);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }



        // GET: HR/Details
        public async Task<IActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            return View((user, role));
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
