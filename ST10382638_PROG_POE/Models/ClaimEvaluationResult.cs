namespace ST10382638_PROG_POE.Models
{
    public class ClaimEvaluationResult
    {
        public int ClaimId { get; set; }

        public bool IsValid { get; set; }

        public bool HoursWithinRange { get; set; }

        public bool RatePositive { get; set; }

        public bool AmountMatchesHoursAndRate { get; set; }

        public double ExpectedAmount { get; set; }

        public string Summary { get; set; } = string.Empty;

        public List<String> Issues { get; set; } = new();
    }
}
