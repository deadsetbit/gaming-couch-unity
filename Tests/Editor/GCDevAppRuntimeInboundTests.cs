using DSB.GC;
using DSB.GC.Dev;
using NUnit.Framework;

public sealed class GCDevAppRuntimeInboundTests
{
    [Test]
    public void TextMessageRoutesRestartIntent()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"restart\",\"timestamp\":123}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Restart));
    }

    [Test]
    public void TextMessageRoutesPlayerIndexInputAsActivePlayerIndex()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerIndex\":2,\"inputs\":{\"a0\":0.25,\"a1\":-0.75,\"b0\":0.6,\"b1\":0.5,\"b2\":1.0}}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(decision.activePlayerIndex, Is.EqualTo(2));
        Assert.That(decision.hasInputSequence, Is.False);
        Assert.That(decision.inputs.a0, Is.EqualTo(0.25f));
        Assert.That(decision.inputs.a1, Is.EqualTo(-0.75f));
        Assert.That(decision.inputs.b0, Is.EqualTo(1));
        Assert.That(decision.inputs.b1, Is.EqualTo(0));
        Assert.That(decision.inputs.b2, Is.EqualTo(1));
    }

    [Test]
    public void TextMessageFallsBackFromLegacyPlayerIdToActivePlayerIndex()
    {
        var resolver = new TestActivePlayerResolver(42, 3);
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerId\":42,\"inputs\":{\"a0\":1.0,\"a1\":0.0,\"b0\":1.0,\"b1\":0.0,\"b2\":0.0}}}",
            Context(activePlayerResolver: resolver)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(decision.activePlayerIndex, Is.EqualTo(3));
        Assert.That(resolver.lastPlatformPlayerId, Is.EqualTo(42));
    }

    [Test]
    public void TextMessageRoutesMissingActivePlayerIndexForAdapterValidation()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"inputs\":{\"a0\":1.0}}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(decision.activePlayerIndex, Is.EqualTo(-1));
    }

    [Test]
    public void TextMessageIgnoresInputWhenLegacyPlayerIdCannotResolve()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"input\",\"payload\":{\"playerId\":99,\"inputs\":{\"a0\":1.0}}}",
            Context(activePlayerResolver: new TestActivePlayerResolver(42, 3))
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(decision.reason, Is.EqualTo("unresolved_active_player_index"));
    }

    [Test]
    public void TextMessageLeavesNonDevToolMalformedTextUnhandled()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage("{not json", Context());

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Unhandled));
        Assert.That(decision.reason, Is.EqualTo("unsupported_message_type"));
    }

    [Test]
    public void TextMessageLetsMalformedDevToolJsonReachAdapterErrorHandling()
    {
        var inbound = new GCDevAppRuntimeInbound();

        Assert.That(
            () => inbound.RouteTextMessage("{\"type\":\"gcdevtool\",", Context()),
            Throws.Exception
        );
    }

    [Test]
    public void TextMessageRoutesTimescaleAndPauseIntent()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\",\"payload\":{\"timescale\":0.5,\"paused\":true}}",
            Context(isPaused: false)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.TimescaleState));
        Assert.That(decision.timescale, Is.EqualTo(0.5f));
        Assert.That(decision.paused, Is.True);
        Assert.That(decision.shouldApplyPause, Is.True);
    }

    [Test]
    public void TextMessageSuppressesUnchangedPauseIntent()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"timescale_state\",\"payload\":{\"timescale\":1.5,\"paused\":true}}",
            Context(isPaused: true)
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.TimescaleState));
        Assert.That(decision.timescale, Is.EqualTo(1.5f));
        Assert.That(decision.paused, Is.True);
        Assert.That(decision.shouldApplyPause, Is.False);
    }

    [Test]
    public void TextMessageRoutesRuntimeOutputOptionIntent()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var decision = inbound.RouteTextMessage(
            "{\"type\":\"gcdevtool\",\"action\":\"runtime_output_options\",\"payload\":{\"runtimeOutput\":{\"runtimeLogCapture\":\"warning_and_error\"}}}",
            Context()
        );

        Assert.That(decision.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(decision.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.RuntimeOutputOptions));
        Assert.That(decision.runtimeLogCaptureMode, Is.EqualTo(GCRuntimeUnityLogCaptureMode.WarningAndError));
    }

    [Test]
    public void CompactInputParserPreservesRouteSequenceAxesAndButtons()
    {
        var frame = CreateCompactInputFrame(
            playerIndex: 2,
            seq: 123,
            timestampMs: 456,
            a0: 1500,
            a1: -250,
            buttons: 0b00000101
        );

        Assert.That(GCDevAppRuntimeInbound.TryParseCompactControllerInputFrame(frame, out var inputFrame), Is.True);
        Assert.That(inputFrame.playerIndex, Is.EqualTo(2));
        Assert.That(inputFrame.seq, Is.EqualTo(123u));
        Assert.That(inputFrame.timestampMs, Is.EqualTo(456u));
        Assert.That(inputFrame.inputs.a0, Is.EqualTo(1f));
        Assert.That(inputFrame.inputs.a1, Is.EqualTo(-0.25f));
        Assert.That(inputFrame.inputs.b0, Is.EqualTo(1));
        Assert.That(inputFrame.inputs.b1, Is.EqualTo(0));
        Assert.That(inputFrame.inputs.b2, Is.EqualTo(1));
    }

    [Test]
    public void CompactInputParserReadsNeutralReleaseFrame()
    {
        var frame = CreateCompactInputFrame(
            playerIndex: 1,
            seq: 124,
            timestampMs: 789,
            a0: 0,
            a1: 0,
            buttons: 0
        );

        Assert.That(GCDevAppRuntimeInbound.TryParseCompactControllerInputFrame(frame, out var inputFrame), Is.True);
        Assert.That(inputFrame.playerIndex, Is.EqualTo(1));
        Assert.That(inputFrame.inputs.a0, Is.EqualTo(0f));
        Assert.That(inputFrame.inputs.a1, Is.EqualTo(0f));
        Assert.That(inputFrame.inputs.b0, Is.EqualTo(0));
        Assert.That(inputFrame.inputs.b1, Is.EqualTo(0));
        Assert.That(inputFrame.inputs.b2, Is.EqualTo(0));
    }

    [Test]
    public void CompactInputSequenceSuppressesStaleFramesPerActivePlayerIndex()
    {
        var inbound = new GCDevAppRuntimeInbound();

        var first = inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 10, 100, 0, 0, 0));
        var duplicate = inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 10, 110, 500, 0, 1));
        var older = inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 9, 120, 500, 0, 1));
        var otherActivePlayer = inbound.RouteBinaryMessage(CreateCompactInputFrame(2, 9, 130, 500, 0, 1));
        var next = inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 11, 140, 500, 0, 1));

        Assert.That(first.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(first.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(first.activePlayerIndex, Is.EqualTo(1));
        Assert.That(first.hasInputSequence, Is.True);
        Assert.That(first.inputSequence, Is.EqualTo(10u));
        Assert.That(duplicate.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(duplicate.reason, Is.EqualTo("stale_input_sequence"));
        Assert.That(older.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(otherActivePlayer.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(otherActivePlayer.activePlayerIndex, Is.EqualTo(2));
        Assert.That(next.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(next.inputSequence, Is.EqualTo(11u));
    }

    [Test]
    public void CompactInputSequenceCanStartAfterActivePlayerIndexValidation()
    {
        var inbound = new GCDevAppRuntimeInbound();
        var frame = CreateCompactInputFrame(1, 10, 100, 0, 0, 0);

        Assert.That(GCDevAppRuntimeInbound.TryParseCompactControllerInputFrame(frame, out var parsedFrame), Is.True);

        var acceptedAfterValidation = inbound.RouteValidatedCompactControllerInputFrame(parsedFrame);
        var duplicateAfterValidation = inbound.RouteValidatedCompactControllerInputFrame(parsedFrame);

        Assert.That(acceptedAfterValidation.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(acceptedAfterValidation.intentKind, Is.EqualTo(GCDevAppRuntimeInboundIntentKind.Input));
        Assert.That(acceptedAfterValidation.activePlayerIndex, Is.EqualTo(1));
        Assert.That(acceptedAfterValidation.inputSequence, Is.EqualTo(10u));
        Assert.That(duplicateAfterValidation.status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));
        Assert.That(duplicateAfterValidation.reason, Is.EqualTo("stale_input_sequence"));
    }

    [Test]
    public void CompactInputSequenceCanResetForNewDevAppConnection()
    {
        var inbound = new GCDevAppRuntimeInbound();
        Assert.That(inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 10, 100, 0, 0, 0)).status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
        Assert.That(inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 10, 110, 0, 0, 0)).status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Ignored));

        inbound.ResetInputSequences();

        Assert.That(inbound.RouteBinaryMessage(CreateCompactInputFrame(1, 10, 120, 0, 0, 0)).status, Is.EqualTo(GCDevAppRuntimeInboundStatus.Intent));
    }

    private static GCDevAppRuntimeInboundContext Context(
        bool isPaused = false,
        IGCDevAppRuntimeActivePlayerResolver activePlayerResolver = null
    )
    {
        return new GCDevAppRuntimeInboundContext
        {
            isPaused = isPaused,
            activePlayerResolver = activePlayerResolver,
        };
    }

    private static byte[] CreateCompactInputFrame(
        ushort playerIndex,
        uint seq,
        uint timestampMs,
        short a0,
        short a1,
        byte buttons
    )
    {
        var frame = new byte[GCDevAppRuntimeInbound.CompactControllerInputByteLength];
        frame[0] = GCDevAppRuntimeInbound.CompactControllerInputTypeByte;
        WriteUInt16LittleEndian(frame, 1, playerIndex);
        WriteUInt32LittleEndian(frame, 3, seq);
        WriteUInt32LittleEndian(frame, 7, timestampMs);
        WriteInt16LittleEndian(frame, 11, a0);
        WriteInt16LittleEndian(frame, 13, a1);
        frame[15] = buttons;
        return frame;
    }

    private static void WriteUInt16LittleEndian(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value & 0xff);
        bytes[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    private static void WriteInt16LittleEndian(byte[] bytes, int offset, short value)
    {
        bytes[offset] = (byte)(value & 0xff);
        bytes[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    private static void WriteUInt32LittleEndian(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value & 0xff);
        bytes[offset + 1] = (byte)((value >> 8) & 0xff);
        bytes[offset + 2] = (byte)((value >> 16) & 0xff);
        bytes[offset + 3] = (byte)((value >> 24) & 0xff);
    }

    private sealed class TestActivePlayerResolver : IGCDevAppRuntimeActivePlayerResolver
    {
        private readonly int platformPlayerId;
        private readonly int activePlayerIndex;

        internal int lastPlatformPlayerId = -1;

        internal TestActivePlayerResolver(int platformPlayerId, int activePlayerIndex)
        {
            this.platformPlayerId = platformPlayerId;
            this.activePlayerIndex = activePlayerIndex;
        }

        public bool TryGetActivePlayerIndexForLegacyPlayerId(int platformPlayerId, out int activePlayerIndex)
        {
            lastPlatformPlayerId = platformPlayerId;
            if (platformPlayerId == this.platformPlayerId)
            {
                activePlayerIndex = this.activePlayerIndex;
                return true;
            }

            activePlayerIndex = -1;
            return false;
        }
    }
}
