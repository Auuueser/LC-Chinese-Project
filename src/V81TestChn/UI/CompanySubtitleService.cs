using BepInEx.Bootstrap;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace V81TestChn;

internal static class CompanySubtitleService
{
    private const string SubtitleRootName = "__V81_CompanyAudioSubtitles";
    private const string TestSubtitleText = "公司音频字幕测试";
    private const float DefaultVerticalOffset = 50f;
    private const float MaxPanelWidthFraction = 0.72f;
    private const float HorizontalPaddingInFontSizes = 0.75f;
    private const float VerticalPaddingInFontSizes = 0.28f;

    private static readonly Dictionary<string, SubtitleTrack> Tracks = CreateTracks();
    private static readonly HashSet<int> CompanySpeakerIds = new();
    private static readonly Dictionary<int, TVScript> TvOwners = new();
    private static readonly List<ActivePlayback> ActivePlaybacks = new(4);

    private static ConfigEntry<bool>? _enabled;
    private static ConfigEntry<bool>? _showTestSubtitle;
    private static ConfigEntry<float>? _fontSize;
    private static ConfigEntry<float>? _verticalOffset;
    private static ConfigEntry<float>? _backgroundOpacity;
    private static ConfigEntry<bool>? _avoidInventorySlots;
    private static bool _enabledFast;
    private static bool _externalSubtitlePluginDetected;
    private static bool _externalPluginWarningLogged;
    private static HUDManager? _hud;
    private static CompanySubtitleDriver? _driver;
    private static GameObject? _subtitleRoot;
    private static TextMeshProUGUI? _subtitleText;
    private static string? _displayedText;

    public static void Initialize(ConfigFile config)
    {
        _enabled = config.Bind(
            ConfigSections.CompanySubtitles,
            "Enabled",
            true,
            "启用飞船广播、电视英语对白和公司售卖话筒语音的中文字幕。");
        _showTestSubtitle = config.Bind(
            ConfigSections.CompanySubtitles,
            "ShowTestSubtitle",
            false,
            "持续显示测试字幕，便于在 LethalConfig 中调整位置、字号和背景；真实字幕播放时会暂时覆盖测试文本。");
        _fontSize = config.Bind(
            ConfigSections.CompanySubtitles,
            "FontSize",
            20f,
            new ConfigDescription(
                "公司音频中文字幕字号。",
                new AcceptableValueRange<float>(18f, 42f)));
        _verticalOffset = config.Bind(
            ConfigSections.CompanySubtitles,
            "VerticalOffset",
            DefaultVerticalOffset,
            new ConfigDescription(
                "字幕黑底距离屏幕底边的 UI 偏移。关闭 AvoidInventorySlots 后可下移并与物品槽位重叠。",
                new AcceptableValueRange<float>(20f, 520f)));
        _backgroundOpacity = config.Bind(
            ConfigSections.CompanySubtitles,
            "BackgroundOpacity",
            0.9f,
            new ConfigDescription(
                "字幕黑色背景的不透明度；设为 1.0 即完全不透明。",
                new AcceptableValueRange<float>(0.2f, 1f)));
        _avoidInventorySlots = config.Bind(
            ConfigSections.CompanySubtitles,
            "AvoidInventorySlots",
            true,
            "自动把字幕抬到物品槽位上方。关闭后 VerticalOffset 将直接控制位置，并允许与物品槽位重叠。");

        _enabledFast = _enabled.Value;
        SubscribeConfigChanges();
        _externalSubtitlePluginDetected = false;
        _externalPluginWarningLogged = false;
        ClearPlaybackState();
    }

    public static void ResetForHudLifecycle(HUDManager? hud)
    {
        DestroyUi();
        ClearPlaybackState();
        _hud = hud;
        _externalSubtitlePluginDetected = DetectExternalSubtitlePlugin();

        if (!_enabledFast || _externalSubtitlePluginDetected || hud == null || hud.HUDContainer == null)
        {
            if (_externalSubtitlePluginDetected && !_externalPluginWarningLogged)
            {
                _externalPluginWarningLogged = true;
                Plugin.Log.LogWarning("Company subtitles disabled because another subtitles plugin is loaded; duplicate subtitles were avoided.");
            }

            return;
        }

        CreateUi(hud);
        RefreshIdleSubtitle();
    }

