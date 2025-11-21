// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/691e5672-00c8-832c-97a1-59b6f53334a9
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ST10382638_PROG_POE.Data;
using ST10382638_PROG_POE.Models;

namespace ST10382638_PROG_POE.Service
{
    /// <summary>
    /// Service responsible for building CSV-based "invoice" reports for lecturers.
    ///
    /// This class supports two main reporting scenarios:
    ///  1) A single-claim invoice:
    ///     - Used when HR wants a CSV for one specific claim.
    ///  2) Period-based invoices:
    ///     - Aggregates multiple claims over a defined period
    ///       (day, week or month) based on the claim's <c>SubmittedOn</c> date.
    ///
    /// The output of each method is a tuple containing:
    ///  - <c>Content</c>: UTF-8 encoded CSV bytes ready for download.
    ///  - <c>FileName</c>: A safe, human-readable filename.
    /// </summary>
    public class LecturerInvoiceReport
    {
        // EF Core context used to query lecturer profiles, users and claims
        private readonly AppDbContext _context;

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Creates a new instance of <see cref="LecturerInvoiceReport"/> using the
        /// application database context.
        ///
        /// The context is used to:
        ///  - Load <see cref="Claim"/> entities and their related
        ///    <see cref="LecturerProfile"/> and <see cref="ApplicationUser"/> data.
        ///  - Build CSV rows that include lecturer identity and claim details.
        /// </summary>
        /// <param name="context">The application's <see cref="AppDbContext"/>.</param>
        public LecturerInvoiceReport(AppDbContext context)
        {
            _context = context;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Builds a CSV invoice for a single claim.
        ///
        /// Steps performed:
        ///  1) Eager-load the specified claim including:
        ///     - its <see cref="LecturerProfile"/>,
        ///     - the lecturer's <see cref="ApplicationUser"/> record.
        ///  2) If any part is missing, return <c>null</c> (no invoice can be built).
        ///  3) Construct a simple CSV "invoice" containing:
        ///     - invoice metadata (invoice number, lecturer name, email, submitted date),
        ///     - a single detail line with hours, rate, amount, status and notes.
        ///  4) Return the CSV as UTF-8 bytes plus a descriptive filename.
        /// </summary>
        /// <param name="claimId">
        /// The primary key of the <see cref="Claim"/> to invoice.
        /// </param>
        /// <returns>
        /// A tuple containing:
        ///  - <c>Content</c>: UTF-8 CSV bytes, and
        ///  - <c>FileName</c>: a suggested download name,
        /// or <c>null</c> if the claim or lecturer details cannot be found.
        /// </returns>
        public async Task<(byte[] Content, string FileName)?> BuildSingleClaimInvoiceAsync(int claimId)
        {
            var claim = await _context.Claim
                .Include(c => c.LecturerProfile)
                    .ThenInclude(lp => lp.User)
                .FirstOrDefaultAsync(c => c.ClaimId == claimId);

            if (claim == null || claim.LecturerProfile == null || claim.LecturerProfile.User == null)
                return null;

            var user = claim.LecturerProfile.User;

            var za = CultureInfo.CreateSpecificCulture("en-ZA");
            var csvCulture = CultureInfo.InvariantCulture;

            var sb = new StringBuilder();

            var invoiceNo = $"INV-{claim.SubmittedOn:yyyyMMdd}-{claim.ClaimId:D3}";

            sb.AppendLine("Invoice");
            sb.AppendLine($"InvoiceNumber,{invoiceNo}");
            sb.AppendLine($"Lecturer,{user.FirstName} {user.Surname}");
            sb.AppendLine($"Email,{user.Email}");
            sb.AppendLine($"SubmittedOn,{claim.SubmittedOn:yyyy-MM-dd}");
            sb.AppendLine();

            sb.AppendLine("ClaimId,HoursWorked,RateAtSubmission,CalculatedAmount,Status,Notes");

            var notes = (claim.Notes ?? string.Empty).Replace("\"", "\"\"");
            if (notes.Contains(',')) notes = $"\"{notes}\"";

            sb.AppendLine(
                $"{claim.ClaimId}," +
                $"{claim.HoursWorked.ToString("0.##", csvCulture)}," +
                $"{claim.RateAtSubmission.ToString("F2", csvCulture)}," +
                $"{claim.CalculatedAmount.ToString("F2", csvCulture)}," +
                $"{claim.Status}," +
                $"{notes}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            var safeSurname = string.IsNullOrWhiteSpace(user.Surname) ? "Lecturer" : user.Surname;
            var fileName = $"Invoice_Claim_{claim.ClaimId:D3}_{safeSurname}.csv";

            return (bytes, fileName);
        }

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Builds a CSV invoice for all claims of a lecturer within a specified period.
        ///
        /// Period handling:
        ///  - <c>"day"</c>   (or any unrecognised string):
        ///      * Uses the <paramref name="referenceDate"/> as a single day.
        ///  - <c>"week"</c>:
        ///      * Calculates the Monday-Sunday week that includes the reference date.
        ///  - <c>"month"</c>:
        ///      * Uses the full calendar month of the reference date.
        ///
        /// For all claims in that period:
        ///  - Each claim becomes a row in the CSV with:
        ///      ClaimId, date, hours, rate, amount, status and notes.
        ///  - Totals are accumulated only for claims in <c>Approved</c> status.
        ///
        /// This method is typically used by HR to generate batch-style reports
        /// for payroll or finance processing.
        /// </summary>
        /// <param name="userId">
        /// The lecturer's Identity user ID (foreign key in <see cref="LecturerProfile"/>).
        /// </param>
        /// <param name="period">
        /// The period type: expected values are <c>"day"</c>, <c>"week"</c> or <c>"month"</c>.
        /// Any other value falls back to a single day.
        /// </param>
        /// <param name="referenceDate">
        /// The date used as the anchor when calculating the required period.
        /// For example, if period is <c>"week"</c>, the week containing this date is used.
        /// </param>
        /// <returns>
        /// A tuple containing:
        ///  - <c>Content</c>: UTF-8 CSV bytes with multiple claim lines and totals, and
        ///  - <c>FileName</c>: a descriptive filename that includes the period label,
        /// or <c>null</c> if the lecturer, claims or period selection result in no data.
        /// </returns>
        public async Task<(byte[] Content, string FileName)?> BuildPeriodInvoiceAsync(
            string userId,
            string period,
            DateTime referenceDate)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            var profile = await _context.LecturerProfile
                .Include(lp => lp.User)
                .Include(lp => lp.Claim)
                .FirstOrDefaultAsync(lp => lp.UserId == userId);

            if (profile == null || profile.User == null)
                return null;

            var claimsAll = (profile.Claim ?? new List<Claim>()).ToList();
            if (!claimsAll.Any())
                return null;

            var periodNorm = (period ?? string.Empty).Trim().ToLowerInvariant();
            var date = referenceDate.Date;

            DateTime start;
            DateTime end;
            string label;

            // Work out start/end bounds and a label string based on the chosen period
            if (periodNorm == "week")
            {
                var dayOfWeek = (int)date.DayOfWeek;
                var offset = dayOfWeek == 0 ? -6 : (1 - dayOfWeek);
                start = date.AddDays(offset);
                end = start.AddDays(7);
                label = $"week_{start:yyyyMMdd}_{end.AddDays(-1):yyyyMMdd}";
            }
            else if (periodNorm == "month")
            {
                start = new DateTime(date.Year, date.Month, 1);
                end = start.AddMonths(1);
                label = $"month_{start:yyyyMM}";
            }
            else
            {
                start = date;
                end = date.AddDays(1);
                label = $"day_{start:yyyyMMdd}";
            }

            // Filter claims that fall within the [start, end) window
            var claims = claimsAll
                .Where(c => c.SubmittedOn >= start && c.SubmittedOn < end)
                .OrderBy(c => c.SubmittedOn)
                .ToList();

            if (!claims.Any())
                return null;

            var za = CultureInfo.CreateSpecificCulture("en-ZA");
            var csvCulture = CultureInfo.InvariantCulture;

            var sb = new StringBuilder();

            // Header section: lecturer details and period metadata
            sb.AppendLine("Invoice Period");
            sb.AppendLine($"Lecturer,{profile.User.FirstName} {profile.User.Surname}");
            sb.AppendLine($"Email,{profile.User.Email}");
            sb.AppendLine($"Period,{label}");
            sb.AppendLine($"From,{start:yyyy-MM-dd}");
            sb.AppendLine($"To,{end.AddDays(-1):yyyy-MM-dd}");
            sb.AppendLine();

            // Table header row for individual claims
            sb.AppendLine("ClaimId,SubmittedOn,HoursWorked,RateAtSubmission,CalculatedAmount,Status,Notes");

            double totalHours = 0;
            double totalAmount = 0;

            // Detail lines for each claim in the period
            foreach (var c in claims)
            {
                var notes = (c.Notes ?? string.Empty).Replace("\"", "\"\"");
                if (notes.Contains(',')) notes = $"\"{notes}\"";

                sb.AppendLine(
                    $"{c.ClaimId}," +
                    $"{c.SubmittedOn:yyyy-MM-dd}," +
                    $"{c.HoursWorked.ToString("0.##", csvCulture)}," +
                    $"{c.RateAtSubmission.ToString("F2", csvCulture)}," +
                    $"{c.CalculatedAmount.ToString("F2", csvCulture)}," +
                    $"{c.Status}," +
                    $"{notes}");

                // Only approved claims contribute to the totals at the bottom
                if (string.Equals(c.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    totalHours += c.HoursWorked;
                    totalAmount += c.CalculatedAmount;
                }
            }

            // Summary section: total approved hours and amounts for the period
            sb.AppendLine();
            sb.AppendLine($",,,TOTAL HOURS,{totalHours.ToString("0.##", csvCulture)}");
            sb.AppendLine($",,,TOTAL AMOUNT,{totalAmount.ToString("F2", csvCulture)}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            var safeSurname = string.IsNullOrWhiteSpace(profile.User.Surname) ? "Lecturer" : profile.User.Surname;
            var fileName = $"Invoice_{label}_{safeSurname}.csv";

            return (bytes, fileName);
        }
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
