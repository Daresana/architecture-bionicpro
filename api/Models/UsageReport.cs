namespace Reports.Api.Models
{
    public class UsageReport
    {
        public DateOnly ReportDate { get; set; }
        public string UserId { get; set; } = string.Empty;
        public List<ReportRow> Prostheses { get; set; } = new();
    }

    public class ReportRow
    {
        public string ClientName { get; set; } = string.Empty;
        public int ProsthesisId { get; set; }
        public ulong MovementCount { get; set; }
        public double AverageSignal { get; set; }
        public double AverageBattery { get; set; }
    }

    public class ReportPeriod
    {
        public string BatchId { get; set; } = string.Empty;
    }

    public class ClickHouseResult<T>
    {
        public List<T> Data { get; set; } = new();
    }
}
