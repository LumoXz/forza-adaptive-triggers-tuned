using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ForzaAdaptiveTriggers.Telemetry;

/// <summary>
/// Listens on UDP port 5300 and yields parsed FH5Packet values.
/// Packets smaller than 324 bytes are silently dropped.
/// </summary>
public sealed class UdpListener : IDisposable
{
    private const int Port = 5300;
    private const int PacketSize = 324;

    private readonly UdpClient _udp;

    public UdpListener()
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
        _udp.Client.ReceiveBufferSize = 65536;
    }

    public async IAsyncEnumerable<FH5Packet> ListenAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (SocketException)
            {
                // Transient network hiccup — keep listening
                continue;
            }

            if (result.Buffer.Length < PacketSize)
                continue;

            yield return MemoryMarshal.Read<FH5Packet>(result.Buffer.AsSpan(0, PacketSize));
        }
    }

    public void Dispose() => _udp.Dispose();
}
