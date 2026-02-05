namespace LumenRTC;

/// <summary>
/// Represents a STUN/TURN server configuration.
/// </summary>
public sealed class IceServer
{
    public IceServer(string uri, string? username = null, string? password = null)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Username = username;
        Password = password;
    }

    public string Uri { get; }
    public string? Username { get; }
    public string? Password { get; }
}
