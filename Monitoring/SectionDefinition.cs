using PlcMonitor.Plc;

namespace PlcMonitor.Monitoring;

// ═══════════════════════════════════════════════════════════════
// Data types
// ═══════════════════════════════════════════════════════════════

public sealed record CauseTag(PlcTag Tag, string Cause);

public sealed record LineDef(string Key, string DisplayName, string Description);

public sealed record SectionDef(
    string Key,
    string LineKey,
    string Category,
    string DisplayName,
    string Description,
    PlcTag RunBit,
    PlcTag StoppedBit,
    bool IsGatedBySizer,
    IReadOnlyList<CauseTag> Causes);

// ═══════════════════════════════════════════════════════════════
// Catalog
// ═══════════════════════════════════════════════════════════════

public static class SectionCatalog
{
    public static readonly IReadOnlyList<LineDef> Lines = new[]
    {
        new LineDef("line1", "Line 1", "Milbor Line C / Filler 1 / Labelling AE107-1"),
        new LineDef("line2", "Line 2", "Milbor Line B / Filler 2 / Labelling AE107-2"),
        new LineDef("line3", "Line 3", "Milbor Line A / Filler 3 / Labelling AE107-3"),
    };

    public static readonly IReadOnlyList<LineDef> Machines = new[]
    {
        new LineDef("tipper", "Box Tipper (Volcador / AE100)",
                              "Crate infeed and box tipper unit"),
    };

