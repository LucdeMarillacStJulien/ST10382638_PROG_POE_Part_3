// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

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
    public class LecturerController : Controller
    {
        // ---------- Dependencies ----------
        private readonly AppDbContext _context; // EF Core DbContext for profiles/claims

        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        /// <param name="context">EF Core database context.</param>
        public LecturerController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Displays the Lecturer dashboard for the given user email.
        /// Loads profile with user and claims, computes KPI tiles and chart data,
        /// and returns the view with the profile as the model and metrics in <see cref="ViewBag"/>.
        /// </summary>
        /// <param name="email">Lecturer's email used to locate the profile.</param>
        /// <returns>The dashboard view for the lecturer, or 404 if profile not found.</returns>
        public async Task<IActionResult> Index(string email)
        {
            // Preserve incoming email for clarity; could be used for breadcrumbs or links.
            var currentEmail = email;

            // Eager-load User and Claim collections to avoid N+1 queries in the view.
            var profile = await _context.LecturerProfile
                .Include(lp => lp.User)
                .Include(lp => lp.Claim)
                .FirstOrDefaultAsync(lp => lp.User.Email == currentEmail);

            // Fail fast if the email does not map to a known lecturer profile.
            if (profile == null) return NotFound();

            // Normalize to an in-memory list to simplify LINQ and null handling.
            var claims = profile.Claim ?? new List<Claim>();

            // --------------------------
            // KPI Tiles (simple counts)
            // --------------------------

            // Count of claims currently awaiting action.
            ViewBag.PendingCount = claims.Count(c => c.Status == "Pending");

            // Count of claims approved in the current calendar month.
            ViewBag.ApprovedThisMonth = claims.Count(c =>
                c.Status == "Approved" &&
                c.SubmittedOn.Month == DateTime.Now.Month &&
                c.SubmittedOn.Year == DateTime.Now.Year);

            // Sum of approved hours in the current month for effort tracking.
            ViewBag.HoursThisMonth = claims
                .Where(c => c.Status == "Approved" &&
                            c.SubmittedOn.Month == DateTime.Now.Month &&
                            c.SubmittedOn.Year == DateTime.Now.Year)
                .Sum(c => c.HoursWorked);

            // ------------------------------------
            // Oldest 3 Pending (attention list)
            // ------------------------------------
            // Surfaces the three longest-waiting pending items to help the lecturer prioritize.
            ViewBag.OldestPending3 = claims
                .Where(c => c.Status == "Pending")
                .OrderBy(c => c.SubmittedOn)
                .Take(3)
                .ToList();

            // -------------------------------------------------
            // Chart Series (last 3 months of approved hours)
            // -------------------------------------------------
            // Build labels (MMM) and a parallel series with total approved hours per month.
            var start = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var months = new[] { start.AddMonths(-2), start.AddMonths(-1), start };

            // Labels like ["Aug", "Sep", "Oct"].
            ViewBag.ChartLabels = months.Select(m => m.ToString("MMM")).ToList();

            // For each month bucket, sum approved HoursWorked where SubmittedOn ∈ [m, m+1).
            ViewBag.ChartHours = months.Select(m =>
                claims.Where(c => c.Status == "Approved" &&
                                  c.SubmittedOn >= m && c.SubmittedOn < m.AddMonths(1))
                      .Sum(c => c.HoursWorked)
            ).ToList();

            // Render dashboard with the profile model; view reads metrics from ViewBag.
            return View(profile);
        }

    }
}
