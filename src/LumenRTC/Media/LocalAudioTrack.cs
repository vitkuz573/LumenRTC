namespace LumenRTC;

/// <summary>
/// Convenience wrapper for a local audio track and its source.
/// </summary>
public sealed class LocalAudioTrack : IDisposable
{
    internal LocalAudioTrack(AudioSource source, AudioTrack track, string trackId, string sourceLabel)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Track = track ?? throw new ArgumentNullException(nameof(track));
        TrackId = trackId;
        SourceLabel = sourceLabel;
    }

    public AudioSource Source { get; }

    public AudioTrack Track { get; }

    public string TrackId { get; }

    public string SourceLabel { get; }

    public bool AddTo(PeerConnection peerConnection, IReadOnlyList<string>? streamIds = null)
    {
        if (peerConnection == null) throw new ArgumentNullException(nameof(peerConnection));
        return peerConnection.AddAudioTrack(Track, streamIds);
    }

    public void Dispose()
    {
        Track.Dispose();
        Source.Dispose();
    }
}
