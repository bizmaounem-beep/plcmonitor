namespace PlcMonitor.Data;

public sealed record ReasonOption(string Key, string Label, string Category)
{
    // Category: "Planned" | "Unplanned" | "Neutral"
}

public static class ReasonCatalog
{
    public static readonly IReadOnlyList<ReasonOption> All = new[]
    {
        new ReasonOption("maintenance",    "Maintenance / Scheduled service", "Planned"),
        new ReasonOption("changeover",     "Changeover / Product setup",      "Planned"),
        new ReasonOption("break",          "Planned operator break",          "Planned"),
        new ReasonOption("cleaning",       "Cleaning / Sanitation",           "Planned"),
        new ReasonOption("end_of_shift",   "End of shift / End of batch",     "Planned"),
        new ReasonOption("breakdown",      "Breakdown / Mechanical failure",  "Unplanned"),
        new ReasonOption("material",       "Material shortage (boxes / fruit)","Unplanned"),
        new ReasonOption("power",          "Power / Network failure",         "Unplanned"),
        new ReasonOption("quality",        "Quality issue / Reject",          "Unplanned"),
        new ReasonOption("blocked",        "Blocked downstream / Saturation", "Unplanned"),
        new ReasonOption("unknown",        "Unknown cause",                   "Neutral"),
        new ReasonOption("other",          "Other (see note)",                "Neutral"),
    };

    public static ReasonOption? Find(string key) =>
        All.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}