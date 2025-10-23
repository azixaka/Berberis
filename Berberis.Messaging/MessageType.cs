namespace Berberis.Messaging;

/// <summary>Message type enumeration.</summary>
public enum MessageType : byte
{
    /// <summary>System trace message.</summary>
    SystemTrace = 0x10,
    /// <summary>Channel state update.</summary>
    ChannelUpdate = 0x20,
    /// <summary>Channel state deletion.</summary>
    ChannelDelete = 0x30,
    /// <summary>Channel state reset.</summary>
    ChannelReset = 0x40
}
