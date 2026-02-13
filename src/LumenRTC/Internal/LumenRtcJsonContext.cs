using System.Text.Json.Serialization;

namespace LumenRTC.Internal;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(RtpCapabilitiesDto))]
[JsonSerializable(typeof(RtpCodecCapabilityDto))]
[JsonSerializable(typeof(RtpHeaderExtensionCapabilityDto))]
internal sealed partial class LumenRtcJsonContext : JsonSerializerContext
{
}
