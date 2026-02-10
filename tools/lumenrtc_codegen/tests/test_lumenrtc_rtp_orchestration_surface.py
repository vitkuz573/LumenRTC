import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
SRC_ROOT = REPO_ROOT / "src" / "LumenRTC"
RTP_TRANSCEIVER_PATH = SRC_ROOT / "Rtp" / "RtpTransceiver.cs"
RTP_SENDER_PATH = SRC_ROOT / "Rtp" / "RtpSender.cs"
RTP_RECEIVER_PATH = SRC_ROOT / "Rtp" / "RtpReceiver.cs"
MEDIA_STREAM_PATH = SRC_ROOT / "Media" / "MediaStream.cs"
PEER_CONNECTION_EXTENSIONS_PATH = SRC_ROOT / "PeerConnection" / "PeerConnectionExtensions.cs"


class RtpOrchestrationSurfaceTests(unittest.TestCase):
    def test_rtp_transceiver_exposes_direction_orchestration_helpers(self) -> None:
        text = RTP_TRANSCEIVER_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public bool CanSendDesired",
            "public bool CanReceiveDesired",
            "public bool CanSendCurrent",
            "public bool CanReceiveCurrent",
            "public bool TrySetSendEnabled(bool enabled, out string? error)",
            "public void SetSendEnabled(bool enabled)",
            "public bool TrySetReceiveEnabled(bool enabled, out string? error)",
            "public void SetReceiveEnabled(bool enabled)",
            "public bool TryPause(out string? error)",
            "public void Pause()",
            "public bool TryResumeBidirectional(out string? error)",
            "public void ResumeBidirectional()",
            "private static RtpTransceiverDirection WithSendEnabled(",
            "private static RtpTransceiverDirection WithReceiveEnabled(",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"RtpTransceiver orchestration surface is incomplete: missing {missing}",
        )

    def test_rtp_sender_exposes_safe_track_and_encoding_helpers(self) -> None:
        text = RTP_SENDER_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public bool IsAudio => MediaType == MediaType.Audio;",
            "public bool IsVideo => MediaType == MediaType.Video;",
            "public bool HasTrack => AudioTrack != null || VideoTrack != null;",
            "public bool TryGetAudioTrack(",
            "public bool TryGetVideoTrack(",
            "public bool TryReplaceTrack(AudioTrack? track, out string? error)",
            "public bool TryReplaceTrack(VideoTrack? track, out string? error)",
            "public bool TryClearTrack(out string? error)",
            "public bool TrySetEncodingParameters(RtpEncodingSettings settings, out string? error)",
            "public bool TrySetEncodingParameters(int encodingIndex, RtpEncodingSettings settings, out string? error)",
            "public bool TrySetStreamIds(IReadOnlyList<string> streamIds, out string? error)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"RtpSender convenience surface is incomplete: missing {missing}",
        )

    def test_rtp_receiver_exposes_safe_track_stream_and_jitter_helpers(self) -> None:
        text = RTP_RECEIVER_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public bool IsAudio => MediaType == MediaType.Audio;",
            "public bool IsVideo => MediaType == MediaType.Video;",
            "public MediaStream? PrimaryStream",
            "public bool TryGetAudioTrack(",
            "public bool TryGetVideoTrack(",
            "public bool TryGetStream(string streamId,",
            "public bool TrySetJitterBufferMinimumDelay(double seconds, out string? error)",
            "public bool TrySetJitterBufferMinimumDelay(TimeSpan delay, out string? error)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"RtpReceiver convenience surface is incomplete: missing {missing}",
        )

    def test_media_stream_exposes_try_track_mutation_helpers(self) -> None:
        text = MEDIA_STREAM_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public string DisplayName => string.IsNullOrWhiteSpace(Label) ? Id : Label;",
            "public bool TryAddTrack(AudioTrack track, out string? error)",
            "public bool TryAddTrack(VideoTrack track, out string? error)",
            "public bool TryRemoveTrack(AudioTrack track, out string? error)",
            "public bool TryRemoveTrack(VideoTrack track, out string? error)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"MediaStream convenience surface is incomplete: missing {missing}",
        )

    def test_peer_connection_extensions_expose_rtp_orchestration_helpers(self) -> None:
        text = PEER_CONNECTION_EXTENSIONS_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public static IReadOnlyList<RtpSender> GetSenders(this PeerConnection peerConnection, MediaType mediaType)",
            "public static IReadOnlyList<RtpReceiver> GetReceivers(this PeerConnection peerConnection, MediaType mediaType)",
            "public static IReadOnlyList<RtpTransceiver> GetTransceivers(this PeerConnection peerConnection, MediaType mediaType)",
            "public static bool TrySetTransceiverDirection(",
            "public static bool TryPauseMedia(this PeerConnection peerConnection, MediaType mediaType, out string? error)",
            "public static bool TryResumeMedia(this PeerConnection peerConnection, MediaType mediaType, out string? error)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"PeerConnection RTP orchestration helpers are incomplete: missing {missing}",
        )


if __name__ == "__main__":
    unittest.main()
