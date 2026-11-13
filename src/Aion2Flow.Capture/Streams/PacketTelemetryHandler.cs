using Cloris.Aion2Flow.Protocol.Packets;

namespace Cloris.Aion2Flow.Capture.Streams;

internal static class PacketTelemetryHandler
{
    // The ping it gives goes into the journal too, so the fight graphs have it for history entries and stored runs.
    public static bool ParseRoundTripEcho0336Packet(ReadOnlySpan<byte> packet, ref PacketParseContext context)
    {
        if (!Packet0336RoundTripParser.TryParse(packet, out var parsed))
        {
            return false;
        }

        if (context.ObserveProtocolRoundTrip(parsed.ClientSentMonotonicMilliseconds) is { } roundTripMilliseconds)
            context.Sink.AppendPing(context.CreateObservationSource(0x0336, packet.Length), roundTripMilliseconds);
        return context.MarkParsed();
    }
}
