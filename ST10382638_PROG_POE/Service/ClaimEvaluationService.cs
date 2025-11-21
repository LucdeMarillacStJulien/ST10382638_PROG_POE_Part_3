// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/691e5672-00c8-8328-b4da-a2c0b7ea1c63
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

using ST10382638_PROG_POE.Models;
using System.Globalization;

namespace ST10382638_PROG_POE.Service
{
    /// <summary>
    /// Provides automated rule-based validation for claims.
    /// Checks hour ranges, positive rate, and consistency between amount, hours and rate,
    /// and returns a structured <see cref="ClaimEvaluationResult"/> with issues and summary.
    /// </summary>
    public class ClaimEvaluationService
    {
        // Culture used when formatting amounts in evaluation messages (South Africa)
        private static readonly CultureInfo zaCulture = CultureInfo.GetCultureInfo("en-ZA");

        //------------------------------------------------------------------------------------------------------------------------//
        /// <summary>
        /// Evaluates a single <see cref="Claim"/> instance against business rules:
        /// - Hours must be within [0.25, 10.0]
        /// - Hourly rate must be greater than zero
        /// - CalculatedAmount must match Hours × Rate (within 1 cent tolerance)
        /// </summary>
        /// <param name="claim">Claim to evaluate.</param>
        /// <returns>A populated <see cref="ClaimEvaluationResult"/> describing the outcome.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="claim"/> is null.</exception>
        public ClaimEvaluationResult Evaluate(Claim claim)
        {
            if (claim == null)
                throw new ArgumentNullException(nameof(claim));

            var result = new ClaimEvaluationResult
            {
                ClaimId = claim.ClaimId,
            };

            var issues = new List<string>();

            // ---------------------------------------------------------------------
            // Rule 1: Hours in allowed range
            // ---------------------------------------------------------------------
            var hours = claim.HoursWorked;
            result.HoursWithinRange = hours >= 0.25 && hours <= 10;
            if (!result.HoursWithinRange)
                issues.Add($"Hours worked ({hours:0.##}) is outside the allowed range 0.25 - 10.0");

            // ---------------------------------------------------------------------
            // Rule 2: Hourly rate must be positive
            // ---------------------------------------------------------------------
            var rate = claim.RateAtSubmission;
            result.RatePositive = rate > 0;
            if (!result.RatePositive)
                issues.Add($"Hourly rate ({rate:0.##}) must be greater than zero");

            // ---------------------------------------------------------------------
            // Rule 3: Amount must match Hours × Rate (to 2 decimal places)
            // ---------------------------------------------------------------------
            var expected = Math.Round(hours * rate, 2);
            var actual = Math.Round(claim.CalculatedAmount, 2);
            result.ExpectedAmount = expected;
            result.AmountMatchesHoursAndRate = Math.Abs(expected - actual) < 0.01;

            if (!result.AmountMatchesHoursAndRate)
                issues.Add(
                    $"Amount mismatch: hours × rate = {expected.ToString("F2", zaCulture)} " +
                    $"but stored amount is {actual.ToString("F2", zaCulture)}.");

            // ---------------------------------------------------------------------
            // Aggregate overall validity and build summary text
            // ---------------------------------------------------------------------
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
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
