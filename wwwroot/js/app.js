/* =========================================================
   Line Monitor — hash-based SPA
   ========================================================= */

const ROUTES = ["dashboard", "line1", "line2", "line3", "tipper", "history"];

/* ---------- Utilities ---------- */
const $ = (sel) => document.querySelector(sel);
const $$ = (sel) => Array.from(document.querySelectorAll(sel));

const fmtDur = (s) => {
    if (s == null || isNaN(s)) return "—";
    s = Math.max(0, Math.floor(s));
    const h = String(Math.floor(s / 3600)).padStart(2, "0");
    const m = String(Math.floor((s % 3600) / 60)).padStart(2, "0");
    const ss = String(s % 60).padStart(2, "0");
    return `${h}:${m}:${ss}`;
};
const fmtDate = (iso) => (iso ? new Date(iso).toLocaleString() : "—");
const esc = (s) => String(s ?? "").replace(/[&<>"']/g,
    (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
const escAttr = (s) => String(s ?? "").replace(/'/g, "\\'").replace(/"/g, "&quot;");

async function api(path) {
    const r = await fetch(path);
    if (!r.ok) throw new Error(`${path} → HTTP ${r.status}`);
    return r.json();
}

/* ---------- Global state ---------- */
const state = {
    current: "dashboard",
    overview: null,
    lines: {},
    tipper: null,
    history: null,
    historyFilters: { section: "", line: "", category: "", from: "", to: "" },
    error: null,
};

/* ---------- Router ---------- */
function currentRoute() {
    const h = location.hash.replace(/^#\/?/, "");
    return ROUTES.includes(h) ? h : "dashboard";
}
function navigate(route) { location.hash = `#/${route}`; }

window.addEventListener("hashchange", () => {
    state.current = currentRoute();
    render();
    refreshCurrent();
});

/* =========================================================
   REASON MODAL
   ========================================================= */
const REASONS = [
    { key: "maintenance", label: "Maintenance / Scheduled service", category: "Planned" },
    { key: "changeover", label: "Changeover / Product setup", category: "Planned" },
    { key: "break", label: "Planned operator break", category: "Planned" },
    { key: "cleaning", label: "Cleaning / Sanitation", category: "Planned" },
    { key: "end_of_shift", label: "End of shift / End of batch", category: "Planned" },
    { key: "breakdown", label: "Breakdown / Mechanical failure", category: "Unplanned" },
    { key: "material", label: "Material shortage (boxes/fruit)", category: "Unplanned" },
    { key: "power", label: "Power / Network failure", category: "Unplanned" },
    { key: "quality", label: "Quality issue / Reject", category: "Unplanned" },
    { key: "blocked", label: "Blocked downstream / Saturation", category: "Unplanned" },
    { key: "unknown", label: "Unknown cause", category: "Neutral" },
    { key: "other", label: "Other (see note)", category: "Neutral" },
];

let _activeReason = { eventId: null, sectionName: "", selectedKey: null };

function openReasonModal(eventId, sectionName, currentKey) {
    _activeReason = { eventId, sectionName, selectedKey: currentKey || null };
    const title = $("#reason-modal-title");
    const sub = $("#reason-modal-sub");
    const note = $("#reason-note");
    const save = $("#reason-save");
    if (title) title.textContent = "Set stop reason";
    if (sub) sub.textContent = sectionName + " — choose a reason";
    if (note) note.value = "";
    if (save) save.disabled = !currentKey;
    renderReasonGrid();
    const modal = $("#reason-modal");
    if (modal) modal.classList.remove("hidden");
}

function closeReasonModal() {
    const m = $("#reason-modal");
    if (m) m.classList.add("hidden");
}

function renderReasonGrid() {
    const grid = $("#reason-grid");
    if (!grid) return;
    grid.innerHTML = REASONS.map((r) => `
        <button type="button"
                class="reason-btn ${_activeReason.selectedKey === r.key ? "selected" : ""}"
                onclick="selectReason('${r.key}')">
            ${r.label}
            <span class="reason-cat">${r.category}</span>
        </button>`).join("");
}

function selectReason(key) {
    _activeReason.selectedKey = key;
    const save = $("#reason-save");
    if (save) save.disabled = false;
    renderReasonGrid();
}

async function saveReason() {
    if (!_activeReason.eventId || !_activeReason.selectedKey) return;
    const note = $("#reason-note");
    const body = {
        reason: _activeReason.selectedKey,
        note: note ? (note.value || null) : null,
    };
    try {
        const res = await fetch(`/api/stoppages/${_activeReason.eventId}/reason`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        closeReasonModal();
        await refreshCurrent();
        render();
    } catch (e) {
        alert("Failed to save reason: " + e.message);
    }
}

(function bindModalEvents() {
    const save = $("#reason-save");
    if (save) save.addEventListener("click", saveReason);

    const modal = $("#reason-modal");
    if (modal) modal.addEventListener("click", (e) => {
        if (e.target.id === "reason-modal") closeReasonModal();
    });
})();

window.openReasonModal = openReasonModal;
window.closeReasonModal = closeReasonModal;
window.selectReason = selectReason;

/* =========================================================
   ALERT BANNER
   ========================================================= */
function renderAlertBanner() {
    const el = $("#alert-banner");
    if (!el) return;

    const allSections = [];
    if (state.overview?.globalSections) allSections.push(...state.overview.globalSections);
    Object.values(state.lines).forEach((l) => l?.sections?.forEach((s) => allSections.push(s)));
    if (state.tipper?.sections) allSections.push(...state.tipper.sections);

    const seen = new Set();
    const unique = allSections.filter((s) => {
        if (seen.has(s.key)) return false;
        seen.add(s.key);
        return true;
    });

    // Only consider sections that are actually live AND stopped
    const active = unique.filter((s) =>
        s.hasLiveData !== false &&
        s.isStopped &&
        !(s.isGatedBySizer && !s.gateOpen)
    );

    if (active.length === 0) {
        el.classList.add("hidden");
        el.innerHTML = "";
        return;
    }

    const first = active[0];
    const extra = active.length > 1 ? ` (+${active.length - 1} more)` : "";
    el.classList.remove("hidden");
    el.innerHTML = `
        <span class="alert-icon">⚠️</span>
        <div>
            <div class="alert-title">${esc(first.displayName)}${esc(extra)}</div>
            <div class="alert-sub">${esc(first.currentCause || "Unknown cause")}</div>
        </div>
        <div class="alert-time">${fmtDur(first.activeStoppageSeconds)}</div>`;
}

/* ---------- Nav highlighting ---------- */
function renderNav() {
    $$("#nav a").forEach((a) => {
        a.classList.toggle("active", a.dataset.route === state.current);
    });
}

/* =========================================================
   PAGE RENDERERS
   ========================================================= */

/* -------- Dashboard -------- */
function renderDashboard() {
    const el = $("#page-dashboard");
    const o = state.overview;
    if (!o) { el.innerHTML = `<p class="empty">Loading…</p>`; return; }

    // ── Live-data flag for the entire snapshot ────────────────
    const live = o.hasLiveData !== false;

    const sizerBadge = live
        ? (o.sizerRunning
            ? `<span class="badge run">SIZER RUNNING</span>`
            : `<span class="badge warn">SIZER OFFLINE — LINE EVENTS GATED</span>`)
        : `<span class="badge" style="background:rgba(130,150,180,.15);color:var(--muted);">
              WAITING FOR PLC
           </span>`;

    // ── Top-line status KPI (respects live flag) ──────────────
    const statusKpi = !live
        ? `<div class="kpi-value" style="color:var(--muted);">NO DATA</div>
           <div class="kpi-sub">Waiting for PLC connection…</div>`
        : `<div class="kpi-value ${o.lineRunning ? "green" : "red"}">
               ${o.lineRunning ? "RUNNING" : "STOPPED"}
           </div>
           <div class="kpi-sub">${o.activeStoppages} active stoppage(s)</div>`;

    const kpiCards = `
    <div class="kpi-grid">
        <div class="kpi">
            <div class="kpi-label">Line Status</div>
            ${statusKpi}
        </div>
        <div class="kpi">
            <div class="kpi-label">Total Downtime</div>
            <div class="kpi-value">${fmtDur(o.totalDowntimeSeconds)}</div>
            <div class="kpi-sub">
                Planned ${fmtDur(o.plannedDowntimeSeconds)} ·
                Unplanned ${fmtDur(o.unplannedDowntimeSeconds)}
            </div>
        </div>
        <div class="kpi">
            <div class="kpi-label">Packaging Lines</div>
            <div class="kpi-value">${o.lines.length}</div>
            <div class="kpi-sub">${o.lines.filter((l) => l.isRunning && !l.isGated).length} running</div>
        </div>
        <div class="kpi">
            <div class="kpi-label">Machines</div>
            <div class="kpi-value">${o.machines.length}</div>
            <div class="kpi-sub">${o.machines.filter((m) => m.isRunning && !m.isGated).length} running</div>
        </div>
        <div class="kpi">
            <div class="kpi-label">Production Gate</div>
            <div class="kpi-value ${!live ? "" : o.sizerRunning ? "green" : "amber"}">
                ${!live ? "—" : (o.sizerRunning ? "OPEN" : "CLOSED")}
            </div>
            <div class="kpi-sub">${sizerBadge}</div>
        </div>
    </div>`;

    // Shared card renderer — now with NO DATA branch
    function renderLineLikeCard(l) {
        const cardLive = l.hasLiveData !== false;

        if (!cardLive) {
            return `
                <div class="line-card" style="opacity:.55;"
                     onclick="navigate('${escAttr(l.key)}')">
                    <div class="line-top">
                        <div>
                            <div class="line-name">${esc(l.displayName)}</div>
                            <div class="line-desc">${esc(l.description)}</div>
                        </div>
                        <span class="badge"
                              style="background:rgba(130,150,180,.15);color:var(--muted);">
                            NO DATA
                        </span>
                    </div>
                    <div class="line-stats">
                        <span>Stations: <strong>${l.totalStations}</strong></span>
                        <span>Waiting for PLC connection…</span>
                    </div>
                </div>`;
        }

        const icon = l.key === "tipper" ? "🏗️ " : "";
        const badge = l.isGated
            ? `<span class="badge warn">GATED</span>`
            : l.isRunning
                ? `<span class="badge run">RUNNING</span>`
                : `<span class="badge stop">${l.activeStops} STOP</span>`;

        return `
            <div class="line-card" onclick="navigate('${escAttr(l.key)}')">
                <div class="line-top">
                    <div>
                        <div class="line-name">${icon}${esc(l.displayName)}</div>
                        <div class="line-desc">${esc(l.description)}</div>
                    </div>
                    ${badge}
                </div>
                <div class="line-stats">
                    <span>Stations: <strong>${l.totalStations}</strong></span>
                    <span>Downtime: <strong>${fmtDur(l.totalDowntimeSeconds)}</strong></span>
                    <span>Unplanned: <strong>${fmtDur(l.unplannedDowntimeSeconds)}</strong></span>
                </div>
            </div>`;
    }

    const lineCards = o.lines.map(renderLineLikeCard).join("");
    const machineCards = o.machines.map(renderLineLikeCard).join("");
    const globalCards = o.globalSections.map(renderStationCard).join("");

    el.innerHTML = `
    ${kpiCards}

    <h2 class="section-title">Packaging Lines</h2>
    <div class="line-grid">${lineCards}</div>

    <h2 class="section-title">Machines</h2>
    <div class="line-grid">${machineCards}</div>

    <h2 class="section-title">Global — Safety & Network</h2>
    <div class="station-grid">${globalCards}</div>`;
}

/* -------- Line detail -------- */
function renderLine(lineKey) {
    const el = $(`#page-${lineKey}`);
    const data = state.lines[lineKey];
    if (!data) { el.innerHTML = `<p class="empty">Loading…</p>`; return; }

    const l = data.line;
    const live = l.hasLiveData !== false;
    const gated = l.isGated && live;

    const gateBanner = gated
        ? `<div class="alert-banner"
                  style="background:var(--amber-bg);border-left-color:var(--amber);animation:none;">
             <span class="alert-icon">⏸</span>
             <div>
               <div class="alert-title" style="color:var(--amber);">
                 Sizer is offline — production gate closed
               </div>
               <div class="alert-sub">
                 Line stoppages on this screen are not being counted until the sizer resumes.
               </div>
             </div>
           </div>` : "";

    const noDataBanner = !live
        ? `<div class="alert-banner"
                  style="background:rgba(130,150,180,.12);border-left-color:var(--muted);animation:none;">
             <span class="alert-icon">📡</span>
             <div>
               <div class="alert-title" style="color:var(--muted);">
                 No PLC data
               </div>
               <div class="alert-sub">
                 Waiting for the first successful poll from ${esc(l.displayName)}.
               </div>
             </div>
           </div>` : "";

    const cards = data.sections.map(renderStationCard).join("");

    const statusText = !live ? "NO DATA" : gated ? "GATED" : l.isRunning ? "RUNNING" : "STOPPED";
    const statusClass = !live ? "" : gated ? "amber" : l.isRunning ? "green" : "red";

    el.innerHTML = `
        ${noDataBanner}
        ${gateBanner}
        <div class="kpi-grid">
            <div class="kpi">
                <div class="kpi-label">${esc(l.displayName)} Status</div>
                <div class="kpi-value ${statusClass}">${statusText}</div>
                <div class="kpi-sub">${esc(l.description)}</div>
            </div>
            <div class="kpi">
                <div class="kpi-label">Active Stops</div>
                <div class="kpi-value ${l.activeStops > 0 && live ? "red" : ""}">
                    ${live ? l.activeStops : "—"}
                </div>
                <div class="kpi-sub">of ${l.totalStations} stations</div>
            </div>
            <div class="kpi">
                <div class="kpi-label">Total Downtime — ${esc(l.displayName)}</div>
                <div class="kpi-value">${fmtDur(l.totalDowntimeSeconds)}</div>
                <div class="kpi-sub">
                    Planned ${fmtDur(l.plannedDowntimeSeconds)} ·
                    Unplanned ${fmtDur(l.unplannedDowntimeSeconds)}
                </div>
            </div>
        </div>

        <h2 class="section-title">Stations</h2>
        <div class="station-grid">${cards}</div>`;
}

/* -------- Box Tipper -------- */
function renderTipper() {
    const el = $("#page-tipper");
    const data = state.tipper;
    if (!data) { el.innerHTML = `<p class="empty">Loading…</p>`; return; }

    const l = data.line;
    const live = l.hasLiveData !== false;
    const gated = l.isGated && live;
    const cards = data.sections.map(renderStationCard).join("");

    const statusText = !live ? "NO DATA" : gated ? "GATED" : l.isRunning ? "RUNNING" : "STOPPED";
    const statusClass = !live ? "" : gated ? "amber" : l.isRunning ? "green" : "red";

    el.innerHTML = `
        <div class="kpi-grid">
            <div class="kpi">
                <div class="kpi-label">Tipper Status</div>
                <div class="kpi-value ${statusClass}">${statusText}</div>
                <div class="kpi-sub">${esc(l.description)}</div>
            </div>
            <div class="kpi">
                <div class="kpi-label">Active Stops</div>
                <div class="kpi-value ${l.activeStops > 0 && live ? "red" : ""}">
                    ${live ? l.activeStops : "—"}
                </div>
                <div class="kpi-sub">of ${l.totalStations} monitored blocks</div>
            </div>
            <div class="kpi">
                <div class="kpi-label">Total Downtime — Tipper</div>
                <div class="kpi-value">${fmtDur(l.totalDowntimeSeconds)}</div>
                <div class="kpi-sub">
                    Planned ${fmtDur(l.plannedDowntimeSeconds)} ·
                    Unplanned ${fmtDur(l.unplannedDowntimeSeconds)}
                </div>
            </div>
        </div>

        <h2 class="section-title">Movement, Pusher, Rotation & Safety</h2>
        <div class="station-grid">${cards}</div>`;
}

/* -------- Shared station card -------- */
function renderStationCard(s) {
    const live = s.hasLiveData !== false;
    const active = s.isStopped && live;
    const gated = s.isGatedBySizer && !s.gateOpen;

    // ═══ NO DATA state ════════════════════════════════════════
    if (!live) {
        return `
            <div class="station" style="opacity:.55;">
                <div class="st-top">
                    <div class="st-name">${esc(s.displayName)}</div>
                    <span class="badge"
                          style="background:rgba(130,150,180,.15);color:var(--muted);">
                        NO DATA
                    </span>
                </div>
                <div class="st-desc">${esc(s.description)}</div>
                <div class="st-timer" style="color:var(--muted);">--:--:--</div>
                <div class="st-cause" style="color:var(--muted);">
                    Waiting for PLC connection…
                </div>
                <div class="st-foot">
                    <span>Total: ${fmtDur(s.totalDowntimeSeconds)}</span>
                    <span>Stops: ${s.stoppageCount}</span>
                </div>
            </div>`;
    }

    // ═══ Live state ═══════════════════════════════════════════
    const timer = active && !gated ? fmtDur(s.activeStoppageSeconds) : "00:00:00";

    const categoryBadge = active && s.operatorReason
        ? `<span class="badge warn">${esc(s.category)}</span>` : "";

    const reasonBtn = active && !gated && s.activeEventId
        ? `<button type="button" class="set-reason-btn"
                   onclick="openReasonModal(${s.activeEventId}, '${escAttr(s.displayName)}')">
               ${s.operatorReason ? "Change reason" : "Set reason…"}
           </button>` : "";

    const body = gated
        ? `<div class="st-cause">⚠ Sizer offline — stoppages not counted</div>`
        : `<div class="st-cause">${active ? "⚠ " + esc(s.currentCause || "Unknown cause") : ""}</div>
           ${categoryBadge}
           ${reasonBtn}`;

    return `
        <div class="station ${active && !gated ? "stopped" : ""}">
            <div class="st-top">
                <div class="st-name">${esc(s.displayName)}</div>
                <span class="badge ${gated ? "warn" : active ? "stop" : "run"}">
                    ${gated ? "GATED" : active ? "STOP" : "RUN"}
                </span>
            </div>
            <div class="st-desc">${esc(s.description)}</div>
            <div class="st-timer">${timer}</div>
            ${body}
            <div class="st-foot">
                <span>Total: ${fmtDur(s.totalDowntimeSeconds)}</span>
                <span>Stops: ${s.stoppageCount}</span>
            </div>
        </div>`;
}

/* -------- History -------- */
function renderHistory() {
    const el = $("#page-history");
    const events = state.history;
    const filters = state.historyFilters;
    const lines = state.overview?.lines || [];

    const lineOptions = `<option value="">All lines</option>` +
        lines.map((l) => `<option value="${esc(l.key)}"
            ${filters.line === l.key ? "selected" : ""}>${esc(l.displayName)}</option>`).join("");

    const categoryOptions = ["", "Planned", "Unplanned", "Neutral", "Untagged"]
        .map((c) => `<option value="${c}" ${filters.category === c ? "selected" : ""}>
                        ${c || "All categories"}
                     </option>`).join("");

    const rows = !events || events.length === 0
        ? `<tr><td colspan="6" class="empty">No stoppage events yet.</td></tr>`
        : events.map((h) => {
            const duration = fmtDur(
                h.durationSeconds ||
                (h.isActive ? (Date.now() - new Date(h.startedAt)) / 1000 : 0)
            );
            const catBadge = h.category && h.category !== "Untagged"
                ? `<span class="badge warn">${esc(h.category)}</span>` : "";
            const reasonTxt = h.operatorReason
                ? `<div style="font-size:.75rem;color:var(--muted);">${esc(h.operatorReason)}</div>` : "";
            return `
                <tr class="${h.isActive ? "active" : ""}">
                    <td>${esc(h.sectionName)}</td>
                    <td class="num">${fmtDate(h.startedAt)}</td>
                    <td class="num">${h.endedAt ? fmtDate(h.endedAt) : '<span class="badge warn">active</span>'}</td>
                    <td class="num">${duration}</td>
                    <td>${esc(h.cause || "—")}</td>
                    <td>${catBadge}${reasonTxt}</td>
                </tr>`;
        }).join("");

    el.innerHTML = `
        <h2 class="section-title">Filter Events</h2>
        <form class="filters" onsubmit="applyHistoryFilters(event)">
            <label>Line
                <select name="line">${lineOptions}</select>
            </label>
            <label>Category
                <select name="category">${categoryOptions}</select>
            </label>
            <label>From
                <input type="date" name="from" value="${esc(filters.from)}" />
            </label>
            <label>To
                <input type="date" name="to" value="${esc(filters.to)}" />
            </label>
            <label>Search
                <input type="text" name="section" placeholder="section key or name"
                       value="${esc(filters.section)}" />
            </label>
            <button type="submit">Apply</button>
            <button type="button" onclick="clearHistoryFilters()"
                    style="background:var(--panel-2);">Clear</button>
        </form>

        <div class="table-wrap">
            <table class="events">
                <thead>
                    <tr>
                        <th>Station</th><th>Started</th><th>Ended</th>
                        <th>Duration</th><th>Cause</th><th>Operator</th>
                    </tr>
                </thead>
                <tbody>${rows}</tbody>
            </table>
        </div>`;
}

function applyHistoryFilters(evt) {
    evt.preventDefault();
    const f = evt.target;
    state.historyFilters = {
        line: f.line.value,
        category: f.category.value,
        from: f.from.value,
        to: f.to.value,
        section: f.section.value,
    };
    refreshHistory();
}

function clearHistoryFilters() {
    state.historyFilters = { section: "", line: "", category: "", from: "", to: "" };
    refreshHistory();
}

/* =========================================================
   DATA REFRESH
   ========================================================= */
async function refreshOverview() {
    try { state.overview = await api("/api/overview"); state.error = null; }
    catch (e) { state.error = e.message; }
}
async function refreshLine(lineKey) {
    try { state.lines[lineKey] = await api(`/api/lines/${lineKey}`); } catch (e) { }
}
async function refreshTipper() {
    try { state.tipper = await api("/api/tipper"); } catch (e) { }
}
async function refreshHistory() {
    try {
        const params = new URLSearchParams();
        if (state.historyFilters.line) params.set("line", state.historyFilters.line);
        if (state.historyFilters.category) params.set("category", state.historyFilters.category);
        if (state.historyFilters.from) params.set("from", state.historyFilters.from);
        if (state.historyFilters.to) params.set("to", state.historyFilters.to);
        params.set("limit", "500");
        state.history = await api(`/api/history?${params.toString()}`);
    } catch (e) { }
}

async function refreshCurrent() {
    if (state.current === "dashboard") {
        await refreshOverview();
    } else if (state.current === "line1" || state.current === "line2" || state.current === "line3") {
        await Promise.all([refreshOverview(), refreshLine(state.current)]);
    } else if (state.current === "tipper") {
        await Promise.all([refreshOverview(), refreshTipper()]);
    } else if (state.current === "history") {
        if (!state.overview) await refreshOverview();
        await refreshHistory();
    }
}

/* =========================================================
   MAIN RENDER
   ========================================================= */
function render() {
    ROUTES.forEach((r) => {
        const el = $(`#page-${r}`);
        if (el) el.classList.toggle("hidden", r !== state.current);
    });

    renderNav();
    renderAlertBanner();

    switch (state.current) {
        case "dashboard": renderDashboard(); break;
        case "line1":
        case "line2":
        case "line3": renderLine(state.current); break;
        case "tipper": renderTipper(); break;
        case "history": renderHistory(); break;
    }

    const conn = $("#conn");
    if (conn) {
        conn.textContent = state.error
            ? `⚠ ${state.error}`
            : `Connected · updated ${new Date().toLocaleTimeString()}`;
    }
}

function tickClock() {
    const el = $("#clock");
    if (el) el.textContent = new Date().toLocaleString();
}

/* =========================================================
   BOOT
   ========================================================= */
state.current = currentRoute();
render();
tickClock();
refreshCurrent();

setInterval(async () => {
    await refreshCurrent();
    render();
}, 1000);

setInterval(tickClock, 1000);

window.navigate = navigate;
window.applyHistoryFilters = applyHistoryFilters;
window.clearHistoryFilters = clearHistoryFilters;