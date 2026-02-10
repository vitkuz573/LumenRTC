namespace LumenRTC;

/// <summary>
/// Timeout-aware async wrappers for peer connection operations.
/// </summary>
public sealed partial class PeerConnection
{
    public Task<SessionDescription> CreateOfferAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            CreateOfferAsync(cancellationToken),
            timeout,
            "CreateOfferAsync",
            cancellationToken);
    }

    public Task<SessionDescription> CreateAnswerAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            CreateAnswerAsync(cancellationToken),
            timeout,
            "CreateAnswerAsync",
            cancellationToken);
    }

    public Task SetLocalDescriptionAsync(string sdp, string type, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            SetLocalDescriptionAsync(sdp, type, cancellationToken),
            timeout,
            "SetLocalDescriptionAsync",
            cancellationToken);
    }

    public Task SetLocalDescriptionAsync(SessionDescription description, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            SetLocalDescriptionAsync(description, cancellationToken),
            timeout,
            "SetLocalDescriptionAsync",
            cancellationToken);
    }

    public Task SetRemoteDescriptionAsync(string sdp, string type, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            SetRemoteDescriptionAsync(sdp, type, cancellationToken),
            timeout,
            "SetRemoteDescriptionAsync",
            cancellationToken);
    }

    public Task SetRemoteDescriptionAsync(SessionDescription description, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            SetRemoteDescriptionAsync(description, cancellationToken),
            timeout,
            "SetRemoteDescriptionAsync",
            cancellationToken);
    }

    public Task<SessionDescription> GetLocalDescriptionAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            GetLocalDescriptionAsync(cancellationToken),
            timeout,
            "GetLocalDescriptionAsync",
            cancellationToken);
    }

    public Task<SessionDescription> GetRemoteDescriptionAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            GetRemoteDescriptionAsync(cancellationToken),
            timeout,
            "GetRemoteDescriptionAsync",
            cancellationToken);
    }

    public Task<string> GetStatsAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            GetStatsAsync(cancellationToken),
            timeout,
            "GetStatsAsync",
            cancellationToken);
    }

    public async Task<RtcStatsReport> GetStatsReportAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var json = await GetStatsAsync(timeout, cancellationToken).ConfigureAwait(false);
        return RtcStatsReport.Parse(json);
    }

    public Task<string> GetSenderStatsAsync(RtpSender sender, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            GetSenderStatsAsync(sender, cancellationToken),
            timeout,
            "GetSenderStatsAsync",
            cancellationToken);
    }

    public async Task<RtcStatsReport> GetSenderStatsReportAsync(
        RtpSender sender,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var json = await GetSenderStatsAsync(sender, timeout, cancellationToken).ConfigureAwait(false);
        return RtcStatsReport.Parse(json);
    }

    public Task<string> GetReceiverStatsAsync(RtpReceiver receiver, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WithTimeoutAsync(
            GetReceiverStatsAsync(receiver, cancellationToken),
            timeout,
            "GetReceiverStatsAsync",
            cancellationToken);
    }

    public async Task<RtcStatsReport> GetReceiverStatsReportAsync(
        RtpReceiver receiver,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var json = await GetReceiverStatsAsync(receiver, timeout, cancellationToken).ConfigureAwait(false);
        return RtcStatsReport.Parse(json);
    }

    private static async Task<T> WithTimeoutAsync<T>(
        Task<T> operation,
        TimeSpan timeout,
        string operationName,
        CancellationToken cancellationToken)
    {
        ValidateTimeout(timeout, operationName);

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        try
        {
            return await operation.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
            when (!cancellationToken.IsCancellationRequested && linkedCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"PeerConnection operation '{operationName}' timed out after {timeout}.",
                ex);
        }
    }

    private static async Task WithTimeoutAsync(
        Task operation,
        TimeSpan timeout,
        string operationName,
        CancellationToken cancellationToken)
    {
        ValidateTimeout(timeout, operationName);

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await operation.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        try
        {
            await operation.WaitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
            when (!cancellationToken.IsCancellationRequested && linkedCts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"PeerConnection operation '{operationName}' timed out after {timeout}.",
                ex);
        }
    }

    private static void ValidateTimeout(TimeSpan timeout, string operationName)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                $"Timeout for '{operationName}' must be greater than zero or Timeout.InfiniteTimeSpan.");
        }
    }
}
