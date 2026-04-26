using Godot;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using Typing.Scripts;

namespace Typing;

[ModInitializer(nameof(Initialize))]
public static class ChatMod
{
    public static void Initialize()
    {
        NGame.Instance?.CallDeferred(Node.MethodName.AddChild, new ChatPanel());
        ModConfigBridge.DeferredRegister();
    }
}
