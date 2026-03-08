# sts2_typing

适用于 [Slay the Spire 2](https://store.steampowered.com/app/646570/Slay_the_Spire_2/) 的游戏内聊天 Mod。支持实时发送消息、分享 Emoji，并可将卡牌、遗物、药水、能力以可交互链接的形式插入聊天。

## 功能

- **聊天面板** — 悬浮于屏幕角落，不操作时自动淡出，不影响游戏体验
- **物品链接** — `Alt + 左键` 任意物品，在聊天中生成带悬停预览的可点击链接
- **Emoji 面板** — 内置 18 个 Emoji（由 [Lucide](https://lucide.dev) 提供）
- **多人同步** — 消息通过游戏内置网络广播给所有在线玩家
- **键盘操作** — `Enter` 打开 / 发送，`Esc` 关闭
- **多语言** — 自动跟随游戏语言，支持 15 种语言

## 快捷键

| 操作 | 按键 |
|---|---|
| 打开 / 发送消息 | `Enter` |
| 关闭聊天框或 Emoji 选择器 | `Esc` |
| 分享当前悬停的物品 | `Alt + 左键` |
| 打开 Emoji 选择器 | 点击输入框内的 Emoji 按钮 |

## 安装

1. 从 [最新 Release](https://github.com/Shiroim/sts2_typing/releases/latest) 下载 `typing.zip`
2. 解压到游戏 Mods 文件夹：
   ```
   <Steam库路径>/steamapps/common/Slay the Spire 2/Mods/
   ```
3. 启动游戏，在 Mod 菜单中启用

## 从源码构建

**环境要求**

- [.NET SDK](https://dotnet.microsoft.com/download)（版本与游戏目录中 `global.json` 一致）
- 支持 .NET 的 [Godot 4](https://godotengine.org/)
- Slay the Spire 2 游戏本体

**步骤**

1. 克隆本仓库
2. 按需修改 `typing.csproj` 中的游戏路径引用
3. 执行 `dotnet build`
4. 通过 Godot 导出，或将 `.pck` / `.dll` 手动复制到 Mods 文件夹

## 物品链接格式

消息中以 `{{type:id}}` 编码，渲染为带样式的可交互文字：

| 类型 | 示例 |
|---|---|
| 卡牌 | `{{card:MegaCrit.Sts2.Cards.Strike:0}}` |
| 药水 | `{{potion:MegaCrit.Sts2.Potions.FirePotion}}` |
| 遗物 | `{{relic:MegaCrit.Sts2.Relics.BurningBlood}}` |
| 能力 / 目标生物 | 通过 `Alt + 左键` 自动插入 |

## 开源协议

[MIT License](LICENSE)。图标来自 [Lucide](https://lucide.dev)（ISC License）。

*本 Mod 与 MegaCrit 官方无关，亦未获得官方背书。*

---

# sts2_typing

An in-game chat mod for [Slay the Spire 2](https://store.steampowered.com/app/646570/Slay_the_Spire_2/). Send messages in real time, share Emoji, and insert cards, relics, potions, and powers as interactive links.

## Features

- **Chat panel** — Floats in the corner of the screen and fades out when idle
- **Item links** — `Alt + Left-click` any item to insert a hoverable link into chat
- **Emoji picker** — 18 built-in icons via [Lucide](https://lucide.dev)
- **Multiplayer sync** — Messages broadcast to all connected players over the built-in network
- **Keyboard-first** — `Enter` to open/send, `Esc` to close
- **Localized** — Automatically follows the game language; 15 languages supported

## Keybindings

| Action | Key |
|---|---|
| Open / Send message | `Enter` |
| Close chat or Emoji picker | `Esc` |
| Share hovered item | `Alt + Left-click` |
| Open Emoji picker | Click the Emoji button in the input box |

## Installation

1. Download `typing.zip` from the [latest release](https://github.com/Shiroim/sts2_typing/releases/latest)
2. Extract into the game's Mods folder:
   ```
   <Steam library>/steamapps/common/Slay the Spire 2/Mods/
   ```
3. Launch the game and enable the mod in the Mod menu

## Building from Source

**Requirements**

- [.NET SDK](https://dotnet.microsoft.com/download) matching the version in the game's `global.json`
- [Godot 4](https://godotengine.org/) with .NET support
- A copy of Slay the Spire 2

**Steps**

1. Clone this repository
2. Edit the game path reference in `typing.csproj` if needed
3. Run `dotnet build`
4. Export via Godot or copy the `.pck` / `.dll` to the Mods folder manually

## Item Link Format

Links are encoded as `{{type:id}}` in message text and rendered as styled interactive spans:

| Type | Example |
|---|---|
| Card | `{{card:MegaCrit.Sts2.Cards.Strike:0}}` |
| Potion | `{{potion:MegaCrit.Sts2.Potions.FirePotion}}` |
| Relic | `{{relic:MegaCrit.Sts2.Relics.BurningBlood}}` |
| Power / Creature | Auto-inserted via `Alt + Left-click` |

## License

[MIT License](LICENSE). Icons from [Lucide](https://lucide.dev) (ISC License).

*This mod is not affiliated with or endorsed by MegaCrit.*
