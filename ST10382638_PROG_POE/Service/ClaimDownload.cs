// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/691e5672-00c8-8328-b4da-a2c0b7ea1c63
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
        // -------------------------------------------------------------------------
        // Dependencies
        // -------------------------------------------------------------------------
        private readonly IConfiguration _config;   // App configuration (crypto keys/settings)
        private readonly AppDbContext _context;    // EF Core context to load claims and documents

        /// <summary>
        /// Constructs the service with required configuration and DbContext.
        /// </summary>
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
        public async Task<(MemoryStream Zip, string FileName)?> BuildDecryptedZipAsync(int claimId)
        {
            // Load claim with lecturer and supporting docs for naming and context
            var claim = await _context.Claim
                .Include(c => c.LecturerProfile).ThenInclude(lp => lp.User)
                .Include(c => c.SupportingDocs)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);

            // Nothing to return if claim or supporting docs do not exist
            if (claim == null || claim.SupportingDocs == null || !claim.SupportingDocs.Any())
                return null;

            // Create memory stream to hold final ZIP
            var zipStream = new MemoryStream();

            // Build ZIP on top of memory stream
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Prevent duplicate entry names in ZIP
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var doc in claim.SupportingDocs)
                {
                    var encPath = Path.Combine(Directory.GetCurrentDirectory(), doc.FileUrl ?? string.Empty);
                    if (!System.IO.File.Exists(encPath)) continue;

                    // Prefer original upload name
                    var baseName = string.IsNullOrWhiteSpace(doc.FileName)
                        ? Path.GetFileNameWithoutExtension(encPath)
                        : doc.FileName.Trim();

                    var entryName = MakeUniqueZipName(baseName, usedNames);
                    var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);

                    // Decrypt directly into ZIP entry stream
                    using var entryOut = entry.Open();
                    await Encryption.DecryptToStreamAsync(encPath, entryOut, _config);
                }
            }

            // Rewind memory stream for caller
            zipStream.Position = 0;

            // Build readable ZIP filename
            var employeeName = (claim.LecturerProfile?.User is { } u)
                ? $"{u.FirstName} {u.Surname}".Trim()
                : "Employee";

            var safeZipName = SanitizeFileName($"{employeeName}_Claim_{claim.ClaimId}.zip");
            return (zipStream, safeZipName);
        }

        // -------------------------------------------------------------------------
        // Helpers (private)
        // -------------------------------------------------------------------------

        /// <summary>
        /// Ensures the ZIP entry name is unique by appending " (n)" when necessary.
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
        /// Replaces invalid filename characters with underscores to ensure compatibility.
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
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
