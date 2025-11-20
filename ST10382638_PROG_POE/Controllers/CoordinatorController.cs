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
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;
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
        private readonly ClaimEvaluationService _evaluationService;

        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        /// <param name="context">EF Core database context.</param>
        public CoordinatorController(AppDbContext context, ClaimEvaluationService evaluationResult)
        {
            _context = context;
            _evaluationService = evaluationResult;
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
            {
                // If the user record cannot be found, redirect to login or an error page.
                return RedirectToAction("Login", "Account");
            }

            // Query for all claims that are currently "Pending" and include the related
            // lecturer profile + user record so the view has everything it needs to render.
            var pending = await _context.Claim
                .Include(c => c.LecturerProfile)
                    .ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs) // ensure documents are present for quick inspection
                .Where(c => c.Status != null && c.Status.Trim().ToLower() == "pending")
                .OrderBy(c => c.SubmittedOn) // show newest first for faster triage
                .ToListAsync();

            // Run automated evaluation for each pending claim so the Coordinator
            // can see rule results per item on the dashboard.
            var evaluations = pending
                .Select(c => _evaluationService.Evaluate(c))
                .ToDictionary(r => r.ClaimId, r => r);

            // Populate ViewBag with data used by the dashboard view (tiles, lists, headers).
            ViewBag.CoordinatorName = $"{me.FirstName} {me.Surname}";
            ViewBag.PendingCount = pending.Count;
            ViewBag.PendingClaims = pending;
            ViewBag.CoordinatorEmail = me.Email;
            ViewBag.Evaluations = evaluations;

            // Render the default view; the model is provided via ViewBag collections/fields.
            return View();
        }
    }
}
