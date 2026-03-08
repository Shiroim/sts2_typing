using System.Collections.Generic;
using Godot;

namespace Typing;

public static class L10n
{
    static readonly Dictionary<string, Dictionary<string, string>> Translations = new()
    {
        ["en"] = new()
        {
            ["input_placeholder"] = "Enter to send, Esc to cancel...",
            ["power_status"] = "pinged {creature}'s status: ",
            ["target_alert"] = "pinged {creature}",
            ["shared_card"] = "shared a card",
            ["shared_potion"] = "shared a potion",
            ["shared_relic"] = "shared a relic",
        },
        ["zh_CN"] = new()
        {
            ["input_placeholder"] = "Enter 发送，Esc 取消...",
            ["power_status"] = "提示了{creature}的状态: ",
            ["target_alert"] = "提示注意{creature}",
            ["shared_card"] = "提示了卡牌",
            ["shared_potion"] = "提示了药水",
            ["shared_relic"] = "提示了遗物",
        },
        ["zh_TW"] = new()
        {
            ["input_placeholder"] = "Enter 傳送，Esc 取消...",
            ["power_status"] = "提示了{creature}的狀態: ",
            ["target_alert"] = "提示注意{creature}",
            ["shared_card"] = "提示了卡牌",
            ["shared_potion"] = "提示了藥水",
            ["shared_relic"] = "提示了遺物",
        },
        ["ja"] = new()
        {
            ["input_placeholder"] = "Enter で送信、Esc でキャンセル...",
            ["power_status"] = "{creature}のステータスを通知: ",
            ["target_alert"] = "{creature}に注意",
            ["shared_card"] = "カードを共有",
            ["shared_potion"] = "ポーションを共有",
            ["shared_relic"] = "レリックを共有",
        },
        ["ko"] = new()
        {
            ["input_placeholder"] = "Enter: 전송, Esc: 취소...",
            ["power_status"] = "{creature}의 상태를 알림: ",
            ["target_alert"] = "{creature}에 주의",
            ["shared_card"] = "카드를 공유",
            ["shared_potion"] = "포션을 공유",
            ["shared_relic"] = "유물을 공유",
        },
        ["fr"] = new()
        {
            ["input_placeholder"] = "Entrée pour envoyer, Échap pour annuler...",
            ["power_status"] = "a signalé le statut de {creature} : ",
            ["target_alert"] = "a signalé {creature}",
            ["shared_card"] = "a partagé une carte",
            ["shared_potion"] = "a partagé une potion",
            ["shared_relic"] = "a partagé une relique",
        },
        ["de"] = new()
        {
            ["input_placeholder"] = "Enter zum Senden, Esc zum Abbrechen...",
            ["power_status"] = "hat {creature}s Status gemeldet: ",
            ["target_alert"] = "hat {creature} markiert",
            ["shared_card"] = "hat eine Karte geteilt",
            ["shared_potion"] = "hat einen Trank geteilt",
            ["shared_relic"] = "hat ein Relikt geteilt",
        },
        ["es"] = new()
        {
            ["input_placeholder"] = "Enter para enviar, Esc para cancelar...",
            ["power_status"] = "señaló el estado de {creature}: ",
            ["target_alert"] = "señaló a {creature}",
            ["shared_card"] = "compartió una carta",
            ["shared_potion"] = "compartió una poción",
            ["shared_relic"] = "compartió una reliquia",
        },
        ["it"] = new()
        {
            ["input_placeholder"] = "Invio per inviare, Esc per annullare...",
            ["power_status"] = "ha segnalato lo stato di {creature}: ",
            ["target_alert"] = "ha segnalato {creature}",
            ["shared_card"] = "ha condiviso una carta",
            ["shared_potion"] = "ha condiviso una pozione",
            ["shared_relic"] = "ha condiviso una reliquia",
        },
        ["pt_BR"] = new()
        {
            ["input_placeholder"] = "Enter para enviar, Esc para cancelar...",
            ["power_status"] = "sinalizou o estado de {creature}: ",
            ["target_alert"] = "sinalizou {creature}",
            ["shared_card"] = "compartilhou uma carta",
            ["shared_potion"] = "compartilhou uma poção",
            ["shared_relic"] = "compartilhou uma relíquia",
        },
        ["ru"] = new()
        {
            ["input_placeholder"] = "Enter — отправить, Esc — отмена...",
            ["power_status"] = "указал на состояние {creature}: ",
            ["target_alert"] = "обратил внимание на {creature}",
            ["shared_card"] = "показал карту",
            ["shared_potion"] = "показал зелье",
            ["shared_relic"] = "показал реликвию",
        },
        ["pl"] = new()
        {
            ["input_placeholder"] = "Enter aby wysłać, Esc aby anulować...",
            ["power_status"] = "wskazał status {creature}: ",
            ["target_alert"] = "zwrócił uwagę na {creature}",
            ["shared_card"] = "udostępnił kartę",
            ["shared_potion"] = "udostępnił miksturę",
            ["shared_relic"] = "udostępnił relikt",
        },
        ["tr"] = new()
        {
            ["input_placeholder"] = "Göndermek için Enter, iptal için Esc...",
            ["power_status"] = "{creature} durumunu bildirdi: ",
            ["target_alert"] = "{creature} hedefini işaretledi",
            ["shared_card"] = "bir kart paylaştı",
            ["shared_potion"] = "bir iksir paylaştı",
            ["shared_relic"] = "bir kalıntı paylaştı",
        },
        ["th"] = new()
        {
            ["input_placeholder"] = "Enter ส่ง, Esc ยกเลิก...",
            ["power_status"] = "แจ้งสถานะของ {creature}: ",
            ["target_alert"] = "แจ้งเตือน {creature}",
            ["shared_card"] = "แชร์การ์ด",
            ["shared_potion"] = "แชร์ยา",
            ["shared_relic"] = "แชร์เรลิก",
        },
        ["id"] = new()
        {
            ["input_placeholder"] = "Enter untuk kirim, Esc untuk batal...",
            ["power_status"] = "menandai status {creature}: ",
            ["target_alert"] = "menandai {creature}",
            ["shared_card"] = "membagikan kartu",
            ["shared_potion"] = "membagikan ramuan",
            ["shared_relic"] = "membagikan relik",
        },
    };

    static string ResolveLocale()
    {
        string raw = TranslationServer.GetLocale();

        if (Translations.ContainsKey(raw))
            return raw;

        if (raw.StartsWith("zh"))
            return raw.Contains("TW") || raw.Contains("HK") ? "zh_TW" : "zh_CN";

        if (raw.StartsWith("pt"))
            return "pt_BR";

        string lang = raw.Split('_')[0];
        if (Translations.ContainsKey(lang))
            return lang;

        return "en";
    }

    public static string Get(string key)
    {
        string locale = ResolveLocale();
        if (Translations.TryGetValue(locale, out var table) && table.TryGetValue(key, out var val))
            return val;
        if (Translations.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var fbVal))
            return fbVal;
        return key;
    }
}