    public static readonly IReadOnlyList<SectionDef> All = new SectionDef[]
    {
        // ── LINE 1 ────────────────────────────────────────────
        new("filler1", "line1", "Filler", "Filler 1 (Llenadora 1)",
            "Milbor Line C — full box exit 1",
            PlcTags.Filler1Life, PlcTags.Filler1OutLife, true,
            new[] { new CauseTag(PlcTags.Filler1OutLife, "Filler 1 output stopped") }),

        new("labelling_c", "line1", "Labelling", "Labelling Line C",
            "Étiqueteuse — Milbor #3,4,5 Ligne C",
            PlcTags.Ae107M7, PlcTags.Ae107DefectC, true,
            new[]
            {
                new CauseTag(PlcTags.Ae107DefectC,  "Signal défaut — Ligne C (Milbor #3,4,5)"),
                new CauseTag(PlcTags.Ae107Vf4,      "VF4 tapis étiqueteuse 1 en défaut"),
                new CauseTag(PlcTags.Ae107Therm1,   "Défaut thermique — Boîtes emballées 1"),
                new CauseTag(PlcTags.Ae107Safety,   "Module sécurité AE107 actif"),
                new CauseTag(PlcTags.Ae107RedPilot, "Voyant rouge — défaut machine AE107"),
            }),

        new("conveyor_m7", "line1", "Conveyor", "Conveyor M7",
            "Tapis sortie boîtes emballées — étiqueteuse 1",
            PlcTags.Ae107M7, PlcTags.Ae107M7, true,
            new[] { new CauseTag(PlcTags.Ae107Therm1, "Défaut thermique boîtes emballées 1") }),

        // ── LINE 2 ────────────────────────────────────────────
        new("filler2", "line2", "Filler", "Filler 2 (Llenadora 2)",
            "Milbor Line B — full box exit 2",
            PlcTags.Filler2Life, PlcTags.Filler2OutLife, true,
            new[] { new CauseTag(PlcTags.Filler2OutLife, "Filler 2 output stopped") }),

        new("labelling_b", "line2", "Labelling", "Labelling Line B",
            "Étiqueteuse — Milbor #3,4,5 Ligne B",
            PlcTags.Ae107M8, PlcTags.Ae107DefectB, true,
            new[]
            {
                new CauseTag(PlcTags.Ae107DefectB, "Signal défaut — Ligne B (Milbor #3,4,5)"),
                new CauseTag(PlcTags.Ae107Vf5,     "VF5 tapis étiqueteuse 2 en défaut"),
                new CauseTag(PlcTags.Ae107Therm2,  "Défaut thermique — Boîtes emballées 2"),
                new CauseTag(PlcTags.Ae107Safety,  "Module sécurité AE107 actif"),
            }),

        new("conveyor_m8", "line2", "Conveyor", "Conveyor M8",
            "Tapis sortie boîtes emballées — étiqueteuse 2",
            PlcTags.Ae107M8, PlcTags.Ae107M8, true,
            new[] { new CauseTag(PlcTags.Ae107Therm2, "Défaut thermique boîtes emballées 2") }),

        // ── LINE 3 ────────────────────────────────────────────
        new("filler3", "line3", "Filler", "Filler 3 (Llenadora 3)",
            "Milbor Line A — full box exit 3",
            PlcTags.Filler3Life, PlcTags.Filler3OutLife, true,
            new[] { new CauseTag(PlcTags.Filler3OutLife, "Filler 3 output stopped") }),

        new("labelling_a", "line3", "Labelling", "Labelling Line A",
            "Étiqueteuse — Milbor #5 Ligne A",
            PlcTags.Ae107M9, PlcTags.Ae107DefectA, true,
            new[]
            {
                new CauseTag(PlcTags.Ae107DefectA, "Signal défaut — Ligne A (Milbor #5)"),
                new CauseTag(PlcTags.Ae107Vf6,     "VF6 tapis étiqueteuse 3 en défaut"),
                new CauseTag(PlcTags.Ae107Therm3,  "Défaut thermique — Boîtes emballées 3"),
                new CauseTag(PlcTags.Ae107Safety,  "Module sécurité AE107 actif"),
            }),

        new("conveyor_m9", "line3", "Conveyor", "Conveyor M9",
            "Tapis sortie boîtes emballées — étiqueteuse 3",
            PlcTags.Ae107M9, PlcTags.Ae107M9, true,
            new[] { new CauseTag(PlcTags.Ae107Therm3, "Défaut thermique boîtes emballées 3") }),

        // ── BOX TIPPER — not gated ────────────────────────────
        new("tipper_main", "tipper", "Tipper", "Box Tipper (Volcador)",
            "Groupe 4 — cycle complet",
            PlcTags.Gr4Run, PlcTags.Gr4Def, false,
            new[]
            {
                new CauseTag(PlcTags.AlarmPanelEStop,    "Bouton d'urgence panneau activé"),
                new CauseTag(PlcTags.AlarmTipperActive,  "Alarme active dans le basculeur"),
                new CauseTag(PlcTags.AlarmNoFruitAttr,   "Aucune attribution de fruit"),
                new CauseTag(PlcTags.AlarmMaxReturn,     "Maximum de fruits en retour"),
                new CauseTag(PlcTags.AlarmNoOutlet,      "Aucune sortie sélectionnée"),
            }),

        new("tipper_ae100_faults", "tipper", "Safety", "Tipper Safety & Faults (AE100)",
            "Interlocks et défauts thermiques AE100",
            PlcTags.TipperAuto, PlcTags.TipperRedPilot, false,
            new[]
            {
                new CauseTag(PlcTags.TipperRedPilot, "Voyant rouge — défaut machine AE100"),
                new CauseTag(PlcTags.TipperBeacon,   "Balise lumineuse — défaut machine"),
                new CauseTag(PlcTags.TipperS2Stop,   "S2 bouton noir arrêt machine"),
                new CauseTag(PlcTags.TipperRunCycle, "Cycle interrompu"),
            }),

        // ── SIZER — the production gate ───────────────────────
        new("sizer", "sizer", "Sizer", "Sizer / Calibrator",
            "Groupe 3 — calibreur et pré-aligneurs (production gate)",
            PlcTags.Gr3Run, PlcTags.Gr3Def, false,
            new[]
            {
                new CauseTag(PlcTags.DelicateFruit,        "Fruit délicat — saturation sorties"),
                new CauseTag(PlcTags.StopIndisposedOutlet, "Sortie indisposée"),
                new CauseTag(PlcTags.FruitOnReturn,        "Fruit en retour — sortie indisposée"),
                new CauseTag(PlcTags.OrpheaStopSizer,      "Orphea demande l'arrêt"),
                new CauseTag(PlcTags.StopSaturation,       "Saturation — sorties en erreur"),
                new CauseTag(PlcTags.StopOutputsError,     "Sorties en erreur ou saturées"),
                new CauseTag(PlcTags.StopMaxReturn,        "Max fruits en retour"),
                new CauseTag(PlcTags.StopFeedMaxReturn,    "Alimentation arrêtée — max retour"),
                new CauseTag(PlcTags.Stop1,                "Arrêt 1 actif"),
                new CauseTag(PlcTags.Stop2,                "Arrêt 2 actif"),
                new CauseTag(PlcTags.Stop3,                "Arrêt 3 actif"),
            }),

        // ── GLOBAL — never gated ──────────────────────────────
        new("emergency", "global", "Safety", "Emergency Stop Chain",
            "Chaîne d'arrêt d'urgence globale",
            PlcTags.SafetyOk, PlcTags.SafetyOk, false,
            new[]
            {
                new CauseTag(PlcTags.EStopS1,         "S1 Arrêt d'urgence activé (tableau)"),
                new CauseTag(PlcTags.Ms1Active,       "Module sécurité MS1 activé"),
                new CauseTag(PlcTags.Ms2Active,       "Module sécurité MS2 activé"),
                new CauseTag(PlcTags.H4EStopRed,      "H4 voyant rouge — arrêt d'urgence"),
                new CauseTag(PlcTags.AlarmPanelEStop, "Bouton d'urgence panneau actif"),
                new CauseTag(PlcTags.AlarmSeqStop,    "Arrêt séquentiel activé par superviseur"),
            }),

        new("master_com", "global", "Network", "Master EIP Network",
            "Communication EtherNet/IP master",
            PlcTags.MasterEipOk, PlcTags.MasterEipOk, false,
            new[]
            {
                new CauseTag(PlcTags.FaultSupCom,     "Défaut communication superviseur → PLC"),
                new CauseTag(PlcTags.FaultOrphea,     "Erreur communication Orphea"),
                new CauseTag(PlcTags.FaultOrpheaBal,  "Erreur communication balance"),
                new CauseTag(PlcTags.AlarmOrpheaCom,  "Erreur communication Orphea (alarme)"),
                new CauseTag(PlcTags.AlarmBalanceCom, "Erreur communication balance (alarme)"),
            }),
    };

    public static SectionDef? Get(string key) =>
        All.FirstOrDefault(s => s.Key == key);
}