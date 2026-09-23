using System.Net;
using System.Net.Sockets;

namespace PlcMonitor.Plc;

/// <summary>
/// Minimal FINS/UDP client for reading Omron PLC memory areas.
/// Supports: CIO (0xB0), W (0xB1), D (0x82), E (0xA0 with bank).
/// </summary>
public sealed class FinsClient : IDisposable
{
    private readonly UdpClient _udp;
    private readonly IPEndPoint _endpoint;
    private readonly byte _destNode;
    private readonly byte _srcNode;
    private byte _sid;

    public FinsClient(string ipAddress, int port, byte destNode, byte srcNode)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        _udp = new UdpClient();
        _udp.Client.ReceiveTimeout = 2000;
        _destNode = destNode;
        _srcNode = srcNode;
    }

    /// <summary>Reads a contiguous block of words from a memory area.</summary>
    public async Task<ushort[]> ReadWordsAsync(byte areaCode, ushort startAddress, ushort count)
    {
        var response = await SendCommandAsync(BuildReadFrame(areaCode, startAddress, count, 0));
        // Response: 14-byte header + 2-byte command + 2-byte end code + data
        if (response.Length < 16) throw new FinsException("FINS response too short.");

        var endCode = (ushort)((response[12] << 8) | response[13]);
        if (endCode != 0) throw new FinsException($"FINS end code 0x{endCode:X4}.");

        var wordCount = (response.Length - 14) / 2;
        var words = new ushort[wordCount];
        for (var i = 0; i < wordCount; i++)
            words[i] = (ushort)((response[14 + i * 2] << 8) | response[15 + i * 2]);

        return words;
    }

    /// <summary>Reads a single bit from a memory area.</summary>
    public async Task<bool> ReadBitAsync(byte areaCode, ushort wordAddress, byte bitIndex)
    {
        var response = await SendCommandAsync(BuildReadFrame(areaCode, wordAddress, 1, bitIndex));
        if (response.Length < 15) throw new FinsException("FINS bit response too short.");
        var endCode = (ushort)((response[12] << 8) | response[13]);
        if (endCode != 0) throw new FinsException($"FINS end code 0x{endCode:X4}.");
        return response[14] != 0;
    }

    private byte[] BuildReadFrame(byte areaCode, ushort address, ushort count, byte bitIndex)
    {
        var frame = new byte[18];
        frame[0] = 0x80; // ICF: command, response required
        frame[1] = 0x00; // RSV
        frame[2] = 0x02; // GCT
        frame[3] = 0x00; // DNA
        frame[4] = _destNode; // DA1
        frame[5] = 0x00; // DA2
        frame[6] = 0x00; // SNA
        frame[7] = _srcNode; // SA1
        frame[8] = 0x00; // SA2
        frame[9] = _sid++; // SID
        frame[10] = 0x01; // Command high
        frame[11] = 0x01; // Command low (MEMORY AREA READ)
        frame[12] = areaCode;
        frame[13] = (byte)(address >> 8);
        frame[14] = (byte)(address & 0xFF);
        frame[15] = bitIndex;
        frame[16] = (byte)(count >> 8);
        frame[17] = (byte)(count & 0xFF);
        return frame;
    }

    private async Task<byte[]> SendCommandAsync(byte[] frame)
    {
        await _udp.SendAsync(frame, frame.Length, _endpoint);
        var result = await _udp.ReceiveAsync();
        return result.Buffer;
    }

    public void Dispose() => _udp.Dispose();
}

public sealed class FinsException(string message) : Exception(message);