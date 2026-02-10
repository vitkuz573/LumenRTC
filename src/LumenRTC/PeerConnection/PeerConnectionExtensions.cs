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

    public static IReadOnlyList<RtpSender> GetSenders(this PeerConnection peerConnection, MediaType mediaType)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        var senders = peerConnection.GetSenders();
        if (senders.Count == 0)
        {
            return Array.Empty<RtpSender>();
        }

        var filtered = new List<RtpSender>();
        foreach (var sender in senders)
        {
            if (sender.MediaType == mediaType)
            {
                filtered.Add(sender);
            }
        }

        return filtered;
    }

    public static IReadOnlyList<RtpReceiver> GetReceivers(this PeerConnection peerConnection, MediaType mediaType)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        var receivers = peerConnection.GetReceivers();
        if (receivers.Count == 0)
        {
            return Array.Empty<RtpReceiver>();
        }

        var filtered = new List<RtpReceiver>();
        foreach (var receiver in receivers)
        {
            if (receiver.MediaType == mediaType)
            {
                filtered.Add(receiver);
            }
        }

        return filtered;
    }

    public static IReadOnlyList<RtpTransceiver> GetTransceivers(this PeerConnection peerConnection, MediaType mediaType)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        var transceivers = peerConnection.GetTransceivers();
        if (transceivers.Count == 0)
        {
            return Array.Empty<RtpTransceiver>();
        }

        var filtered = new List<RtpTransceiver>();
        foreach (var transceiver in transceivers)
        {
            if (transceiver.MediaType == mediaType)
            {
                filtered.Add(transceiver);
            }
        }

        return filtered;
    }

    public static bool TrySetTransceiverDirection(
        this PeerConnection peerConnection,
        MediaType mediaType,
        RtpTransceiverDirection direction,
        out string? error)
    {
        if (peerConnection == null)
        {
            error = "Peer connection cannot be null.";
            return false;
        }

        var transceivers = peerConnection.GetTransceivers(mediaType);
        foreach (var transceiver in transceivers)
        {
            if (transceiver.TrySetDirection(direction, out error))
            {
                continue;
            }

            error = $"Failed to set {mediaType} transceiver direction to {direction}: {error}";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryPauseMedia(this PeerConnection peerConnection, MediaType mediaType, out string? error)
    {
        return peerConnection.TrySetTransceiverDirection(
            mediaType,
            RtpTransceiverDirection.Inactive,
            out error);
    }

    public static bool TryResumeMedia(this PeerConnection peerConnection, MediaType mediaType, out string? error)
    {
        return peerConnection.TrySetTransceiverDirection(
            mediaType,
            RtpTransceiverDirection.SendRecv,
            out error);
    }
}
