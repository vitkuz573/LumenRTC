namespace LumenRTC;

/// <summary>
/// Fluent helpers for configuring peer connection settings.
/// </summary>
public static class RtcConfigurationExtensions
{
    public static RtcConfiguration WithIceServer(this RtcConfiguration config, IceServer server)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (server == null) throw new ArgumentNullException(nameof(server));
        config.IceServers.Add(server);
        return config;
    }

    public static RtcConfiguration WithIceServers(this RtcConfiguration config, IEnumerable<IceServer> servers)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (servers == null) throw new ArgumentNullException(nameof(servers));
        foreach (var server in servers)
        {
            if (server != null)
            {
                config.IceServers.Add(server);
            }
        }
        return config;
    }

    public static RtcConfiguration WithStun(this RtcConfiguration config, string uri)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentException("STUN URI is required.", nameof(uri));
        config.IceServers.Add(new IceServer(uri));
        return config;
    }

    public static RtcConfiguration WithTurn(this RtcConfiguration config, string uri, string username, string password)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(uri)) throw new ArgumentException("TURN URI is required.", nameof(uri));
        config.IceServers.Add(new IceServer(uri, username, password));
        return config;
    }

    public static RtcConfiguration EnableDscp(this RtcConfiguration config, bool enabled = true)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.EnableDscp = enabled;
        return config;
    }

    public static RtcConfiguration WithBandwidth(this RtcConfiguration config, uint audioKbps, uint videoKbps)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.LocalAudioBandwidth = audioKbps;
        config.LocalVideoBandwidth = videoKbps;
        return config;
    }

    public static RtcConfiguration WithIceCandidatePoolSize(this RtcConfiguration config, int size)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        config.IceCandidatePoolSize = size;
        return config;
    }
}
