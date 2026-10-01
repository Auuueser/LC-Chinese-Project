using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Dissonance;
using Dissonance.Audio.Capture;
using Dissonance.VAD;
using GameNetcodeStuff;
using HarmonyLib;
using NAudio.Wave;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace V81TestChn;

internal static class SpeechInputService
{
    private sealed class AudioListener : IMicrophoneSubscriber, IVoiceActivationListener
    {
        internal readonly SpeechAudioBuffer Buffer = new();
        internal readonly SpeechVoiceTail Tail = new();
        public void ReceiveMicrophoneData(ArraySegment<float> buffer, WaveFormat format)
            => Buffer.Receive(buffer, format.SampleRate, format.Channels);
        public void Reset() => Buffer.Reset();
        public void VoiceActivationStart() { var now = Now; Tail.Voice(true, now); Buffer.Voice(true, now); }
        public void VoiceActivationStop() { var now = Now; Tail.Voice(false, now); Buffer.Voice(false, now); }
    }

    private static ConfigEntry<bool>? _enabled, _suppress, _showStatus;
    private static ConfigEntry<Key>? _hotkey;
    private static ConfigEntry<SpeechMouseHotkey>? _mouseHotkey;
    private static ConfigEntry<float>? _silence;
    private static bool _isEnabled, _show, _installed, _transportFailed, _audioSubscribed, _vadSubscribed;
    private static volatile bool _suppressVoice, _recording, _settingsPending, _tailPending;
    private static Key _key;
    private static SpeechMouseHotkey _mouseKey;
    private static double _silenceSeconds, _idleSince, _noticeUntil, _nextSend;
    private static string _directory = "";
    private static Task<AudioListener>? _preparation;
    private static AudioListener? _audio;
    private static DissonanceComms? _comms;
    private static object? _pipeline, _microphone;
    private static FieldInfo? _pipelineField;
    private static FieldInfo? _transportField;
    private static volatile object? _transport;
    private static Harmony? _harmony;
    private static readonly HashSet<MethodBase> VoiceSendHooks = new();
    private static double _nextTransportCheck;
    private static double _nextStartFailureLog;
    private static HUDManager? _hud;
    private static PlayerControllerB? _owner;
    private static NetworkManager? _network;
    private static TMP_Text? _status;
    private static SpeechWorkerClient? _worker;
    private static Task<string>? _result;
    private static long _resultId;
    private static List<string>? _messages;
    private static int _messageIndex;
    private static double Now => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    internal static void Initialize(string pluginDirectory, ConfigFile config, Harmony harmony)
    {
        // Native speech libraries have no CLR entry and are skipped by BepInEx.
        // Resolve beside the plugin for both manager and manual installations.
        _directory = Path.Combine(pluginDirectory, "speech");
        // Retain the previous layout as an upgrade fallback.
        if (!File.Exists(Path.Combine(_directory, "V81SpeechWorker.exe")))
            _directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "monomod", "Aueser-LC_Chinese_Project", "V81SpeechWorker");
        if (!File.Exists(Path.Combine(_directory, "V81SpeechWorker.exe")))
            _directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "monomod", "V81SpeechWorker");
        _enabled = config.Bind(ConfigSections.SpeechInput, "Enabled", true, "启用离线语音转文字。模型仅在使用时加载；关闭会取消录入并恢复原声。");
        _hotkey = config.Bind(ConfigSections.SpeechInput, "Hotkey", Key.NumpadPlus, "按一次开始语音录入，再按一次结束并发送文字；不打开聊天框。默认小键盘 +，同时支持主键盘 Shift+=。None 关闭键盘热键，鼠标侧键独立设置。");
        _mouseHotkey = config.Bind(ConfigSections.SpeechInput, "MouseHotkey", SpeechMouseHotkey.None,
            "鼠标侧键也可开始/结束录入：MouseBack 为后退侧键（通常 Mouse4），MouseForward 为前进侧键（通常 Mouse5），None 关闭鼠标热键。与键盘热键独立，即时生效。");
        _silence = config.Bind(ConfigSections.SpeechInput, "SilenceSeconds", 1.5f,
            new ConfigDescription("说话后静音多久自动结束（秒）。未说话 5 秒取消，单次录音最长 30 秒。",
                new AcceptableValueRange<float>(0.5f, 5f)));
        _suppress = config.Bind(ConfigSections.SpeechInput, "SuppressOriginalVoice", true, "录入期间暂停向队友传送原声；主动按热键结束后固定暂停 1 秒再恢复，不因继续说话延长；自动结束后等待说话结束并连续静音 0.35 秒恢复。取消任务或关闭此选项立即解除拦截，不修改原有静音设置。");
        _showStatus = config.Bind(ConfigSections.SpeechInput, "ShowStatus", true, "在物品栏上方显示语音输入和识别状态，与双手已满提示叠放。");
        _enabled.SettingChanged += SettingsChanged; _hotkey.SettingChanged += SettingsChanged;
        _mouseHotkey.SettingChanged += SettingsChanged;
        _silence.SettingChanged += SettingsChanged; _suppress.SettingChanged += SettingsChanged; _showStatus.SettingChanged += SettingsChanged;
        LiveConfigRegistration.RegisterBool(_enabled); LiveConfigRegistration.RegisterEnum(_hotkey);
        LiveConfigRegistration.RegisterEnum(_mouseHotkey);
        LiveConfigRegistration.RegisterFloat(_silence); LiveConfigRegistration.RegisterBool(_suppress); LiveConfigRegistration.RegisterBool(_showStatus);
        _pipelineField = AccessTools.Field(typeof(DissonanceComms), "_capture");
        _transportField = AccessTools.Field(typeof(DissonanceComms), "_net");
        _harmony = harmony;
        var type = typeof(DissonanceComms).Assembly.GetType("Dissonance.Audio.Capture.CapturePipelineManager");
        var update = type == null ? null : AccessTools.Method(type, "Update", new[] { typeof(bool), typeof(float) });
        if (update == null || _pipelineField == null || _transportField == null) throw new MissingMethodException("Dissonance voice pipeline is unavailable.");
        harmony.Patch(update, prefix: new HarmonyMethod(typeof(SpeechInputService), nameof(VoiceTransmissionPrefix)) { priority = int.MinValue });
        _installed = true;
        _settingsPending = false;
        ApplySettings();
    }

    private static void SettingsChanged(object? sender, EventArgs args)
        => _settingsPending = true;

    private static void ApplySettings()
    {
        _isEnabled = _enabled?.Value == true;
        _key = _hotkey?.Value ?? Key.NumpadPlus;
        if (!Enum.IsDefined(typeof(Key), _key)) _key = Key.NumpadPlus;
        _mouseKey = _mouseHotkey?.Value ?? SpeechMouseHotkey.None;
        if (!Enum.IsDefined(typeof(SpeechMouseHotkey), _mouseKey)) _mouseKey = SpeechMouseHotkey.None;
        var silence = _silence?.Value ?? 1.5f;
        _silenceSeconds = float.IsNaN(silence) || float.IsInfinity(silence) ? 1.5 : Mathf.Clamp(silence, 0.5f, 5f);
        _suppressVoice = _suppress?.Value == true; _show = _showStatus?.Value == true;
        if (!_suppressVoice && _tailPending) ReleaseVoiceTail();
        if (!_isEnabled) { Cancel(); _audio = null; }
        else if (_audio == null && _preparation == null) _preparation = Task.Run(() => new AudioListener());
        if (_status != null) _status.enabled = _show && (_recording || _result != null || Now < _noticeUntil);
    }

    private static void VoiceTransmissionPrefix(object __instance, ref bool muted)
        => muted = SpeechVoiceGate.Mute(muted, __instance, _pipeline, _recording || _tailPending, _suppressVoice);

    private static bool VoiceSendPrefix(object __instance)
        => !SpeechVoiceGate.Mute(false, __instance, _transport, _recording || _tailPending, _suppressVoice);

    private static bool PrepareTransport()
    {
        if (_transportFailed) return false;
        try { return PrepareTransportCore(); }
        catch (Exception ex)
        {
            // A failed adapter must not retry patches or log every second.
            // Retry only for a new HUD/session, keeping ordinary chat intact.
            _transportFailed = true;
            Plugin.Log.LogWarning("Speech voice adapter unavailable: " + ex.GetType().Name);
            return false;
        }
    }

    private static bool PrepareTransportCore()
    {
        var comms = StartOfRound.Instance != null ? StartOfRound.Instance.voiceChatModule : null;
        var transport = comms == null ? null : _transportField?.GetValue(comms);
        if (transport == null) return false;
        if (ReferenceEquals(transport, _transport)) return true;
        // Resolve actual implementations, including explicit implementations
        // and inherited generic transports. No mod names or GUIDs are used.
        var map = transport.GetType().GetInterfaceMap(typeof(Dissonance.Networking.ICommsNetwork));
        MethodInfo? send = null;
        for (var i = 0; i < map.InterfaceMethods.Length; i++)
            if (map.InterfaceMethods[i].Name == "SendVoice") { send = map.TargetMethods[i]; break; }
        if (send == null || send.IsAbstract) { _transportFailed = true; return false; }
        // Interface maps reflect inherited members through the concrete type.
        // Harmony requires the member reflected through its declaring type.
        send = AccessTools.DeclaredMethod(send.DeclaringType, send.Name, new[] { typeof(ArraySegment<byte>) });
        if (send == null) { _transportFailed = true; return false; }
        if (!VoiceSendHooks.Contains(send))
        {
            _harmony!.Patch(send, prefix: new HarmonyMethod(typeof(SpeechInputService), nameof(VoiceSendPrefix)) { priority = int.MinValue });
            VoiceSendHooks.Add(send);
        }
        _transport = transport;
        return true;
    }

    internal static void Attach(HUDManager hud)
    {
        Cancel();
        if (_status != null) UnityEngine.Object.Destroy(_status.gameObject);
        _status = null; _hud = hud; _transportFailed = false; _nextTransportCheck = 0;
        // Create our own text only when first needed; do not clone chat behaviours.
    }

    internal static void Pump()
    {
        if (!_installed || Plugin.IsRuntimeShuttingDown) return;
        try { PumpCore(); }
        catch (Exception ex)
        {
            Cancel();
            Notify("语音输入暂时不可用");
            Plugin.Log.LogWarning("Speech input stopped: " + ex.GetType().Name);
        }
    }

    private static void PumpCore()
    {
        // Config reloads can originate outside Unity's main thread.
        // Apply all cancellation, subscriptions and HUD changes here.
        if (_settingsPending) { _settingsPending = false; ApplySettings(); }
        if (_preparation != null && _preparation.IsCompleted)
        {
            if (_preparation.Status == TaskStatus.RanToCompletion && _isEnabled) _audio = _preparation.Result;
            else SpeechWorkerClient.Forget(_preparation);
            _preparation = null;
        }
        if (!_isEnabled) return;
        var now = Now;
        if (_hud != null && now >= _nextTransportCheck)
        {
            _nextTransportCheck = now + 1;
            PrepareTransport();
        }
        if (_status != null && _status.enabled && !_recording && _result == null && now >= _noticeUntil) _status.enabled = false;
        if (_worker != null && !_recording && _result == null && _messages == null && now - _idleSince >= 300)
        { _worker.Dispose(); _worker = null; }
        if (_recording || _tailPending || _result != null || _messages != null)
        {
            if (!CanUse(out var player) || !ReferenceEquals(player, _owner) || !ReferenceEquals(NetworkManager.Singleton, _network)
                || !ReferenceEquals(StartOfRound.Instance!.voiceChatModule, _comms)) { Cancel(); return; }
        }
        // Device recovery can reset or replace capture while keeping the same
        // DissonanceComms. Validate before the stop hotkey can submit old audio.
        if ((_recording || _tailPending) && (_audio!.Buffer.Faulted
            || !ReferenceEquals(_pipelineField!.GetValue(_comms), _pipeline)
            || !ReferenceEquals(_comms!.MicrophoneCapture, _microphone)
            || _comms.MicrophoneCapture == null || !_comms.MicrophoneCapture.IsRecording))
        { Cancel(); Notify("麦克风已重置或停止，请重新录入"); return; }
        if (_tailPending && _audio!.Tail.ReadyToRelease(now)) ReleaseVoiceTail();
        var keyboard = Keyboard.current;
        var keyboardPressed = keyboard != null && _key != Key.None
            && (keyboard[_key].wasPressedThisFrame || (_key == Key.NumpadPlus
                && keyboard[Key.Equals].wasPressedThisFrame
                && (keyboard[Key.LeftShift].isPressed || keyboard[Key.RightShift].isPressed)));
        var mouse = Mouse.current;
        var mousePressed = mouse != null && ((_mouseKey == SpeechMouseHotkey.MouseBack && mouse.backButton.wasPressedThisFrame)
            || (_mouseKey == SpeechMouseHotkey.MouseForward && mouse.forwardButton.wasPressedThisFrame));
        var pressed = Application.isFocused && (keyboardPressed || mousePressed);
        if (pressed)
        {
            // Recover a missed HUD.Start attachment without a scene search.
            if (_hud == null && HUDManager.Instance != null)
            {
                HandsFullLayoutService.Attach(HUDManager.Instance);
                Attach(HUDManager.Instance);
            }
            if (_recording) Finish(manual: true);
            else if (_result != null || _messages != null) Notify("正在识别或发送，请稍候");
            else if (CanUse(out var player, out var reason)) Begin(player!);
            else RefuseStart(reason);
        }
        if (_recording)
        {
            if (_worker!.Ready.IsFaulted || _worker.Ready.IsCanceled) { Cancel(); Notify("语音模型加载失败"); return; }
            var stop = _audio!.Buffer.Poll(now, _silenceSeconds);
            if (stop == SpeechStopReason.NoSpeech) { Cancel(); Notify("未检测到说话"); }
            else if (stop != SpeechStopReason.None) Finish();
        }
        if (_result != null && _result.IsCompleted)
        {
            var result = _result; _idleSince = now;
            if (result.Status != TaskStatus.RanToCompletion) { SpeechWorkerClient.Forget(result); Cancel(); Notify("语音识别失败，请重新录入"); return; }
            _result = null;
            if (!_audio!.Buffer.Session.Accepts(_resultId)) return;
            var text = SpeechText.Clean(result.Result);
            if (SpeechText.IsCommand(text))
            {
                var length = Math.Min(24, text.Length);
                if (length < text.Length && char.IsHighSurrogate(text[length - 1])) length--;
                Notify("指令请手动输入：" + text.Substring(0, length) + (length < text.Length ? "…" : ""), 7);
                return;
            }
            if (text.Length == 0) { Notify("未识别到文字"); return; }
            try { _messages = SpeechText.Split(text); }
            catch (InvalidOperationException) { Notify("识别结果含指令，请手动输入", 5); return; }
            _messageIndex = 0; _nextSend = now;
        }
        if (_messages != null && now >= _nextSend)
        {
            if (_messageIndex < _messages.Count)
            {
                _hud!.AddTextToChatOnServer(_messages[_messageIndex++], (int)_owner!.playerClientId);
                _nextSend = now + SpeechText.SendInterval;
            }
            if (_messageIndex == _messages.Count) { _messages = null; Notify("语音文字已发送", 1.5); _idleSince = now; }
        }
    }

    private static bool CanUse(out PlayerControllerB? player)
        => CanUse(out player, out _);

    private static bool CanUse(out PlayerControllerB? player, out string reason)
    {
        player = GameNetworkManager.Instance != null ? GameNetworkManager.Instance.localPlayerController : null;
        var network = NetworkManager.Singleton;
        reason = "";
        if (!Application.isFocused) reason = "请先切回游戏";
        else if (player is null || player == null || !player.isPlayerControlled) reason = "请进入游戏后再使用语音输入";
        else if (_hud == null) reason = "游戏界面尚未就绪";
        else if (player.isPlayerDead) reason = "死亡时无法使用语音输入";
        else if (player.isTypingChat) reason = "请先关闭文字聊天";
        else if (player.inTerminalMenu) reason = "请先退出终端";
        else if (player.quickMenuManager != null && player.quickMenuManager.isMenuOpen) reason = "请先关闭菜单";
        else if (!((player.IsOwner && (!player.IsServer || player.isHostPlayerObject)) || player.isTestingPlayer)) reason = "本地玩家尚未就绪";
        else if (network is null || network == null || !network.IsListening || !(network.IsClient || network.IsHost)) reason = "房间连接尚未就绪";
        else if (StartOfRound.Instance == null || StartOfRound.Instance.voiceChatModule == null) reason = "游戏语音尚未就绪";
        return reason.Length == 0;
    }

    private static void Begin(PlayerControllerB player)
    {
        if (_audio == null) { RefuseStart("正在准备语音输入，请稍候"); return; }
        if (!PrepareTransport()) { RefuseStart("游戏语音尚未就绪，请稍候"); return; }
        if (!File.Exists(Path.Combine(_directory, "V81SpeechWorker.exe")) || !File.Exists(Path.Combine(_directory, "models", "model.int8.onnx")))
        { RefuseStart("语音组件缺失，请安装完整汉化包"); return; }
        _owner = player; _network = NetworkManager.Singleton; _comms = StartOfRound.Instance!.voiceChatModule;
        _pipeline = _pipelineField!.GetValue(_comms);
        _microphone = _comms.MicrophoneCapture;
        if (_pipeline == null || _microphone == null || !_comms.MicrophoneCapture.IsRecording)
        { RefuseStart("麦克风尚未就绪，请等待恢复后重试"); return; }
        _resultId = _audio.Buffer.Begin(Now);
        _recording = true;
        try
        {
            // Keep the packet guard active if a new recording begins while
            // the previous utterance is still draining, then rebind VAD once.
            if (_tailPending) ReleaseVoiceTail();
            _audio.Tail.Begin(Now);
            _audioSubscribed = true;
            _comms.SubscribeToRecordedAudio(_audio);
            _vadSubscribed = true;
            _comms.SubcribeToVoiceActivation(_audio);
            _worker ??= new SpeechWorkerClient(_directory);
            SetStatus("语音输入中…");
        }
        catch { Cancel(); throw; }
    }

    private static void Finish(bool manual = false)
    {
        var count = _audio!.Buffer.End(out var spoken);
        StopListening(keepVoiceTail: true, manual: manual);
        _idleSince = Now;
        if (!spoken || count == 0) { Notify("未检测到说话"); return; }
        _result = _worker!.RecognizeAsync(_resultId, _audio.Buffer.Samples, count);
        SetStatus("正在识别…");
    }

    private static void StopListening(bool keepVoiceTail = false, bool manual = false)
    {
        // Set the tail guard BEFORE recording is cleared: the final SendVoice
        // callback may run concurrently with this main-thread transition.
        if (keepVoiceTail && _suppressVoice && _audio != null)
        {
            _audio.Tail.Hold(Now, manual);
            _tailPending = true;
        }
        else
        {
            _tailPending = false;
            _audio?.Tail.Cancel();
        }
        _recording = false;
        if (_audioSubscribed)
        {
            _audioSubscribed = false;
            if (_comms != null && _audio != null)
                try { _comms.UnsubscribeFromRecordedAudio(_audio); } catch (Exception) { }
        }
        if (!_tailPending) UnsubscribeVoiceActivation();
    }

    private static void ReleaseVoiceTail()
    {
        _tailPending = false;
        _audio?.Tail.Cancel();
        UnsubscribeVoiceActivation();
    }

    private static void UnsubscribeVoiceActivation()
    {
        if (!_vadSubscribed) return;
        _vadSubscribed = false;
        if (_comms != null && _audio != null)
            try { _comms.UnsubscribeFromVoiceActivation(_audio); } catch (Exception) { }
    }

    internal static void Cancel()
    {
        _audio?.Buffer.Cancel();
        var wasActive = _recording || _tailPending || _result != null || _messages != null;
        try { StopListening(); } finally { _recording = _tailPending = false; }
        if (_result != null) SpeechWorkerClient.Forget(_result);
        _result = null; _messages = null;
        if (wasActive && _worker != null) { _worker.Dispose(); _worker = null; }
        _pipeline = null; _microphone = null; _owner = null; _network = null;
        _noticeUntil = 0;
        if (_status != null) _status.enabled = false;
    }

    private static void Notify(string text, double duration = 3)
    { SetStatus(text); _noticeUntil = Now + duration; }

    private static void RefuseStart(string reason)
    {
        Notify(reason);
        var now = Now;
        if (now < _nextStartFailureLog) return;
        _nextStartFailureLog = now + 3;
        Plugin.Log.LogWarning("Speech input could not start: " + reason);
    }

    private static void SetStatus(string text)
    {
        if (_status == null && _hud != null && _hud.typingIndicator != null)
        {
            var original = _hud.typingIndicator;
            var obj = new GameObject("LCChineseSpeechStatus", typeof(RectTransform));
            obj.transform.SetParent(original.transform.parent, false);
            var rect = (RectTransform)obj.transform;
            var sourceRect = original.rectTransform;
            rect.anchorMin = sourceRect.anchorMin; rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot; rect.sizeDelta = new Vector2(720, sourceRect.sizeDelta.y);
            rect.anchoredPosition3D = sourceRect.anchoredPosition3D; rect.localScale = sourceRect.localScale;
            _status = obj.AddComponent<TextMeshProUGUI>();
            _status.font = original.font; _status.fontSharedMaterial = original.fontSharedMaterial;
            _status.fontSize = original.fontSize; _status.color = original.color;
            _status.alignment = TextAlignmentOptions.Center; _status.raycastTarget = false;
            _status.richText = false; _status.enableWordWrapping = false;
            HandsFullLayoutService.AttachSpeech(_status);
        }
        if (_status == null) return;
        _status.text = text; _status.enabled = _show;
    }

    internal static void Shutdown()
    {
        Cancel(); _worker?.Dispose(); _worker = null;
        if (_enabled != null) _enabled.SettingChanged -= SettingsChanged;
        if (_hotkey != null) _hotkey.SettingChanged -= SettingsChanged;
        if (_mouseHotkey != null) _mouseHotkey.SettingChanged -= SettingsChanged;
        if (_silence != null) _silence.SettingChanged -= SettingsChanged;
        if (_suppress != null) _suppress.SettingChanged -= SettingsChanged;
        if (_showStatus != null) _showStatus.SettingChanged -= SettingsChanged;
        if (_preparation != null) SpeechWorkerClient.Forget(_preparation);
        _preparation = null; _audio = null;
        if (_status != null) UnityEngine.Object.Destroy(_status.gameObject);
        _status = null; _hud = null; _comms = null; _pipeline = null; _microphone = null; _installed = false;
        _transport = null; _harmony = null; _transportFailed = false; _settingsPending = false; VoiceSendHooks.Clear();
    }
}
