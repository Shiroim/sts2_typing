using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;

namespace Typing;

[ModInitializer(nameof(Initialize))]
public static class ChatMod
{
    public static void Initialize()
    {
        NGame.Instance?.CallDeferred(Node.MethodName.AddChild, new ChatPanel());
    }
}
