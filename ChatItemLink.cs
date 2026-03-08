using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace Typing;

public enum ItemLinkType
{
    Card,
    Potion,
    Relic,
    Power,
    Target
}

public readonly record struct ItemLinkData(ItemLinkType Type, string ModelIdStr, int UpgradeLevel = 0)
{
    public string Encode()
    {
        return Type == ItemLinkType.Card
            ? $"{{{{{Type.ToString().ToLowerInvariant()}:{ModelIdStr}:{UpgradeLevel}}}}}"
            : $"{{{{{Type.ToString().ToLowerInvariant()}:{ModelIdStr}}}}}";
    }

    public string MetaTag => Type == ItemLinkType.Card
        ? $"{Type.ToString().ToLowerInvariant()}:{ModelIdStr}:{UpgradeLevel}"
        : $"{Type.ToString().ToLowerInvariant()}:{ModelIdStr}";
}

public readonly record struct PowerLinkData(
    string PowerIdStr, int Amount, string CreatureName, string CreatureColorHex,
    string ApplierName = "");

public readonly record struct TargetData(
    string CreatureName, string CreatureColorHex);

public abstract record MessageSegment;
public sealed record TextSegment(string Text) : MessageSegment;
public sealed record LinkSegment(ItemLinkData Link, string DisplayName) : MessageSegment;
public sealed record PowerSegment(PowerLinkData Power, string DisplayName) : MessageSegment;
public sealed record TargetSegment(TargetData Target) : MessageSegment;

