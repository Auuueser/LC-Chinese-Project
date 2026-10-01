using System;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;

namespace V81TestChn;

// Arrange the existing HUD labels; the game owns their text and visibility.
internal static class HandsFullLayoutService
{
    private sealed class Hint
    {
        internal TMP_Text? Text;
        private Transform? _parent;
        private Vector3 _position;
        private bool _moved;
        internal bool Visible => Text != null && Text.isActiveAndEnabled;

        internal void Bind(TMP_Text? text)
        {
            Restore();
            Text = text;
            _parent = text != null ? text.rectTransform.parent : null;
            _position = text != null ? text.rectTransform.anchoredPosition3D : default;
        }

        internal RectTransform? Parent(out bool changed)
        {
            changed = false;
            if (Text == null || Text.rectTransform.parent is not RectTransform parent) return null;
            if (!ReferenceEquals(_parent, parent))
            {
                // A HUD mod reparented this label: restore to its new layout.
                _parent = parent;
                _position = Text.rectTransform.anchoredPosition3D;
                _moved = false;
                changed = true;
            }
            return parent;
        }

        internal void Move(Vector3 delta)
        {
            if (Text == null || delta.sqrMagnitude <= 0.0001f) return;
            Text.rectTransform.anchoredPosition3D += delta;
            _moved = true;
        }

        internal void Restore()
        {
            if (_moved && Text != null && ReferenceEquals(Text.rectTransform.parent, _parent))
                Text.rectTransform.anchoredPosition3D = _position;
            _moved = false;
        }
    }

    private const float HintSeparation = 6f;
    private const float MoveDuration = 0.18f;
    private static readonly Hint Hands = new(), Typing = new(), Speech = new();
    private static readonly Vector3[] Corners = new Vector3[4];
    private static ConfigEntry<bool>? _aboveInventory, _typingAboveInventory;
    private static ConfigEntry<float>? _gap;
    private static HUDManager? _hud;
    private static bool _enabled = true, _typingEnabled = true, _subscribed;
    private static float _spacing = 12f;
    private static int _lastFrame = -1;
    private static bool _stackInitialized, _handsWereVisible;
    private static float _stackOffset, _moveFrom, _moveTarget, _moveStartedAt;

    internal static void Initialize(ConfigFile config)
    {
        _aboveInventory = config.Bind(ConfigSections.HudLayout, "HandsFullAboveInventory", true,
            "将“手上拿满了”居中放在物品栏上方。与“正在输入”同时显示时平滑上移，输入结束后下移；关闭恢复原位置，即时生效。");
        _typingAboveInventory = config.Bind(ConfigSections.HudLayout, "TypingAboveInventory", true,
            "将“正在输入”居中放在物品栏上方。关闭恢复原位置；即时生效。");
        _gap = config.Bind(ConfigSections.HudLayout, "HandsFullInventoryGap", 12f,
            new ConfigDescription("提示框与物品栏顶部的间距（UI 单位，随 HUD 缩放）。默认 12，建议 8～20；即时生效。",
                new AcceptableValueRange<float>(0f, 80f)));
        _aboveInventory.SettingChanged += SettingsChanged;
        _typingAboveInventory.SettingChanged += SettingsChanged;
        _gap.SettingChanged += SettingsChanged;
        SettingsChanged(null!, EventArgs.Empty);
        LiveConfigRegistration.RegisterBool(_aboveInventory);
        LiveConfigRegistration.RegisterBool(_typingAboveInventory);
        LiveConfigRegistration.RegisterFloat(_gap);
        if (!_subscribed)
        {
            Canvas.willRenderCanvases += BeforeRender;
            _subscribed = true;
        }
    }

    private static void SettingsChanged(object? sender, EventArgs args)
    {
        _enabled = _aboveInventory?.Value ?? true;
        _typingEnabled = _typingAboveInventory?.Value ?? true;
        var spacing = _gap?.Value ?? 12f;
        _spacing = float.IsNaN(spacing) || float.IsInfinity(spacing) ? 12f : Mathf.Clamp(spacing, 0f, 80f);
        _stackInitialized = false;
        _lastFrame = -1;
        ApplyLayout(preposition: true);
    }

