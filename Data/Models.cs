namespace PlcMonitor.Data;

// ═══════════════════════════════════════════════════════════════
// Stoppage event (persisted in SQLite)
// ═══════════════════════════════════════════════════════════════

public sealed class StoppageEvent
{
    public long Id { get; set; }
    public string SectionKey { get; set; } = "";
    public string SectionName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public double DurationSeconds { get; set; }

    // Auto-detected from PLC
    public string Cause { get; set; } = "";

    // Operator-set (nullable until operator categorizes)
    public string Category { get; set; } = "Untagged";   // Planned | Unplanned | Neutral | Untagged
    public string? OperatorReason { get; set; }
    public string? OperatorNote { get; set; }
    public DateTime? OperatorSetAt { get; set; }

    public bool IsActive { get; set; }

    public TimeSpan Duration => EndedAt.HasValue
        ? EndedAt.Value - StartedAt
        : DateTime.UtcNow - StartedAt;

    public bool IsCategorized => OperatorReason != null;
}

// ═══════════════════════════════════════════════════════════════
// API DTOs
// ═══════════════════════════════════════════════════════════════

public sealed class SectionStatusDto
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool IsStopped { get; set; }
    public bool HasLiveData { get; set; }        // false when PLC poll is stale
    public bool IsGatedBySizer { get; set; }
    public bool GateOpen { get; set; }           // sizer running?

    public string CurrentCause { get; set; } = "";
    public long? ActiveEventId { get; set; }
    public string Category { get; set; } = "Untagged";
    public string OperatorReason { get; set; } = "";

    public DateTime? StoppageStartedAt { get; set; }
    public double ActiveStoppageSeconds { get; set; }
    public double TotalDowntimeSeconds { get; set; }
    public int StoppageCount { get; set; }
}

public sealed class LineSummaryDto
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsRunning { get; set; }
    public bool HasLiveData { get; set; }
    public bool IsGated { get; set; }
    public int ActiveStops { get; set; }
    public int TotalStations { get; set; }
    public double TotalDowntimeSeconds { get; set; }
    public double PlannedDowntimeSeconds { get; set; }
    public double UnplannedDowntimeSeconds { get; set; }
}

public sealed class OverviewDto
{
    public bool LineRunning { get; set; }
    public bool SizerRunning { get; set; }
    public bool HasLiveData { get; set; }
    public double TotalDowntimeSeconds { get; set; }
    public double PlannedDowntimeSeconds { get; set; }
    public double UnplannedDowntimeSeconds { get; set; }
    public double UntaggedDowntimeSeconds { get; set; }
    public int ActiveStoppages { get; set; }

    public List<LineSummaryDto> Lines { get; set; } = new();
    public List<LineSummaryDto> Machines { get; set; } = new();
    public List<SectionStatusDto> GlobalSections { get; set; } = new();
}

public sealed class SetReasonRequest
{
    public string Reason { get; set; } = "";
    public string? Note { get; set; }
}