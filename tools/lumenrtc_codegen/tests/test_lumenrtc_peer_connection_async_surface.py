import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
PEER_CONNECTION_PATH = REPO_ROOT / "src" / "LumenRTC" / "PeerConnection" / "PeerConnection.cs"
PEER_CONNECTION_TIMEOUTS_PATH = REPO_ROOT / "src" / "LumenRTC" / "PeerConnection" / "PeerConnection.Timeouts.cs"


class PeerConnectionAsyncSurfaceTests(unittest.TestCase):
    def test_peer_connection_releases_one_shot_callbacks(self) -> None:
        text = PEER_CONNECTION_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "private readonly object _keepAliveSync = new();",
            "KeepCallbackAlive(",
            "ReleaseCallbacks(",
            "private void KeepCallbackAlive(Delegate callback)",
            "private void ReleaseCallbacks(params Delegate?[] callbacks)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"PeerConnection callback lifecycle is incomplete: missing {missing}",
        )

    def test_peer_connection_has_timeout_aware_async_overloads(self) -> None:
        text = PEER_CONNECTION_TIMEOUTS_PATH.read_text(encoding="utf-8")
        expected_signatures = [
            "public Task<SessionDescription> CreateOfferAsync(TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task<SessionDescription> CreateAnswerAsync(TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task SetLocalDescriptionAsync(string sdp, string type, TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task SetRemoteDescriptionAsync(string sdp, string type, TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task<SessionDescription> GetLocalDescriptionAsync(TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task<SessionDescription> GetRemoteDescriptionAsync(TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task<string> GetStatsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task<string> GetSenderStatsAsync(RtpSender sender, TimeSpan timeout, CancellationToken cancellationToken = default)",
            "public Task<string> GetReceiverStatsAsync(RtpReceiver receiver, TimeSpan timeout, CancellationToken cancellationToken = default)",
        ]
        missing = [item for item in expected_signatures if item not in text]
        self.assertFalse(
            missing,
            f"PeerConnection timeout async overloads are incomplete: missing {missing}",
        )

        behavior_snippets = [
            "throw new TimeoutException(",
            "ValidateTimeout(timeout, operationName);",
            "WaitAsync(linkedCts.Token)",
        ]
        missing_behavior = [item for item in behavior_snippets if item not in text]
        self.assertFalse(
            missing_behavior,
            f"PeerConnection timeout behavior is incomplete: missing {missing_behavior}",
        )


if __name__ == "__main__":
    unittest.main()