    public static void RegisterCompanyDesk(DepositItemsDesk? desk)
    {
        if (desk?.speakerAudio == null)
        {
            return;
        }

        CompanySpeakerIds.Add(desk.speakerAudio.GetInstanceID());
    }

    public static void RegisterTv(TVScript? tv)
    {
        if (tv?.tvSFX == null)
        {
            return;
        }

        TvOwners[tv.tvSFX.GetInstanceID()] = tv;
    }

    public static void UnregisterTv(TVScript? tv)
    {
        if (tv?.tvSFX == null)
        {
            return;
        }

        var source = tv.tvSFX;
        TvOwners.Remove(source.GetInstanceID());
        StopForSource(source);
    }

    public static void OnAudioSourcePlay(AudioSource? source)
    {
        if (!CanProcessAudio() || source == null || !TvOwners.TryGetValue(source.GetInstanceID(), out var tv))
        {
            return;
        }

        var clip = source.clip;
        if (clip == null || !TryStartPlayback(source, clip, PlaybackKind.Tv, tv))
        {
            StopForSource(source);
        }
    }

    public static void OnAudioSourcePlayOneShot(AudioSource? source, AudioClip? clip)
    {
        if (!CanProcessAudio() || source == null || clip == null)
        {
            return;
        }

        var sourceId = source.GetInstanceID();
        if (TvOwners.ContainsKey(sourceId))
        {
            // TV switch clicks share tvSFX with the programme audio and must not replace its subtitle track.
            return;
        }

        if (IsShipSpeaker(source))
        {
            if (!TryStartPlayback(source, clip, PlaybackKind.Ship, null))
            {
                StopForSource(source);
            }

            return;
        }

        if (CompanySpeakerIds.Contains(sourceId) && !TryStartPlayback(source, clip, PlaybackKind.CompanyDesk, null))
        {
            StopForSource(source);
        }
    }

    public static void OnTvPowerChanged(TVScript? tv, bool on)
    {
        if (tv?.tvSFX == null)
        {
            return;
        }

        if (!on)
        {
            StopForSource(tv.tvSFX);
        }
    }

    public static void OnShipSpeakerDisabled(StartOfRound? round)
    {
        if (round?.speakerAudioSource != null)
        {
            StopForSource(round.speakerAudioSource);
        }
    }

    public static void OnSceneUnloaded()
    {
        CompanySpeakerIds.Clear();
        TvOwners.Clear();
        ClearPlaybackState();
        HideSubtitle();
    }

    public static void Shutdown()
    {
        UnsubscribeConfigChanges();
        DestroyUi();
        CompanySpeakerIds.Clear();
        TvOwners.Clear();
        ClearPlaybackState();
        _hud = null;
        _enabled = null;
        _showTestSubtitle = null;
        _fontSize = null;
        _verticalOffset = null;
        _backgroundOpacity = null;
        _avoidInventorySlots = null;
        _enabledFast = false;
        _externalSubtitlePluginDetected = false;
        _externalPluginWarningLogged = false;
    }

    internal static void Tick(float unscaledDeltaTime)
    {
        if (Plugin.IsRuntimeShuttingDown || !_enabledFast || _externalSubtitlePluginDetected)
        {
            ClearPlaybackState();
            HideSubtitle();
            return;
        }

        ActivePlayback? selected = null;
        for (var i = ActivePlaybacks.Count - 1; i >= 0; i--)
        {
            var playback = ActivePlaybacks[i];
            if (!UpdatePlayback(playback, unscaledDeltaTime))
            {
                ActivePlaybacks.RemoveAt(i);
                continue;
            }

            if (!CanDisplay(playback))
            {
                continue;
            }

            if (selected == null || playback.Priority > selected.Priority ||
                (playback.Priority == selected.Priority && playback.Sequence > selected.Sequence))
            {
                selected = playback;
            }
        }

        if (ActivePlaybacks.Count == 0)
        {
            RefreshIdleSubtitle();
            return;
        }

        if (selected == null || !TryGetCueText(selected, out var text))
        {
            HideSubtitle();
            return;
        }

        ShowSubtitle(text);
    }

