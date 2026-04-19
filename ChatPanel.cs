using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Platform;
using MegaCrit.Sts2.Core.Runs;

namespace Typing;

public partial class ChatPanel : CanvasLayer
{
    const int MaxMessages = 50;
    const int MaxInputLength = 200;
    const float FadeDelay = 5f;
    const float FadeDuration = 0.6f;
    const float PanelWidth = 420f;
    const float MessageAreaHeight = 250f;
    const float InputBarHeight = 36f;
    const float TopOffset = 100f;
    const float Margin = 30f;
    const int FontSize = 18;
    const int EmojiDisplaySize = 20;
    const int EmojiButtonSize = 22;
    const int EmojiColumns = 9;
    const int AutocompleteMaxResults = 8;
    const int AutocompleteFontSize = 16;
    const float AutocompleteItemHeight = 28f;
    const float NetworkCheckInterval = 1.0f;

    static readonly Color BgColor = new(0.05f, 0.05f, 0.1f, 0.75f);
    static readonly Color InputBgColor = new(0.08f, 0.08f, 0.15f, 0.9f);
    static readonly Color InputBorderColor = new(0.3f, 0.35f, 0.5f, 0.6f);
    static readonly Color PlaceholderColor = new(0.55f, 0.55f, 0.65f);
    static readonly Color DefaultNameColor = new(0.85f, 0.85f, 0.85f);
    static readonly Color EmojiHoverColor = new(0.2f, 0.2f, 0.3f, 0.9f);

    Control _root = null!;
    Control _chatContainer = null!;
    PanelContainer _messagePanel = null!;
    ScrollContainer _scroll = null!;
    VBoxContainer _messageList = null!;
    PanelContainer _inputBar = null!;
    LineEdit _chatInput = null!;
    Button _emojiButton = null!;
    Control _emojiOverlay = null!;
    PanelContainer _emojiPopup = null!;
    PanelContainer _autocompletePopup = null!;
    VBoxContainer _autocompleteList = null!;

    bool _inputActive;
    bool _emojiOpen;
    bool _autocompleteOpen;
    bool _pendingScroll;
    INetGameService? _netService;
    bool _handlerRegistered;
    readonly List<RichTextLabel> _messageLabels = new();
    readonly List<ItemAutocompleteEntry> _autocompleteResults = new();
    readonly List<Button> _autocompleteButtons = new();
    int _autocompleteSelectedIndex = -1;
    int _autocompleteVisibleStart;
    int _autocompleteReplaceStart = -1;
    int _autocompleteReplaceEnd = -1;

    Tween? _fadeTween;
    float _fadeTimer;
    bool _faded;

    Control? _activePreview;
    string? _activePreviewMeta;

    float _networkCheckTimer;
    NCharacterSelectScreen? _cachedCharScreen;

    Font? _sharpFont;

    // Returns a high-quality version of Godot's default fallback font.
    // FontFile entries get per-font oversampling scaled to the window/viewport ratio;
    // SystemFont entries (used for CJK and other OS-provided glyphs) get MSDF enabled
    // so they render as resolution-independent vector shapes instead of upscaled bitmaps.
    // A final SystemFont+MSDF entry is appended as a safety net for any characters
    // that aren't covered by the built-in fallback chain.
    Font BuildSharpFont()
    {
        var windowSize = DisplayServer.WindowGetSize();
        float viewportWidth = (float)ProjectSettings.GetSetting("display/window/size/viewport_width");
        float oversampling = windowSize.X / viewportWidth * 2f;

        var root = WithSharpSettings(ThemeDB.Singleton.FallbackFont, oversampling);

        var fallbacks = root.Fallbacks;
        fallbacks.Add(new SystemFont { MultichannelSignedDistanceField = true, AllowSystemFallback = true });
        root.Fallbacks = fallbacks;

        return root;
    }

    // Duplicates a font and applies oversampling / MSDF to it and every entry in its
    // fallback chain so that all glyphs, regardless of which font actually provides
    // them, benefit from sharp rendering.
    static Font WithSharpSettings(Font source, float oversampling)
    {
        var dup = (Font)source.Duplicate();

        switch (dup)
        {
            case FontFile ff:
                ff.Oversampling = oversampling;
                break;
            case SystemFont sf:
                sf.MultichannelSignedDistanceField = true;
                break;
        }

        var fallbacks = dup.Fallbacks;
        for (int i = 0; i < fallbacks.Count; i++)
            if (fallbacks[i] is Font f)
                fallbacks[i] = WithSharpSettings(f, oversampling);
        dup.Fallbacks = fallbacks;

        return dup;
    }

    public override void _Ready()
    {
        Layer = 100;
        EmojiData.LoadAll();
        _sharpFont = BuildSharpFont();
        BuildUi();
        HideInput();
        CloseEmojiPopup();
        _chatContainer.Modulate = new Color(1, 1, 1, 0);
        _faded = true;
        ManageNetworkLifecycle();
    }

    public override void _ExitTree()
    {
        UnregisterHandler();
        DismissPreview();
    }

