using Microsoft.Extensions.Options;
using PlcMonitor.Plc;

namespace PlcMonitor.Monitoring;

public sealed class PlcOptions
{
    public string IpAddress { get; set; } = "192.168.1.99";
    public int Port { get; set; } = 9600;
    public byte DestNode { get; set; } = 0;
    public byte SrcNode { get; set; } = 0;
    public int PollIntervalMs { get; set; } = 500;
}

public sealed class MonitoringService : BackgroundService
{
    private readonly StoppageDetector _detector;
    private readonly PlcOptions _options;
    private readonly ILogger<MonitoringService> _log;
    private FinsClient? _fins;

    /// <summary>UTC timestamp of the most recent successful poll.</summary>
    public static DateTime? LastSuccessfulPollUtc { get; private set; }

    public MonitoringService(
        StoppageDetector detector,
        IOptions<PlcOptions> options,
        ILogger<MonitoringService> log)
    {
        _detector = detector;
        _options = options.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Monitoring service starting. PLC {Ip}:{Port}",
            _options.IpAddress, _options.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _fins ??= new FinsClient(_options.IpAddress, _options.Port,
                                          _options.DestNode, _options.SrcNode);

                var runBits = new Dictionary<string, bool>();
                var stoppedBits = new Dictionary<string, bool>();
                var causes = new Dictionary<string, string>();

                foreach (var section in SectionCatalog.All)
                {
                    // ── Read the run bit ──────────────────────────────
                    var run = await ReadTagAsync(_fins, section.RunBit);
                    runBits[section.Key] = run;

                    // A stoppage is simply "not running".
                    stoppedBits[section.Key] = !run;

                    // ── Determine the active cause ────────────────────
                    string cause = "";
                    foreach (var ct in section.Causes)
                    {
                        if (await ReadTagAsync(_fins, ct.Tag))
                        {
                            cause = ct.Cause;
                            break;
                        }
                    }
                    causes[section.Key] = cause;
                }

                _detector.ProcessSnapshot(runBits, stoppedBits, causes);

                // ⬇ Mark this poll as fresh so the UI knows data is live
                LastSuccessfulPollUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "PLC poll failed; will retry.");
                _fins?.Dispose();
                _fins = null;
            }

            await Task.Delay(_options.PollIntervalMs, stoppingToken);
        }
    }

    /// <summary>
    /// Reads a single bit from a PLC tag. Handles E-area bank addressing.
    /// </summary>
    private static async Task<bool> ReadTagAsync(FinsClient fins, PlcTag tag)
    {
        if (tag.Area == FinsArea.E && tag.Address >= 0x8000)
        {
            // E-area bank: E0_xxxxx uses bank 0, address & 0x7FFF
            var addr = (ushort)(tag.Address & 0x7FFF);
            return await fins.ReadBitAsync(FinsArea.E, addr, tag.Bit);
        }
        return await fins.ReadBitAsync(tag.Area, tag.Address, tag.Bit);
    }
}