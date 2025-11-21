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
    /// Builds CSV "invoice" reports for a lecturer, either:
    ///  - for a single claim, or
    ///  - aggregated by day / week / month.
    /// </summary>
    public class LecturerInvoiceReport
    {
        private readonly AppDbContext _context;

        //------------------------------------------------------------------------------------------------------------------------//
        public LecturerInvoiceReport(AppDbContext context)
        {
            _context = context;
        }

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Builds a CSV invoice for a single claim.
        /// </summary>
        /// <param name="claimId">ClaimId primary key.</param>
        /// <returns>CSV bytes + filename, or null if not found.</returns>
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
        /// Builds a CSV invoice for all claims of a lecturer in a period
        /// (day / week / month) based on SubmittedOn.
        /// </summary>
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

            var claims = claimsAll
                .Where(c => c.SubmittedOn >= start && c.SubmittedOn < end)
                .OrderBy(c => c.SubmittedOn)
                .ToList();

            if (!claims.Any())
                return null;

            var za = CultureInfo.CreateSpecificCulture("en-ZA");
            var csvCulture = CultureInfo.InvariantCulture;

            var sb = new StringBuilder();

            sb.AppendLine("Invoice Period");
            sb.AppendLine($"Lecturer,{profile.User.FirstName} {profile.User.Surname}");
            sb.AppendLine($"Email,{profile.User.Email}");
            sb.AppendLine($"Period,{label}");
            sb.AppendLine($"From,{start:yyyy-MM-dd}");
            sb.AppendLine($"To,{end.AddDays(-1):yyyy-MM-dd}");
            sb.AppendLine();

            sb.AppendLine("ClaimId,SubmittedOn,HoursWorked,RateAtSubmission,CalculatedAmount,Status,Notes");

            double totalHours = 0;
            double totalAmount = 0;

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

                if (string.Equals(c.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                {
                    totalHours += c.HoursWorked;
                    totalAmount += c.CalculatedAmount;
                }
            }

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
