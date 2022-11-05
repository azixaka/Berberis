namespace Berberis.Messaging;

public enum MessageType : byte { SystemTrace = 0x10, ChannelUpdate = 0x20, ChannelDelete = 0x30, ChannelReset = 0x40 }
