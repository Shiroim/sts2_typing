using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace Typing;

public struct ChatMessage : INetMessage, IPacketSerializable
{
    public string text;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Info;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(text ?? string.Empty);
    }

    public void Deserialize(PacketReader reader)
    {
        text = reader.ReadString();
    }
}
