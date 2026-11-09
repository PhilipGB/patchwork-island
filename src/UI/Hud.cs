using System;
using System.Collections.Generic;
using Godot;

namespace PatchworkIsland;

/// <summary>
/// All player-facing UI: job card, coin badge, destination compass, contextual
/// prompts, toasts, title/pause screens and modal panels. Built entirely in code
/// so it scales cleanly from the 1280x720 baseline.
/// </summary>
public partial class Hud : CanvasLayer
{
    public event Action<bool> ModalStateChanged;
    public Action JobsButtonPressed;
    public Action HintButtonPressed;

    public bool ModalOpen => _modalLayer != null;
    public bool ModalDismissible { get; private set; } = true;
    public bool BuildBarOpen => _buildRoot != null;

    private Control _root;
    private PanelContainer _missionCard;
    private Label _missionHeader;
    private Label _missionTitle;
    private Label _missionInstruction;
    private VBoxContainer _missionProgress;
    private TextureRect _missionIcon;
    private PanelContainer _coinPanel;
    private Label _coinLabel;
    private PanelContainer _destPanel;
    private Label _destArrow;
    private Label _destLabel;
    private TextureRect _destIcon;
    private PanelContainer _promptPanel;
    private Label _promptKey;
    private Label _promptText;
    private PanelContainer _toastPanel;
    private Label _toastLabel;
    private float _toastTime;
    private Control _modalLayer;
    private VBoxContainer _modalBody;
    private Label _modalTitle;
    private Label _modalStatus;
    private VBoxContainer _modalStatusHost;
    private bool _modalShadeStopsInput = true;
    private bool _modalStateReported;
    private Control _buildRoot;
    private readonly List<(TextureRect Icon, Label Text, string IconName, string TextValue)> _missionRows = new();
    private string _lastMissionIconName = "";
    private string _lastCoinText = "";
    private string _lastPromptText;
    private string _lastPromptKey;
    private string _lastDestText;
    private string _lastDestIconName;
    private int _lastDestArrow = -1;

    public override void _Ready()
    {
        Layer = 20;
        ProcessMode = ProcessModeEnum.Always;
        BuildRoot();
        SetNoMission();
        SetPrompt(null);
        SetDestination(null, 0, 0, null);
        SetCoins(0);
        SetProcess(true);
    }

