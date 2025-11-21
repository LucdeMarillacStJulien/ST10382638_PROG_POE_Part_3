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
using ST10382638_PROG_POE.Service;
using System.Threading.Tasks;

namespace ST10382638_PROG_POE.Controllers
{
    // Coordinator-facing controller that shows all Pending claims,
    // including related Lecturer and SupportingDocs for review.
    [Authorize(Roles = "Coordinator")]
    public class CoordinatorController : Controller
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------
        private readonly AppDbContext _context;                 // EF Core DbContext for users/claims
        private readonly ClaimEvaluationService _evaluationService; // Service for automated rule evaluation

        // -------------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------------
        // Injects the DbContext and the evaluation service used on Pending claims.
        public CoordinatorController(AppDbContext context, ClaimEvaluationService evaluationResult)
        {
            _context = context;
            _evaluationService = evaluationResult;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        // Coordinator dashboard: list all Pending claims with evaluation results
        // Loads the logged-in coordinator, queries Pending claims and populates
        // ViewBag with counts, claim list and evaluation results for the view.
        public async Task<IActionResult> Index()
        {
            var email = User?.Identity?.Name;

            // Load coordinator user record for displaying name and email in the UI
            var me = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (me == null)
            {
                // If the user record is missing, redirect back to login
                return RedirectToAction("Login", "Account");
            }

            // Query all claims with "Pending" status and include related lecturer/user/docs
            var pending = await _context.Claim
                .Include(c => c.LecturerProfile)
                    .ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs)
                .Where(c => c.Status != null && c.Status.Trim().ToLower() == "pending")
                .OrderBy(c => c.SubmittedOn)
                .ToListAsync();

            // Run automated evaluation rules for each pending claim
            var evaluations = pending
                .Select(c => _evaluationService.Evaluate(c))
                .ToDictionary(r => r.ClaimId, r => r);

            // Populate ViewBag with coordinator identity and dashboard metrics
            ViewBag.CoordinatorName = $"{me.FirstName} {me.Surname}";
            ViewBag.PendingCount = pending.Count;
            ViewBag.PendingClaims = pending;
            ViewBag.CoordinatorEmail = me.Email;
            ViewBag.Evaluations = evaluations;

            // Render dashboard view
            return View();
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
