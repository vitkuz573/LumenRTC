namespace LumenRTC;

/// <summary>
/// Convenience helpers for working with peer connections.
/// </summary>
public static class PeerConnectionExtensions
{
    public static bool AddTrack(this PeerConnection peerConnection, LocalAudioTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        if (track == null) throw new ArgumentNullException(nameof(track));
        return peerConnection.AddAudioTrack(track.Track, streamIds);
    }

    public static bool AddTrack(this PeerConnection peerConnection, LocalVideoTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        if (track == null) throw new ArgumentNullException(nameof(track));
        return peerConnection.AddVideoTrack(track.Track, streamIds);
    }

    public static RtpSender AddTrackSender(this PeerConnection peerConnection, LocalAudioTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        if (track == null) throw new ArgumentNullException(nameof(track));
        return peerConnection.AddAudioTrackSender(track.Track, streamIds);
    }

    public static RtpSender AddTrackSender(this PeerConnection peerConnection, LocalVideoTrack track, IReadOnlyList<string>? streamIds = null)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        if (track == null) throw new ArgumentNullException(nameof(track));
        return peerConnection.AddVideoTrackSender(track.Track, streamIds);
    }
}
