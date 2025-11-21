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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;

namespace ST10382638_PROG_POE.Controllers
{
    /// <summary>
    /// Lecturer-facing controller that loads a lecturer profile (by email) and
    /// prepares dashboard metrics: pending counts, this-month approvals/hours,
    /// a shortlist of oldest pending items, and a 3-month hours series for charts.
    /// </summary>
    [Authorize(Roles = "Lecturer")]
    public class LecturerController : Controller
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------
        private readonly AppDbContext _context; // EF Core DbContext for profiles/claims

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        public LecturerController(AppDbContext context)
        {
            _context = context;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // Lecturer Dashboard: loads lecturer profile and builds ViewBag KPI metrics
        public async Task<IActionResult> Index()
        {
            var currentEmail = User?.Identity?.Name;

            var profile = await _context.LecturerProfile
                .Include(lp => lp.User)
                .Include(lp => lp.Claim)
                .FirstOrDefaultAsync(lp => lp.User.Email == currentEmail);

            if (profile == null) return NotFound();

            var claims = profile.Claim ?? new List<Claim>();

            ViewBag.PendingCount = claims.Count(c => c.Status == "Pending");

            ViewBag.ApprovedThisMonth = claims.Count(c =>
                c.Status == "Approved" &&
                c.SubmittedOn.Month == DateTime.Now.Month &&
                c.SubmittedOn.Year == DateTime.Now.Year);

            ViewBag.HoursThisMonth = claims
                .Where(c => c.Status == "Approved" &&
                            c.SubmittedOn.Month == DateTime.Now.Month &&
                            c.SubmittedOn.Year == DateTime.Now.Year)
                .Sum(c => c.HoursWorked);

            ViewBag.OldestPending3 = claims
                .Where(c => c.Status == "Pending")
                .OrderBy(c => c.SubmittedOn)
                .Take(3)
                .ToList();

            var start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var months = new[] { start.AddMonths(-2), start.AddMonths(-1), start };

            ViewBag.ChartLabels = months.Select(m => m.ToString("MMM")).ToList();

            ViewBag.ChartHours = months.Select(m =>
                claims.Where(c => c.Status == "Approved" &&
                                  c.SubmittedOn >= m && c.SubmittedOn < m.AddMonths(1))
                      .Sum(c => c.HoursWorked)
            ).ToList();

            return View(profile);
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
