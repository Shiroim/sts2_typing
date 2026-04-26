// ModConfig integration for the typing mod.
// Adapted from https://github.com/xhyrzldf/ModConfig-STS2 (examples/ModConfigBridge.cs).
// Zero DLL reference: everything is done via reflection. If ModConfig is not
// installed, GetValue<T>() returns the supplied fallback and the mod still works.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace Typing.Scripts;

internal static class ModConfigBridge
{
    internal const string ModId = "typing";

    internal const string KeyChatToggle = "chatToggleKey";
    internal static readonly Key ChatToggleKeyDefault = Key.Enter;

    internal static Key ChatToggleKey { get; private set; } = ChatToggleKeyDefault;

    private static bool _available;
    private static bool _registered;
    private static Type? _apiType;
    private static Type? _entryType;
    private static Type? _configTypeEnum;

    internal static void DeferredRegister()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame += OnNextFrame;
    }

    private static void OnNextFrame()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.ProcessFrame -= OnNextFrame;
        Detect();
        if (_available)
        {
            Register();
            ChatToggleKey = (Key)GetValue<long>(KeyChatToggle, (long)ChatToggleKeyDefault);
        }
    }

    private static void Detect()
    {
        try
        {
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Type.EmptyTypes; }
                })
                .ToArray();

            _apiType = allTypes.FirstOrDefault(t => t.FullName == "ModConfig.ModConfigApi");
            _entryType = allTypes.FirstOrDefault(t => t.FullName == "ModConfig.ConfigEntry");
            _configTypeEnum = allTypes.FirstOrDefault(t => t.FullName == "ModConfig.ConfigType");
            _available = _apiType != null && _entryType != null && _configTypeEnum != null;
        }
        catch
        {
            _available = false;
        }
    }

    private static void Register()
    {
        if (_registered) return;
        _registered = true;

        try
        {
            var entries = BuildEntries();

            var displayNames = new Dictionary<string, string>
            {
                ["en"] = "Typing Chat",
                ["zhs"] = "聊天 Typing",
                ["zht"] = "聊天 Typing",
                ["ja"] = "Typing チャット",
                ["ko"] = "Typing 채팅",
            };

            var registerMethod = _apiType!.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "Register")
                .OrderByDescending(m => m.GetParameters().Length)
                .First();

            if (registerMethod.GetParameters().Length == 4)
            {
                registerMethod.Invoke(null, new object[]
                {
                    ModId,
                    displayNames["en"],
                    displayNames,
                    entries,
                });
            }
            else
            {
                registerMethod.Invoke(null, new object[]
                {
                    ModId,
                    displayNames["en"],
                    entries,
                });
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[typing] ModConfig registration failed: {e}");
        }
    }

    internal static T GetValue<T>(string key, T fallback)
    {
        if (!_available) return fallback;
        try
        {
            var result = _apiType!.GetMethod("GetValue", BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(typeof(T))
                ?.Invoke(null, new object[] { ModId, key });
            return result != null ? (T)result : fallback;
        }
        catch { return fallback; }
    }

    private static Array BuildEntries()
    {
        var list = new List<object>();

        list.Add(Entry(cfg =>
        {
            Set(cfg, "Key", KeyChatToggle);
            Set(cfg, "Label", "Open Chat Hotkey");
            Set(cfg, "Labels", L("Open Chat Hotkey", "打开聊天快捷键"));
            Set(cfg, "Type", EnumVal("KeyBind"));
            Set(cfg, "DefaultValue", (object)(long)ChatToggleKeyDefault);
            Set(cfg, "Description", "Press this key to open the chat input.");
            Set(cfg, "Descriptions", L(
                "Press this key to open the chat input.",
                "按下此键打开聊天输入框。"));
            Set(cfg, "OnChanged", new Action<object>(v =>
            {
                ChatToggleKey = (Key)Convert.ToInt64(v);
            }));
        }));

        var result = Array.CreateInstance(_entryType!, list.Count);
        for (int i = 0; i < list.Count; i++)
            result.SetValue(list[i], i);
        return result;
    }

    private static object Entry(Action<object> configure)
    {
        var inst = Activator.CreateInstance(_entryType!)!;
        configure(inst);
        return inst;
    }

    private static void Set(object obj, string name, object value)
        => obj.GetType().GetProperty(name)?.SetValue(obj, value);

    private static Dictionary<string, string> L(string en, string zhs)
        => new() { ["en"] = en, ["zhs"] = zhs };

    private static object EnumVal(string name)
        => Enum.Parse(_configTypeEnum!, name);
}