    public override void _Process(double delta)
    {
        _networkCheckTimer -= (float)delta;
        if (_networkCheckTimer <= 0f)
        {
            _networkCheckTimer = NetworkCheckInterval;
            ManageNetworkLifecycle();
        }

        if (!_inputActive)
        {
            _fadeTimer -= (float)delta;
            if (_fadeTimer <= 0f && !_faded)
                FadeOut();
        }

        UpdatePreviewPosition();
        if (_autocompleteOpen)
            PositionAutocompletePopup();
    }

    public override void _Input(InputEvent evt)
    {
        if (evt is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, AltPressed: true })
        {
            if (TrySendHoveredItemLink())
                GetViewport().SetInputAsHandled();
            return;
        }

        if (evt is not InputEventKey { Pressed: true } key)
            return;

        if (_inputActive)
        {
            if (HandleAutocompleteInput(key))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Escape)
            {
                if (_emojiOpen)
                    CloseEmojiPopup();
                else
                    CloseInput();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            {
                SubmitMessage();
                CloseInput();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if ((key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            && !key.AltPressed && !key.CtrlPressed)
        {
            var focused = GetViewport().GuiGetFocusOwner();
            if (focused is TextEdit or LineEdit)
                return;

            OpenInput();
            GetViewport().SetInputAsHandled();
        }
    }

    #region UI Construction

    void BuildUi()
    {
        _root = new Control
        {
            Name = "ChatRoot",
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_root);

        _chatContainer = new Control
        {
            Name = "ChatContainer",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _root.AddChild(_chatContainer);
        AnchorChatContainer();

        _messagePanel = new PanelContainer
        {
            Name = "MessagePanel",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        ApplyPanelStyle(_messagePanel, BgColor, 6);
        _messagePanel.Position = Vector2.Zero;
        _messagePanel.Size = new Vector2(PanelWidth, MessageAreaHeight);
        _chatContainer.AddChild(_messagePanel);

        _scroll = new ScrollContainer
        {
            Name = "ScrollContainer",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _messagePanel.AddChild(_scroll);

        _messageList = new VBoxContainer
        {
            Name = "MessageList",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _messageList.AddThemeConstantOverride("separation", 4);
        _scroll.AddChild(_messageList);

        _scroll.GetVScrollBar().Changed += OnScrollRangeChanged;

        _inputBar = new PanelContainer
        {
            Name = "InputBar",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        ApplyPanelStyle(_inputBar, InputBgColor, 6);
        _inputBar.Position = new Vector2(0, MessageAreaHeight + 4);
        _inputBar.Size = new Vector2(PanelWidth, InputBarHeight);
        _chatContainer.AddChild(_inputBar);

        var hbox = new HBoxContainer
        {
            Name = "InputHBox",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        hbox.AddThemeConstantOverride("separation", 4);
        _inputBar.AddChild(hbox);

        _chatInput = new LineEdit
        {
            Name = "ChatInput",
            PlaceholderText = L10n.Get("input_placeholder"),
            MaxLength = MaxInputLength,
            CaretBlink = true,
            ContextMenuEnabled = false,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        ApplyInputStyle(_chatInput);
        _chatInput.TextChanged += OnChatInputTextChanged;
        hbox.AddChild(_chatInput);

        _emojiButton = new Button
        {
            Name = "EmojiButton",
            CustomMinimumSize = new Vector2(InputBarHeight - 4, InputBarHeight - 4),
            TooltipText = "Emoji",
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        ApplyEmojiButtonStyle(_emojiButton);
        if (EmojiData.TryGetTexture("smile", out var smileTex))
        {
            var icon = new TextureRect
            {
                Texture = smileTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(20, 20),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                LayoutMode = 1,
                AnchorsPreset = (int)Control.LayoutPreset.Center
            };
            _emojiButton.AddChild(icon);
        }
        _emojiButton.Pressed += OnEmojiButtonPressed;
        hbox.AddChild(_emojiButton);

        BuildEmojiPopup();
        BuildAutocompletePopup();
        _messagePanel.Visible = false;
    }

    void BuildEmojiPopup()
    {
        _emojiOverlay = new Control
        {
            Name = "EmojiOverlay",
            AnchorsPreset = (int)Control.LayoutPreset.FullRect,
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        var overlayStyle = new StyleBoxEmpty();
        _emojiOverlay.AddThemeStyleboxOverride("panel", overlayStyle);
        _emojiOverlay.GuiInput += OnOverlayInput;
        _root.AddChild(_emojiOverlay);

        _emojiPopup = new PanelContainer
        {
            Name = "EmojiPopup",
            Visible = false
        };
        ApplyPanelStyle(_emojiPopup, InputBgColor, 8);
        _root.AddChild(_emojiPopup);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _emojiPopup.AddChild(margin);

        var grid = new GridContainer
        {
            Name = "EmojiGrid",
            Columns = EmojiColumns,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        grid.AddThemeConstantOverride("h_separation", 2);
        grid.AddThemeConstantOverride("v_separation", 2);
        margin.AddChild(grid);

        Color tint = GetLocalPlayerColor();

        foreach (var (name, _) in EmojiData.Emojis)
        {
            if (!EmojiData.TryGetTexture(name, out var tex)) continue;

            var btn = new TextureButton
            {
                Name = $"Emoji_{name}",
                CustomMinimumSize = new Vector2(EmojiButtonSize, EmojiButtonSize),
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                TextureNormal = tex,
                Modulate = tint,
                MouseFilter = Control.MouseFilterEnum.Stop,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var emojiName = name;
            btn.Pressed += () => OnEmojiSelected(emojiName);
            grid.AddChild(btn);
        }
    }

    void BuildAutocompletePopup()
    {
        _autocompletePopup = new PanelContainer
        {
            Name = "AutocompletePopup",
            Visible = false
        };
        ApplyPanelStyle(_autocompletePopup, InputBgColor, 8);
        _root.AddChild(_autocompletePopup);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_right", 4);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _autocompletePopup.AddChild(margin);

        _autocompleteList = new VBoxContainer
        {
            Name = "AutocompleteList",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _autocompleteList.AddThemeConstantOverride("separation", 2);
        margin.AddChild(_autocompleteList);
    }

    void PositionEmojiPopup()
    {
        float popupW = PanelWidth;
        int rows = (EmojiData.Emojis.Length + EmojiColumns - 1) / EmojiColumns;
        float popupH = rows * (EmojiButtonSize + 2) + 8 + 16;

        var containerPos = _chatContainer.Position;
        float x = containerPos.X - 40;
        float y = containerPos.Y + MessageAreaHeight + 4 + InputBarHeight + 4;

        _emojiPopup.Position = new Vector2(x, y);
        _emojiPopup.Size = new Vector2(popupW, popupH);
    }

    void PositionAutocompletePopup()
    {
        int visibleCount = GetVisibleAutocompleteCount();
        float popupH = visibleCount * (AutocompleteItemHeight + 2f) + 18f;
        var containerPos = _chatContainer.Position;

        _autocompletePopup.Position = new Vector2(
            containerPos.X,
            containerPos.Y + MessageAreaHeight + 4 + InputBarHeight + 4);
        _autocompletePopup.Size = new Vector2(PanelWidth, popupH);
    }

    void AnchorChatContainer()
    {
        float totalHeight = MessageAreaHeight + 4 + InputBarHeight;
        var viewport = GetViewport().GetVisibleRect().Size;
        _chatContainer.Position = new Vector2(
            viewport.X - PanelWidth - Margin,
            TopOffset
        );
        _chatContainer.Size = new Vector2(PanelWidth, totalHeight);
    }

    static void ApplyPanelStyle(PanelContainer panel, Color bgColor, int cornerRadius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bgColor,
            CornerRadiusTopLeft = cornerRadius,
            CornerRadiusTopRight = cornerRadius,
            CornerRadiusBottomLeft = cornerRadius,
            CornerRadiusBottomRight = cornerRadius,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    void ApplyInputStyle(LineEdit input)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.3f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            BorderColor = InputBorderColor,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        input.AddThemeStyleboxOverride("normal", normal);
        input.AddThemeStyleboxOverride("focus", normal);
        input.AddThemeColorOverride("font_color", Colors.White);
        input.AddThemeColorOverride("font_placeholder_color", PlaceholderColor);
        if (_sharpFont != null) input.AddThemeFontOverride("font", _sharpFont);
        input.AddThemeFontSizeOverride("font_size", FontSize);
    }

    static void ApplyEmojiButtonStyle(Button button)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.2f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(0.15f, 0.15f, 0.25f, 0.8f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        var pressed = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.1f, 0.2f, 0.9f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", pressed);
        button.AddThemeStyleboxOverride("focus", normal);
    }

    #endregion

    #region Input State

    void OpenInput()
    {
        _inputActive = true;
        _inputBar.Visible = true;
        _inputBar.MouseFilter = Control.MouseFilterEnum.Stop;
        _chatInput.PlaceholderText = L10n.Get("input_placeholder");
        _chatInput.Text = string.Empty;
        CloseAutocompletePopup();
        _chatInput.CallDeferred(Control.MethodName.GrabFocus);
        FadeIn();
    }

    void CloseInput()
    {
        _inputActive = false;
        CloseEmojiPopup();
        CloseAutocompletePopup();
        _chatInput.ReleaseFocus();
        HideInput();
        ResetFadeTimer();
    }

    void HideInput()
    {
        _inputBar.Visible = false;
        _inputBar.MouseFilter = Control.MouseFilterEnum.Ignore;
        CloseAutocompletePopup();
    }

    #endregion

    #region Autocomplete

    void OnChatInputTextChanged(string _)
    {
        if (_autocompleteOpen)
            RefreshAutocomplete(forceOpen: true);
    }

    bool HandleAutocompleteInput(InputEventKey key)
    {
        switch (key.Keycode)
        {
            case Key.Tab:
                return HandleAutocompleteTab(key.ShiftPressed);
            case Key.Up:
                if (!_autocompleteOpen) return false;
                MoveAutocompleteSelection(-1);
                return true;
            case Key.Down:
                if (!_autocompleteOpen) return false;
                MoveAutocompleteSelection(1);
                return true;
            case Key.Enter:
            case Key.KpEnter:
                if (!_autocompleteOpen || _autocompleteSelectedIndex < 0) return false;
                AcceptAutocompleteSelection(_autocompleteSelectedIndex);
                return true;
            case Key.Escape:
                if (!_autocompleteOpen) return false;
                CloseAutocompletePopup();
                return true;
            default:
                return false;
        }
    }

    bool HandleAutocompleteTab(bool reverse)
    {
        bool wasOpen = _autocompleteOpen;
        if (!RefreshAutocomplete(forceOpen: true))
            return false;

        if (!wasOpen || _autocompleteSelectedIndex < 0)
            _autocompleteSelectedIndex = 0;
        else
            MoveAutocompleteSelection(reverse ? -1 : 1);

        RefreshAutocompleteButtons();
        return true;
    }

    bool RefreshAutocomplete(bool forceOpen)
    {
        if (!TryGetAutocompleteContext(out int replaceStart, out int replaceEnd, out string prefix))
        {
            CloseAutocompletePopup();
            return false;
        }

        var matches = ChatItemLink.FindAutocompleteEntries(prefix);
        if (matches.Count == 0)
        {
            CloseAutocompletePopup();
            return false;
        }

        string? selectedName = _autocompleteSelectedIndex >= 0
            && _autocompleteSelectedIndex < _autocompleteResults.Count
            ? _autocompleteResults[_autocompleteSelectedIndex].DisplayName
            : null;

        _autocompleteReplaceStart = replaceStart;
        _autocompleteReplaceEnd = replaceEnd;
        _autocompleteResults.Clear();
        _autocompleteResults.AddRange(matches);
        _autocompleteSelectedIndex = selectedName is null
            ? 0
            : _autocompleteResults.FindIndex(entry => entry.DisplayName == selectedName);
        if (_autocompleteSelectedIndex < 0)
            _autocompleteSelectedIndex = 0;

        EnsureAutocompleteSelectionVisible();

        if (forceOpen || _autocompleteOpen)
            OpenAutocompletePopup();

        return true;
    }

    bool TryGetAutocompleteContext(out int replaceStart, out int replaceEnd, out string prefix)
    {
        replaceStart = -1;
        replaceEnd = -1;
        prefix = string.Empty;

        string text = _chatInput.Text;
        int caret = Math.Clamp(_chatInput.CaretColumn, 0, text.Length);
        if (text.Length == 0)
            return false;

        int start = caret;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1]))
            start--;

        int end = caret;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;

        prefix = text[start..caret].Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            return false;

        replaceStart = start;
        replaceEnd = end;
        return true;
    }

    void OpenAutocompletePopup()
    {
        _autocompleteOpen = true;
        PositionAutocompletePopup();
        RefreshAutocompleteButtons();
        _autocompletePopup.Visible = true;
    }

    void CloseAutocompletePopup()
    {
        _autocompleteOpen = false;
        _autocompletePopup.Visible = false;
        _autocompleteResults.Clear();
        _autocompleteSelectedIndex = -1;
        _autocompleteVisibleStart = 0;
        _autocompleteReplaceStart = -1;
        _autocompleteReplaceEnd = -1;

        foreach (var button in _autocompleteButtons)
            button.QueueFree();
        _autocompleteButtons.Clear();
    }

    void RefreshAutocompleteButtons()
    {
        foreach (var button in _autocompleteButtons)
            button.QueueFree();
        _autocompleteButtons.Clear();

        int visibleStart = GetAutocompleteVisibleStart();
        int visibleEnd = Math.Min(visibleStart + AutocompleteMaxResults, _autocompleteResults.Count);

        for (int i = visibleStart; i < visibleEnd; i++)
        {
            int index = i;
            var entry = _autocompleteResults[i];
            var button = new Button
            {
                Text = $"[{ChatItemLink.GetAutocompleteTypeLabel(entry.Link.Type)}] {entry.DisplayName}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(PanelWidth - 16f, AutocompleteItemHeight),
                FocusMode = Control.FocusModeEnum.None,
                ClipText = true,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            ApplyAutocompleteButtonStyle(
                button,
                i == _autocompleteSelectedIndex,
                ChatItemLink.GetRarityColor(entry.Link));
            if (_sharpFont != null) button.AddThemeFontOverride("font", _sharpFont);
            button.AddThemeFontSizeOverride("font_size", AutocompleteFontSize);
            button.Pressed += () => AcceptAutocompleteSelection(index);
            _autocompleteList.AddChild(button);
            _autocompleteButtons.Add(button);
        }
    }

    void ApplyAutocompleteButtonStyle(Button button, bool selected, Color textColor)
    {
        var normal = new StyleBoxFlat
        {
            BgColor = selected ? new Color(0.22f, 0.27f, 0.4f, 0.95f) : new Color(0f, 0f, 0f, 0.2f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };
        var hover = new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.23f, 0.34f, 0.95f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", hover);
        button.AddThemeStyleboxOverride("pressed", hover);
        button.AddThemeStyleboxOverride("focus", normal);
        button.AddThemeColorOverride("font_color", textColor);
        button.AddThemeColorOverride("font_hover_color", textColor);
        button.AddThemeColorOverride("font_pressed_color", textColor);
        button.AddThemeColorOverride("font_focus_color", textColor);
    }

    void MoveAutocompleteSelection(int delta)
    {
        if (_autocompleteResults.Count == 0)
            return;

        _autocompleteSelectedIndex = (_autocompleteSelectedIndex + delta + _autocompleteResults.Count)
            % _autocompleteResults.Count;
        EnsureAutocompleteSelectionVisible();
        RefreshAutocompleteButtons();
    }

    void AcceptAutocompleteSelection(int index)
    {
        if (index < 0 || index >= _autocompleteResults.Count)
            return;

        string text = _chatInput.Text;
        int start = Math.Clamp(_autocompleteReplaceStart, 0, text.Length);
        int end = Math.Clamp(_autocompleteReplaceEnd, start, text.Length);

        string replacement = $"[{_autocompleteResults[index].DisplayName}]";
        if (end == text.Length)
            replacement += " ";

        _chatInput.Text = text[..start] + replacement + text[end..];
        _chatInput.CaretColumn = start + replacement.Length;
        CloseAutocompletePopup();
        _chatInput.GrabFocus();
    }

    int GetVisibleAutocompleteCount() => Math.Min(_autocompleteResults.Count, AutocompleteMaxResults);

    int GetAutocompleteVisibleStart()
    {
        int visibleCount = GetVisibleAutocompleteCount();
        if (visibleCount == 0)
            return 0;

        int maxStart = Math.Max(0, _autocompleteResults.Count - visibleCount);
        return Math.Clamp(_autocompleteVisibleStart, 0, maxStart);
    }

    void EnsureAutocompleteSelectionVisible()
    {
        int visibleCount = GetVisibleAutocompleteCount();
        if (visibleCount == 0 || _autocompleteSelectedIndex < 0)
        {
            _autocompleteVisibleStart = 0;
            return;
        }

        if (_autocompleteSelectedIndex < _autocompleteVisibleStart)
        {
            _autocompleteVisibleStart = _autocompleteSelectedIndex;
            return;
        }

        if (_autocompleteSelectedIndex >= _autocompleteVisibleStart + visibleCount)
            _autocompleteVisibleStart = _autocompleteSelectedIndex - visibleCount + 1;

        int maxStart = Math.Max(0, _autocompleteResults.Count - visibleCount);
        _autocompleteVisibleStart = Math.Clamp(_autocompleteVisibleStart, 0, maxStart);
    }

    #endregion

    #region Emoji Popup

    void OnEmojiButtonPressed()
    {
        if (_emojiOpen)
            CloseEmojiPopup();
        else
            OpenEmojiPopup();
    }

    void OpenEmojiPopup()
    {
        _emojiOpen = true;
        PositionEmojiPopup();
        UpdateEmojiTint();
        _emojiOverlay.Visible = true;
        _emojiPopup.Visible = true;
    }

    void CloseEmojiPopup()
    {
        _emojiOpen = false;
        _emojiOverlay.Visible = false;
        _emojiPopup.Visible = false;
    }

    void OnOverlayInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton { Pressed: true })
        {
            CloseEmojiPopup();
            GetViewport().SetInputAsHandled();
        }
    }

    void OnEmojiSelected(string emojiName)
    {
        CloseEmojiPopup();
        string emojiText = $":{emojiName}:";
        SendText(emojiText);
    }

    void UpdateEmojiTint()
    {
        Color tint = GetLocalPlayerColor();
        var grid = _emojiPopup.GetNode<MarginContainer>("MarginContainer")
            ?.GetChild<GridContainer>(0);
        if (grid is null) return;

        foreach (var child in grid.GetChildren())
        {
            if (child is TextureButton btn)
                btn.Modulate = tint;
        }
    }

    #endregion

    #region Messaging

    void SubmitMessage()
    {
        string text = ChatItemLink.ReplaceLocalizedItemShortcuts(_chatInput.Text.Trim());
        if (string.IsNullOrEmpty(text))
            return;
        SendText(text);
    }

    void SendText(string text)
    {
        if (_netService is not null)
        {
            _netService.SendMessage(new ChatMessage { text = text });
            AppendLocalMessage(_netService.NetId, text);
        }
        else
        {
            ulong localId = 0;
            try { localId = PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform); }
            catch { }
            AppendLocalMessage(localId, text);
        }
    }

    void HandleChatMessage(ChatMessage message, ulong senderId)
    {
        CallDeferred(MethodName.DeferredAppendMessage, (long)senderId, message.text);
    }

    void DeferredAppendMessage(long senderId, string text)
    {
        AppendRemoteMessage((ulong)senderId, text);
    }

    void AppendLocalMessage(ulong senderId, string text)
    {
        AddChatEntry(senderId, text);
    }

    void AppendRemoteMessage(ulong senderId, string text)
    {
        AddChatEntry(senderId, text);
    }

    void AddChatEntry(ulong senderId, string text)
    {
        _messagePanel.Visible = true;

        string playerName = GetPlayerName(senderId);
        Color nameColor = GetPlayerColor(senderId);

        bool hasLinks = ChatItemLink.ContainsLink(text);

        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = hasLinks ? Control.MouseFilterEnum.Pass : Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        if (_sharpFont != null) label.AddThemeFontOverride("normal_font", _sharpFont);
        label.AddThemeFontSizeOverride("normal_font_size", FontSize);
        label.AddThemeColorOverride("default_color", Colors.White);

        label.PushColor(nameColor);
        label.AddText(playerName);
        label.Pop();

        if (EmojiData.IsEmoji(text, out _, out var emojiTex))
        {
            label.AddText(": ");
            label.AddImage(emojiTex, EmojiDisplaySize, EmojiDisplaySize, nameColor);
        }
        else if (hasLinks)
        {
            var segments = ChatItemLink.Parse(text);
            var firstPower = segments.OfType<PowerSegment>().FirstOrDefault();
            var firstTarget = segments.OfType<TargetSegment>().FirstOrDefault();

            bool hasMeta = false;

            if (firstPower is not null)
            {
                var creatureColor = new Color(firstPower.Power.CreatureColorHex);
                var statusParts = L10n.Get("power_status").Split("{creature}");
                label.AddText(statusParts[0]);
                label.PushColor(creatureColor);
                label.AddText(firstPower.Power.CreatureName);
                label.Pop();
                if (statusParts.Length > 1) label.AddText(statusParts[1]);

                foreach (var seg in segments)
                {
                    switch (seg)
                    {
                        case TextSegment ts:
                            label.AddText(ts.Text);
                            break;
                        case PowerSegment ps:
                            bool isPlayer = ps.Power.CreatureColorHex != "FF5555";
                            string powerMeta = $"power:{ps.Power.PowerIdStr}:{ps.Power.Amount}:{(isPlayer ? 1 : 0)}:{ps.Power.ApplierName}";
                            label.PushMeta(powerMeta);
                            label.PushColor(ChatItemLink.GetPowerColor(ps.Power));
                            string amountStr = ps.Power.Amount != 0 ? $"{ps.Power.Amount}" : "";
                            label.AddText($"[{ps.DisplayName}{amountStr}]");
                            label.Pop();
                            label.Pop();
                            hasMeta = true;
                            break;
                    }
                }
            }
            else if (firstTarget is not null)
            {
                var creatureColor = new Color(firstTarget.Target.CreatureColorHex);
                var alertParts = L10n.Get("target_alert").Split("{creature}");
                label.AddText(alertParts[0]);
                label.PushColor(creatureColor);
                label.AddText(firstTarget.Target.CreatureName);
                label.Pop();
                if (alertParts.Length > 1) label.AddText(alertParts[1]);
            }
            else
            {
                var firstLink = segments.OfType<LinkSegment>().FirstOrDefault();
                string verb = firstLink is not null
                    ? ChatItemLink.GetItemTypeLabel(firstLink.Link.Type)
                    : "";
                label.AddText(verb + ": ");

                foreach (var seg in segments)
                {
                    switch (seg)
                    {
                        case TextSegment ts:
                            label.AddText(ts.Text);
                            break;
                        case LinkSegment ls:
                            label.PushMeta(ls.Link.MetaTag);
                            label.PushColor(ChatItemLink.GetRarityColor(ls.Link));
                            label.AddText($"[{ls.DisplayName}]");
                            label.Pop();
                            label.Pop();
                            hasMeta = true;
                            break;
                    }
                }
            }

            if (hasMeta)
            {
                label.MetaHoverStarted += meta => OnMetaHoverStarted(meta.AsString());
                label.MetaHoverEnded += meta => OnMetaHoverEnded(meta.AsString());
            }
        }
        else
        {
            label.AddText(": ");
            label.AddText(text);
        }

        _messageList.AddChild(label);
        _messageLabels.Add(label);

        while (_messageLabels.Count > MaxMessages)
        {
            var oldest = _messageLabels[0];
            _messageLabels.RemoveAt(0);
            oldest.QueueFree();
        }

        _pendingScroll = true;
        FadeIn();
        ResetFadeTimer();
    }

    void OnScrollRangeChanged()
    {
        if (_pendingScroll)
        {
            _scroll.ScrollVertical = (int)_scroll.GetVScrollBar().MaxValue;
            _pendingScroll = false;
        }
    }

    #endregion

    #region Item Link Sending

    bool TrySendHoveredItemLink()
    {
        var hovered = GetViewport().GuiGetHoveredControl();
        if (hovered is null) return false;
        if (hovered.IsAncestorOf(_root) || _root.IsAncestorOf(hovered) || hovered == _root)
            return false;

        Node? current = hovered;
        while (current is not null)
        {
            switch (current)
            {
                case NPower nPower when nPower.Model is { } pm:
                    SendText(ChatItemLink.EncodePower(pm, pm.Owner));
                    return true;

                case NCardHolder holder when holder.CardModel is { } card:
                    SendText(ChatItemLink.EncodeCard(card));
                    return true;

                case NPotionHolder potionHolder when potionHolder.Potion is { } potion:
                    SendText(ChatItemLink.EncodePotion(potion.Model));
                    return true;

                case NRelicInventoryHolder relicHolder when relicHolder.Relic?.Model is { } relic:
                    SendText(ChatItemLink.EncodeRelic(relic));
                    return true;

                case NCreature nCreature when nCreature.Entity is { } entity:
                    SendText(ChatItemLink.EncodeTarget(entity));
                    return true;
            }
            current = current.GetParent();
        }

        return false;
    }

    #endregion

    #region Item Link Preview

    void OnMetaHoverStarted(string meta)
    {
        if (_faded) return;
        if (meta == _activePreviewMeta) return;

        DismissPreview();

        Control? preview;

        if (TryParsePowerMeta(meta, out var powerData, out bool isPlayer))
        {
            preview = CreatePowerPreview(powerData, isPlayer);
        }
        else if (ChatItemLink.TryParseMeta(meta, out var link))
        {
            bool isCard = link.Type == ItemLinkType.Card;
            preview = isCard
                ? InstantiateCardContainer()
                : link.Type switch
                {
                    ItemLinkType.Potion => CreatePotionPreview(link),
                    ItemLinkType.Relic => CreateRelicPreview(link),
                    ItemLinkType.Power => CreateStandalonePowerPreview(link),
                    _ => null
                };

            if (preview is null) return;

            preview.MouseFilter = Control.MouseFilterEnum.Ignore;
            SetSubtreeMouseIgnore(preview);
            _root.AddChild(preview);

            if (isCard && !InitCardPreview(preview, link))
            {
                preview.QueueFree();
                return;
            }

            _activePreview = preview;
            _activePreviewMeta = meta;
            PositionPreviewAtMouse(preview);
            return;
        }
        else
        {
            return;
        }

        if (preview is null) return;

        preview.MouseFilter = Control.MouseFilterEnum.Ignore;
        SetSubtreeMouseIgnore(preview);
        _root.AddChild(preview);

        _activePreview = preview;
        _activePreviewMeta = meta;
        PositionPreviewAtMouse(preview);
    }

    void OnMetaHoverEnded(string meta)
    {
        if (_activePreviewMeta == meta)
            DismissPreview();
    }

    void DismissPreview()
    {
        if (_activePreview is not null && GodotObject.IsInstanceValid(_activePreview))
            _activePreview.QueueFree();
        _activePreview = null;
        _activePreviewMeta = null;
    }

    void UpdatePreviewPosition()
    {
        if (_activePreview is null || !GodotObject.IsInstanceValid(_activePreview))
            return;
        PositionPreviewAtMouse(_activePreview);
    }

    void PositionPreviewAtMouse(Control preview)
    {
        var mousePos = GetViewport().GetMousePosition();
        var vpSize = GetViewport().GetVisibleRect().Size;

        preview.ResetSize();
        float pw = preview.Size.X;
        float ph = preview.Size.Y;

        float x = mousePos.X - pw - 12f;
        float y = mousePos.Y - ph * 0.5f;

        if (x < 4f) x = mousePos.X + 16f;
        y = Math.Clamp(y, 4f, vpSize.Y - ph - 4f);
        x = Math.Clamp(x, 4f, vpSize.X - pw - 4f);

        preview.Position = new Vector2(x, y);
    }

    static Control? InstantiateCardContainer()
    {
        try
        {
            return PreloadManager.Cache
                .GetScene("res://scenes/ui/card_hover_tip.tscn")
                .Instantiate<Control>();
        }
        catch { return null; }
    }

    static bool InitCardPreview(Control container, ItemLinkData link)
    {
        var card = ChatItemLink.ResolveCard(link);
        if (card is null) return false;

        try
        {
            var nCard = container.GetNode<NCard>("%Card");
            nCard.Model = card;
            nCard.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
            return true;
        }
        catch { return false; }
    }

    Control? CreatePotionPreview(ItemLinkData link)
    {
        var potion = ChatItemLink.ResolvePotion(link);
        if (potion is null) return null;
        return CreateHoverTipControl(potion.HoverTip);
    }

    Control? CreateRelicPreview(ItemLinkData link)
    {
        var relic = ChatItemLink.ResolveRelic(link);
        if (relic is null) return null;
        return CreateHoverTipControl(relic.HoverTip);
    }

    Control? CreateStandalonePowerPreview(ItemLinkData link)
    {
        var tip = ChatItemLink.ResolvePowerHoverTip(link);
        if (tip is null) return null;
        return CreateHoverTipControl(tip.Value);
    }

    Control? CreatePowerPreview(PowerLinkData data, bool isPlayer)
    {
        var tip = ChatItemLink.ResolvePowerHoverTip(data, isPlayer);
        if (tip is null) return null;
        return CreateHoverTipControl(tip.Value);
    }

    static bool TryParsePowerMeta(string meta, out PowerLinkData data, out bool isPlayer)
    {
        data = default;
        isPlayer = true;
        if (!meta.StartsWith("power:")) return false;

        string[] parts = meta.Split(':');
        if (parts.Length < 3) return false;

        string powerId = parts[1];
        if (!int.TryParse(parts[2], out int amount)) return false;

        if (parts.Length >= 4)
            isPlayer = parts[3] == "1";

        string applierName = parts.Length >= 5 ? parts[4] : "";

        data = new PowerLinkData(powerId, amount, "", "", applierName);
        return true;
    }

    static Control? CreateHoverTipControl(HoverTip tip)
    {
        try
        {
            var scene = PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn");
            var control = scene.Instantiate<Control>();

            var title = control.GetNode<MegaLabel>("%Title");
            if (tip.Title is null)
                title.Visible = false;
            else
                title.SetTextAutoSize(tip.Title);

            control.GetNode<MegaRichTextLabel>("%Description").Text = tip.Description;
            control.GetNode<TextureRect>("%Icon").Texture = tip.Icon;

            if (tip.IsDebuff)
            {
                var bg = control.GetNode<CanvasItem>("%Bg");
                bg.Material = PreloadManager.Cache.GetMaterial("res://materials/ui/hover_tip_debuff.tres");
            }

            control.ResetSize();
            return control;
        }
        catch { return null; }
    }

    static void SetSubtreeMouseIgnore(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Control c)
                c.MouseFilter = Control.MouseFilterEnum.Ignore;
            SetSubtreeMouseIgnore(child);
        }
    }

    #endregion

    #region Fade Animation

    void ResetFadeTimer()
    {
        _fadeTimer = FadeDelay;
        _faded = false;
    }

    void FadeIn()
    {
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_chatContainer, "modulate:a", 1.0f, 0.2f);
        _faded = false;
        ResetFadeTimer();
    }

    void FadeOut()
    {
        if (_inputActive) return;
        DismissPreview();
        _fadeTween?.Kill();
        _fadeTween = CreateTween();
        _fadeTween.TweenProperty(_chatContainer, "modulate:a", 0.0f, FadeDuration);
        _faded = true;
    }

    #endregion

    #region Network Lifecycle

    void ManageNetworkLifecycle()
    {
        var available = TryGetNetService();

        if (available is not null && !_handlerRegistered)
        {
            RegisterHandler(available);
        }
        else if (available is not null && _handlerRegistered && available != _netService)
        {
            UnregisterHandler();
            RegisterHandler(available);
        }
        else if (available is null && _handlerRegistered)
        {
            UnregisterHandler();
        }
    }

    void RegisterHandler(INetGameService netService)
    {
        _netService = netService;
        _netService.RegisterMessageHandler<ChatMessage>(HandleChatMessage);
        _handlerRegistered = true;
    }

    void UnregisterHandler()
    {
        if (_handlerRegistered && _netService is not null)
        {
            try { _netService.UnregisterMessageHandler<ChatMessage>(HandleChatMessage); }
            catch { }
        }
        _netService = null;
        _handlerRegistered = false;
        _cachedCharScreen = null;
    }

    INetGameService? TryGetNetService()
    {
        var rm = RunManager.Instance;
        if (rm is not null && rm.IsInProgress
            && rm.NetService?.Type.IsMultiplayer() == true
            && rm.NetService.IsConnected)
            return rm.NetService;

        var charScreen = FindCharacterSelectScreenCached();
        if (charScreen?.Lobby?.NetService is { } lobbyNet
            && lobbyNet.Type.IsMultiplayer()
            && lobbyNet.IsConnected)
            return lobbyNet;

        return null;
    }

    NCharacterSelectScreen? FindCharacterSelectScreenCached()
    {
        if (_cachedCharScreen is not null
            && GodotObject.IsInstanceValid(_cachedCharScreen)
            && _cachedCharScreen.IsInsideTree())
            return _cachedCharScreen;

        var game = NGame.Instance;
        if (game is null) return null;
        var scene = game.RootSceneContainer?.CurrentScene;
        if (scene is null) return null;

        _cachedCharScreen = FindChildOfType<NCharacterSelectScreen>(scene);
        return _cachedCharScreen;
    }

    static T? FindChildOfType<T>(Node root) where T : class
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
                return match;
            var found = FindChildOfType<T>(child);
            if (found is not null)
                return found;
        }
        return null;
    }

    #endregion

    #region Player Info

    string GetPlayerName(ulong senderId)
    {
        if (_netService is null)
            return senderId.ToString();

        try
        {
            return PlatformUtil.GetPlayerName(_netService.Platform, senderId);
        }
        catch
        {
            return senderId.ToString();
        }
    }

    Color GetPlayerColor(ulong senderId)
    {
        try
        {
            var state = RunManager.Instance?.DebugOnlyGetState();
            var player = state?.Players.FirstOrDefault(p => p.NetId == senderId);
            if (player is not null)
                return player.Character.NameColor;
        }
        catch { }

        return DefaultNameColor;
    }

    Color GetLocalPlayerColor()
    {
        ulong localId = 0;
        try { localId = PlatformUtil.GetLocalPlayerId(PlatformUtil.PrimaryPlatform); }
        catch { }
        return GetPlayerColor(localId);
    }

    #endregion
}
