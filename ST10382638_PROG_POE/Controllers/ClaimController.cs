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
    // Handles claim creation, validation, encrypted file uploads and status changes
    // across Lecturer → Coordinator → Manager workflow, plus document download.
    public class ClaimController : Controller
    {
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------
        private readonly AppDbContext _context;            // EF Core DbContext for persistence
        private readonly IConfiguration _config;           // App configuration (used by encryption service)
        private readonly ClaimDownload _downloadService;   // Service that builds decrypted ZIPs

        // Server-side validation and guard constants
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".xlsx" }; // Allowed file types

        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB soft limit per file
        private const double MinHoursPerClaim = 0.25;      // 15-minute minimum granularity
        private const double MaxHoursPerClaim = 10.0;      // Upper bound to catch input mistakes

        // -------------------------------------------------------------------------
        // Constructor
        // -------------------------------------------------------------------------
        // Injects database context, configuration and claim download service.
        public ClaimController(AppDbContext context, IConfiguration config, ClaimDownload downloadService)
        {
            _context = context;
            _config = config;
            _downloadService = downloadService;
        }

        // -------------------------------------------------------------------------
        // Lecturer: display Create Claim form for a specific LecturerProfile
        // -------------------------------------------------------------------------
        // GET: Claim/Create
        public async Task<IActionResult> Create(int? id)
        {
            if (id == null)
            {
                return BadRequest("Lecturer profile id is required.");
            }


            // Load the lecturer profile with linked user to access email and hourly rate
            var lecturer = await _context.LecturerProfile
                .Include(lp => lp.User)
                .FirstOrDefaultAsync(lp => lp.LecturerProfileId == id);

            if (lecturer == null)
            {
                return NotFound("Lecturer profile not found.");
            }

            // Pass lecturer details to the view for display and context
            ViewBag.Lecturer = lecturer;
            ViewBag.Email = lecturer.User?.Email ?? string.Empty;

            // Pre-populate claim with profile id and rate at submission time
            var model = new Claim
            {
                LecturerProfileId = lecturer.LecturerProfileId,
                RateAtSubmission = lecturer.HourlyRate
            };

            return View(model);
        }

        // -------------------------------------------------------------------------
        // Lecturer: submit a new claim with supporting documents (encrypted)
        // -------------------------------------------------------------------------
        // POST: Claim/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Claim claim, List<IFormFile> files)
        {
            var currentEmail = User?.Identity?.Name;

            // ---------------------------------------------------------------------
            // 1) Server-side validation
            // ---------------------------------------------------------------------



            // Validate supporting document types and file sizes
            if (files != null && files.Count > 0)
            {
                var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".pdf", ".doc", ".docx", ".xls", ".xlsx"
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

                    // === SIMPLE SIZE CHECK REQUESTED ===
                    if (f.Length > maxFileSize)
                    {
                        ModelState.AddModelError("", "File is bigger than the maximum allowed size.");
                        return View(claim);
                    }
                }
            }

            // Validate HoursWorked against numeric validity and business rules
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

            // Remove server-side-only properties that are not part of the form
            // so that ModelState validation focuses on user-entered fields.
            ModelState.Remove(nameof(Claim.CalculatedAmount));
            ModelState.Remove(nameof(Claim.Status));
            ModelState.Remove(nameof(Claim.SubmittedOn));
            ModelState.Remove(nameof(Claim.SupportingDocs));
            ModelState.Remove(nameof(Claim.LecturerProfile));

            if (!ModelState.IsValid)
            {
                // Reload lecturer profile for re-displaying the form with errors
                var lecturerVm = await _context.LecturerProfile
                    .Include(lp => lp.User)
                    .FirstOrDefaultAsync(lp => lp.LecturerProfileId == claim.LecturerProfileId);

                ViewBag.Lecturer = lecturerVm;
                ViewBag.Email = !string.IsNullOrWhiteSpace(currentEmail)
                    ? currentEmail
                    : lecturerVm?.User?.Email ?? string.Empty;

                return View(claim);
            }

            // ---------------------------------------------------------------------
            // 2) Compute server-side fields and persist the claim
            // ---------------------------------------------------------------------

            // Calculate total amount using hours and stored hourly rate
            claim.CalculatedAmount = claim.HoursWorked * claim.RateAtSubmission;

            // New claims start in Pending state
            claim.Status = "Pending";

            // Store submission time in UTC+2 (SAST) to match project convention
            claim.SubmittedOn = DateTime.UtcNow.AddHours(2);

            _context.Claim.Add(claim);
            await _context.SaveChangesAsync(); // ClaimId is now available

            // ---------------------------------------------------------------------
            // 3) Encrypt and store uploaded supporting documents
            // ---------------------------------------------------------------------

            if (files != null && files.Count > 0)
            {
                // Build folder path under App_Data/ClaimDocs/{ClaimId}
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

                        // Encrypt input stream directly to encrypted file on disk
                        using (var input = file.OpenReadStream())
                        {
                            await Encryption.EncryptStreamAsync(input, encPath, _config);
                        }

                        // Store relative path to encrypted file for later retrieval
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
                    // Expose non-fatal upload issues to the user as warnings
                    TempData["UploadWarnings"] = string.Join(Environment.NewLine, uploadErrors);
                }
            }

            // ---------------------------------------------------------------------
            // 4) Redirect back to Lecturer dashboard after successful creation
            // ---------------------------------------------------------------------
            return RedirectToAction("Index", "Lecturer");
        }

        // =====================================================================
        // Lecturer claim listing (dashboard-style pending view)
        // =====================================================================
        public async Task<IActionResult> Pending()
        {
            var email = User?.Identity?.Name;

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Lecturer email is required.");

            // Load lecturer profile with associated user and claims collection
            var profile = await _context.LecturerProfile
                .Include(lp => lp.User)
                .Include(lp => lp.Claim)
                .FirstOrDefaultAsync(lp => lp.User.Email == email);

            if (profile == null)
                return NotFound("Lecturer profile not found.");

            // Local helper for safe status comparison
            static bool IsStatus(string? s, string target) =>
                (s ?? "").Trim().Equals(target, StringComparison.OrdinalIgnoreCase);

            var claimsAll = (profile.Claim ?? new List<Claim>()).ToList();

            // Extract approved claims for KPI totals
            var approvedClaims = claimsAll.Where(c => IsStatus(c.Status, "Approved")).ToList();

            // Total pending claims count for the lecturer
            ViewBag.TotalPending = claimsAll.Count(c => IsStatus(c.Status, "Pending"));

            // Global totals across all claims for the lecturer (hours and amount)
            ViewBag.TotalHoursAll = claimsAll.Sum(c => c.HoursWorked);
            ViewBag.TotalAmountAll = claimsAll.Sum(c =>
            {
                var hasStored = c.CalculatedAmount != 0;
                var storedAmt = Convert.ToDouble(c.CalculatedAmount);
                var computed = c.HoursWorked * c.RateAtSubmission;
                return hasStored ? storedAmt : computed;
            });

            // KPI totals for APPROVED claims only
            ViewBag.TotalHours = approvedClaims.Sum(c => c.HoursWorked);
            ViewBag.TotalAmount = approvedClaims.Sum(c =>
            {
                var hasStored = c.CalculatedAmount != 0;
                var storedAmt = Convert.ToDouble(c.CalculatedAmount);
                var computed = c.HoursWorked * c.RateAtSubmission;
                return hasStored ? storedAmt : computed;
            });

            // Per-status counts for UI tabs or filter badges
            ViewBag.PendingCountAll = claimsAll.Count(c => IsStatus(c.Status, "Pending"));
            ViewBag.VerifiedCountAll = claimsAll.Count(c => IsStatus(c.Status, "Verified"));
            ViewBag.ApprovedCountAll = claimsAll.Count(c => IsStatus(c.Status, "Approved"));
            ViewBag.RejectedCountAll = claimsAll.Count(c => IsStatus(c.Status, "Rejected"));

            // Status sort order helper (kept for future improvements if needed)
            int StatusOrder(string? s) =>
                IsStatus(s, "Pending") ? 0 :
                IsStatus(s, "Verified") ? 1 :
                IsStatus(s, "Approved") ? 2 :
                IsStatus(s, "Rejected") ? 3 : 9;

            // All claims ordered by submission date (most recent first)
            ViewBag.AllClaims = claimsAll
                .OrderByDescending(c => c.SubmittedOn)
                .ToList();

            // Pass lecturer email back to the view for navigation links
            ViewBag.LecturerEmail = profile.User.Email;

            return View(profile);
        }

        // =====================================================================
        // Coordinator and Manager workflow actions
        // =====================================================================

        // Coordinator: mark a pending claim as Verified
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Only Pending claims can be verified by the Coordinator
            if (!string.Equals(claim.Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only pending claims can be verified by the Coordinator.");

            claim.Status = "Verified";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Coordinator");
        }

        // Coordinator: mark a pending claim as Rejected
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Only Pending claims can be rejected by the Coordinator
            if (!string.Equals(claim.Status?.Trim(), "Pending", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only pending claims can be rejected by the Coordinator.");

            claim.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Coordinator");
        }

        // Program Manager: approve a Verified claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Only Verified claims may be approved by the Program Manager
            if (!string.Equals(claim.Status?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only verified claims can be approved by the Program Manager.");

            claim.Status = "Approved";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Manager");
        }

        // Program Manager: reject a Verified claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerReject(int id)
        {
            var claim = await _context.Claim.FindAsync(id);
            if (claim == null)
                return NotFound("Claim not found.");

            // Only Verified claims may be rejected by the Program Manager
            if (!string.Equals(claim.Status?.Trim(), "Verified", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only verified claims can be rejected by the Program Manager.");

            claim.Status = "Rejected";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Manager");
        }

        // =====================================================================
        // Supporting document download for Coordinator / Manager / HR
        // =====================================================================

        // Build a decrypted ZIP of all supporting documents linked to the claim
        [HttpGet]
        public async Task<IActionResult> DownloadClaimFolder(int claimId)
        {
            // Delegate ZIP assembly and decryption to the download service
            var result = await _downloadService.BuildDecryptedZipAsync(claimId);
            if (result == null)
                return NotFound("No documents to download for this claim.");

            var (zipStream, fileName) = result.Value;
            return File(zipStream, "application/zip", fileName);
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//  