    private static bool TryStartPlayback(AudioSource source, AudioClip clip, PlaybackKind kind, TVScript? tv)
    {
        if (!Tracks.TryGetValue(clip.name, out var track))
        {
            return false;
        }

        EnsureUi();
        if (_driver == null || _subtitleRoot == null || _subtitleText == null)
        {
            return false;
        }

        StopForSource(source);
        var startTime = kind == PlaybackKind.Tv ? GetSafeAudioTime(source) : 0f;
        ActivePlaybacks.Add(new ActivePlayback(source, clip, track, kind, tv, startTime, ++_playbackSequence));
        _driver.enabled = true;
        Tick(0f);
        return true;
    }

    private static int _playbackSequence;

    private static bool UpdatePlayback(ActivePlayback playback, float unscaledDeltaTime)
    {
        if (playback.Source == null || playback.Clip == null)
        {
            return false;
        }

        if (playback.Kind == PlaybackKind.Tv)
        {
            if (playback.Source.clip != playback.Clip)
            {
                return false;
            }

            playback.Elapsed = GetSafeAudioTime(playback.Source);
        }
        else
        {
            playback.Elapsed += Mathf.Max(0f, unscaledDeltaTime);
        }

        return playback.Elapsed <= playback.Clip.length + 0.15f;
    }

    private static bool CanDisplay(ActivePlayback playback)
    {
        var source = playback.Source;
        if (source == null || !source.isActiveAndEnabled || source.mute || source.volume <= 0.001f)
        {
            return false;
        }

        var player = GameNetworkManager.Instance?.localPlayerController;
        if (source.spatialBlend > 0.01f && player != null)
        {
            var maxDistance = Mathf.Max(source.minDistance, source.maxDistance) + 0.5f;
            if ((source.transform.position - player.transform.position).sqrMagnitude > maxDistance * maxDistance)
            {
                return false;
            }
        }

        if (playback.Kind != PlaybackKind.Tv)
        {
            return true;
        }

        var tv = playback.Tv;
        return tv != null && tv.tvOn && player != null && !player.isInsideFactory;
    }

    private static bool TryGetCueText(ActivePlayback playback, out string text)
    {
        var cues = playback.Track.Cues;
        for (var i = 0; i < cues.Length; i++)
        {
            var cue = cues[i];
            if (playback.Elapsed >= cue.Start && playback.Elapsed < cue.End)
            {
                text = cue.Text;
                return true;
            }
        }

        text = string.Empty;
        return false;
    }

    private static void StopForSource(AudioSource source)
    {
        for (var i = ActivePlaybacks.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(ActivePlaybacks[i].Source, source))
            {
                ActivePlaybacks.RemoveAt(i);
            }
        }

