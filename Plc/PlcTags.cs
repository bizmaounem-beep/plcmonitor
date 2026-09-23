namespace PlcMonitor.Plc;

/// <summary>Omron FINS memory area codes.</summary>
public static class FinsArea
{
    public const byte CIO = 0xB0;
    public const byte W = 0xB1;
    public const byte D = 0x82;
    public const byte E = 0xA0; // bank 0
}

/// <summary>A single PLC tag with its address and bit position.</summary>
public readonly record struct PlcTag(byte Area, ushort Address, byte Bit)
{
    public static PlcTag W(int word, int bit) => new(FinsArea.W, (ushort)word, (byte)bit);
    public static PlcTag D(int word) => new(FinsArea.D, (ushort)word, 0);
    public static PlcTag CIO(int word, int bit) => new(FinsArea.CIO, (ushort)word, (byte)bit);
    public static PlcTag E(int word, int bit) => new(FinsArea.E, (ushort)word, (byte)bit);
}

/// <summary>
/// All tags relevant for line monitoring, grouped by section.
/// Addresses are taken directly from the PLC program (PDF).
/// </summary>
public static class PlcTags
{
    // ── Machine groups (Main > Start) ────────────────────────────────
    public static readonly PlcTag Gr1Run = PlcTag.W(211, 3);   // W211.03
    public static readonly PlcTag Gr1Def = PlcTag.W(211, 0);   // W211.00
    public static readonly PlcTag Gr1Ini = PlcTag.W(211, 5);   // W211.05
    public static readonly PlcTag Gr1Stop = PlcTag.W(211, 4);  // W211.04

    public static readonly PlcTag Gr2Run = PlcTag.W(212, 3);   // W212.03
    public static readonly PlcTag Gr2Def = PlcTag.W(212, 0);   // W212.00
    public static readonly PlcTag Gr2Ini = PlcTag.W(212, 5);   // W212.05
    public static readonly PlcTag Gr2Stop = PlcTag.W(212, 4);  // W212.04

    public static readonly PlcTag Gr3Run = PlcTag.W(213, 3);   // W213.03 — Sizer
    public static readonly PlcTag Gr3Def = PlcTag.W(213, 0);   // W213.00
    public static readonly PlcTag Gr3Ini = PlcTag.W(213, 5);   // W213.05
    public static readonly PlcTag Gr3Stop = PlcTag.W(213, 4);  // W213.04

    public static readonly PlcTag Gr4Run = PlcTag.W(214, 3);   // W214.03 — Box tipper
    public static readonly PlcTag Gr4Def = PlcTag.W(214, 0);   // W214.00
    public static readonly PlcTag Gr4Ini = PlcTag.W(214, 5);   // W214.05
    public static readonly PlcTag Gr4Stop = PlcTag.W(214, 4);  // W214.04

    // ── Emergency stops (Main > Emergency_stops) ─────────────────────
    public static readonly PlcTag SafetyOk = PlcTag.W(99, 4);     // W099.04
    public static readonly PlcTag EStopS1 = PlcTag.W(3, 4);       // W003.04
    public static readonly PlcTag EStopSel = PlcTag.W(3, 5);      // W003.05
    public static readonly PlcTag Ms1Active = PlcTag.W(0, 0);     // W000.00
    public static readonly PlcTag Ms2Active = PlcTag.W(0, 1);     // W000.01
    public static readonly PlcTag H4EStopRed = PlcTag.W(101, 2);  // W101.02
    public static readonly PlcTag H5EStopGreen = PlcTag.W(101, 3);// W101.03
    public static readonly PlcTag H6LineStart = PlcTag.W(101, 4); // W101.04
    public static readonly PlcTag H7GeneralFault = PlcTag.W(101, 5); // W101.05

    // ── Sizer stoppage causes (Communication > Sizer_outputs) ────────
    public static readonly PlcTag DelicateFruit = PlcTag.W(265, 0);   // W265.00
    public static readonly PlcTag StopIndisposedOutlet = PlcTag.W(265, 1); // W265.01
    public static readonly PlcTag FruitOnReturn = PlcTag.W(265, 2);   // W265.02
    public static readonly PlcTag OrpheaStopSizer = PlcTag.W(265, 3); // W265.03
    public static readonly PlcTag StopSaturation = PlcTag.W(265, 4);  // W265.04
    public static readonly PlcTag StopOutputsError = PlcTag.W(265, 5);// W265.05
    public static readonly PlcTag StopMaxReturn = PlcTag.W(265, 7);   // W265.07
    public static readonly PlcTag StopFeedMaxReturn = PlcTag.W(180, 0); // W180.00
    public static readonly PlcTag Stop1 = PlcTag.W(197, 0);           // W197.00
    public static readonly PlcTag Stop2 = PlcTag.W(197, 1);           // W197.01
    public static readonly PlcTag Stop3 = PlcTag.W(197, 2);           // W197.02
    public static readonly PlcTag StopSequential = PlcTag.W(197, 3);  // W197.03

