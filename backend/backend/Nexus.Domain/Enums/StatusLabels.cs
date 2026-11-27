namespace Nexus.Domain.Enums;

/// <summary>
/// The labels the UI shows and sends back ("Applications open", "Not certified").
/// One place maps enum values to labels in both directions.
/// </summary>
public static class StatusLabels
{
    private static readonly Dictionary<BatchStatus, string> Batch = new()
    {
        [BatchStatus.Draft] = "Draft",
        [BatchStatus.ApplicationsOpen] = "Applications open",
        [BatchStatus.Selection] = "Selection",
        [BatchStatus.Running] = "Running",
        [BatchStatus.Completed] = "Completed",
        [BatchStatus.Cancelled] = "Cancelled",
    };

    private static readonly Dictionary<ApplicationStatus, string> App = new()
    {
        [ApplicationStatus.Submitted] = "Submitted",
        [ApplicationStatus.Shortlisted] = "Shortlisted",
        [ApplicationStatus.Waitlisted] = "Waitlisted",
        [ApplicationStatus.Selected] = "Selected",
        [ApplicationStatus.Active] = "Active",
        [ApplicationStatus.Completed] = "Completed",
        [ApplicationStatus.Certified] = "Certified",
        [ApplicationStatus.NotCertified] = "Not certified",
        [ApplicationStatus.Rejected] = "Rejected",
        [ApplicationStatus.Withdrawn] = "Withdrawn",
        [ApplicationStatus.Terminated] = "Terminated",
    };

    private static readonly Dictionary<ReportStatus, string> Report = new()
    {
        [ReportStatus.Submitted] = "Submitted",
        [ReportStatus.RevisionRequested] = "Revision requested",
        [ReportStatus.Accepted] = "Accepted",
    };

    public static string Label(this BatchStatus s) => Batch[s];
    public static string Label(this ApplicationStatus s) => App[s];
    public static string Label(this ReportStatus s) => Report[s];

    public static IReadOnlyList<string> BatchLabels => Batch.Values.ToList();
    public static IReadOnlyList<string> ApplicationLabels => App.Values.ToList();

    public static bool TryParseBatch(string? label, out BatchStatus status) => TryParse(Batch, label, out status);
    public static bool TryParseApplication(string? label, out ApplicationStatus status) => TryParse(App, label, out status);

    public static bool TryParseExamMode(string? label, out ExamMode mode) =>
        Enum.TryParse(label, ignoreCase: true, out mode) && Enum.IsDefined(mode);

    private static bool TryParse<T>(Dictionary<T, string> map, string? label, out T value) where T : struct, Enum
    {
        foreach (var (k, v) in map)
        {
            if (string.Equals(v, label, StringComparison.OrdinalIgnoreCase) || string.Equals(k.ToString(), label, StringComparison.OrdinalIgnoreCase))
            {
                value = k;
                return true;
            }
        }
        value = default;
        return false;
    }
}
