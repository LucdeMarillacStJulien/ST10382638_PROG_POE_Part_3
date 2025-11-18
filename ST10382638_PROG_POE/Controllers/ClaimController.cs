// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References (for commenting/documentation style and C# reference):
//   1) Project-specific outline: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials: https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;

namespace ST10382638_PROG_POE.Controllers
{
    /// <summary>
    /// Handles Claim creation, validation, file upload (encrypted), and status changes
    /// across Lecturer → Coordinator → Manager workflows. Also provides a download
    /// endpoint that assembles and decrypts the claim's supporting documents into a ZIP.
    /// </summary>
    public class ClaimController : Controller
    {
        // ---------- Dependencies ----------
        private readonly AppDbContext _context;            // EF Core DbContext for persistence
        private readonly IConfiguration _config;           // App configuration (used by encryption service)
        private readonly ClaimDownload _downloadService;   // Service that builds decrypted ZIPs

        // ---------- Server-side constants/guards ----------
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".xlsx" }; // Allowed file types
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB soft limit per file
        private const double MinHoursPerClaim = 0.25;      // 15-minute minimum granularity
        private const double MaxHoursPerClaim = 10.0;      // Practical upper bound to catch input mistakes

        /// <summary>
        /// Initializes the controller with its dependencies.
        /// </summary>
        /// <param name="context">EF Core database context.</param>
        /// <param name="config">Application configuration (for crypto settings/keys, etc.).</param>
        /// <param name="downloadService">Service to construct a decrypted ZIP for a claim.</param>
        public ClaimController(AppDbContext context, IConfiguration config, ClaimDownload downloadService)
        {
            _context = context;
            _config = config;
            _downloadService = downloadService;
        }

        /// <summary>
        /// Displays the Create Claim form for a specific LecturerProfile.
        /// </summary>
        /// <param name="id">LecturerProfileId (required).</param>
        /// <returns>Claim creation view pre-populated with lecturer and email context.</returns>
        [HttpGet]
        public async Task<IActionResult> Create(int? id)
        {
            // Validate route parameter early to avoid null dereferences.
            if (id == null)
                return BadRequest("Lecturer profile id is required.");

            // Eager-load User for email; fail fast if profile not found.
            var lecturer = await _context.LecturerProfile
                .Include(lp => lp.User)
                .FirstOrDefaultAsync(lp => lp.LecturerProfileId == id);
            if (lecturer == null) return NotFound("Lecturer profile not found.");

            // Provide view context for UI display and downstream postback usage.
            ViewBag.Lecturer = lecturer;
            ViewBag.Email = lecturer.User.Email;
            Console.WriteLine(lecturer.User.Email);
            return View();
        }

