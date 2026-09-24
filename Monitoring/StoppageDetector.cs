using PlcMonitor.Data;

namespace PlcMonitor.Monitoring;

public sealed class StoppageDetector
{
    /// <summary>How long a poll result is considered "fresh" (seconds).</summary>
    private const double LiveDataMaxAgeSeconds = 5.0;

    private readonly StoppageRepository _repo;
    private readonly Dictionary<string, SectionState> _states = new();
    private bool _sizerRunning;

    public StoppageDetector(StoppageRepository repo)
    {
        _repo = repo;
        foreach (var def in SectionCatalog.All)
            _states[def.Key] = new SectionState();
    }

    public void ProcessSnapshot(
        IReadOnlyDictionary<string, bool> runBits,
        IReadOnlyDictionary<string, bool> stoppedBits,
        IReadOnlyDictionary<string, string> causes)
    {
        // ── 1. Update gate state from the sizer first ───────────────
        _sizerRunning = runBits.GetValueOrDefault("sizer", false);

        // ── 2. Process each section ─────────────────────────────────
        foreach (var def in SectionCatalog.All)
        {
            var state = _states[def.Key];
            var isRunning = runBits.GetValueOrDefault(def.Key, false);
            var cause = causes.GetValueOrDefault(def.Key, "");
            var actuallyStopped = !isRunning;

            // ── Gate check: gated sections skip stoppage detection when sizer is off
            if (def.IsGatedBySizer && !_sizerRunning)
            {
                if (state.WasStopped && state.ActiveId.HasValue)
                {
                    var ended = DateTime.UtcNow;
                    var dur = (ended - state.StartedAt!.Value).TotalSeconds;
                    _repo.CloseStoppage(state.ActiveId.Value, ended, dur,
                        string.IsNullOrWhiteSpace(state.Cause)
                            ? "(closed: sizer offline)"
                            : state.Cause + " (closed: sizer offline)");
                }
                state.WasStopped = false;
                state.StartedAt = null;
                state.ActiveId = null;
                state.Cause = "";
                continue;
            }

            // ── Normal detection ────────────────────────────────────
            if (actuallyStopped && !state.WasStopped)
            {
                state.WasStopped = true;
                state.StartedAt = DateTime.UtcNow;
                state.Cause = string.IsNullOrWhiteSpace(cause) ? "Unknown" : cause;
                state.ActiveId = _repo.InsertActiveStoppage(new StoppageEvent
                {
                    SectionKey = def.Key,
                    SectionName = def.DisplayName,
                    StartedAt = state.StartedAt.Value,
                    Cause = state.Cause
                });
            }
            else if (actuallyStopped && state.WasStopped)
            {
                if (!string.IsNullOrWhiteSpace(cause) && cause != state.Cause)
                {
                    state.Cause = cause;
                    if (state.ActiveId.HasValue)
                        _repo.UpdateActiveCause(state.ActiveId.Value, cause);
                }
            }
            else if (!actuallyStopped && state.WasStopped)
            {
                var ended = DateTime.UtcNow;
                var dur = (ended - state.StartedAt!.Value).TotalSeconds;
                if (state.ActiveId.HasValue)
                    _repo.CloseStoppage(state.ActiveId.Value, ended, dur, state.Cause);
                state.WasStopped = false;
                state.StartedAt = null;
                state.ActiveId = null;
                state.Cause = "";
            }
        }
    }

    public bool SizerRunning => _sizerRunning;

    public IReadOnlyList<SectionStatusDto> GetStatus()
    {
        // ── Determine if the PLC is currently reachable ──────────────
        var live = MonitoringService.LastSuccessfulPollUtc is { } last &&
                   (DateTime.UtcNow - last).TotalSeconds < LiveDataMaxAgeSeconds;

        // ── If PLC is offline, forcibly close any active events ──────
        //    and reset internal state so we don't show fake stoppages.
        if (!live)
        {
            foreach (var (key, st) in _states)
            {
                if (st.WasStopped && st.ActiveId.HasValue)
                {
                    var ended = DateTime.UtcNow;
                    var dur = (ended - st.StartedAt!.Value).TotalSeconds;
                    _repo.CloseStoppage(st.ActiveId.Value, ended, dur,
                        string.IsNullOrWhiteSpace(st.Cause)
                            ? "(closed: PLC communication lost)"
                            : st.Cause + " (closed: PLC communication lost)");
                }
                st.WasStopped = false;
                st.StartedAt = null;
                st.ActiveId = null;
                st.Cause = "";
            }
            _sizerRunning = false;
        }

        var list = new List<SectionStatusDto>();
        foreach (var def in SectionCatalog.All)
        {
            var state = _states[def.Key];
            var total = _repo.GetTotalDowntimeSeconds(def.Key);
            var history = _repo.GetHistory(def.Key, int.MaxValue);

            string category = "Untagged";
            string operatorReason = "";
            long? activeId = null;
            if (state.WasStopped && state.ActiveId.HasValue)
            {
                activeId = state.ActiveId;
                var active = history.FirstOrDefault(h => h.Id == state.ActiveId.Value);
                if (active != null)
                {
                    category = active.Category;
                    operatorReason = active.OperatorReason ?? "";
                }
            }

            list.Add(new SectionStatusDto
            {
                Key = def.Key,
                DisplayName = def.DisplayName,
                Description = def.Description,
                IsRunning = !state.WasStopped,
                IsStopped = state.WasStopped,
                HasLiveData = live,
                IsGatedBySizer = def.IsGatedBySizer,
                GateOpen = _sizerRunning,
                CurrentCause = state.Cause,
                ActiveEventId = activeId,
                Category = category,
                OperatorReason = operatorReason,
                StoppageStartedAt = state.StartedAt,
                ActiveStoppageSeconds = state.WasStopped && state.StartedAt.HasValue
                    ? (DateTime.UtcNow - state.StartedAt.Value).TotalSeconds
                    : 0,
                TotalDowntimeSeconds = total,
                StoppageCount = history.Count
            });
        }
        return list;
    }

    private sealed class SectionState
    {
        public bool WasStopped;
        public DateTime? StartedAt;
        public long? ActiveId;
        public string Cause = "";
    }
}