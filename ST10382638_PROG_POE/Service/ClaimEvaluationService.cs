using ST10382638_PROG_POE.Models;
using System.Globalization;

namespace ST10382638_PROG_POE.Service
{
    public class ClaimEvaluationService
    {
        private static readonly CultureInfo zaCulture = CultureInfo.GetCultureInfo("en-ZA");

        public ClaimEvaluationResult Evaluate(Claim claim)
        {
            if (claim == null)
                throw new ArgumentNullException(nameof(claim));

            var result = new ClaimEvaluationResult
            {
                ClaimId = claim.ClaimId,
            };

            var issues = new List<string>();

            var hours = claim.HoursWorked;
            result.HoursWithinRange = hours >= 0.25 && hours <= 10;
            if (!result.HoursWithinRange)
                issues.Add($"Hours worked ({hours:0.##}) is outside the allowed range 0.25 - 10.0");

            var rate = claim.RateAtSubmission;
            result.RatePositive = rate > 0;
            if (!result.RatePositive)
                issues.Add($"Hourly rate ({rate:0.##}) must be greater than zero");

            var expected = Math.Round(hours * rate, 2);
            var actual = Math.Round(claim.CalculatedAmount, 2);
            result.ExpectedAmount = expected;
            result.AmountMatchesHoursAndRate = Math.Abs(expected - actual) < 0.01;

            if(!result.AmountMatchesHoursAndRate)
                issues.Add(
                    $"Amount mismatch: hours × rate = {expected.ToString("F2", zaCulture)} " +
                    $"but stored amount is {actual.ToString("F2", zaCulture)}.");

            result.IsValid = result.HoursWithinRange && result.RatePositive && result.AmountMatchesHoursAndRate;

            if (issues.Count == 0)
            {
                result.Summary = "All automated checks passed.";
            }
            else
            {
                result.Summary = string.Join(" ", issues);
            }

            result.Issues = issues;
            return result;
        }
    }
}
