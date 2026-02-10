import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]
MEDIA_ROOT = REPO_ROOT / "src" / "LumenRTC" / "Media"
AUDIO_TRACK_PATH = MEDIA_ROOT / "AudioTrack.cs"
VIDEO_TRACK_PATH = MEDIA_ROOT / "VideoTrack.cs"
LOCAL_AUDIO_TRACK_PATH = MEDIA_ROOT / "LocalAudioTrack.cs"
LOCAL_VIDEO_TRACK_PATH = MEDIA_ROOT / "LocalVideoTrack.cs"
LOCAL_TRACK_STATE_PATH = MEDIA_ROOT / "LocalTrackLifecycleState.cs"


class MediaTrackLifecycleSurfaceTests(unittest.TestCase):
    def test_audio_track_exposes_high_level_helpers(self) -> None:
        text = AUDIO_TRACK_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public bool IsLive => State == TrackState.Live;",
            "public bool IsEnded => State == TrackState.Ended;",
            "public void Mute()",
            "public void Unmute()",
            "public bool TrySetEnabled(bool enabled, out string? error)",
            "public bool TrySetVolume(double volume, out string? error)",
            "public void SetNormalizedVolume(double normalizedVolume)",
            "public bool TrySetNormalizedVolume(double normalizedVolume, out string? error)",
            "public bool TryAddSink(AudioSink sink, out string? error)",
            "public bool TryRemoveSink(AudioSink sink, out string? error)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"AudioTrack high-level helpers are incomplete: missing {missing}",
        )

    def test_video_track_exposes_high_level_helpers(self) -> None:
        text = VIDEO_TRACK_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public bool IsLive => State == TrackState.Live;",
            "public bool IsEnded => State == TrackState.Ended;",
            "public void Mute()",
            "public void Unmute()",
            "public bool TrySetEnabled(bool enabled, out string? error)",
            "public bool TryAddSink(VideoSink sink, out string? error)",
            "public bool TryRemoveSink(VideoSink sink, out string? error)",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"VideoTrack high-level helpers are incomplete: missing {missing}",
        )

    def test_local_audio_track_has_lifecycle_state_machine(self) -> None:
        text = LOCAL_AUDIO_TRACK_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public LocalTrackLifecycleState LifecycleState => _lifecycleState;",
            "public bool IsRunning => _lifecycleState == LocalTrackLifecycleState.Running;",
            "public bool IsStopped => _lifecycleState == LocalTrackLifecycleState.Stopped;",
            "public bool Start()",
            "public bool TryStart(out string? error)",
            "public void Stop()",
            "public bool TryStop(out string? error)",
            "public bool TrySetVolume(double volume, out string? error)",
            "public void SetNormalizedVolume(double normalizedVolume)",
            "public bool TrySetNormalizedVolume(double normalizedVolume, out string? error)",
            "_lifecycleState = LocalTrackLifecycleState.Disposed;",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"LocalAudioTrack lifecycle surface is incomplete: missing {missing}",
        )

    def test_local_video_track_has_lifecycle_state_machine(self) -> None:
        text = LOCAL_VIDEO_TRACK_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public LocalTrackLifecycleState LifecycleState => _lifecycleState;",
            "public bool IsRunning => _lifecycleState == LocalTrackLifecycleState.Running;",
            "public bool IsStopped => _lifecycleState == LocalTrackLifecycleState.Stopped;",
            "public bool Start()",
            "public bool TryStart(out string? error)",
            "public void Stop()",
            "public bool TryStop(out string? error)",
            "public bool TrySetEnabled(bool enabled, out string? error)",
            "private bool StartCore()",
            "private void StopCore()",
            "_lifecycleState = LocalTrackLifecycleState.Disposed;",
            "ObjectDisposedException.ThrowIf(_disposed, this);",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"LocalVideoTrack lifecycle surface is incomplete: missing {missing}",
        )

    def test_local_track_lifecycle_enum_is_defined(self) -> None:
        text = LOCAL_TRACK_STATE_PATH.read_text(encoding="utf-8")
        expected_snippets = [
            "public enum LocalTrackLifecycleState",
            "Created = 0,",
            "Running = 1,",
            "Stopped = 2,",
            "Disposed = 3,",
        ]
        missing = [item for item in expected_snippets if item not in text]
        self.assertFalse(
            missing,
            f"LocalTrackLifecycleState enum is incomplete: missing {missing}",
        )


if __name__ == "__main__":
    unittest.main()
