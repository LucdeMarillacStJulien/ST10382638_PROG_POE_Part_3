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
    /// <summary>
    /// Program Manager–facing controller that lists all claims in the <c>Verified</c> state
    /// for approval/rejection. Eager-loads lecturer identity and supporting documents to
    /// avoid N+1 queries and provide full context to the view.
    /// </summary>
    [Authorize(Roles = "Manager")]
    public class ManagerController : Controller
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------
        private readonly AppDbContext _context;
        private readonly ClaimEvaluationService _evaluationService;

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        public ManagerController(AppDbContext context, ClaimEvaluationService evaluationService)
        {
            _context = context;
            _evaluationService = evaluationService;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Displays the Program Manager dashboard with all <c>Verified</c> claims.
        /// Provides manager identity (name/email), count of verified items, and the claim list.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Get the currently logged in manager email from Identity
            var email = User?.Identity?.Name;

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Program manager email is required");

            var me = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (me == null)
                return NotFound("Program manager not found");

            var verified = await _context.Claim
                .Include(c => c.LecturerProfile)
                    .ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs)
                .Where(c => c.Status != null && c.Status.Trim().ToLower() == "verified")
                .OrderBy(c => c.SubmittedOn)
                .ToListAsync();

            var evaluations = verified
                .Select(c => _evaluationService.Evaluate(c))
                .ToDictionary(r => r.ClaimId, r => r);

            ViewBag.ProgramManagerName = $"{me.FirstName} {me.Surname}";
            ViewBag.ProgramManagerEmail = me.Email;
            ViewBag.VerifiedCount = verified.Count;
            ViewBag.VerifiedClaims = verified;
            ViewBag.Evaluations = evaluations;

            return View();
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
