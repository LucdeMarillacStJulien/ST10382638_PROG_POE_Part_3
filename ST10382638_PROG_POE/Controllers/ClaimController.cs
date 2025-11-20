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
using Microsoft.Extensions.Configuration;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;
using ST10382638_PROG_POE.Service;
using System;

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
        public ClaimController(AppDbContext context, IConfiguration config, ClaimDownload downloadService)
        {
            _context = context;
            _config = config;
            _downloadService = downloadService;
        }

        /// <summary>
        /// Displays the Create Claim form for a specific LecturerProfile.
        /// </summary>
        /// <param name="id">LecturerProfile primary key.</param>
        /// <returns>View bound to a new <see cref="Claim"/> instance.</returns>
        // GET: Claim/Create
        public async Task<IActionResult> Create(int? id)
        {
            if (id == null)
            {
                return BadRequest("Lecturer profile id is required.");
            }

            // Load the lecturer profile with the linked user so we have the email and rate
            var lecturer = await _context.LecturerProfile
                .Include(lp => lp.User)
                .FirstOrDefaultAsync(lp => lp.LecturerProfileId == id);

            if (lecturer == null)
            {
                return NotFound("Lecturer profile not found.");
            }

            // Used by the view to show lecturer info and keep email context
            ViewBag.Lecturer = lecturer;
            ViewBag.Email = lecturer.User?.Email ?? string.Empty;

            // Pre-populate LecturerProfileId and RateAtSubmission
            var model = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                RateAtSubmission = lecturer.HourlyRate
            };

            return View(model);
        }

        // POST: Claim/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim, List<IFormFile> files)
        {
            var currentEmail = User?.Identity?.Name;

            // -------------------------
            // 1) Server-side validation
            // -------------------------

            // File validation (type + size)
            if (files != null && files.Count > 0)
            {
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg"
        };
                const long maxFileSize = 10 * 1024 * 1024; // 10 MB

                foreach (var f in files.Where(x => x != null && x.Length > 0))
                {
                    var ext = Path.GetExtension((f.FileName ?? string.Empty).Trim());

                    if (string.IsNullOrWhiteSpace(ext) || !allowedExtensions.Contains(ext))
                    {
                        ModelState.AddModelError("files",
                            $"{f.FileName} has an invalid file type. Allowed: .pdf, .doc(x), .xls(x), .png, .jpg, .jpeg");
                    }

                    if (f.Length > maxFileSize)
                    {
                        ModelState.AddModelError("files",
                            $"{f.FileName} exceeds the {(maxFileSize / 1024 / 1024)} MB limit.");
                    }
                }
            }

            // HoursWorked validation (matches your business rules)
            if (double.IsNaN(claim.HoursWorked) || double.IsInfinity(claim.HoursWorked))
            {
                ModelState.AddModelError(nameof(Claim.HoursWorked), "Hours Worked value is invalid.");
            }
            else
            {
                if (claim.HoursWorked <= 0)
                {
                    ModelState.AddModelError(nameof(Claim.HoursWorked), "Hours Worked must be greater than zero.");
                }

                if (claim.HoursWorked < 0.25 || claim.HoursWorked > 10.0)
                {
                    ModelState.AddModelError(nameof(Claim.HoursWorked),
                        "Hours Worked must be between 0.25 and 10.0.");
                }
            }

            // ------------------------------------------------------------
            // FIX: remove server-side-only properties from ModelState
            // ------------------------------------------------------------
            // These are set by the controller (not the user) and some are marked [Required]
            // on the Claim model. If we don't remove them, ModelState.IsValid will fail
            // before we get a chance to populate them.
            ModelState.Remove(nameof(Claim.CalculatedAmount));
            ModelState.Remove(nameof(Claim.Status));
            ModelState.Remove(nameof(Claim.SubmittedOn));
            ModelState.Remove(nameof(Claim.SupportingDocs));
            ModelState.Remove(nameof(Claim.LecturerProfile));

            if (!ModelState.IsValid)
            {
                // Reload lecturer so the view can re-render correctly
                var lecturerVm = await _context.LecturerProfile
                    .Include(lp => lp.User)
                    .FirstOrDefaultAsync(lp => lp.LecturerProfileId == claim.LecturerProfileId);

                ViewBag.Lecturer = lecturerVm;
                ViewBag.Email = !string.IsNullOrWhiteSpace(currentEmail)
                    ? currentEmail
                    : lecturerVm?.User?.Email ?? string.Empty;

                return View(claim);
            }

            // ---------------------------------------
            // 2) Compute server-side fields and save
            // ---------------------------------------

            claim.CalculatedAmount = claim.HoursWorked * claim.RateAtSubmission;
            claim.Status = "Pending";
            // Store as UTC+2 (SAST) to match the rest of your project
            claim.SubmittedOn = DateTime.UtcNow.AddHours(2);

            _context.Claim.Add(claim);
            await _context.SaveChangesAsync(); // we now have ClaimId

            // ---------------------------------------
            // 3) Encrypt and save uploaded documents
            // ---------------------------------------

            if (files != null && files.Count > 0)
            {
                var solutionRoot = Directory.GetCurrentDirectory();
                var claimFolder = Path.Combine(solutionRoot, "App_Data", "ClaimDocs", claim.ClaimId.ToString());
                Directory.CreateDirectory(claimFolder);

                var uploadErrors = new List<string>();

                foreach (var file in files.Where(f => f != null && f.Length > 0))
                {
                    try
                    {
                        var originalName = Path.GetFileName(file.FileName);
                        var safeName = $"{Guid.NewGuid()}_{originalName}";
                        var encFileName = safeName + ".enc";
                        var encPath = Path.Combine(claimFolder, encFileName);

                        // Encrypt directly from upload stream to disk
                        using (var input = file.OpenReadStream())
                        {
                            await Encryption.EncryptStreamAsync(input, encPath, _config);
                        }

                        // Store relative path to encrypted file
                        var relativePath = Path.Combine("App_Data", "ClaimDocs",
                            claim.ClaimId.ToString(), encFileName);

                        _context.SupportingDoc.Add(new SupportingDoc
                        {
                            ClaimId = claim.ClaimId,
                            FileName = originalName,
                            FileUrl = relativePath,
                            FileType = file.ContentType
                        });
                    }
                    catch (Exception ex)
                    {
                        uploadErrors.Add($"Failed to store file '{file.FileName}': {ex.Message}");
                    }
                }

                await _context.SaveChangesAsync();

                if (uploadErrors.Count > 0)
                {
                    TempData["UploadWarnings"] = string.Join(Environment.NewLine, uploadErrors);
                }
            }

            // ---------------------------------------
            // 4) Redirect back to lecturer dashboard
            // ---------------------------------------

            return RedirectToAction("Index", "Lecturer");
        }


        // =====================================================================
        // Lecturer claim listing (grouped by status) – PENDING VIEW
        // =====================================================================

        public async Task<IActionResult> Pending()
        {
            var email = User?.Identity?.Name;

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


        // =====================================================================
        // Coordinator and Manager workflow actions
        // =====================================================================

        /// <summary>
        /// Coordinator action: mark a claim as Verified.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <returns>Redirect to Coordinator dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            // Fetch and guard against missing claims.
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Guardrail: only PENDING claims may be verified at Coordinator stage.
            if (!string.Equals(claim.Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only pending claims can be verified by the Coordinator.");

            // Mark claim as verified to move it forward in the workflow.
            claim.Status = "Verified";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Coordinator");
        }

        /// <summary>
        /// Coordinator action: mark a claim as Rejected.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <returns>Redirect to Coordinator dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Guardrail: only PENDING claims may be rejected at Coordinator stage.
            if (!string.Equals(claim.Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only pending claims can be rejected by the Coordinator.");

            // Rejection at Coordinator stage (e.g., insufficient documentation).
            claim.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Coordinator");
        }

        /// <summary>
        /// Program Manager action: approve a previously Verified claim.
        /// </summary>
        /// <param name="id">Claim identifier.</param>
        /// <returns>Redirect to Manager dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

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
        /// <returns>Redirect to Manager dashboard.</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerReject(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Guardrail: Only VERIFIED claims can be rejected by the Program Manager.
            if (!string.Equals(claim.Status?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only verified claims can be rejected by the Program Manager.");

            claim.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Manager");
        }

        // =====================================================================
        // Supporting document download for Coordinator/Manager/HR
        // =====================================================================

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
