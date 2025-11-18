// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using System.Threading.Tasks;

namespace ST10382638_PROG_POE.Controllers
{
    /// <summary>
    /// Coordinator-facing controller that renders a view of all <c>Pending</c> claims.
    /// Loads related Lecturer and SupportingDocs info for display and review.
    /// </summary>

    [Authorize(Roles = "Coordinator")]
    public class CoordinatorController : Controller
    {
        // ---------- Dependencies ----------
        private readonly AppDbContext _context; // EF Core DbContext for querying users/claims

        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        /// <param name="context">EF Core database context.</param>
        public CoordinatorController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Displays the Coordinator dashboard listing all claims with a status of <c>Pending</c>.
        /// Provides basic header metrics and context for the logged-in coordinator.
        /// </summary>
        /// <param name="email">Coordinator email used to identify the current user.</param>
        /// <returns>The dashboard view with pending claims and coordinator details in <see cref="ViewBag"/>.</returns>
        public async Task<IActionResult> Index()
        {
            var email = User?.Identity?.Name;

            // Load the coordinator user record to provide name/email in the UI.
            var me = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (me == null)
                return NotFound("Coordinator not found");

            // Local helper to compare status safely (kept for readability;
            // the actual query below uses a direct lowercase comparison).
            static bool IsPending(string? s) =>
                (s ?? "").Trim().Equals("Pending", StringComparison.OrdinalIgnoreCase);

            // Query all Pending claims, including lecturer (and their user) and supporting docs.
            // Includes are eager-loaded to avoid N+1 roundtrips and to supply the view with full context.
            var pending = await _context.Claim
                .Include(c => c.LecturerProfile)
                    .ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs) // ensure documents are present for quick inspection
                .Where(c => c.Status != null && c.Status.Trim().ToLower() == "pending")
                .OrderBy(c => c.SubmittedOn) // show newest first for faster triage
                .ToListAsync();

            // Populate ViewBag with data used by the dashboard view (tiles, lists, headers).
            ViewBag.CoordinatorName = $"{me.FirstName} {me.Surname}";
            ViewBag.PendingCount = pending.Count;
            ViewBag.PendingClaims = pending;
            ViewBag.CoordinatorEmail = me.Email;

            // Render the default view; the model is provided via ViewBag collections/fields.
            return View();
        }
    }
}