        /// <summary>
        /// Handles Claim submission:
        /// - Validates hours and file attachments (type/size).
        /// - Calculates server-side amount, sets default status and timestamp.
        /// - Encrypts supporting docs directly to disk without storing plaintext.
        /// - Persists SupportingDoc rows referencing encrypted paths.
        /// </summary>
        /// <param name="claim">Claim payload from the form (server fills computed fields).</param>
        /// <param name="files">Uploaded supporting documents (optional).</param>
        /// <param name="lecturerEmail">Lecturer email for post-save redirect context.</param>
        /// <returns>On success redirects to Lecturer dashboard; otherwise re-renders form with errors.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim, List<IFormFile> files)
        {
            var currentEmail = User?.Identity?.Name;
            // ------------------------------------------------------------
            // 0) PRE-VALIDATION: Files (type + size) and HoursWorked
            //    - Fail fast to give immediate feedback and avoid partial writes.
            // ------------------------------------------------------------
            if (files != null && files.Count > 0)
            {
                foreach (var f in files.Where(x => x != null && x.Length > 0))
                {
                    var ext = Path.GetExtension((f.FileName ?? string.Empty).Trim());
                    if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                    {
                        // Surface friendly, per-file errors to the UI.
                        ModelState.AddModelError("files", $"{f.FileName} has an invalid file type. Allowed: .pdf, .docx, .xlsx");
                    }
                    if (f.Length > MaxFileSize)
                    {
                        ModelState.AddModelError("files", $"{f.FileName} exceeds the {(MaxFileSize / 1024 / 1024)} MB limit.");
                    }
                }
            }

            // Server-side validation for HoursWorked to guard business rules and data integrity.
            if (double.IsNaN(claim.HoursWorked) || double.IsInfinity(claim.HoursWorked))
            {
                ModelState.AddModelError(nameof(Claim.HoursWorked), "Hours Worked value is invalid.");
            }
            else
            {
                if (claim.HoursWorked <= 0)
                    ModelState.AddModelError(nameof(Claim.HoursWorked), "Hours Worked must be greater than 0.");
                if (claim.HoursWorked < MinHoursPerClaim)
                    ModelState.AddModelError(nameof(Claim.HoursWorked), $"Minimum allowed is {MinHoursPerClaim} hours.");
                if (claim.HoursWorked > MaxHoursPerClaim)
                    ModelState.AddModelError(nameof(Claim.HoursWorked), $"Hours Worked seems unrealistic (> {MaxHoursPerClaim}). Please check.");

                // Optional 15-min step enforcement (kept as a doc hint, not enforced by default):
                // if (Math.Abs((claim.HoursWorked * 4) - Math.Round(claim.HoursWorked * 4)) > 1e-9)
                //     ModelState.AddModelError(nameof(Claim.HoursWorked), "Use 0.25-hour increments (15 minutes).");
            }

            // ------------------------------------------------------------
            // 1) Remove server-set properties from validation
            //    (these are set by the server and should not block ModelState)
            // ------------------------------------------------------------
            ModelState.Remove(nameof(Claim.CalculatedAmount));
            ModelState.Remove(nameof(Claim.Status));
            ModelState.Remove(nameof(Claim.SubmittedOn));
            ModelState.Remove(nameof(Claim.SupportingDocs));
            ModelState.Remove(nameof(Claim.Notes));
            ModelState.Remove(nameof(Claim.LecturerProfile));

            // ------------------------------------------------------------
            // 2) Stop early if anything invalid so nothing gets saved
            //    - Rehydrate minimal view context so the user can correct entries.
            // ------------------------------------------------------------
            if (!ModelState.IsValid)
            {
                Console.WriteLine(string.Join(Environment.NewLine,
                    ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                var lecturerVm = await _context.LecturerProfile
                    .Include(lp => lp.User)
                    .FirstOrDefaultAsync(lp => lp.LecturerProfileId == claim.LecturerProfileId);

                ViewBag.Lecturer = lecturerVm;
                ViewBag.Email = !string.IsNullOrWhiteSpace(currentEmail);

                return View(claim); // nothing saved
            }

            // ------------------------------------------------------------
            // 3) Compute server-side fields & save Claim (to get ClaimId)
            //    - CalculatedAmount derived from HoursWorked * RateAtSubmission.
            //    - Status initialized to "Pending".
            //    - SubmittedOn recorded in SAST (UTC+2) to align with local time.
            // ------------------------------------------------------------
            claim.CalculatedAmount = claim.HoursWorked * claim.RateAtSubmission;
            claim.Status = "Pending";
            claim.SubmittedOn = DateTime.UtcNow.AddHours(2);
            Console.WriteLine(claim.SubmittedOn);
            _context.Claim.Add(claim);
            await _context.SaveChangesAsync(); // ensures ClaimId for folder path

            // ------------------------------------------------------------
            // 4) Save files (encrypted-only) into Claim's folder
            //    Folder: <solution>/App_Data/ClaimDocs/{ClaimId}/
            //    - Streams are encrypted directly; plaintext is never persisted to disk.
            //    - A SupportingDoc row is created per file with path to the .enc payload.
            // ------------------------------------------------------------
            if (files != null && files.Count > 0)
            {
                var solutionRoot = Directory.GetCurrentDirectory();
                var claimFolder = Path.Combine(solutionRoot, "App_Data", "ClaimDocs", claim.ClaimId.ToString());
                Directory.CreateDirectory(claimFolder);

                var uploadErrors = new List<string>();
                var savedCount = 0;

                foreach (var file in files.Where(f => f != null && f.Length > 0))
                {
                    try
                    {
                        // Double-guard validation to keep loop robust against mixed batches.
                        var ext = Path.GetExtension((file.FileName ?? string.Empty).Trim());
                        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
                        {
                            ModelState.AddModelError(string.Empty,
                                $"{file?.FileName ?? "(unnamed file)"} has an invalid file type. Allowed: .pdf, .docx, .xlsx");
                            continue;
                        }
                        if (file.Length > MaxFileSize)
                        {
                            ModelState.AddModelError(string.Empty,
                                $"{file.FileName} exceeds the {(MaxFileSize / (1024 * 1024))} MB limit.");
                            continue;
                        }

                        // Generate a unique encrypted filename while retaining the original name for display.
                        var originalName = Path.GetFileName(file.FileName);
                        var safeName = $"{Guid.NewGuid()}_{originalName}";
                        var encFileName = safeName + ".enc";
                        var encPath = Path.Combine(claimFolder, encFileName);

                        // Encrypt directly from the upload stream to the .enc file (no temp plaintext).
                        using (var input = file.OpenReadStream())
                        {
                            await Encryption.EncryptStreamAsync(input, encPath, _config);
                        }

                        // Persist a SupportingDoc record that points to the encrypted relative path.
                        var relativePath = Path.Combine("App_Data", "ClaimDocs", claim.ClaimId.ToString(), encFileName);

                        _context.SupportingDoc.Add(new SupportingDoc
                        {
                            ClaimId = claim.ClaimId,
                            FileName = originalName,
                            FileUrl = relativePath,        // points to .enc
                            FileType = file.ContentType
                        });

                        savedCount++;
                    }
                    catch (Exception ex)
                    {
                        // Collect per-file error messages to display as warnings after loop.
                        uploadErrors.Add($"{file?.FileName ?? "(unnamed file)"} failed to upload — {ex.Message}");
                    }
                }

                // If nothing saved, re-render with aggregated errors; do not redirect yet.
                if (savedCount == 0 && (uploadErrors.Count > 0 || !ModelState.IsValid))
                {
                    foreach (var err in uploadErrors) ModelState.AddModelError(string.Empty, err);

                    var lecturerVm2 = await _context.LecturerProfile
                        .Include(lp => lp.User)
                        .FirstOrDefaultAsync(lp => lp.LecturerProfileId == claim.LecturerProfileId);

                    ViewBag.Lecturer = lecturerVm2;
                    ViewBag.Email = !string.IsNullOrWhiteSpace(currentEmail);

                    return View(claim); // don't persist docs or redirect
                }

                // Commit any SupportingDoc rows created during the loop.
                await _context.SaveChangesAsync();

                // Surface non-fatal per-file issues as user warnings post-redirect.
                if (uploadErrors.Count > 0)
                    TempData["UploadWarnings"] = string.Join(Environment.NewLine, uploadErrors);
            }

            // ------------------------------------------------------------
            // 5) Go back to Lecturer dashboard
            //    - Use lecturerEmail context to land on the correct profile view.
            // ------------------------------------------------------------
            return RedirectToAction("Index", "Lecturer");
        }

        /// <summary>
        /// Shows the Lecturer's pending/verified/approved/rejected claims along with
        /// useful header metrics and a status-ordered table view.
        /// </summary>
        /// <param name="email">Lecturer's email (required to locate profile).</param>
        /// <returns>The pending claims view with summary metrics.</returns>
        [HttpGet]
        public async Task<IActionResult> Pending()
        {
            var email = User?.Identity?.Name;

            // Basic input validation: email is required to locate the correct profile.
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Lecturer email is required.");

            // Load profile with associated User and Claims for aggregation and display.
            var profile = await _context.LecturerProfile
                .Include(lp => lp.User)
                .Include(lp => lp.Claim) // collection of claims for this lecturer
                .FirstOrDefaultAsync(lp => lp.User.Email == email);

            if (profile == null)
                return NotFound("Lecturer profile not found.");

            // Local helper to normalize/compare string statuses safely.
            static bool IsStatus(string? s, string target) =>
                (s ?? "").Trim().Equals(target, StringComparison.OrdinalIgnoreCase);

            var claimsAll = (profile.Claim ?? new List<Claim>()).ToList();

            // ---- Header metrics (APPROVED ONLY) ----
            var approvedClaims = claimsAll.Where(c => IsStatus(c.Status, "Approved")).ToList();

            // Count of pending (for a tile chip)
            ViewBag.TotalPending = claimsAll.Count(c => IsStatus(c.Status, "Pending"));

            // Global totals across all claims (Hours and Amount)
            ViewBag.TotalHoursAll = claimsAll.Sum(c => c.HoursWorked);
            ViewBag.TotalAmountAll = claimsAll.Sum(c =>
            {
                var hasStored = c.CalculatedAmount != 0;
                var storedAmt = Convert.ToDouble(c.CalculatedAmount);
                var computed = c.HoursWorked * c.RateAtSubmission;
                return hasStored ? storedAmt : computed;
            });

            // Totals for APPROVED ONLY (top KPI cards)
            ViewBag.TotalHours = approvedClaims.Sum(c => c.HoursWorked);
            ViewBag.TotalAmount = approvedClaims.Sum(c =>
            {
                var hasStored = c.CalculatedAmount != 0;
                var storedAmt = Convert.ToDouble(c.CalculatedAmount);
                var computed = c.HoursWorked * c.RateAtSubmission;
                return hasStored ? storedAmt : computed;
            });

            // ---- Optional: per-status counts for UI filters/tabs ----
            ViewBag.PendingCountAll = claimsAll.Count(c => IsStatus(c.Status, "Pending"));
            ViewBag.VerifiedCountAll = claimsAll.Count(c => IsStatus(c.Status, "Verified"));
            ViewBag.ApprovedCountAll = claimsAll.Count(c => IsStatus(c.Status, "Approved"));
            ViewBag.RejectedCountAll = claimsAll.Count(c => IsStatus(c.Status, "Rejected"));

            // ---- Table rows: ALL claims, ordered by Status then date ----
            // Custom status sort order makes the list more actionable in the UI.
            int StatusOrder(string? s) =>
                IsStatus(s, "Pending") ? 0 :
                IsStatus(s, "Verified") ? 1 :
                IsStatus(s, "Approved") ? 2 :
                IsStatus(s, "Rejected") ? 3 : 9;

            ViewBag.AllClaims = claimsAll
                .OrderByDescending(c => c.SubmittedOn)
                .ToList();

            // Provide email back to the view for "Back to Dashboard" navigation.
            ViewBag.LecturerEmail = profile.User.Email;

            return View(profile);
        }

        /// <summary>
        /// Coordinator action: mark a claim as Verified.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <param name="email">Coordinator's email used for redirect context.</param>
        /// <returns>Redirect to Coordinator dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            // Fetch and guard against missing claims.
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Mark claim as verified to move it forward in the workflow.
            claim.Status = "Verified";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Coordinator");
        }

        /// <summary>
        /// Coordinator action: mark a claim as Rejected.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <param name="email">Coordinator's email used for redirect context.</param>
        /// <returns>Redirect to Coordinator dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Rejection at Coordinator stage (e.g., insufficient documentation).
            claim.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Coordinator");
        }