        if (ActivePlaybacks.Count == 0)
        {
            RefreshIdleSubtitle();
        }
    }

    private static bool IsShipSpeaker(AudioSource source)
    {
        var round = StartOfRound.Instance;
        return round != null && ReferenceEquals(round.speakerAudioSource, source);
    }

    private static bool CanProcessAudio()
    {
        return _enabledFast && !_externalSubtitlePluginDetected && !Plugin.IsRuntimeShuttingDown;
    }

    private static float GetSafeAudioTime(AudioSource source)
    {
        try
        {
            return Mathf.Max(0f, source.time);
        }
        catch
        {
            return 0f;
        }
    }

    private static void EnsureUi()
    {
        if (_subtitleRoot != null && _subtitleText != null && _driver != null)
        {
            return;
        }

        var hud = HUDManager.Instance ?? _hud;
        if (hud != null)
        {
            ResetForHudLifecycle(hud);
        }
    }

    private static void CreateUi(HUDManager hud)
    {
        var parent = hud.HUDContainer.transform;
        var root = new GameObject(SubtitleRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        root.transform.SetAsLastSibling();

        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.sizeDelta = Vector2.zero;

        var background = root.GetComponent<Image>();
        background.raycastTarget = false;

        var canvasGroup = root.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.ignoreParentGroups = true;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.maxVisibleLines = 2;
        text.enableAutoSizing = false;
        text.text = "公司音频字幕";

        var template = hud.controlTipLines != null && hud.controlTipLines.Length > 0
            ? hud.controlTipLines[0]
            : hud.weightCounter;
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
        }

        FontFallbackService.TryUseManagedFallbackAsPrimary(text, text.text);
        FontFallbackService.ApplyFallback(text, text.text, candidateContainsEastAsianGlyph: true);

        _hud = hud;
        _subtitleRoot = root;
        _subtitleText = text;
        _driver = hud.GetComponent<CompanySubtitleDriver>() ?? hud.gameObject.AddComponent<CompanySubtitleDriver>();
        _driver.enabled = false;
        ApplyCurrentUiSettings();
        root.SetActive(false);
    }

    private static void SubscribeConfigChanges()
    {
        UnsubscribeConfigChanges();
        if (_enabled != null)
        {
            _enabled.SettingChanged += OnConfigSettingChanged;
        }

        if (_showTestSubtitle != null)
        {
            _showTestSubtitle.SettingChanged += OnConfigSettingChanged;
        }

        if (_fontSize != null)
        {
            _fontSize.SettingChanged += OnConfigSettingChanged;
        }

        if (_verticalOffset != null)
        {
            _verticalOffset.SettingChanged += OnConfigSettingChanged;
        }

        if (_backgroundOpacity != null)
        {
            _backgroundOpacity.SettingChanged += OnConfigSettingChanged;
        }

        if (_avoidInventorySlots != null)
        {
            _avoidInventorySlots.SettingChanged += OnConfigSettingChanged;
        }
    }

    private static void UnsubscribeConfigChanges()
    {
        if (_enabled != null)
        {
            _enabled.SettingChanged -= OnConfigSettingChanged;
        }

        if (_showTestSubtitle != null)
        {
            _showTestSubtitle.SettingChanged -= OnConfigSettingChanged;
        }

        if (_fontSize != null)
        {
            _fontSize.SettingChanged -= OnConfigSettingChanged;
        }

        if (_verticalOffset != null)
        {
            _verticalOffset.SettingChanged -= OnConfigSettingChanged;
        }

        if (_backgroundOpacity != null)
        {
            _backgroundOpacity.SettingChanged -= OnConfigSettingChanged;
        }

        if (_avoidInventorySlots != null)
        {
            _avoidInventorySlots.SettingChanged -= OnConfigSettingChanged;
        }
    }

    private static void OnConfigSettingChanged(object sender, EventArgs args)
    {
        _enabledFast = _enabled?.Value ?? true;
        if (!_enabledFast || _externalSubtitlePluginDetected)
        {
            ClearPlaybackState();
            HideSubtitle();
            return;
        }

        if (_subtitleRoot == null && _hud != null && _hud.HUDContainer != null)
        {
            CreateUi(_hud);
        }

        ApplyCurrentUiSettings();
        if (ActivePlaybacks.Count == 0)
        {
            RefreshIdleSubtitle();
        }
    }

    private static void RefreshIdleSubtitle()
    {
        if (ActivePlaybacks.Count != 0)
        {
            return;
        }

        if (_driver != null)
        {
            _driver.enabled = false;
        }

        if (_enabledFast && !_externalSubtitlePluginDetected && _showTestSubtitle?.Value == true)
        {
            ShowSubtitle(TestSubtitleText);
            return;
        }

        HideSubtitle();
    }

    private static void ApplyCurrentUiSettings()
    {
        if (_hud == null || _subtitleRoot == null || _subtitleText == null)
        {
            return;
        }

        var rootRect = (RectTransform)_subtitleRoot.transform;
        var configuredOffset = Mathf.Clamp(_verticalOffset?.Value ?? DefaultVerticalOffset, 20f, 520f);
        var resolvedOffset = _avoidInventorySlots?.Value == false
            ? configuredOffset
            : ResolveInventorySafeVerticalOffset(_hud, rootRect.parent as RectTransform, configuredOffset);
        rootRect.anchoredPosition = new Vector2(0f, resolvedOffset);

        var background = _subtitleRoot.GetComponent<Image>();
        if (background != null)
        {
            background.color = new Color(0f, 0f, 0f, Mathf.Clamp(_backgroundOpacity?.Value ?? 0.9f, 0.2f, 1f));
        }

        _subtitleText.fontSizeMax = Mathf.Clamp(_fontSize?.Value ?? 20f, 18f, 42f);
        _subtitleText.fontSize = _subtitleText.fontSizeMax;
        _subtitleText.SetVerticesDirty();
        ResizePanelToContent();
    }

    private static void ResizePanelToContent()
    {
        if (_subtitleRoot == null || _subtitleText == null)
        {
            return;
        }

        var rootRect = (RectTransform)_subtitleRoot.transform;
        var parentRect = rootRect.parent as RectTransform;
        var parentWidth = parentRect != null && parentRect.rect.width > 0f
            ? parentRect.rect.width
            : Screen.width;
        if (parentWidth <= 0f)
        {
            return;
        }

        var fontSize = Mathf.Clamp(_subtitleText.fontSize, 18f, 42f);
        var horizontalPadding = Mathf.Ceil(Mathf.Max(14f, fontSize * HorizontalPaddingInFontSizes));
        var verticalPadding = Mathf.Ceil(Mathf.Max(5f, fontSize * VerticalPaddingInFontSizes));
        var maxPanelWidth = Mathf.Max(160f, parentWidth * MaxPanelWidthFraction);
        var maxTextWidth = Mathf.Max(80f, maxPanelWidth - horizontalPadding * 2f);
        var preferred = _subtitleText.GetPreferredValues(_subtitleText.text, maxTextWidth, 0f);
        var textWidth = Mathf.Clamp(Mathf.Ceil(preferred.x), 1f, maxTextWidth);
        var textHeight = Mathf.Max(fontSize, Mathf.Ceil(preferred.y));

        rootRect.sizeDelta = new Vector2(
            textWidth + horizontalPadding * 2f,
            textHeight + verticalPadding * 2f);

        var textRect = _subtitleText.rectTransform;
        textRect.offsetMin = new Vector2(horizontalPadding, verticalPadding);
        textRect.offsetMax = new Vector2(-horizontalPadding, -verticalPadding);
    }

    private static float ResolveInventorySafeVerticalOffset(HUDManager hud, RectTransform? parent, float configuredOffset)
    {
        var frames = hud.itemSlotIconFrames;
        if (parent == null || frames == null || frames.Length == 0 || parent.rect.height <= 0f)
        {
            return configuredOffset;
        }

        var corners = new Vector3[4];
        var highestSlotPointFromBottom = 0f;
        var foundBottomInventorySlot = false;
        foreach (var frame in frames)
        {
            if (frame == null || !frame.gameObject.activeInHierarchy)
            {
                continue;
            }

            frame.rectTransform.GetWorldCorners(corners);
            for (var i = 0; i < corners.Length; i++)
            {
                var local = parent.InverseTransformPoint(corners[i]);
                var fromBottom = local.y - parent.rect.yMin;
                if (fromBottom < 0f || fromBottom > parent.rect.height * 0.55f)
                {
                    continue;
                }

                foundBottomInventorySlot = true;
                highestSlotPointFromBottom = Mathf.Max(highestSlotPointFromBottom, fromBottom);
            }
        }

        if (!foundBottomInventorySlot)
        {
            return configuredOffset;
        }

        return Mathf.Clamp(Mathf.Max(configuredOffset, highestSlotPointFromBottom + 18f), 100f, 520f);
    }

    private static void ShowSubtitle(string text)
    {
        if (_subtitleRoot == null || _subtitleText == null)
        {
            return;
        }

        if (!string.Equals(_displayedText, text, StringComparison.Ordinal))
        {
            _displayedText = text;
            _subtitleText.text = text;
            FontFallbackService.TryUseManagedFallbackAsPrimary(_subtitleText, text);
            ResizePanelToContent();
        }

        if (!_subtitleRoot.activeSelf)
        {
            _subtitleRoot.SetActive(true);
            _subtitleRoot.transform.SetAsLastSibling();
        }
    }

    private static void HideSubtitle()
    {
        _displayedText = null;
        if (_subtitleRoot != null && _subtitleRoot.activeSelf)
        {
            _subtitleRoot.SetActive(false);
        }
    }

    private static void DestroyUi()
    {
        if (_driver != null)
        {
            _driver.enabled = false;
        }

        if (_subtitleRoot != null)
        {
            UnityEngine.Object.Destroy(_subtitleRoot);
        }

        _subtitleRoot = null;
        _subtitleText = null;
        _driver = null;
        _displayedText = null;
    }

    private static void ClearPlaybackState()
    {
        ActivePlaybacks.Clear();
        _playbackSequence = 0;
        if (_driver != null)
        {
            _driver.enabled = false;
        }
    }

    private static bool DetectExternalSubtitlePlugin()
    {
        try
        {
            foreach (var pair in Chainloader.PluginInfos)
            {
                var metadata = pair.Value?.Metadata;
                var guid = metadata?.GUID ?? pair.Key;
                var name = metadata?.Name ?? string.Empty;
                if (string.Equals(guid, Plugin.PluginGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (guid.IndexOf("subtitle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("subtitle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"External subtitles plugin detection failed safely: {ex.Message}");
        }

        return false;
    }

    private static Dictionary<string, SubtitleTrack> CreateTracks()
    {
        var tracks = new Dictionary<string, SubtitleTrack>(StringComparer.Ordinal)
        {
            ["IntroCompanySpeech"] = new SubtitleTrack(
                Cue(0f, 5f, "[公司提示音]"),
                Cue(7.31f, 10.05f, "欢迎来到上班第一天！"),
                Cue(10.14f, 12.35f, "这是配发给你们的自动驾驶飞船，"),
                Cue(12.44f, 15.20f, "合同期间，你们将在这里用餐和休息。"),
                Cue(15.28f, 19.75f, "[高速播放的模糊语音]"),
                Cue(19.94f, 21.60f, "请把这里当作自己的家。"),
                Cue(21.69f, 23.70f, "要完成入职流程，"),
                Cue(23.77f, 25.60f, "请查看操作手册，"),
                Cue(25.65f, 28.20f, "并登录飞船上的电脑终端。"),
                Cue(28.29f, 31.00f, "我们相信，你会成为公司的宝贵资产。"),
                Cue(31.035f, 32.75f, "宝贵……公司的宝贵资产……"),
                Cue(32.83f, 35.10f, "资产……宝贵……宝贵……公司的宝贵资产……"),
                Cue(35.235f, 37.129f, "宝贵……资……资产……")),
            ["0DaysLeftAlert"] = new SubtitleTrack(
                Cue(0f, 4.8f, "[公司提示音]"),
                Cue(4.969f, 7.10f, "请立即前往公司大楼，"),
                Cue(7.189f, 9.60f, "出售废料和其他物品。"),
                Cue(9.758f, 13.00f, "距离完成利润指标的期限只剩零天。"),
                Cue(13.085f, 14.80f, "你可以通过终端，"),
                Cue(14.874f, 17.061f, "让自动驾驶系统前往公司大楼。")),
            ["FiredVoiceline"] = new SubtitleTrack(
                Cue(0.15f, 2.18f, "由于你们未能完成利润指标，"),
                Cue(2.20f, 4.30f, "工作表现被评定为低于标准。"),
                Cue(5.47f, 7.52f, "欢迎进入公司的惩戒流程。")),
            ["SnareFleaTipChannel"] = new SubtitleTrack(
                Cue(0.33f, 2.75f, "如果某个实体接触了船员，"),
                Cue(2.83f, 5.20f, "请不要立即采取自卫措施。"),
                Cue(5.30f, 7.40f, "请先询问该船员以下问题："),
                Cue(7.495f, 9.32f, "“这个实体有攻击性吗？”"),
                Cue(9.41f, 10.58f, "“你受伤了吗？”"),
                Cue(10.635f, 11.67f, "“你需要帮助吗？”"),
                Cue(11.73f, 13.84f, "如果这些问题的答案都是“是”，"),
                Cue(13.92f, 15.82f, "再开始采取自卫措施。"),
                Cue(15.90f, 17.12f, "如果船员感到紧张，"),
                Cue(17.19f, 19.54f, "可以问一句：“今天过得怎么样？”"),
                Cue(19.62f, 22.147f, "感谢配合，祝你旅途愉快！")),
            ["Mic1"] = OneLine("你们的工作让公司十分满意。"),
            ["Mic2"] = OneLine("我们重视你们的投入。"),
            ["Mic4"] = OneLine("你们的辛勤工作对公司极具价值。"),
            ["Mic9"] = OneLine("你们诚实的工作对公司极具价值。"),
            ["Mic10"] = OneLine("你们是真正的专业人士。"),
            ["Mic3"] = OneLine("我们需要你们…… [信号故障]"),
            ["Mic6"] = OneLine("[失真的喊声]"),
            ["Mic7"] = OneLine("公司必须保持满意……公司必…… [信号中断]"),
            ["Mic8"] = OneLine("这面墙无法困住…… [信号故障]"),
            ["Mic11"] = OneLine("让我们的投资者满意。")
        };

        return tracks;
    }

    private static SubtitleTrack OneLine(string text) => new(Cue(0f, float.MaxValue, text));

    private static SubtitleCue Cue(float start, float end, string text) => new(start, end, NormalizeStandaloneCue(text));

    private static string NormalizeStandaloneCue(string text)
    {
        var normalized = text.Replace("。", " ").TrimEnd();
        return normalized.EndsWith("，", StringComparison.Ordinal) || normalized.EndsWith(",", StringComparison.Ordinal)
            ? normalized[..^1]
            : normalized;
    }

    private enum PlaybackKind
    {
        Tv = 1,
        CompanyDesk = 2,
        Ship = 3
    }

    private sealed class ActivePlayback
    {
        public ActivePlayback(
            AudioSource source,
            AudioClip clip,
            SubtitleTrack track,
            PlaybackKind kind,
            TVScript? tv,
            float elapsed,
            int sequence)
        {
            Source = source;
            Clip = clip;
            Track = track;
            Kind = kind;
            Tv = tv;
            Elapsed = elapsed;
            Sequence = sequence;
        }

        public AudioSource Source { get; }
        public AudioClip Clip { get; }
        public SubtitleTrack Track { get; }
        public PlaybackKind Kind { get; }
        public TVScript? Tv { get; }
        public float Elapsed { get; set; }
        public int Sequence { get; }
        public int Priority => (int)Kind;
    }

    private sealed class SubtitleTrack
    {
        public SubtitleTrack(params SubtitleCue[] cues)
        {
            Cues = cues;
        }

        public SubtitleCue[] Cues { get; }
    }

    private readonly struct SubtitleCue
    {
        public SubtitleCue(float start, float end, string text)
        {
            Start = start;
            End = end;
            Text = text;
        }

        public float Start { get; }
        public float End { get; }
        public string Text { get; }
    }
}

internal sealed class CompanySubtitleDriver : MonoBehaviour
{
    private void Update()
    {
        CompanySubtitleService.Tick(Time.unscaledDeltaTime);
    }
}
