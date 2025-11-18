// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/68f2c6ad-b79c-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using System.IO.Compression;
using System.Text;

namespace ST10382638_PROG_POE.Service
{
    /// <summary>
    /// Provides functionality to build an in-memory ZIP file containing the
    /// decrypted supporting documents for a specific claim.
    /// </summary>
    public class ClaimDownload
    {
        // ---------- Dependencies ----------
        private readonly IConfiguration _config;   // App configuration (e.g., crypto keys/settings)
        private readonly AppDbContext _context;    // EF Core context to load claims and docs

        /// <summary>
        /// Constructs the service with required configuration and DbContext.
        /// </summary>
        /// <param name="config">Application configuration (used by encryption routines).</param>
        /// <param name="context">EF Core DbContext for querying claims and documents.</param>
        public ClaimDownload(IConfiguration config, AppDbContext context)
        {
            _config = config;
            _context = context;
        }

        /// <summary>
        /// Creates an in-memory ZIP archive containing all decrypted supporting documents
        /// for the specified <paramref name="claimId"/>. Documents are written at the ZIP root
        /// (no subfolder) with unique, user-friendly names. No plaintext files are written to disk.
        /// </summary>
        /// <param name="claimId">The unique identifier of the claim.</param>
        /// <returns>
        /// A tuple of (<see cref="MemoryStream"/> Zip, <see cref="string"/> FileName) when documents exist,
        /// or <c>null</c> if the claim does not exist or has no documents.
        /// </returns>
        public async Task<(MemoryStream Zip, string FileName)?> BuildDecryptedZipAsync(int claimId)
        {
            // Load claim with lecturer and supporting docs for naming and content.
            var claim = await _context.Claim
                .Include(c => c.LecturerProfile).ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);

            // If claim or documents are missing, there is nothing to build.
            if (claim == null || claim.SupportingDocs == null || !claim.SupportingDocs.Any())
                return null;

            // Prepare an in-memory stream that will hold the final ZIP.
            var zipStream = new MemoryStream();

            // Create the ZIP archive *over* the memory stream. Leave it open so we can rewind later.
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // We want all files at the ZIP root (no nested folder).
                // Track used names so duplicate names (e.g., same FileName) are made unique.
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var doc in claim.SupportingDocs)
                {
                    // Resolve path to the encrypted on-disk file; skip if the file is missing.
                    var encPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FileUrl ?? string.Empty);
                    if (!System.IO.File.Exists(encPath)) continue;

                    // Prefer the original uploaded name for readability; fall back to file stem.
                    var baseName = string.IsNullOrWhiteSpace(doc.FileName)
                        ? Path.GetFileNameWithoutExtension(encPath)
                        : doc.FileName.Trim();

                    // Ensure each filename in the ZIP is unique and placed at the root.
                    var entryName = MakeUniqueZipName(baseName, usedNames);
                    var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);

                    // Decrypt directly into the ZIP entry stream (no temp plaintext files).
                    using var entryOut = entry.Open();
                    await Encryption.DecryptToStreamAsync(encPath, entryOut, _config);
                }
            }

            // Rewind the stream so callers can read from the beginning.
            zipStream.Position = 0;

            // Build a user-friendly ZIP filename including the employee name and claim id.
            var employeeName = (claim.LecturerProfile?.User is { } u)
                ? $"{u.FirstName} {u.Surname}".Trim()
                : "Employee";

            var safeZipName = SanitizeFileName($"{employeeName}_Claim_{claim.ClaimId}.zip");
            return (zipStream, safeZipName);
        }

        // ---------- Helpers (private) ----------

        /// <summary>
        /// Ensures the ZIP entry name is unique by appending " (n)" when needed.
        /// </summary>
        private static string MakeUniqueZipName(string original, HashSet<string> used)
        {
            var name = Path.GetFileNameWithoutExtension(original);
            var ext = Path.GetExtension(original);
            var candidate = $"{name}{ext}";
            var i = 1;
            while (used.Contains(candidate))
                candidate = $"{name} ({i++}){ext}";
            used.Add(candidate);
            return candidate;
        }

        /// <summary>
        /// Replaces invalid filesystem characters with underscores to form a safe filename.
        /// </summary>
        private static string SanitizeFileName(string s)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s) sb.Append(invalid.Contains(ch) ? '_' : ch);
            return sb.ToString();
        }
    }
}
