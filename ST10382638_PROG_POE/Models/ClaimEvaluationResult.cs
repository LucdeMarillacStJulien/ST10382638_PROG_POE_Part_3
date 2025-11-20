// =====================================================================================
// Name: Luc de Marillac St Julien
// Student Number: ST10382638
// Group: 1
//
// References:
//   1) Project outline/instructions: https://chatgpt.com/c/691e5672-00c8-8328-b4da-a2c0b7ea1c63
//   2) C# Reference & Tutorials:   https://www.w3schools.com/cs/index.php
// =====================================================================================

namespace ST10382638_PROG_POE.Models
{
    /// <summary>
    /// Represents the result of automated claim evaluation.
    /// Provides boolean flags for rule outcomes, computed expected amount,
    /// a text summary, and a list of rule violations (if any).
    /// </summary>
    public class ClaimEvaluationResult
    {
        public int ClaimId { get; set; }

        public bool IsValid { get; set; }

        public bool HoursWithinRange { get; set; }

        public bool RatePositive { get; set; }

        public bool AmountMatchesHoursAndRate { get; set; }

        public double ExpectedAmount { get; set; }

        public string Summary { get; set; } = string.Empty;

        public List<string> Issues { get; set; } = new();
    }
}
//------------------------------------------...ooo000 END OF FILE 000ooo...------------------------------------------------------//