    private void BuildRoot()
    {
        _root = new Control { Name = "HudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        BuildMissionCard();
        BuildCoinPanel();
        BuildDestination();
        BuildPrompt();
        BuildToast();
    }

    private void BuildMissionCard()
    {
        _missionCard = UiKit.Panel(_root, Palette.Cream, Palette.Blue, 20, 5);
        UiKit.Anchor(_missionCard, 0, 0, 0, 0, 20, 20, 405, 374);
        _missionCard.MouseFilter = Control.MouseFilterEnum.Stop;

        var margin = UiKit.Margin(_missionCard, 4, 4, 4, 4);
        var box = UiKit.VBox(margin, 7);

        var headerRow = UiKit.HBox(box, 8);
        _missionIcon = UiKit.Icon(headerRow, "star", 30);
        _missionHeader = UiKit.Label(headerRow, "CURRENT JOB", 17, Palette.Blue, HorizontalAlignment.Left, false);
        UiKit.Spacer(headerRow);

        _missionTitle = UiKit.Label(box, "", 25, Palette.Navy);
        _missionTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _missionInstruction = UiKit.Label(box, "", 19, Palette.Navy);
        _missionInstruction.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _missionInstruction.CustomMinimumSize = new Vector2(0, 44);

        _missionProgress = UiKit.VBox(box, 2);

        var buttonRow = UiKit.HBox(box, 8);
        var hint = UiKit.Button(buttonRow, "Hint  [H]", 18, new Vector2(128, 44), new Color("FFF3C9"), Palette.Orange);
        hint.Pressed += () => HintButtonPressed?.Invoke();
        var jobs = UiKit.Button(buttonRow, "Jobs  [J]", 18, new Vector2(128, 44));
        jobs.Pressed += () => JobsButtonPressed?.Invoke();
    }

    private void BuildCoinPanel()
    {
        _coinPanel = UiKit.Panel(_root, Palette.Cream, Palette.Orange, 20, 5);
        UiKit.Anchor(_coinPanel, 1, 0, 1, 0, -190, 20, -20, 78);
        _coinPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var row = UiKit.HBox(_coinPanel, 8);
        UiKit.Icon(row, "coin", 34);
        _coinLabel = UiKit.Label(row, "0", 30, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(row);
    }

    private void BuildDestination()
    {
        _destPanel = UiKit.Panel(_root, Palette.Cream, Palette.Yellow, 18, 4);
        UiKit.Anchor(_destPanel, 0.5f, 0, 0.5f, 0, -235, 20, 235, 72);
        _destPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var row = UiKit.HBox(_destPanel, 8);
        _destArrow = UiKit.Label(row, "", 22, Palette.Navy, HorizontalAlignment.Left, false);
        _destIcon = UiKit.Icon(row, "waypoint", 26);
        _destLabel = UiKit.Label(row, "", 19, Palette.Navy, HorizontalAlignment.Left, false);
        _destLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Spacer(row);
    }

    private void BuildPrompt()
    {
        _promptPanel = UiKit.Panel(_root, Palette.Cream, Palette.Blue, 18, 5);
        UiKit.Anchor(_promptPanel, 0.5f, 1, 0.5f, 1, -280, -92, 280, -20);
        _promptPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var row = UiKit.HBox(_promptPanel, 12);
        var keyPanel = UiKit.Panel(row, Palette.Blue, Palette.Blue, 12, 0);
        _promptKey = UiKit.Label(keyPanel, "E", 24, Palette.White, HorizontalAlignment.Center, false);
        _promptKey.CustomMinimumSize = new Vector2(36, 0);
        _promptText = UiKit.Label(row, "", 24, Palette.Navy, HorizontalAlignment.Left, false);
        _promptText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Spacer(row);
    }

    private void BuildToast()
    {
        _toastPanel = UiKit.Panel(_root, Palette.Cream, Palette.Yellow, 18, 4);
        UiKit.Anchor(_toastPanel, 0.5f, 1, 0.5f, 1, -360, -168, 360, -104);
        _toastPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _toastLabel = UiKit.Label(_toastPanel, "", 21, Palette.Navy, HorizontalAlignment.Center);
        _toastPanel.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_toastTime > 0f)
        {
            _toastTime -= (float)delta;
            if (_toastTime <= 0f)
                _toastPanel.Visible = false;
        }
    }

    public void SetCoins(int coins)
    {
        string text = coins.ToString();
        if (text == _lastCoinText) return;
        _lastCoinText = text;
        _coinLabel.Text = text;
    }

    public void SetNoMission()
    {
        _lastMissionIconName = "star";
        _missionIcon.Texture = Geo.IconTexture("star");
        _missionHeader.Text = "READY FOR A JOB";
        _missionTitle.Text = "Choose a job";
        _missionInstruction.Text = "Visit the yellow JOBS board near the town hall.";
        UiKit.ClearChildren(_missionProgress);
        _missionRows.Clear();
        UiKit.Label(_missionProgress, "You always keep your progress.", 17, Palette.Success);
    }

    public void SetMission(string title, string instruction, string categoryIcon,
        IEnumerable<(string Icon, string Text)> progress)
    {
        if (_lastMissionIconName != categoryIcon)
        {
            _lastMissionIconName = categoryIcon;
            _missionIcon.Texture = Geo.IconTexture(categoryIcon);
        }
        _missionHeader.Text = "CURRENT JOB";
        _missionTitle.Text = title;
        _missionInstruction.Text = instruction;

        var rows = new List<(string Icon, string Text)>();
        if (progress != null)
            rows.AddRange(progress);

        if (rows.Count == _missionRows.Count)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var (icon, text) = rows[i];
                var row = _missionRows[i];
                if (row.IconName != icon)
                {
                    row.Icon.Texture = Geo.IconTexture(icon);
                    _missionRows[i] = (row.Icon, row.Text, icon, row.TextValue);
                }
                if (row.TextValue != text)
                {
                    row.Text.Text = text;
                    _missionRows[i] = (row.Icon, row.Text, row.IconName, text);
                }
            }
            return;
        }

