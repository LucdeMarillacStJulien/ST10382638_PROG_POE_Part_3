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
using System.Threading.Tasks;

namespace ST10382638_PROG_POE.Controllers
{
    /// <summary>
    /// Program Manager–facing controller that lists all claims in the <c>Verified</c> state
    /// for approval/rejection. Eager-loads lecturer identity and supporting documents to
    /// avoid N+1 queries and provide full context to the view.
    /// </summary>
    public class ManagerController : Controller
    {
        // ---------- Dependencies ----------
        private readonly AppDbContext _context; // EF Core DbContext for users/claims

        /// <summary>
        /// Initializes the controller with the application's DbContext.
        /// </summary>
        /// <param name="context">EF Core database context.</param>
        public ManagerController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Displays the Program Manager dashboard with all <c>Verified</c> claims.
        /// Provides manager identity (name/email), count of verified items, and the claim list.
        /// </summary>
        /// <param name="email">Program Manager email used to identify the current user.</param>
        /// <returns>The dashboard view populated via <see cref="ViewBag"/> fields.</returns>
        public async Task<IActionResult> Index(string email)
        {
            // Validate input early; manager identity is required for personalized context.
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Program manager email is required");

            // Load manager user record for header display and navigation context.
            var me = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (me == null)
                return NotFound("Program manager not found");

            // Fetch all Verified claims with related lecturer and their user identity,
            // plus supporting docs for quick inspection. Newest first to prioritize recent work.
            var verified = await _context.Claim
                .Include(c => c.LecturerProfile)
                    .ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs) // ensure docs are available in the view
                .Where(c => c.Status != null && c.Status.Trim().ToLower() == "verified")
                .OrderBy(c => c.SubmittedOn)
                .ToListAsync();

            // Fill ViewBag with model data the view expects (header, counts, list).
            ViewBag.ProgramManagerName = $"{me.FirstName} {me.Surname}";
            ViewBag.ProgramManagerEmail = me.Email;
            ViewBag.VerifiedCount = verified.Count;
            ViewBag.VerifiedClaims = verified;

            // Render the dashboard view (model supplied via ViewBag).
            return View();
        }
    }
}