    // ── Communication faults ─────────────────────────────────────────
    public static readonly PlcTag FaultSupCom = PlcTag.W(99, 2);     // W099.02
    public static readonly PlcTag FaultOrpheaBal = PlcTag.W(99, 7);  // W099.07
    public static readonly PlcTag FaultOrphea = PlcTag.W(99, 9);     // W099.09
    public static readonly PlcTag MasterEipOk = PlcTag.E(1001, 1);   // E0_01001.01

    // ── Full box exits (Communication > Datalink) ────────────────────
    public static readonly PlcTag Filler1Life = PlcTag.E(15000, 15); // E0_15000.15
    public static readonly PlcTag Filler1OutLife = PlcTag.E(17000, 15);
    public static readonly PlcTag Filler2Life = PlcTag.E(15100, 15);
    public static readonly PlcTag Filler2OutLife = PlcTag.E(17100, 15);
    public static readonly PlcTag Filler3Life = PlcTag.E(15200, 15);
    public static readonly PlcTag Filler3OutLife = PlcTag.E(17200, 15);

    // ── Labelling AE107 (Main > Input_Conv) ──────────────────────────
    public static readonly PlcTag Ae107Safety = PlcTag.W(40, 0);      // W040.00
    public static readonly PlcTag Ae107Vf1 = PlcTag.W(40, 1);         // W040.01
    public static readonly PlcTag Ae107Vf2 = PlcTag.W(40, 2);
    public static readonly PlcTag Ae107Vf3 = PlcTag.W(40, 3);
    public static readonly PlcTag Ae107Vf4 = PlcTag.W(40, 4);
    public static readonly PlcTag Ae107Vf5 = PlcTag.W(40, 5);
    public static readonly PlcTag Ae107Vf6 = PlcTag.W(40, 6);
    public static readonly PlcTag Ae107Therm1 = PlcTag.W(40, 7);      // W040.07
    public static readonly PlcTag Ae107Therm2 = PlcTag.W(40, 8);
    public static readonly PlcTag Ae107Therm3 = PlcTag.W(40, 9);
    public static readonly PlcTag Ae107DefectC = PlcTag.W(41, 3);     // W041.03
    public static readonly PlcTag Ae107DefectB = PlcTag.W(41, 5);     // W041.05
    public static readonly PlcTag Ae107DefectA = PlcTag.W(41, 7);     // W041.07
    public static readonly PlcTag Ae107RedPilot = PlcTag.W(140, 0);   // W140.00
    public static readonly PlcTag Ae107M7 = PlcTag.W(141, 0);         // W141.00
    public static readonly PlcTag Ae107M8 = PlcTag.W(141, 1);
    public static readonly PlcTag Ae107M9 = PlcTag.W(141, 2);

    // ── Box tipper AE100 (Box_tipper program) ────────────────────────
    public static readonly PlcTag TipperAuto = PlcTag.E(10, 0);       // E0_10.00
    public static readonly PlcTag TipperManual = PlcTag.E(10, 1);     // E0_10.01
    public static readonly PlcTag TipperS1Start = PlcTag.E(0, 8);     // E0_00000.08
    public static readonly PlcTag TipperS2Stop = PlcTag.E(0, 9);      // E0_00000.09
    public static readonly PlcTag TipperRunCycle = PlcTag.E(452, 0);  // E0_00452.00
    public static readonly PlcTag TipperRedPilot = PlcTag.W(111, 0);  // W111.00
    public static readonly PlcTag TipperGreenPilot = PlcTag.W(111, 1);// W111.01
    public static readonly PlcTag TipperBeacon = PlcTag.W(111, 2);    // W111.02

    // ── System alarms (W415) ─────────────────────────────────────────
    public static readonly PlcTag AlarmNoOutlet = PlcTag.W(415, 0);   // W415.00
    public static readonly PlcTag AlarmNoFruitAttr = PlcTag.W(415, 1);// W415.01
    public static readonly PlcTag AlarmSeqStop = PlcTag.W(415, 2);    // W415.02
    public static readonly PlcTag AlarmOilLevel = PlcTag.W(415, 5);   // W415.05
    public static readonly PlcTag AlarmOilPressure = PlcTag.W(415, 6);// W415.06
    public static readonly PlcTag AlarmPanelEStop = PlcTag.W(415, 7);// W415.07
    public static readonly PlcTag AlarmTipperActive = PlcTag.W(415, 9); // W415.09
    public static readonly PlcTag AlarmMaxReturn = PlcTag.W(415, 10); // W415.10
    public static readonly PlcTag AlarmOrpheaCom = PlcTag.W(415, 11);// W415.11
    public static readonly PlcTag AlarmBalanceCom = PlcTag.W(415, 12);// W415.12

    // ── Production data (Box_tipper > Comunicacion) ──────────────────
    public static readonly PlcTag CyclesPerMin = PlcTag.D(2050);      // D2050
    public static readonly PlcTag CyclesPerHour = PlcTag.D(2052);     // D2052
    public static readonly PlcTag PartialCycles = PlcTag.D(2058);     // D2058
}