public static class ChatItemLink
{
    static readonly Regex LinkPattern = new(
        @"\{\{(card|potion|relic):([A-Z0-9_]+\.[A-Z0-9_]+)(?::(\d+))?\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex PowerPattern = new(
        @"\{\{power:([A-Z0-9_]+\.[A-Z0-9_]+):(-?\d+)\|(.+?)\|([0-9A-Fa-f]{6,8})(?:\|(.*?))?\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly Regex TargetPattern = new(
        @"\{\{target\|(.+?)\|([0-9A-Fa-f]{6,8})\}\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string EncodeCard(CardModel card)
    {
        var data = new ItemLinkData(ItemLinkType.Card, card.Id.ToString(), card.CurrentUpgradeLevel);
        return data.Encode();
    }

    public static string EncodePotion(PotionModel potion)
    {
        var data = new ItemLinkData(ItemLinkType.Potion, potion.Id.ToString());
        return data.Encode();
    }

    public static string EncodeRelic(RelicModel relic)
    {
        var data = new ItemLinkData(ItemLinkType.Relic, relic.Id.ToString());
        return data.Encode();
    }

    public static string EncodePower(PowerModel power, Creature owner)
    {
        string colorHex = GetCreatureColorHex(owner);
        string applierName = power.Applier?.Name ?? "";
        return $"{{{{power:{power.Id}:{power.Amount}|{owner.Name}|{colorHex}|{applierName}}}}}";
    }

    public static string EncodeTarget(Creature creature)
    {
        string colorHex = GetCreatureColorHex(creature);
        return $"{{{{target|{creature.Name}|{colorHex}}}}}";
    }

    public static string GetCreatureColorHex(Creature creature)
    {
        if (creature.IsPlayer)
            return creature.Player!.Character.NameColor.ToHtml(false);
        if (creature.IsPet)
            return creature.PetOwner!.Character.NameColor.ToHtml(false);
        return "FF5555";
    }

    public static List<MessageSegment> Parse(string text)
    {
        var segments = new List<MessageSegment>();
        var allMatches = new List<(Match Match, string Kind)>();

        foreach (Match m in LinkPattern.Matches(text))
            allMatches.Add((m, "link"));
        foreach (Match m in PowerPattern.Matches(text))
            allMatches.Add((m, "power"));
        foreach (Match m in TargetPattern.Matches(text))
            allMatches.Add((m, "target"));

        allMatches.Sort((a, b) => a.Match.Index.CompareTo(b.Match.Index));

        int lastIndex = 0;
        foreach (var (match, kind) in allMatches)
        {
            if (match.Index < lastIndex) continue;

            if (match.Index > lastIndex)
                segments.Add(new TextSegment(text[lastIndex..match.Index]));

            switch (kind)
            {
                case "link":
                {
                    string typeStr = match.Groups[1].Value.ToLowerInvariant();
                    string idStr = match.Groups[2].Value;
                    int upgrade = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

                    if (!TryParseType(typeStr, out var linkType))
                    {
                        segments.Add(new TextSegment(match.Value));
                        break;
                    }

                    var link = new ItemLinkData(linkType, idStr, upgrade);
                    string? displayName = ResolveDisplayName(link);
                    segments.Add(displayName is null
                        ? new TextSegment(match.Value)
                        : new LinkSegment(link, displayName));
                    break;
                }
                case "power":
                {
                    string powerId = match.Groups[1].Value;
                    int amount = int.Parse(match.Groups[2].Value);
                    string creatureName = match.Groups[3].Value;
                    string colorHex = match.Groups[4].Value;
                    string applierName = match.Groups[5].Success ? match.Groups[5].Value : "";

                    var data = new PowerLinkData(powerId, amount, creatureName, colorHex, applierName);
                    string? displayName = ResolvePowerDisplayName(data);
                    segments.Add(displayName is null
                        ? new TextSegment(match.Value)
                        : new PowerSegment(data, displayName));
                    break;
                }
                case "target":
                {
                    string creatureName = match.Groups[1].Value;
                    string colorHex = match.Groups[2].Value;
                    segments.Add(new TargetSegment(new TargetData(creatureName, colorHex)));
                    break;
                }
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
            segments.Add(new TextSegment(text[lastIndex..]));

        return segments;
    }

    public static bool ContainsLink(string text) =>
        LinkPattern.IsMatch(text) || PowerPattern.IsMatch(text) || TargetPattern.IsMatch(text);

    public static bool TryParseMeta(string meta, out ItemLinkData link)
    {
        link = default;
        string[] parts = meta.Split(':');
        if (parts.Length < 2) return false;

        if (!TryParseType(parts[0], out var linkType)) return false;

        string idStr = parts[1];
        int upgrade = parts.Length >= 3 && int.TryParse(parts[2], out int u) ? u : 0;

        link = new ItemLinkData(linkType, idStr, upgrade);
        return true;
    }

    public static CardModel? ResolveCard(ItemLinkData link)
    {
        try
        {
            var modelId = ModelId.Deserialize(link.ModelIdStr);
            var canonical = ModelDb.GetById<CardModel>(modelId);
            var mutable = canonical.ToMutable();
            for (int i = 0; i < link.UpgradeLevel; i++)
            {
                mutable.UpgradeInternal();
                mutable.FinalizeUpgradeInternal();
            }
            return mutable;
        }
        catch { return null; }
    }

    public static PotionModel? ResolvePotion(ItemLinkData link)
    {
        try
        {
            var modelId = ModelId.Deserialize(link.ModelIdStr);
            return ModelDb.GetById<PotionModel>(modelId);
        }
        catch { return null; }
    }

    public static RelicModel? ResolveRelic(ItemLinkData link)
    {
        try
        {
            var modelId = ModelId.Deserialize(link.ModelIdStr);
            return ModelDb.GetById<RelicModel>(modelId);
        }
        catch { return null; }
    }

    public static PowerModel? ResolvePower(PowerLinkData data)
    {
        try
        {
            var modelId = ModelId.Deserialize(data.PowerIdStr);
            return ModelDb.GetById<PowerModel>(modelId);
        }
        catch { return null; }
    }

    public static HoverTip? ResolvePowerHoverTip(PowerLinkData data, bool isPlayer)
    {
        try
        {
            var modelId = ModelId.Deserialize(data.PowerIdStr);
            var power = ModelDb.GetById<PowerModel>(modelId);

            if (!power.HasSmartDescription)
                return power.DumbHoverTip;

            var desc = power.SmartDescription;
            desc.Add("Amount", (decimal)data.Amount);
            desc.Add("OnPlayer", isPlayer);
            desc.Add("IsMultiplayer", true);
            desc.Add("PlayerCount", 2m);
            desc.Add("OwnerName", data.CreatureName);
            desc.Add("ApplierName", data.ApplierName);
            desc.Add("TargetName", "");
            desc.Add("singleStarIcon", "[img]res://images/packed/sprite_fonts/star_icon.png[/img]");
            desc.Add("energyPrefix", EnergyIconHelper.GetPrefix(power));

            try { power.DynamicVars.AddTo(desc); } catch { }

            if (!string.IsNullOrEmpty(data.ApplierName))
            {
                desc.AddObj("ApplierName", new StringVar("ApplierName", data.ApplierName));
                desc.AddObj("Applier", new StringVar("Applier", data.ApplierName));
            }
            if (!string.IsNullOrEmpty(data.CreatureName))
                desc.Add("OwnerName", data.CreatureName);

            return new HoverTip(power, desc.GetFormattedText(), true);
        }
        catch { return null; }
    }

    static string? ResolvePowerDisplayName(PowerLinkData data)
    {
        var power = ResolvePower(data);
        return power?.Title.GetFormattedText();
    }

    public static Color GetPowerColor(PowerLinkData data)
    {
        var power = ResolvePower(data);
        var type = power?.GetTypeForAmount(data.Amount) ?? PowerType.None;
        return type switch
        {
            PowerType.Buff => new Color("77ff67"),
            PowerType.Debuff => new Color("ff6563"),
            _ => new Color("FFF6E2")
        };
    }

    static string? ResolveDisplayName(ItemLinkData link)
    {
        try
        {
            return link.Type switch
            {
                ItemLinkType.Card => ResolveCard(link)?.Title,
                ItemLinkType.Potion => ResolvePotion(link)?.Title.GetFormattedText(),
                ItemLinkType.Relic => ResolveRelic(link)?.Title.GetFormattedText(),
                _ => null
            };
        }
        catch { return null; }
    }

    public static string GetItemTypeLabel(ItemLinkType type) => type switch
    {
        ItemLinkType.Card => "提示了卡牌",
        ItemLinkType.Potion => "提示了药水",
        ItemLinkType.Relic => "提示了遗物",
        _ => ""
    };

    public static Color GetRarityColor(ItemLinkData link)
    {
        try
        {
            return link.Type switch
            {
                ItemLinkType.Card => GetCardRarityColor(ResolveCard(link)?.Rarity ?? CardRarity.Common),
                ItemLinkType.Potion => GetPotionRarityColor(ResolvePotion(link)?.Rarity ?? PotionRarity.Common),
                ItemLinkType.Relic => GetRelicRarityColor(ResolveRelic(link)?.Rarity ?? RelicRarity.Common),
                _ => Colors.White
            };
        }
        catch { return Colors.White; }
    }

    static Color GetCardRarityColor(CardRarity rarity) => rarity switch
    {
        CardRarity.Basic or CardRarity.Common => new Color("9C9C9C"),
        CardRarity.Uncommon => new Color("64FFFF"),
        CardRarity.Rare => new Color("FFDA36"),
        CardRarity.Curse => new Color("E669FF"),
        CardRarity.Event => new Color("13BE1A"),
        CardRarity.Quest => new Color("F46836"),
        _ => new Color("9C9C9C")
    };

    static Color GetRelicRarityColor(RelicRarity rarity) => rarity switch
    {
        RelicRarity.Uncommon or RelicRarity.Shop => new Color("87CEEB"),
        RelicRarity.Rare => new Color("EFC851"),
        RelicRarity.Event => new Color("7FFF00"),
        RelicRarity.Ancient => new Color("FF5555"),
        _ => new Color("FFF6E2")
    };

    static Color GetPotionRarityColor(PotionRarity rarity) => rarity switch
    {
        PotionRarity.Uncommon => new Color("87CEEB"),
        PotionRarity.Rare => new Color("EFC851"),
        PotionRarity.Event => new Color("7FFF00"),
        _ => new Color("FFF6E2")
    };

    static bool TryParseType(string typeStr, out ItemLinkType type)
    {
        type = default;
        switch (typeStr)
        {
            case "card": type = ItemLinkType.Card; return true;
            case "potion": type = ItemLinkType.Potion; return true;
            case "relic": type = ItemLinkType.Relic; return true;
            default: return false;
        }
    }
}