        UiKit.ClearChildren(_missionProgress);
        _missionRows.Clear();
        foreach (var (icon, text) in rows)
        {
            var row = UiKit.HBox(_missionProgress, 6);
            var texture = UiKit.Icon(row, icon, 24);
            var label = UiKit.Label(row, text, 18, Palette.Navy, HorizontalAlignment.Left, false);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _missionRows.Add((texture, label, icon, text));
        }
    }

    public void SetPrompt(string action, string key = "E")
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            if (!_promptPanel.Visible && _lastPromptText == null) return;
            _lastPromptText = null;
            _lastPromptKey = null;
            _promptPanel.Visible = false;
            return;
        }
        if (_promptPanel.Visible && _lastPromptText == action && _lastPromptKey == key) return;
        _promptPanel.Visible = true;
        _lastPromptKey = key;
        _lastPromptText = action;
        _promptKey.Text = key;
        _promptText.Text = action;
    }

    public void SetDestination(string label, float distance, int arrowIndex, string iconName)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            if (!_destPanel.Visible) return;
            _destPanel.Visible = false;
            _lastDestText = null;
            _lastDestIconName = null;
            _lastDestArrow = -1;
            return;
        }
        string text = distance >= 0.5f ? $"{label}   {Mathf.RoundToInt(distance)} m" : label;
        if (_destPanel.Visible && text == _lastDestText && arrowIndex == _lastDestArrow &&
            iconName == _lastDestIconName)
            return;

        _destPanel.Visible = true;
        string[] arrows = { "▲", "↗", "→", "↘", "▼", "↙", "←", "↖" };
        _destArrow.Text = arrows[Mathf.Clamp(arrowIndex, 0, 7)];
        _destLabel.Text = text;
        _lastDestText = text;
        _lastDestArrow = arrowIndex;
        if (!string.IsNullOrWhiteSpace(iconName) && iconName != _lastDestIconName)
        {
            _lastDestIconName = iconName;
            var texture = Geo.IconTexture(iconName);
            _destIcon.Texture = texture;
            _destIcon.Visible = texture != null;
        }
    }

    public void ShowToast(string text, float seconds = 2.8f, Color? border = null)
    {
        _toastPanel.Visible = true;
        _toastLabel.Text = text;
        _toastPanel.AddThemeStyleboxOverride("panel",
            UiKit.Style(Palette.Cream, border ?? Palette.Yellow, 18, 4));
        _toastTime = seconds;
    }

    public void ShowHint(string hint)
    {
        ShowToast("HINT: " + hint, 7f, Palette.Orange);
    }

    public void ShowBigToast(string text, float seconds = 4f, Color? color = null)
    {
        ShowToast(text, seconds, color ?? Palette.Yellow);
    }

    public void ShowBuildTip(string text)
    {
        ShowToast(text, 2.4f, Palette.Lavender);
    }

    // --------------------------------------------------------------- modals

    public VBoxContainer OpenModal(string title, Vector2 minSize, Color? border = null,
        bool dismissible = true, Action onClosed = null, bool showCloseButton = true)
    {
        CloseModal(false);
        ModalDismissible = dismissible;
        _modalLayer = new Control { Name = "ModalLayer", MouseFilter = Control.MouseFilterEnum.Stop };
        _modalLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_modalLayer);

        if (_modalShadeStopsInput)
            UiKit.Shade(_modalLayer, 0.58f);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _modalLayer.AddChild(center);

        var card = UiKit.Panel(center, Palette.Cream, border ?? Palette.Blue, 26, 6);
        card.CustomMinimumSize = minSize;
        card.MouseFilter = Control.MouseFilterEnum.Stop;
        var margin = UiKit.Margin(card, 18, 14, 18, 16);
        var outer = UiKit.VBox(margin, 10);

        var header = UiKit.HBox(outer, 12);
        _modalTitle = UiKit.Label(header, title, 30, Palette.Navy);
        _modalTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        if (showCloseButton)
        {
            var close = UiKit.Button(header, "Close", 19, new Vector2(110, 44), Palette.Cream, Palette.Stone);
            close.Pressed += () => CloseModal();
        }
        UiKit.Separator(outer);

        _modalStatusHost = UiKit.VBox(outer, 4);
        _modalStatusHost.Visible = false;

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        outer.AddChild(scroll);
        _modalBody = UiKit.VBox(scroll, 10);
        _modalBody.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        _modalStatus = null;
        _modalClosedAction = onClosed;
        PublishModalState();
        Callable.From(FocusFirstModalButton).CallDeferred();
        return _modalBody;
    }

    private Action _modalClosedAction;

    private void FocusFirstModalButton()
    {
        if (_modalLayer == null) return;
        var buttons = _modalLayer.FindChildren("*", "Button", true, false);
        foreach (var node in buttons)
        {
            if (node is Button b && b.Visible && !b.Disabled)
            {
                b.GrabFocus();
                break;
            }
        }
    }

    public void CloseModal(bool notify = true)
    {
        if (_modalLayer == null) return;
        var closed = _modalLayer;
        _modalLayer = null;
        _modalBody = null;
        _modalTitle = null;
        _modalStatus = null;
        _modalStatusHost = null;
        closed.QueueFree();
        var action = _modalClosedAction;
        _modalClosedAction = null;
        if (notify)
        {
            action?.Invoke();
            PublishModalState();
        }
    }

    /// <summary>
    /// Reports modal state only when the actual open/closed state changes. A
    /// close callback that immediately opens another modal (title → settings or
    /// pause → settings) therefore cannot emit a stale "unpause" event.
    /// </summary>
    private void PublishModalState()
    {
        bool open = _modalLayer != null;
        if (open == _modalStateReported) return;
        _modalStateReported = open;
        ModalStateChanged?.Invoke(open);
    }

    public void SetModalTitle(string title)
    {
        if (_modalTitle != null) _modalTitle.Text = title;
    }

    /// <summary>Inline save status inside the current modal, above its shade.</summary>
    public void SetModalStatus(string text, bool isError)
    {
        if (_modalStatusHost == null) return;
        Color color = isError ? Palette.Red : Palette.Success;
        _modalStatusHost.Visible = true;
        if (_modalStatus == null)
        {
            _modalStatus = UiKit.Label(_modalStatusHost, text, 18, color);
            _modalStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        else
        {
            _modalStatus.Text = text;
            _modalStatus.AddThemeColorOverride("font_color", color);
        }
    }

    public VBoxContainer ModalBody => _modalBody;

    public string ModalTitleText => _modalTitle?.Text ?? "";

    public string ModalStatusText => _modalStatus?.Text ?? "";

    public bool ModalStatusVisible => _modalStatusHost?.Visible == true && _modalStatus != null;

    public string ToastText => _toastLabel?.Text ?? "";

    public bool ToastVisible => _toastPanel?.Visible == true;

    public string CoinText => _coinLabel?.Text ?? "";

    public void AddModalActionRow(VBoxContainer body, params (string Text, Action Action, Color? Color)[] actions)
    {
        var row = UiKit.HBox(body, 10);
        foreach (var item in actions)
        {
            var button = UiKit.Button(row, item.Text, 21, new Vector2(0, 50), Palette.Cream,
                item.Color ?? Palette.Blue);
            var captured = item.Action;
            button.Pressed += () => captured?.Invoke();
        }
    }

    public void SetModalShade(bool enabled) => _modalShadeStopsInput = enabled;

    public void ShowInfo(string title, string text, Action onClosed = null)
    {
        var body = OpenModal(title, new Vector2(620, 260), Palette.Blue, true, onClosed);
        UiKit.Label(body, text, 22, Palette.Navy);
    }

    public void Confirm(string title, string message, string confirmText, Action onConfirm,
        string cancelText = "Cancel", Action onCancel = null)
    {
        var body = OpenModal(title, new Vector2(620, 290), Palette.Orange, true, onCancel);
        UiKit.Label(body, message, 21, Palette.Navy);
        var row = UiKit.HBox(body, 12);
        var cancel = UiKit.Button(row, cancelText, 21, new Vector2(180, 50), Palette.Cream, Palette.Stone);
        cancel.Pressed += () => CloseModal();
        var yes = UiKit.Button(row, confirmText, 21, new Vector2(240, 50), new Color("FFE0D6"), Palette.Red);
        yes.Pressed += () =>
        {
            CloseModal(false);
            onConfirm?.Invoke();
            PublishModalState();
        };
    }

    public void ShowTitleScreen(bool hasSave, bool canContinue, Action onContinue, Action onNewGame,
        Action onSettings, Action onQuit, string notice = null)
    {
        var body = OpenModal("", new Vector2(720, 682), Palette.Lavender, false, null, false);
        var logo = UiKit.Label(body, "PATCHWORK\nISLAND", 48, Palette.Lavender, HorizontalAlignment.Center);
        logo.AddThemeConstantOverride("line_spacing", -10);
        var sub = UiKit.Label(body, "A happy little village adventure", 21, Palette.Navy, HorizontalAlignment.Center);
        var controls = UiKit.Label(body,
            "WASD / arrows to walk   •   Space to jump   •   E to interact\nMouse wheel to zoom   •   Right-drag to look around   •   Esc to pause",
            16, new Color("42526B"), HorizontalAlignment.Center);
        controls.CustomMinimumSize = new Vector2(600, 44);
        if (!string.IsNullOrWhiteSpace(notice))
        {
            var noticePanel = UiKit.Panel(body, new Color("FFF3C9"), Palette.Orange, 12, 3);
            var noticeLabel = UiKit.Label(noticePanel, notice, 15, Palette.Navy, HorizontalAlignment.Center);
            noticeLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
        UiKit.Spacer(body, 0, 2);
        if (canContinue)
        {
            var continueButton = UiKit.Button(body, "Continue adventure", 23, new Vector2(0, 50), Palette.Cream, Palette.Blue);
            continueButton.Pressed += () => onContinue?.Invoke();
        }
        var newButton = UiKit.Button(body, hasSave ? "Start a new game" : "Start adventure", 23,
            new Vector2(0, 50), new Color("FFF3C9"), Palette.Orange);
        newButton.Pressed += () => onNewGame?.Invoke();
        if (onSettings != null)
        {
            var settings = UiKit.Button(body, "Settings", 19, new Vector2(0, 42), Palette.Cream, Palette.Stone);
            settings.Pressed += () => onSettings?.Invoke();
        }
        var quit = UiKit.Button(body, "Quit", 18, new Vector2(0, 38), Palette.Cream, Palette.Stone);
        quit.Pressed += () => onQuit?.Invoke();
    }

    public void ShowPauseMenu(Action resume, Action saveNow, Action settings, Action newGame,
        Action quitToTitle, Action quitDesktop)
    {
        var body = OpenModal("Paused", new Vector2(520, 500), Palette.Blue, true, resume);
        var resumeButton = UiKit.Button(body, "Keep playing", 24, new Vector2(0, 56), new Color("DFF6E3"), Palette.Success);
        resumeButton.Pressed += () => CloseModal();
        var save = UiKit.Button(body, "Save now", 21, new Vector2(0, 48), Palette.Cream, Palette.Blue);
        save.Pressed += () => saveNow?.Invoke();
        var set = UiKit.Button(body, "Settings", 21, new Vector2(0, 48), Palette.Cream, Palette.Lavender);
        set.Pressed += () => settings?.Invoke();
        var newG = UiKit.Button(body, "Start a new game", 21, new Vector2(0, 48), new Color("FFE0D6"), Palette.Red);
        newG.Pressed += () => newGame?.Invoke();
        var title = UiKit.Button(body, "Return to title", 21, new Vector2(0, 48), Palette.Cream, Palette.Stone);
        title.Pressed += () => quitToTitle?.Invoke();
        var quit = UiKit.Button(body, "Quit to desktop", 21, new Vector2(0, 48), Palette.Cream, Palette.Stone);
        quit.Pressed += () => quitDesktop?.Invoke();
    }

    public void ShowSuccess(string title, int coins, string detail, Action onClose)
    {
        var body = OpenModal("JOB COMPLETE!", new Vector2(620, 390), Palette.Success, true, onClose, false);
        var row = UiKit.HBox(body, 10);
        UiKit.Icon(row, "star", 48);
        var titleLabel = UiKit.Label(row, title, 30, Palette.Success, HorizontalAlignment.Left, false);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        UiKit.Label(body, detail, 22, Palette.Navy);
        var coinRow = UiKit.HBox(body, 10);
        UiKit.Icon(coinRow, "coin", 34);
        UiKit.Label(coinRow, $"+{coins} coins", 28, Palette.Navy, HorizontalAlignment.Left, false);
        var button = UiKit.Button(body, "Continue", 24, new Vector2(0, 56), new Color("DFF6E3"), Palette.Success);
        button.Pressed += () => CloseModal();
    }


    // ------------------------------------------------------------- build bar

    public void ShowBuildBar(string title, string instruction, IReadOnlyList<BuildBarOption> options,
        string footer, Action onClose, Action onUndo = null)
    {
        HideBuildBar();
        _buildRoot = new Control { Name = "BuildBar", MouseFilter = Control.MouseFilterEnum.Ignore };
        _buildRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_buildRoot);

        var panel = UiKit.Panel(_buildRoot, Palette.Cream, Palette.Lavender, 20, 5);
        UiKit.Anchor(panel, 0.5f, 1, 0.5f, 1, -500, -210, 500, -20);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        var margin = UiKit.Margin(panel, 8, 8, 8, 8);
        var box = UiKit.VBox(margin, 7);

        var titleRow = UiKit.HBox(box, 10);
        UiKit.Icon(titleRow, "hammer", 30);
        UiKit.Label(titleRow, title, 24, Palette.Navy, HorizontalAlignment.Left, false);
        UiKit.Spacer(titleRow);
        if (onUndo != null)
        {
            var undo = UiKit.Button(titleRow, "Undo last", 18, new Vector2(125, 44), new Color("FFE0D6"), Palette.Red);
            undo.Pressed += () => onUndo.Invoke();
        }
        var close = UiKit.Button(titleRow, "Done  [Esc]", 19, new Vector2(150, 44), new Color("E8E1FF"), Palette.Lavender);
        close.Pressed += () => onClose?.Invoke();

        var instructionLabel = UiKit.Label(box, instruction, 19, Palette.Navy);
        instructionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var optionsRow = UiKit.HBox(box, 10);
        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var wrapper = UiKit.HBox(optionsRow, 6);
            if (!string.IsNullOrWhiteSpace(option.Icon))
                UiKit.Icon(wrapper, option.Icon, 28);
            var label = option.Selected ? $"▶ {option.Label}  {option.Detail}" : $"{option.Label}  {option.Detail}";
            var button = UiKit.Button(wrapper, label, 19, new Vector2(220, 48),
                option.Selected ? new Color("FFF3C9") : Palette.Cream,
                option.Selected ? Palette.Orange : Palette.Blue);
            button.Disabled = !option.Enabled;
            int captured = i;
            button.Pressed += () => options[captured].Action?.Invoke();
        }
        var footerLabel = UiKit.Label(box, footer, 16, new Color("5C6B7A"), HorizontalAlignment.Left, false);
        footerLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
    }

    public void HideBuildBar()
    {
        if (_buildRoot == null) return;
        var old = _buildRoot;
        _buildRoot = null;
        old.QueueFree();
    }

    public void SetWorldDim(bool dim)
    {
        // Reserved for future reduced-motion / accessibility options.
    }
}

public sealed class BuildBarOption
{
    public string Label = "";
    public string Detail = "";
    public string Icon = "";
    public bool Selected;
    public bool Enabled = true;
    public System.Action Action;
}
