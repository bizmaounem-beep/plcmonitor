using PlcMonitor.Data;
using PlcMonitor.Monitoring;

namespace PlcMonitor.Web;

public static class DashboardEndpoints
{
    public static void MapDashboard(this WebApplication app)
    {
        app.MapGet("/api/status", (StoppageDetector d) => Results.Ok(d.GetStatus()));

        app.MapGet("/api/reasons", () =>
            Results.Ok(ReasonCatalog.All.Select(r => new { r.Key, r.Label, r.Category })));

        // ── Operator sets reason for an active or closed stoppage ──
        app.MapPost("/api/stoppages/{id:long}/reason",
            (long id, SetReasonRequest req, StoppageRepository repo, StoppageDetector detector) =>
            {
                var opt = ReasonCatalog.Find(req.Reason);
                if (opt is null)
                    return Results.BadRequest(new { error = "Unknown reason", reason = req.Reason });

                var ok = repo.SetOperatorReason(id, req.Reason, req.Note);
                if (!ok) return Results.NotFound();

                return Results.Ok(new { id, reason = opt.Key, label = opt.Label, category = opt.Category });
            });

        // ── Overview ──────────────────────────────────────────────
        app.MapGet("/api/overview", (StoppageDetector detector, StoppageRepository repo) =>
        {
            var all = detector.GetStatus();
            var bySection = all.ToDictionary(s => s.Key);

            LineSummaryDto BuildSummary(LineDef line)
            {
                var lineSections = SectionCatalog.All.Where(s => s.LineKey == line.Key).ToList();
                var keys = lineSections.Select(s => s.Key).ToList();
                var activeStops = keys.Count(k =>
                    bySection.TryGetValue(k, out var st) && st.IsStopped && st.HasLiveData);
                var anyGated = lineSections.Any(s => s.IsGatedBySizer) && !detector.SizerRunning;
                var anyLive = keys.Any(k =>
                    bySection.TryGetValue(k, out var st) && st.HasLiveData);

                return new LineSummaryDto
                {
                    Key = line.Key,
                    DisplayName = line.DisplayName,
                    Description = line.Description,
                    IsRunning = activeStops == 0,
                    HasLiveData = anyLive,
                    IsGated = anyGated,
                    ActiveStops = anyGated ? 0 : activeStops,
                    TotalStations = lineSections.Count,
                    TotalDowntimeSeconds = repo.GetTotalDowntimeSecondsForSections(keys),
                    PlannedDowntimeSeconds = repo.SumByCategoryForSections(keys, "Planned"),
                    UnplannedDowntimeSeconds = repo.SumByCategoryForSections(keys, "Unplanned"),
                };
            }

            var lines = SectionCatalog.Lines.Select(BuildSummary).ToList();
            var machines = SectionCatalog.Machines.Select(BuildSummary).ToList();

            var globalKeys = SectionCatalog.All
                .Where(s => s.LineKey == "global")
                .Select(s => s.Key)
                .ToHashSet();
            var global = all.Where(s => globalKeys.Contains(s.Key)).ToList();

            var everySection = SectionCatalog.All.Select(s => s.Key).ToList();
            var anyActive = all.Any(s => s.IsStopped && s.HasLiveData);
            var anyLiveOverall = all.Any(s => s.HasLiveData);

            return Results.Ok(new OverviewDto
            {
                LineRunning = !anyActive,
                SizerRunning = anyLiveOverall && detector.SizerRunning,
                HasLiveData = anyLiveOverall,
                TotalDowntimeSeconds = repo.GetTotalDowntimeSecondsForSections(everySection),
                PlannedDowntimeSeconds = repo.SumByCategoryForSections(everySection, "Planned"),
                UnplannedDowntimeSeconds = repo.SumByCategoryForSections(everySection, "Unplanned"),
                UntaggedDowntimeSeconds = repo.SumByCategoryForSections(everySection, "Untagged"),
                ActiveStoppages = all.Count(s => s.IsStopped && s.HasLiveData),
                Lines = lines,
                Machines = machines,
                GlobalSections = global,
            });
        });

        // ── Line detail ───────────────────────────────────────────
        app.MapGet("/api/lines/{lineKey}", (string lineKey, StoppageDetector detector, StoppageRepository repo) =>
        {
            var line = SectionCatalog.Lines.FirstOrDefault(l => l.Key == lineKey);
            if (line is null) return Results.NotFound();

            var sections = SectionCatalog.All.Where(s => s.LineKey == lineKey).ToList();
            var keys = sections.Select(s => s.Key).ToList();
            var status = detector.GetStatus().Where(s => keys.Contains(s.Key)).ToList();
            var anyGated = sections.Any(s => s.IsGatedBySizer) && !detector.SizerRunning;

            return Results.Ok(new
            {
                line = new LineSummaryDto
                {
                    Key = line.Key,
                    DisplayName = line.DisplayName,
                    Description = line.Description,
                    TotalStations = sections.Count,
                    ActiveStops = anyGated ? 0 : status.Count(s => s.IsStopped),
                    IsRunning = status.All(s => !s.IsStopped),
                    IsGated = anyGated,
                    TotalDowntimeSeconds = repo.GetTotalDowntimeSecondsForSections(keys),
                    PlannedDowntimeSeconds = repo.SumByCategoryForSections(keys, "Planned"),
                    UnplannedDowntimeSeconds = repo.SumByCategoryForSections(keys, "Unplanned"),
                },
                sizerRunning = detector.SizerRunning,
                sections = status,
            });
        });

        // ── Tipper detail ─────────────────────────────────────────
        app.MapGet("/api/tipper", (StoppageDetector detector, StoppageRepository repo) =>
        {
            var sections = SectionCatalog.All.Where(s => s.LineKey == "tipper").ToList();
            var keys = sections.Select(s => s.Key).ToList();
            var status = detector.GetStatus().Where(s => keys.Contains(s.Key)).ToList();
            var anyGated = sections.Any(s => s.IsGatedBySizer) && !detector.SizerRunning;

            return Results.Ok(new
            {
                line = new LineSummaryDto
                {
                    Key = "tipper",
                    DisplayName = "Box Tipper (Volcador / AE100)",
                    Description = "Crate infeed and box tipper unit",
                    TotalStations = sections.Count,
                    ActiveStops = anyGated ? 0 : status.Count(s => s.IsStopped),
                    IsRunning = status.All(s => !s.IsStopped),
                    IsGated = anyGated,
                    TotalDowntimeSeconds = repo.GetTotalDowntimeSecondsForSections(keys),
                    PlannedDowntimeSeconds = repo.SumByCategoryForSections(keys, "Planned"),
                    UnplannedDowntimeSeconds = repo.SumByCategoryForSections(keys, "Unplanned"),
                },
                sizerRunning = detector.SizerRunning,
                sections = status,
            });
        });

        // ── History ───────────────────────────────────────────────
        app.MapGet("/api/history", (StoppageRepository repo,
            string? section, string? line, string? category,
            string? from, string? to, int? limit) =>
        {
            var events = repo.GetHistory(section, limit ?? 500).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(line))
            {
                var lineKeys = SectionCatalog.All
                    .Where(s => s.LineKey.Equals(line, StringComparison.OrdinalIgnoreCase))
                    .Select(s => s.Key).ToHashSet();
                events = events.Where(e => lineKeys.Contains(e.SectionKey));
            }
            if (!string.IsNullOrWhiteSpace(category))
                events = events.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (DateTime.TryParse(from, out var f)) events = events.Where(e => e.StartedAt >= f);
            if (DateTime.TryParse(to, out var t)) events = events.Where(e => e.StartedAt <= t);

            return Results.Ok(events.ToList());
        });

        app.MapGet("/api/history/filters", () => Results.Ok(new
        {
            lines = SectionCatalog.Lines.Select(l => new { l.Key, l.DisplayName }),
            sections = SectionCatalog.All.Select(s => new { s.Key, s.DisplayName, s.LineKey }),
            categories = new[] { "Untagged", "Planned", "Unplanned", "Neutral" },
            reasons = ReasonCatalog.All.Select(r => new { r.Key, r.Label, r.Category }),
        }));

        app.MapGet("/api/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));
    }
}