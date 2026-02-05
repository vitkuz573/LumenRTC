namespace LumenRTC;

/// <summary>
/// Controls how media streams are bundled on a single transport.
/// </summary>
public enum BundlePolicy
{
    Balanced = 0,
    MaxBundle = 1,
    MaxCompat = 2,
}
