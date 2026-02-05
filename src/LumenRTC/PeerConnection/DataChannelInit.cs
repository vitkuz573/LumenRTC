namespace LumenRTC;

public readonly struct DataChannelInit
{
    public static DataChannelInit Default => new(
        ordered: true,
        reliable: true,
        maxRetransmitTime: -1,
        maxRetransmits: -1,
        protocol: "sctp",
        negotiated: false,
        id: 0);

    public DataChannelInit(
        bool ordered,
        bool reliable,
        int maxRetransmitTime,
        int maxRetransmits,
        string protocol,
        bool negotiated,
        int id)
    {
        Ordered = ordered;
        Reliable = reliable;
        MaxRetransmitTime = maxRetransmitTime;
        MaxRetransmits = maxRetransmits;
        Protocol = protocol ?? "sctp";
        Negotiated = negotiated;
        Id = id;
    }

    public bool Ordered { get; }
    public bool Reliable { get; }
    public int MaxRetransmitTime { get; }
    public int MaxRetransmits { get; }
    public string Protocol { get; }
    public bool Negotiated { get; }
    public int Id { get; }
}