    internal static void Attach(HUDManager hud)
    {
        Restore();
        _hud = hud;
        Speech.Bind(null);
        Hands.Bind(hud.holdingTwoHandedItem);
        Typing.Bind(hud.typingIndicator);
        _lastFrame = -1;
        ApplyLayout(preposition: true);
    }

    internal static void OnTextEnabled(TMP_Text text)
    {
        if (!ReferenceEquals(text, Hands.Text) && !ReferenceEquals(text, Typing.Text) && !ReferenceEquals(text, Speech.Text)) return;
        if (ReferenceEquals(text, Hands.Text)) _handsWereVisible = false;
        // Position before the first mesh/render pass. An existing visible hands
        // hint animates only when typing changes its stacked target.
        ApplyLayout();
    }

    internal static void AttachSpeech(TMP_Text text)
    {
        Speech.Bind(text);
        _lastFrame = -1;
        ApplyLayout();
    }

    private static void BeforeRender()
    {
        if (Plugin.IsRuntimeShuttingDown) { Restore(); return; }
        if (_lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;
        if (!_enabled) Hands.Restore();
        if (!_typingEnabled) Typing.Restore();
        if (!(_enabled && Hands.Visible) && !(_typingEnabled && Typing.Visible) && !Speech.Visible)
        {
            _handsWereVisible = _stackInitialized = false;
            return;
        }
        ApplyLayout();
    }

    private static void ApplyLayout(bool preposition = false)
    {
        if (Plugin.IsRuntimeShuttingDown) { Restore(); return; }
        if (!_enabled) Hands.Restore();
        if (!_typingEnabled) Typing.Restore();
        if (_hud == null) return;

        var handsParent = Hands.Parent(out var parentChanged);
        var typingParent = Typing.Parent(out _);
        var speechParent = Speech.Parent(out _);
        // Both labels share one layout space even when chat mods give the
        // typing indicator a different parent and scale.
        var space = handsParent ?? typingParent ?? speechParent;
        if (space == null || !InventoryBounds(space, out var center, out var top))
        {
            Restore();
            return;
        }
        var typingVisible = _typingEnabled && Typing.Visible;
        var typingPlaced = false;
        Hint? lowerHint = null;
        if (_typingEnabled && (typingVisible || preposition))
        {
            if (typingParent != null)
            {
                Align(Typing, typingParent, space, center, top + _spacing);
                typingPlaced = typingVisible;
                if (typingPlaced) lowerHint = Typing;
            }
            else Typing.Restore();
        }

        // Manual chat cancels dictation. A third-party HUD showing both still
        // gets separate rows, with the hands hint above both input hints.
        if (Speech.Visible || preposition)
        {
            if (speechParent != null)
            {
                var speechBottom = top + _spacing;
                if (typingPlaced)
                {
                    TextBounds(Typing.Text!, space, out _, out _, out var typingTop);
                    speechBottom = typingTop + HintSeparation;
                }
                Align(Speech, speechParent, space, center, speechBottom);
                if (Speech.Visible) { lowerHint = Speech; typingPlaced = true; }
            }
        }

        var handsVisible = _enabled && Hands.Visible;
        if (_enabled && (handsVisible || preposition))
        {
            if (handsParent != null)
            {
                var target = 0f;
                if (typingPlaced)
                {
                    TextBounds(lowerHint!.Text!, space, out _, out _, out var lowerTop);
                    target = lowerTop - (top + _spacing) + HintSeparation;
                }
                UpdateStack(target, handsVisible, preposition || parentChanged);
                Align(Hands, handsParent, space, center, top + _spacing + _stackOffset);
            }
            else { Hands.Restore(); _stackInitialized = false; }
        }
        else _stackInitialized = false;
        _handsWereVisible = handsVisible;
    }

    private static void UpdateStack(float target, bool visible, bool snap)
    {
        var now = Time.unscaledTime;
        if (snap || !_stackInitialized || !_handsWereVisible || !visible)
        {
            _stackOffset = _moveFrom = _moveTarget = target;
            _moveStartedAt = now;
            _stackInitialized = true;
            return;
        }
        var progress = Mathf.Clamp((now - _moveStartedAt) / MoveDuration, 0f, 1f);
        var eased = progress * progress * (3f - 2f * progress);
        _stackOffset = _moveFrom + (_moveTarget - _moveFrom) * eased;
        if (Mathf.Abs(target - _moveTarget) > 0.001f)
        {
            // Retarget from the current height when chat closes/reopens mid-move.
            _moveFrom = _stackOffset;
            _moveTarget = target;
            _moveStartedAt = now;
        }
    }

    private static void Align(Hint hint, RectTransform parent, RectTransform space, float center, float bottom)
    {
        TextBounds(hint.Text!, space, out var textCenter, out var textBottom, out _);
        var delta = new Vector3(center - textCenter, bottom - textBottom, 0f);
        if (!ReferenceEquals(parent, space))
        {
            var origin = parent.InverseTransformPoint(space.TransformPoint(Vector3.zero));
            var point = parent.InverseTransformPoint(space.TransformPoint(delta));
            delta = new Vector3(point.x - origin.x, point.y - origin.y, 0f);
        }
        hint.Move(delta);
    }

    private static void TextBounds(TMP_Text text, RectTransform parent, out float center, out float bottom, out float top)
    {
        text.rectTransform.GetWorldCorners(Corners);
        var left = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        bottom = float.PositiveInfinity;
        top = float.NegativeInfinity;
        foreach (var corner in Corners)
        {
            var point = parent.InverseTransformPoint(corner);
            left = Mathf.Min(left, point.x);
            right = Mathf.Max(right, point.x);
            bottom = Mathf.Min(bottom, point.y);
            top = Mathf.Max(top, point.y);
        }
        center = (left + right) * 0.5f;
    }

    private static bool InventoryBounds(RectTransform parent, out float center, out float top)
    {
        var left = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        top = float.NegativeInfinity;
        var frames = _hud!.itemSlotIconFrames;
        if (frames != null)
            foreach (var frame in frames)
                // The separate item-only frame belongs to the top-left utility slot.
                if (frame != null && frame != _hud.itemOnlySlotIconFrame && frame.isActiveAndEnabled)
                    Include(frame.rectTransform, parent, ref left, ref right, ref top);
        center = (left + right) * 0.5f;
        return !float.IsInfinity(top);
    }

    private static void Include(RectTransform slot, RectTransform parent, ref float left, ref float right, ref float top)
    {
        // Keep layout position/rotation/container scale without selection scaling.
        if (slot.parent == null) return;
        slot.GetLocalCorners(Corners);
        var position = slot.localPosition;
        var rotation = slot.localRotation;
        foreach (var corner in Corners)
        {
            var point = parent.InverseTransformPoint(slot.parent.TransformPoint(position + rotation * corner));
            left = Mathf.Min(left, point.x);
            right = Mathf.Max(right, point.x);
            top = Mathf.Max(top, point.y);
        }
    }

    private static void Restore()
    {
        Hands.Restore();
        Typing.Restore();
        Speech.Restore();
        _handsWereVisible = _stackInitialized = false;
        _stackOffset = _moveFrom = _moveTarget = 0f;
    }

    internal static void Shutdown()
    {
        if (_subscribed) Canvas.willRenderCanvases -= BeforeRender;
        _subscribed = false;
        Restore();
        if (_aboveInventory != null) _aboveInventory.SettingChanged -= SettingsChanged;
        if (_typingAboveInventory != null) _typingAboveInventory.SettingChanged -= SettingsChanged;
        if (_gap != null) _gap.SettingChanged -= SettingsChanged;
        Hands.Bind(null);
        Typing.Bind(null);
        Speech.Bind(null);
        _hud = null;
        _aboveInventory = _typingAboveInventory = null;
        _gap = null;
    }
}