        /// <summary>
        /// Program Manager action: approve a previously Verified claim.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <param name="email">Program Manager email (optional; read from form if missing).</param>
        /// <returns>Redirect to Manager dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null) return NotFound("Claim not found.");

            // Guardrail: Only VERIFIED claims can be approved by the Program Manager.
            if (!string.Equals(claim.Status?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only verified claims can be approved by the Program Manager.");

            claim.Status = "Approved";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Manager");
        }

        /// <summary>
        /// Program Manager action: reject a previously Verified claim.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <param name="email">Program Manager email (optional; read from form if missing).</param>
        /// <returns>Redirect to Manager dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerReject(int id)
        {

            var claim = await _context.Claim.FindAsync(id);
            if (claim == null) return NotFound("Claim not found.");

            // Guardrail: Only VERIFIED claims can be rejected by the Program Manager.
            if (!string.Equals(claim.Status?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only verified claims can be rejected by the Program Manager.");

            claim.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Manager");
        }

        /// <summary>
        /// Builds a decrypted ZIP (in-memory stream) of a claim's encrypted supporting
        /// documents using <see cref="ClaimDownload"/> and returns it as a file download.
        /// </summary>
        /// <param name="claimId">The claim whose documents should be downloaded.</param>
        /// <returns>ZIP file result, or 404 if no documents are available.</returns>
        [HttpGet]
        public async Task<IActionResult> DownloadClaimFolder(int claimId)
        {
            // Delegate to service which assembles and decrypts all .enc files for the claim.
            var result = await _downloadService.BuildDecryptedZipAsync(claimId);
            if (result == null)
                return NotFound("No documents to download for this claim.");

            var (zipStream, fileName) = result.Value;
            return File(zipStream, "application/zip", fileName);
        }

    }
}
