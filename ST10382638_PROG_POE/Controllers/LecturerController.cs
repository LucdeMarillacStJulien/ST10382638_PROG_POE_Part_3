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

        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        public LecturerController(AppDbContext context)
        {
            _context = context;
        }

        // -------------------------------------------------------------------------
        // Lecturer Dashboard: loads lecturer profile and builds ViewBag KPI metrics
        // -------------------------------------------------------------------------
        public async Task<IActionResult> Index()
        {
            // Logged-in lecturer email from Identity system
            var currentEmail = User?.Identity?.Name;

            // Load lecturer with identity user and claim collection
            var profile = await _context.LecturerProfile
                .Include(lp => lp.User)
                .Include(lp => lp.Claim)
                .FirstOrDefaultAsync(lp => lp.User.Email == currentEmail);

            // Return 404 if no matching lecturer profile exists
            if (profile == null) return NotFound();

            // Normalize to a list for safe LINQ operations
            var claims = profile.Claim ?? new List<Claim>();

            // ---------------------------------------------------------------------
            // KPI Tiles
            // ---------------------------------------------------------------------

            // Total pending claims
            ViewBag.PendingCount = claims.Count(c => c.Status == "Pending");

            // Number of approved claims this calendar month
            ViewBag.ApprovedThisMonth = claims.Count(c =>
                c.Status == "Approved" &&
                c.SubmittedOn.Month == DateTime.Now.Month &&
                c.SubmittedOn.Year == DateTime.Now.Year);

            // Total approved hours for current calendar month
            ViewBag.HoursThisMonth = claims
                .Where(c => c.Status == "Approved" &&
                            c.SubmittedOn.Month == DateTime.Now.Month &&
                            c.SubmittedOn.Year == DateTime.Now.Year)
                .Sum(c => c.HoursWorked);

            // ---------------------------------------------------------------------
            // Oldest 3 Pending items for attention
            // ---------------------------------------------------------------------
            ViewBag.OldestPending3 = claims
                .Where(c => c.Status == "Pending")
                .OrderBy(c => c.SubmittedOn)
                .Take(3)
                .ToList();

            // ---------------------------------------------------------------------
            // Charts: last 3 months of approved hours
            // ---------------------------------------------------------------------
            var start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var months = new[] { start.AddMonths(-2), start.AddMonths(-1), start };

            // Labels: (e.g., ["Aug", "Sep", "Oct"])
            ViewBag.ChartLabels = months.Select(m => m.ToString("MMM")).ToList();

            // Series: total approved hours per month in chronological order
            ViewBag.ChartHours = months.Select(m =>
                claims.Where(c => c.Status == "Approved" &&
                                  c.SubmittedOn >= m && c.SubmittedOn < m.AddMonths(1))
                      .Sum(c => c.HoursWorked)
            ).ToList();

            // Display dashboard with profile as model; metrics are in ViewBag
            return View(profile);
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
