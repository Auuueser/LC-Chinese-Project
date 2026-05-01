using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Text;

namespace V81TestChn;

internal static class TextPatches
{
    private static readonly FieldInfo? TerminalModifyingTextField = AccessTools.Field(typeof(Terminal), "modifyingText");
    private const float HudScannerLocalizationIntervalSeconds = 0.1f;
    private const float HudScannerRootLocalizationIntervalSeconds = 0.5f;
    private const float SignalTranslatorLocalizationWindowSeconds = 2.0f;
    private const float SignalTranslatorLocalizationRetryIntervalSeconds = 0.05f;
    private const float SignalTranslatorReceivingSignalFontScale = 1.25f;
    private const string SignalTranslatorReceivingSignalEnglish = "RECEIVING SIGNAL";
    private const string SignalTranslatorReceivingSignalChinese = "\u6b63\u5728\u63a5\u6536\u4fe1\u53f7";
    private const int TextClassificationCacheLimit = 4096;
    private static float _nextHudScannerLocalizationTime;
    private static float _nextHudScannerRootLocalizationTime;
    private static float _signalTranslatorLocalizationUntil;
    private static float _nextSignalTranslatorLocalizationTime;
    private static int _lastHudScannerRootId;
    private static int _lastHudScannerTranslatedRootId;
    private static int _signalTranslatorTextCacheRootId;
    private static string? _lastHudScannerTotalText;
    private static TMP_Text[] SignalTranslatorTextCache = Array.Empty<TMP_Text>();
    private static readonly Dictionary<int, CachedTextClassification> InputFieldTextCache = new();
    private static readonly Dictionary<int, CachedTextClassification> LobbySlotTextCache = new();
    private static readonly Dictionary<int, float> SignalTranslatorReceivingSignalOriginalFontSizes = new();

    private readonly struct CachedTextClassification
    {
        public CachedTextClassification(int parentId, bool value)
        {
            ParentId = parentId;
            Value = value;
        }

        public int ParentId { get; }
        public bool Value { get; }
    }

    public static int Install(Harmony harmony)
    {
        var patched = 0;

        PatchPostfix(harmony, typeof(MenuManager), "OnEnable", nameof(MenuManagerOnEnablePostfix), ref patched);
        PatchPostfix(harmony, typeof(MenuManager), "EnableUIPanel", nameof(MenuManagerEnableUIPanelPostfix), ref patched);
        PatchPrefix(harmony, typeof(MenuManager), "DisplayMenuNotification", nameof(MenuManagerDisplayMenuNotificationPrefix), ref patched);
        PatchPostfix(harmony, typeof(DeleteFileButton), "SetFileToDelete", nameof(DeleteFileButtonSetFileToDeletePostfix), ref patched);

        PatchPostfix(harmony, typeof(PreInitSceneScript), "Start", nameof(PreInitSceneScriptStartPostfix), ref patched);
        PatchPostfix(harmony, typeof(PreInitSceneScript), "SetLaunchPanelsEnabled", nameof(PreInitSceneScriptSetLaunchPanelsEnabledPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "OpenQuickMenu", nameof(QuickMenuManagerOpenPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "Start", nameof(StartOfRoundStartPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "Start", nameof(StartOfRoundStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "AutoSaveShipData", nameof(StartOfRoundAutoSaveShipDataPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "ChangeLevel", nameof(StartOfRoundChangeLevelPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "ChangePlanet", nameof(StartOfRoundChangePlanetPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "ChangePlanet", nameof(StartOfRoundChangePlanetPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel", nameof(StartOfRoundSetMapScreenInfoPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel", nameof(StartOfRoundSetMapScreenInfoPostfix), ref patched);
        PatchPrefix(harmony, typeof(GameNetworkManager), "SaveGame", nameof(GameNetworkManagerSaveGamePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "Start", nameof(HudManagerStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "UseSignalTranslatorClientRpc", nameof(HudManagerUseSignalTranslatorClientRpcPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UseSignalTranslatorClientRpc", nameof(HudManagerUseSignalTranslatorClientRpcPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UpdateScanNodes", nameof(HudManagerUpdateScanNodesPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayCreditsEarning", nameof(HudManagerDisplayCreditsEarningPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayNewDeadline", nameof(HudManagerDisplayNewDeadlinePostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UpdateBoxesSpectateUI", nameof(HudManagerUpdateBoxesSpectateUiPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "FillEndGameStats", nameof(HudManagerFillEndGameStatsPostfix), ref patched);
        PatchPostfix(harmony, typeof(ChallengeLeaderboardSlot), "SetSlotValues", nameof(ChallengeLeaderboardSlotSetSlotValuesPostfix), ref patched);
        PatchPostfix(harmony, typeof(LobbySlot), "SetModdedIcon", nameof(LobbySlotSetModdedIconPostfix), ref patched);
        PatchPostfix(harmony, typeof(HangarShipDoor), "Start", nameof(HangarShipDoorStartPostfix), ref patched);

        PatchPrefix(harmony, typeof(HUDManager), "DisplayTip", nameof(HudManagerDisplayTipPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "DisplayStatusEffect", nameof(HudManagerDisplayStatusEffectPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ChangeControlTip", nameof(HudManagerChangeControlTipPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ChangeControlTipMultiple", nameof(HudManagerChangeControlTipMultiplePrefix), ref patched);

        PatchPostfix(harmony, typeof(GrabbableObject), "Start", nameof(GrabbableObjectStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(GrabbableObject), "SetControlTipsForItem", nameof(GrabbableObjectSetControlTipsPrefix), ref patched);

        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text)), nameof(TmpSetTextPrefix), ref patched);
        PatchPostfix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.text)), nameof(TmpSetTextPostfix), ref patched);
        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.color)), nameof(TmpSetColorPrefix), ref patched);
        PatchPostfix(harmony, AccessTools.PropertySetter(typeof(TMP_Text), nameof(TMP_Text.color)), nameof(TmpSetColorPostfix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(TMP_FontAsset), "Awake"), nameof(TmpFontAssetAwakePostfix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(Animator), nameof(Animator.SetTrigger), new[] { typeof(string) }), nameof(AnimatorSetTriggerPostfix), ref patched);
        PatchPrefix(harmony, AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(string), typeof(bool) }), nameof(AnimatorSetBoolPrefix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(Animator), nameof(Animator.SetBool), new[] { typeof(string), typeof(bool) }), nameof(AnimatorSetBoolPostfix), ref patched);
        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(Text), nameof(Text.text)), nameof(UiTextSetTextPrefix), ref patched);
        PatchPostfix(harmony, AccessTools.PropertySetter(typeof(Text), nameof(Text.text)), nameof(UiTextSetTextPostfix), ref patched);
        PatchPrefix(harmony, AccessTools.PropertySetter(typeof(TextMesh), nameof(TextMesh.text)), nameof(TextMeshSetTextPrefix), ref patched);
        PatchPostfix(harmony, AccessTools.PropertySetter(typeof(TextMesh), nameof(TextMesh.text)), nameof(TextMeshSetTextPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "TextPostProcess", nameof(TerminalTextPostProcessPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "LoadNewNode", nameof(TerminalLoadNewNodePostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "OnSubmit", nameof(TerminalOnSubmitPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "ParsePlayerSentence", nameof(TerminalParsePlayerSentencePostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "loadTextAnimation", nameof(TerminalLoadTextAnimationPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "BeginUsingTerminal", nameof(TerminalBeginUsingPostfix), ref patched);
        PatchPrefix(harmony, typeof(Terminal), "SetItemSales", nameof(TerminalSetItemSalesPrefix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "SetItemSales", nameof(TerminalSetItemSalesPostfix), ref patched);
        PatchPrefix(harmony, typeof(VideoPlayer), nameof(VideoPlayer.Play), nameof(VideoPlayerPlayPrefix), ref patched);
        PatchPostfix(harmony, typeof(VideoPlayer), nameof(VideoPlayer.Play), nameof(VideoPlayerPlayPostfix), ref patched);

        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(bool) }),
            nameof(TmpSetTextStringBoolPrefix),
            ref patched);
        PatchPostfix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(bool) }),
            nameof(TmpSetTextPostfix),
            ref patched);
        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(float) }),
            nameof(TmpSetTextStringFloatPrefix),
            ref patched);
        PatchPostfix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(string), typeof(float) }),
            nameof(TmpSetTextPostfix),
            ref patched);
        PatchPrefix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(StringBuilder) }),
            nameof(TmpSetTextStringBuilderPrefix),
            ref patched);
        PatchPostfix(
            harmony,
            AccessTools.Method(typeof(TMP_Text), nameof(TMP_Text.SetText), new[] { typeof(StringBuilder) }),
            nameof(TmpSetTextPostfix),
            ref patched);
        return patched;
    }

    private static void PatchPrefix(Harmony harmony, Type targetType, string targetMethod, string patchMethod, ref int patched)
    {
        PatchPrefix(harmony, AccessTools.Method(targetType, targetMethod), patchMethod, ref patched);
    }

    private static void PatchPostfix(Harmony harmony, Type targetType, string targetMethod, string patchMethod, ref int patched)
    {
        PatchPostfix(harmony, AccessTools.Method(targetType, targetMethod), patchMethod, ref patched);
    }

    private static void PatchPrefix(Harmony harmony, MethodBase? original, string patchMethod, ref int patched)
    {
        Patch(harmony, original, prefixName: patchMethod, postfixName: null, ref patched);
    }

    private static void PatchPostfix(Harmony harmony, MethodBase? original, string patchMethod, ref int patched)
    {
        Patch(harmony, original, prefixName: null, postfixName: patchMethod, ref patched);
    }

    private static void Patch(Harmony harmony, MethodBase? original, string? prefixName, string? postfixName, ref int patched)
    {
        var patchName = prefixName ?? postfixName ?? "";
        try
        {
            if (original == null)
            {
                Plugin.Log.LogWarning($"Manual patch skipped; target not found for {patchName}");
                return;
            }

            var patchMethod = AccessTools.Method(typeof(TextPatches), patchName);
            if (patchMethod == null)
            {
                Plugin.Log.LogWarning($"Manual patch skipped; patch method not found: {patchName}");
                return;
            }

            harmony.Patch(
                original,
                prefixName == null ? null : new HarmonyMethod(patchMethod),
                postfixName == null ? null : new HarmonyMethod(patchMethod));
            patched++;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Manual patch failed for {patchName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(MenuManager), "OnEnable")]
    [HarmonyPostfix]
    private static void MenuManagerOnEnablePostfix(MenuManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        Plugin.LogPatchEntry("MenuManager.OnEnable");
        TargetedUiTranslator.TranslateMenuManager(__instance, "MenuManager.OnEnable");
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes("MenuManager.OnEnable.autosave");
    }

    [HarmonyPatch(typeof(MenuManager), "DisplayMenuNotification")]
    [HarmonyPrefix]
    private static void MenuManagerDisplayMenuNotificationPrefix(ref string notificationText, ref string buttonText)
    {
        notificationText = TargetedUiTranslator.TranslateDynamic(notificationText);
        buttonText = TargetedUiTranslator.TranslateDynamic(buttonText);
    }

    private static void MenuManagerEnableUIPanelPostfix(GameObject enablePanel)
    {
        TargetedUiTranslator.TranslateRoot(enablePanel, "MenuManager.EnableUIPanel");
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes("MenuManager.EnableUIPanel.autosave");
    }

    private static void DeleteFileButtonSetFileToDeletePostfix(DeleteFileButton __instance)
    {
        if (__instance == null)
        {
            return;
        }

        var root = __instance.transform.parent?.gameObject ?? __instance.gameObject;
        TargetedUiTranslator.TranslateRoot(root, "DeleteFileButton.SetFileToDelete");
    }

    [HarmonyPatch(typeof(PreInitSceneScript), "Start")]
    [HarmonyPostfix]
    private static void PreInitSceneScriptStartPostfix(PreInitSceneScript __instance)
    {
        if (__instance == null)
        {
            return;
        }

        TargetedUiTranslator.TranslatePreInit(__instance, "PreInitSceneScript.Start");
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes("PreInitSceneScript.Start.autosave");
    }

    [HarmonyPatch(typeof(PreInitSceneScript), "SetLaunchPanelsEnabled")]
    [HarmonyPostfix]
    private static void PreInitSceneScriptSetLaunchPanelsEnabledPostfix(PreInitSceneScript __instance)
    {
        if (__instance == null)
        {
            return;
        }

        TargetedUiTranslator.TranslatePreInit(__instance, "PreInitSceneScript.SetLaunchPanelsEnabled");
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes("PreInitSceneScript.SetLaunchPanelsEnabled.autosave");
    }

    [HarmonyPatch(typeof(QuickMenuManager), "OpenQuickMenu")]
    [HarmonyPostfix]
    private static void QuickMenuManagerOpenPostfix(QuickMenuManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateQuickMenu(__instance, "QuickMenuManager.OpenQuickMenu");
    }

    [HarmonyPatch(typeof(StartOfRound), "Start")]
    [HarmonyPrefix]
    private static void StartOfRoundStartPrefix(StartOfRound __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.Start enter inShipPhase={__instance.inShipPhase} currentLevel={LevelName(__instance)}");
    }

    [HarmonyPatch(typeof(StartOfRound), "Start")]
    [HarmonyPostfix]
    private static void StartOfRoundStartPostfix(StartOfRound __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.Start exit inShipPhase={__instance.inShipPhase} currentLevel={LevelName(__instance)}");
        // Plugin.Log.LogInfo($"Patch entry StartOfRound.Start shipHasLanded={__instance.shipHasLanded} inShipPhase={__instance.inShipPhase} currentLevel={__instance.currentLevel?.name ?? "<null>"}");
    }

    private static void StartOfRoundAutoSaveShipDataPrefix()
    {
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes("StartOfRound.AutoSaveShipData.autosave");
    }

    private static void GameNetworkManagerSaveGamePrefix()
    {
        TargetedUiTranslator.TranslateAutosaveTextInLoadedScenes("GameNetworkManager.SaveGame.autosave");
    }

    [HarmonyPatch(typeof(StartOfRound), "ChangeLevel")]
    [HarmonyPostfix]
    private static void StartOfRoundChangeLevelPostfix(StartOfRound __instance, int levelID)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"Patch entry StartOfRound.ChangeLevel levelID={levelID} currentLevel={__instance.currentLevel?.name ?? "<null>"} inShipPhase={__instance.inShipPhase}");
    }

    [HarmonyPatch(typeof(StartOfRound), "ChangePlanet")]
    [HarmonyPrefix]
    private static void StartOfRoundChangePlanetPrefix(StartOfRound __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.ChangePlanet enter currentLevel={LevelName(__instance)} currentPlanetPrefab={ObjectName(__instance.currentPlanetPrefab)} planetPrefab={ObjectName(__instance.currentLevel?.planetPrefab)} planetContainer={ObjectName(__instance.planetContainer)}");
    }

    [HarmonyPatch(typeof(StartOfRound), "ChangePlanet")]
    [HarmonyPostfix]
    private static void StartOfRoundChangePlanetPostfix(StartOfRound __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.ChangePlanet exit currentLevel={LevelName(__instance)} currentPlanetPrefab={ObjectName(__instance.currentPlanetPrefab)}");
    }

    [HarmonyPatch(typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel")]
    [HarmonyPrefix]
    private static void StartOfRoundSetMapScreenInfoPrefix(StartOfRound __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.SetMapScreenInfoToCurrentLevel enter currentLevel={LevelName(__instance)} screenText={TextInfo(__instance.screenLevelDescription)} mapScreen={ObjectName(__instance.mapScreen)}");
    }

    [HarmonyPatch(typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel")]
    [HarmonyPostfix]
    private static void StartOfRoundSetMapScreenInfoPostfix(StartOfRound __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.SetMapScreenInfoToCurrentLevel exit currentLevel={LevelName(__instance)} screenText={TextInfo(__instance.screenLevelDescription)}");
    }

    [HarmonyPatch(typeof(Terminal), "SetItemSales")]
    [HarmonyPrefix]
    private static void TerminalSetItemSalesPrefix(Terminal __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe Terminal.SetItemSales enter items={__instance.buyableItemsList?.Length ?? -1} vehicles={__instance.buyableVehicles?.Length ?? -1} sales={__instance.itemSalesPercentages?.Length ?? -1}");
    }

    [HarmonyPatch(typeof(Terminal), "SetItemSales")]
    [HarmonyPostfix]
    private static void TerminalSetItemSalesPostfix(Terminal __instance)
    {
        if (__instance == null)
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe Terminal.SetItemSales exit items={__instance.buyableItemsList?.Length ?? -1} vehicles={__instance.buyableVehicles?.Length ?? -1} sales={__instance.itemSalesPercentages?.Length ?? -1}");
    }

    [HarmonyPatch(typeof(VideoPlayer), nameof(VideoPlayer.Play))]
    [HarmonyPrefix]
    private static void VideoPlayerPlayPrefix(VideoPlayer __instance)
    {
        if (!ReferenceEquals(__instance, StartOfRound.Instance?.screenLevelVideoReel))
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe VideoPlayer.Play enter target=screenLevelVideoReel enabled={__instance.enabled} active={__instance.gameObject.activeSelf} clip={ObjectName(__instance.clip)}");
    }

    [HarmonyPatch(typeof(VideoPlayer), nameof(VideoPlayer.Play))]
    [HarmonyPostfix]
    private static void VideoPlayerPlayPostfix(VideoPlayer __instance)
    {
        if (!ReferenceEquals(__instance, StartOfRound.Instance?.screenLevelVideoReel))
        {
            return;
        }

        // Plugin.Log.LogInfo($"RoomCreateProbe VideoPlayer.Play exit target=screenLevelVideoReel isPlaying={__instance.isPlaying} clip={ObjectName(__instance.clip)}");
    }

    private static string LevelName(StartOfRound startOfRound)
    {
        var level = startOfRound.currentLevel;
        return level == null ? "<null>" : $"{level.name}/{level.PlanetName}";
    }

    private static string ObjectName(UnityEngine.Object? unityObject)
    {
        return unityObject == null ? "<null>" : unityObject.name;
    }

    private static string TextInfo(TMP_Text? text)
    {
        if (text == null)
        {
            return "<null>";
        }

        var value = text.text;
        return $"{text.name}:len={value?.Length ?? -1}:enabled={text.enabled}";
    }

    [HarmonyPatch(typeof(HUDManager), "Start")]
    [HarmonyPostfix]
    private static void HudManagerStartPostfix(HUDManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        RadiationWarningPlaybackService.ResetForHudLifecycle(__instance, "HUDManager.Start");
        AlertTextureReplacementService.ForceApplySystemOnlineOverlay(__instance, "HUDManager.Start");
        AlertTextureReplacementService.BeginSystemOnlineExactPathWatcher(__instance, "HUDManager.Start");
        AlertTextureReplacementService.SyncFixedSceneLabels(__instance, "HUDManager.Start");
        AlertTextureReplacementService.BeginFixedSceneLabelWatcher(__instance, "HUDManager.Start");
        TargetedUiTranslator.TranslateHud(__instance, "HUDManager.Start.hud");
        ApplyHudScannerLocalization(__instance, "HUDManager.Start.scanner");
        TargetedUiTranslator.TranslateHudPlanetInfo(__instance, "HUDManager.Start.planet-info");
        TargetedUiTranslator.TranslateHudChatPrompts(__instance, "HUDManager.Start.chat-prompts");
        // Plugin.Log.LogInfo($"Patch entry HUDManager.Start loadingText={__instance.loadingText?.name ?? "<null>"} riskText={__instance.planetRiskLevelText?.name ?? "<null>"}");
    }

    [HarmonyPatch(typeof(HUDManager), "UseSignalTranslatorClientRpc")]
    [HarmonyPrefix]
    private static void HudManagerUseSignalTranslatorClientRpcPrefix(HUDManager __instance)
    {
        BeginSignalTranslatorLocalizationWindow(__instance, "HUDManager.UseSignalTranslatorClientRpc.prefix");
    }

    [HarmonyPatch(typeof(HUDManager), "UseSignalTranslatorClientRpc")]
    [HarmonyPostfix]
    private static void HudManagerUseSignalTranslatorClientRpcPostfix(HUDManager __instance)
    {
        BeginSignalTranslatorLocalizationWindow(__instance, "HUDManager.UseSignalTranslatorClientRpc.postfix");
    }

    private static void BeginSignalTranslatorLocalizationWindow(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        _signalTranslatorLocalizationUntil = Math.Max(
            _signalTranslatorLocalizationUntil,
            Time.unscaledTime + SignalTranslatorLocalizationWindowSeconds);
        ApplySignalTranslatorHudLocalization(hud, reason);
        _nextSignalTranslatorLocalizationTime = Time.unscaledTime + SignalTranslatorLocalizationRetryIntervalSeconds;
    }

    private static void ApplySignalTranslatorHudLocalization(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        var translated = 0;
        var seen = 0;
        TranslateSignalTranslatorText(hud.signalTranslatorText, ref translated, ref seen);

        var root = hud.signalTranslatorAnimator == null ? null : hud.signalTranslatorAnimator.gameObject;
        if (root != null)
        {
            foreach (var text in GetSignalTranslatorTexts(root))
            {
                if (ReferenceEquals(text, hud.signalTranslatorText))
                {
                    continue;
                }

                TranslateSignalTranslatorText(text, ref translated, ref seen);
            }
        }

        if (translated > 0)
        {
            Plugin.ReportTranslationHit();
            // Plugin.Log.LogInfo($"{reason} translated signal translator HUD text: {translated}/{seen}");
        }
    }

    private static TMP_Text[] GetSignalTranslatorTexts(GameObject root)
    {
        var rootId = root.GetInstanceID();
        if (rootId == _signalTranslatorTextCacheRootId)
        {
            return SignalTranslatorTextCache;
        }

        _signalTranslatorTextCacheRootId = rootId;
        SignalTranslatorTextCache = root.GetComponentsInChildren<TMP_Text>(true);
        return SignalTranslatorTextCache;
    }

    private static void TranslateSignalTranslatorText(TMP_Text? text, ref int translated, ref int seen)
    {
        if (text == null)
        {
            return;
        }

        seen++;
        var original = text.text;
        var isReceivingSignal = IsSignalTranslatorReceivingSignalText(original);
        if (!TranslationService.TryTranslate(original, out var value) ||
            string.Equals(original, value, StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(text, original);
            ApplySignalTranslatorReceivingSignalFontSize(text, original, isReceivingSignal);
            return;
        }

        text.text = value;
        FontFallbackService.ApplyFallback(text, value);
        ApplySignalTranslatorReceivingSignalFontSize(text, value, isReceivingSignal || IsSignalTranslatorReceivingSignalText(value));
        translated++;
    }

    private static void ApplySignalTranslatorReceivingSignalFontSize(TMP_Text text, string value, bool isReceivingSignal)
    {
        var id = text.GetInstanceID();
        if (isReceivingSignal || IsSignalTranslatorReceivingSignalText(value))
        {
            if (!SignalTranslatorReceivingSignalOriginalFontSizes.TryGetValue(id, out var originalSize))
            {
                originalSize = text.fontSize;
                SignalTranslatorReceivingSignalOriginalFontSizes[id] = originalSize;
            }

            var targetSize = originalSize * SignalTranslatorReceivingSignalFontScale;
            if (text.fontSize < targetSize)
            {
                text.fontSize = targetSize;
            }

            return;
        }

        if (SignalTranslatorReceivingSignalOriginalFontSizes.TryGetValue(id, out var storedSize))
        {
            text.fontSize = storedSize;
            SignalTranslatorReceivingSignalOriginalFontSizes.Remove(id);
        }
    }

    private static bool IsSignalTranslatorReceivingSignalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeSignalTranslatorText(value);
        return string.Equals(normalized, SignalTranslatorReceivingSignalEnglish, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, SignalTranslatorReceivingSignalChinese, StringComparison.Ordinal);
    }

    private static string NormalizeSignalTranslatorText(string value)
    {
        var builder = new StringBuilder(value.Length);
        var sawWhitespace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!sawWhitespace)
                {
                    builder.Append(' ');
                    sawWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            sawWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
    [HarmonyPostfix]
    private static void HudManagerUpdateScanNodesPostfix(HUDManager __instance)
    {
        if (ShouldRetrySignalTranslatorLocalization())
        {
            ApplySignalTranslatorHudLocalization(__instance, "HUDManager.UpdateScanNodes.signal-translator-window");
        }

        ApplyHudScannerLocalization(__instance, "HUDManager.UpdateScanNodes");
    }

    private static bool ShouldRetrySignalTranslatorLocalization()
    {
        var now = Time.unscaledTime;
        if (now > _signalTranslatorLocalizationUntil)
        {
            return false;
        }

        if (Time.unscaledTime < _nextSignalTranslatorLocalizationTime)
        {
            return false;
        }

        _nextSignalTranslatorLocalizationTime = now + SignalTranslatorLocalizationRetryIntervalSeconds;
        return true;
    }

    private static void ApplyHudScannerLocalization(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        var root = hud.scanInfoAnimator == null ? hud.totalValueText?.transform.parent?.gameObject : hud.scanInfoAnimator.gameObject;
        if (!ShouldSkipHudScannerLocalization(hud, root, reason))
        {
            ApplyDirectTextTranslation(hud.totalValueText, reason);
        }

        if (ShouldTranslateHudScannerRoot(root, reason))
        {
            TargetedUiTranslator.TranslateRoot(root, reason);
        }
    }

    private static bool ShouldSkipHudScannerLocalization(HUDManager hud, GameObject? root, string reason)
    {
        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            return false;
        }

        var totalText = hud.totalValueText?.text ?? string.Empty;
        var rootId = root == null ? 0 : root.GetInstanceID();
        var changed = rootId != _lastHudScannerRootId ||
                      !string.Equals(totalText, _lastHudScannerTotalText, StringComparison.Ordinal);
        var now = Time.unscaledTime;
        if (!changed && now < _nextHudScannerLocalizationTime)
        {
            return true;
        }

        _lastHudScannerRootId = rootId;
        _lastHudScannerTotalText = totalText;
        _nextHudScannerLocalizationTime = now + HudScannerLocalizationIntervalSeconds;
        return false;
    }

    private static bool ShouldTranslateHudScannerRoot(GameObject? root, string reason)
    {
        if (root == null)
        {
            return false;
        }

        if (!string.Equals(reason, "HUDManager.UpdateScanNodes", StringComparison.Ordinal))
        {
            return true;
        }

        var rootId = root.GetInstanceID();
        var changed = rootId != _lastHudScannerTranslatedRootId;
        var now = Time.unscaledTime;
        if (!changed && now < _nextHudScannerRootLocalizationTime)
        {
            return false;
        }

        _lastHudScannerTranslatedRootId = rootId;
        _nextHudScannerRootLocalizationTime = now + HudScannerRootLocalizationIntervalSeconds;
        return true;
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayCreditsEarning")]
    [HarmonyPostfix]
    private static void HudManagerDisplayCreditsEarningPostfix(HUDManager __instance)
    {
        ApplyHudCreditsEarningLocalization(__instance, "HUDManager.DisplayCreditsEarning");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayNewDeadline")]
    [HarmonyPostfix]
    private static void HudManagerDisplayNewDeadlinePostfix(HUDManager __instance)
    {
        ApplyHudNewDeadlineLocalization(__instance, "HUDManager.DisplayNewDeadline");
    }

    [HarmonyPatch(typeof(HangarShipDoor), "Start")]
    [HarmonyPostfix]
    private static void HangarShipDoorStartPostfix(HangarShipDoor __instance)
    {
        ApplyHangarShipDoorLocalization(__instance, "HangarShipDoor.Start");
    }

    private static void ApplyHudCreditsEarningLocalization(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        ApplyDirectTextTranslation(hud.moneyRewardsTotalText, reason);
        ApplyDirectTextTranslation(hud.moneyRewardsListText, reason);
        var root = hud.moneyRewardsAnimator == null ? hud.moneyRewardsTotalText?.transform.parent?.gameObject : hud.moneyRewardsAnimator.gameObject;
        TargetedUiTranslator.TranslateRoot(root, reason);
    }

    private static void ApplyHudNewDeadlineLocalization(HUDManager? hud, string reason)
    {
        if (hud == null)
        {
            return;
        }

        var root = hud.reachedProfitQuotaAnimator == null ? null : hud.reachedProfitQuotaAnimator.gameObject;
        TargetedUiTranslator.TranslateRoot(root, reason);
        ApplyDirectTextTranslation(hud.reachedProfitQuotaBonusText, reason);
    }

    private static void ApplyHangarShipDoorLocalization(HangarShipDoor? door, string reason)
    {
        if (door == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateRoot(door.hydraulicsDisplay, reason);
        ApplyDirectTextTranslation(door.doorPowerDisplay, reason);
    }

    private static bool ApplyDirectTextTranslation(TMP_Text? text, string reason)
    {
        if (text == null)
        {
            return false;
        }

        var original = text.text;
        var translated = TranslationService.TranslateComposite(original);
        if (string.Equals(original, translated, StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(text, original);
            RuntimeTextCollector.Record(text, original);
            return false;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        FontFallbackService.ApplySystemOnlineProbeFix(text, reason, translated);
        Plugin.ReportTranslationHit();
        return true;
    }

    private static void LobbySlotSetModdedIconPostfix(LobbySlot __instance)
    {
        if (__instance == null)
        {
            return;
        }

        TargetedUiTranslator.TranslateLobbySlotStatic(__instance, "LobbySlot.SetModdedIcon");
    }

    [HarmonyPatch(typeof(HUDManager), "UpdateBoxesSpectateUI")]
    [HarmonyPostfix]
    private static void HudManagerUpdateBoxesSpectateUiPostfix(HUDManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        EndGameLocalizationService.ApplySpectateUiLocalization(__instance, "HUDManager.UpdateBoxesSpectateUI");
    }

    [HarmonyPatch(typeof(HUDManager), "FillEndGameStats")]
    [HarmonyPostfix]
    private static void HudManagerFillEndGameStatsPostfix(HUDManager __instance)
    {
        if (__instance == null)
        {
            return;
        }

        EndGameLocalizationService.ApplyHudEndGameLocalization(__instance, "HUDManager.FillEndGameStats");
    }

    [HarmonyPatch(typeof(ChallengeLeaderboardSlot), "SetSlotValues")]
    [HarmonyPostfix]
    private static void ChallengeLeaderboardSlotSetSlotValuesPostfix(ChallengeLeaderboardSlot __instance)
    {
        if (__instance == null)
        {
            return;
        }

        EndGameLocalizationService.ApplyChallengeSlotLocalization(__instance, "ChallengeLeaderboardSlot.SetSlotValues");
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayTip")]
    [HarmonyPrefix]
    private static void HudManagerDisplayTipPrefix(ref string headerText, ref string bodyText)
    {
        headerText = TargetedUiTranslator.TranslateDynamic(headerText);
        bodyText = TargetedUiTranslator.TranslateDynamic(bodyText);
    }

    [HarmonyPatch(typeof(HUDManager), "DisplayStatusEffect")]
    [HarmonyPrefix]
    private static void HudManagerDisplayStatusEffectPrefix(ref string statusEffect)
    {
        statusEffect = TargetedUiTranslator.TranslateDynamic(statusEffect);
    }

    [HarmonyPatch(typeof(HUDManager), "ChangeControlTip")]
    [HarmonyPrefix]
    private static void HudManagerChangeControlTipPrefix(ref string changeTo)
    {
        changeTo = TargetedUiTranslator.TranslateDynamic(changeTo);
    }

    [HarmonyPatch(typeof(HUDManager), "ChangeControlTipMultiple")]
    [HarmonyPrefix]
    private static void HudManagerChangeControlTipMultiplePrefix(ref string[] allLines, bool holdingItem, Item itemProperties)
    {
        TargetedUiTranslator.TranslateItem(itemProperties);
        if (allLines == null)
        {
            return;
        }

        for (var i = 0; i < allLines.Length; i++)
        {
            allLines[i] = TargetedUiTranslator.TranslateDynamic(allLines[i]);
        }
    }

    [HarmonyPatch(typeof(GrabbableObject), "Start")]
    [HarmonyPostfix]
    private static void GrabbableObjectStartPostfix(GrabbableObject __instance)
    {
        TargetedUiTranslator.TranslateItem(__instance.itemProperties);
    }

    [HarmonyPatch(typeof(GrabbableObject), "SetControlTipsForItem")]
    [HarmonyPrefix]
    private static void GrabbableObjectSetControlTipsPrefix(GrabbableObject __instance)
    {
        TargetedUiTranslator.TranslateItem(__instance.itemProperties);
    }

    [HarmonyPatch(typeof(TMP_Text), "set_text")]
    [HarmonyPrefix]
    private static void TmpSetTextPrefix(TMP_Text __instance, ref string value)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        if (ReferenceEquals(__instance, StartOfRound.Instance?.screenLevelDescription))
        {
            var start = DateTime.UtcNow;
            // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText prefix enter len={value?.Length ?? -1}");
            FontFallbackService.ApplyFallback(__instance, value);
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText after raw fallback");
            if (TranslationService.TryTranslateMapScreenDescription(value, out var monitorTranslated))
            {
                // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText after translate changed=True ms={(DateTime.UtcNow - start).TotalMilliseconds:0.0} len={monitorTranslated.Length}");
                value = monitorTranslated;
                FontFallbackService.ApplyFallback(__instance, monitorTranslated);
                // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText after translated fallback");
                Plugin.ReportTranslationHit();
            }
            else
            {
                // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText after translate changed=False ms={(DateTime.UtcNow - start).TotalMilliseconds:0.0}");
                RuntimeTextCollector.Record(__instance, value);
            }

            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.set_text.map-screen", value);
            // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText prefix exit ms={(DateTime.UtcNow - start).TotalMilliseconds:0.0} len={value?.Length ?? -1}");
            return;
        }

        EndGameLocalizationService.TryRewriteSpectateDeadValue(__instance, ref value, "TMP_Text.set_text");
        TranslateTmpText(__instance, ref value);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.set_text", value);
    }

    [HarmonyPriority(Priority.Last)]
    private static void TmpSetTextPostfix(TMP_Text __instance)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        var isMapScreenText = ReferenceEquals(__instance, StartOfRound.Instance?.screenLevelDescription);
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo($"RoomCreateProbe TmpMapScreenSetText postfix enter len={__instance.text?.Length ?? -1}");
        }

        EndGameLocalizationService.TryLocalizeSpectateDeadLabel(__instance, "TMP_Text.post_set_text");
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix after spectate-dead");
        }

        FontFallbackService.RepairPostTranslationText(__instance, "TMP_Text.post_set_text");
        ApplyBootSplashTypography(__instance, __instance.text);
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix after font-repair");
        }

        SyncRelayTitleHooks(__instance, "TMP_Text.post_set_text");
        if (isMapScreenText)
        {
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix after relay-sync");
            // Plugin.Log.LogInfo("RoomCreateProbe TmpMapScreenSetText postfix exit");
            return;
        }

        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "TMP_Text.post_set_text");
    }

    [HarmonyPatch(typeof(TMP_Text), "set_color")]
    [HarmonyPrefix]
    private static void TmpSetColorPrefix(TMP_Text __instance, ref Color value)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        FontFallbackService.SanitizeAssignedColor(__instance, ref value, __instance.text);
        FontFallbackService.SanitizeSystemOnlineAssignedColor(__instance, ref value, "TMP_Text.set_color");
    }

    [HarmonyPriority(Priority.Last)]
    private static void TmpSetColorPostfix(TMP_Text __instance)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        SyncRelayTitleHooks(__instance, "TMP_Text.post_set_color");
        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "TMP_Text.post_set_color");
    }

    [HarmonyPatch(typeof(TMP_FontAsset), "Awake")]
    [HarmonyPostfix]
    private static void TmpFontAssetAwakePostfix(TMP_FontAsset __instance)
    {
        FontFallbackService.OnFontAssetAwake(__instance);
    }

    private static void TmpSubMeshUiUpdateMaterialPostfix(TMP_SubMeshUI __instance)
    {
        return;
    }

    private static void TmpSubMeshUpdateMaterialPostfix(TMP_SubMesh __instance)
    {
        return;
    }

    private static void AnimatorSetTriggerPostfix(Animator __instance, string name)
    {
        if (__instance == null || HUDManager.Instance == null)
        {
            return;
        }

        if (name == "RadiationWarning" && ReferenceEquals(__instance, HUDManager.Instance.radiationGraphicAnimator))
        {
            RadiationWarningAuditService.OnRadiationWarningTriggered(HUDManager.Instance, "Animator.SetTrigger.RadiationWarning");
            RadiationWarningPlaybackService.OnRadiationWarningTriggered(HUDManager.Instance, "Animator.SetTrigger.RadiationWarning");
        }
    }

    private static void AnimatorSetBoolPrefix(Animator __instance, string name, bool value)
    {
        if (__instance == null || HUDManager.Instance == null)
        {
            return;
        }

        if (name != "IsLoading" || !ReferenceEquals(__instance, HUDManager.Instance.LoadingScreen))
        {
            return;
        }

        if (value)
        {
            AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(HUDManager.Instance, "Animator.SetBool.IsLoading.prefix.true");
        }
    }

    private static void AnimatorSetBoolPostfix(Animator __instance, string name, bool value)
    {
        if (__instance == null || HUDManager.Instance == null)
        {
            return;
        }

        if (name == "transmitting" && ReferenceEquals(__instance, HUDManager.Instance.signalTranslatorAnimator))
        {
            if (value)
            {
                BeginSignalTranslatorLocalizationWindow(HUDManager.Instance, "Animator.SetBool.transmitting.true");
            }
            else
            {
                _signalTranslatorLocalizationUntil = 0f;
            }
        }

        if (name != "IsLoading" || !ReferenceEquals(__instance, HUDManager.Instance.LoadingScreen))
        {
            return;
        }

        if (value)
        {
            AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(HUDManager.Instance, "Animator.SetBool.IsLoading.postfix.true");
        }
        else
        {
            AlertTextureReplacementService.HideEnteringAtmosphereOverlayForHud(HUDManager.Instance, "Animator.SetBool.IsLoading.false");
        }
    }

    private static bool TranslateTmpText(TMP_Text __instance, ref string value)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return false;
        }

        if (IsLobbySlotDynamicText(__instance))
        {
            FontFallbackService.ApplyFallback(__instance, value);
            return false;
        }

        FontFallbackService.ApplyFallback(__instance, value);
        ApplyBootSplashTypography(__instance, value);
        if (TranslationService.TryTranslate(value, out var translated))
        {
            value = translated;
            FontFallbackService.ApplyFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            Plugin.ReportTranslationHit();
            return true;
        }

        RuntimeTextCollector.Record(__instance, value);
        return false;
    }

    [HarmonyPatch(typeof(Text), "set_text")]
    [HarmonyPrefix]
    private static void UiTextSetTextPrefix(Text __instance, ref string value)
    {
        TranslateUiText(__instance, ref value);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "UI.Text.set_text", value);
    }

    [HarmonyPriority(Priority.Last)]
    private static void UiTextSetTextPostfix(Text __instance)
    {
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "UI.Text.post_set_text");
        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "UI.Text.post_set_text");
    }

    private static bool TranslateUiText(Text __instance, ref string value)
    {
        if (TranslationService.TryTranslate(value, out var translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
            return true;
        }

        RuntimeTextCollector.Record(__instance, value);
        return false;
    }

    [HarmonyPatch(typeof(TextMesh), "set_text")]
    [HarmonyPrefix]
    private static void TextMeshSetTextPrefix(TextMesh __instance, ref string value)
    {
        if (TranslationService.TryTranslate(value, out var translated))
        {
            value = translated;
            Plugin.ReportTranslationHit();
        }
        else
        {
            RuntimeTextCollector.Record(__instance, value);
        }

        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TextMesh.set_text", value);
    }

    [HarmonyPriority(Priority.Last)]
    private static void TextMeshSetTextPostfix(TextMesh __instance)
    {
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TextMesh.post_set_text");
        AlertTextureReplacementService.TryReplaceSystemOnlineText(__instance, "TextMesh.post_set_text");
    }

    [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
    [HarmonyPostfix]
    private static void TerminalTextPostProcessPostfix(ref string __result)
    {
        var translated = TranslationService.TranslateTerminalOutput(__result);
        if (translated != __result)
        {
            __result = translated;
            Plugin.ReportTranslationHit();
        }
    }

    [HarmonyPatch(typeof(Terminal), "LoadNewNode")]
    [HarmonyPostfix]
    private static void TerminalLoadNewNodePostfix(Terminal __instance)
    {
        Plugin.LogPatchEntry("Terminal.LoadNewNode");
        ApplyTerminalCurrentTextTranslation(__instance, "Terminal.LoadNewNode.currentText");
        ApplyTerminalScreenTranslation(__instance, "Terminal.LoadNewNode");
    }

    [HarmonyPatch(typeof(Terminal), "OnSubmit")]
    [HarmonyPostfix]
    private static void TerminalOnSubmitPostfix(Terminal __instance)
    {
        ApplyTerminalCurrentTextTranslation(__instance, "Terminal.OnSubmit");
        ApplyTerminalScreenTranslation(__instance, "Terminal.OnSubmit");
    }

    [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
    [HarmonyPostfix]
    private static void TerminalParsePlayerSentencePostfix(Terminal __instance)
    {
        ApplyTerminalCurrentTextTranslation(__instance, "Terminal.ParsePlayerSentence");
    }

    [HarmonyPatch(typeof(Terminal), "loadTextAnimation")]
    [HarmonyPostfix]
    private static void TerminalLoadTextAnimationPostfix(Terminal __instance)
    {
        ApplyTerminalCurrentTextTranslation(__instance, "Terminal.loadTextAnimation");
    }

    [HarmonyPatch(typeof(Terminal), "BeginUsingTerminal")]
    [HarmonyPostfix]
    private static void TerminalBeginUsingPostfix(Terminal __instance)
    {
        ApplyTerminalScreenTranslation(__instance, "Terminal.BeginUsingTerminal");
    }

    private static void ApplyTerminalCurrentTextTranslation(Terminal? terminal, string reason)
    {
        if (terminal == null || string.IsNullOrEmpty(terminal.currentText))
        {
            return;
        }

        var original = terminal.currentText;
        var translated = TranslationService.TranslateTerminalOutput(original);
        if (string.Equals(original, translated, StringComparison.Ordinal))
        {
            return;
        }

        terminal.currentText = translated;
        terminal.textAdded = 0;

        if (terminal.screenText != null &&
            string.Equals(terminal.screenText.text, original, StringComparison.Ordinal))
        {
            SetTerminalModifyingText(terminal, true);
            terminal.screenText.text = translated;
            terminal.screenText.caretPosition = translated.Length;
            FontFallbackService.ApplyFallback(terminal.screenText.textComponent, translated);
        }

        Plugin.ReportTranslationHit();
        // Plugin.Log.LogInfo($"{reason} translated terminal currentText: {original.Length}->{translated.Length}");
    }

    private static void ApplyTerminalScreenTranslation(Terminal? terminal, string reason)
    {
        if (terminal?.screenText == null)
        {
            return;
        }

        var original = terminal.screenText.text;
        var translated = TranslationService.TranslateTerminalOutput(original);
        if (string.Equals(original, translated, StringComparison.Ordinal))
        {
            FontFallbackService.ApplyFallback(terminal.screenText.textComponent, original);
            return;
        }

        terminal.currentText = translated;
        terminal.textAdded = 0;
        SetTerminalModifyingText(terminal, true);
        terminal.screenText.text = translated;
        terminal.currentText = translated;
        terminal.textAdded = 0;
        terminal.screenText.caretPosition = translated.Length;
        FontFallbackService.ApplyFallback(terminal.screenText.textComponent, translated);
        Plugin.ReportTranslationHit();
        // Plugin.Log.LogInfo($"{reason} translated terminal screen text: {original?.Length ?? 0}->{translated.Length}");
    }

    private static void SetTerminalModifyingText(Terminal terminal, bool value)
    {
        try
        {
            TerminalModifyingTextField?.SetValue(terminal, value);
        }
        catch
        {
            // Keep terminal input usable even if a future game build changes the private field.
        }
    }

    private static bool IsInputFieldTextComponent(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        if (TryGetCachedTextClassification(text, InputFieldTextCache, out var cached))
        {
            return cached;
        }

        var inputField = text.GetComponentInParent<TMP_InputField>(true);
        if (inputField == null)
        {
            CacheTextClassification(text, InputFieldTextCache, false);
            return false;
        }

        var result = ReferenceEquals(inputField.textComponent, text);
        CacheTextClassification(text, InputFieldTextCache, result);
        return result;
    }

    private static bool IsLobbySlotDynamicText(TMP_Text? text)
    {
        if (text == null)
        {
            return false;
        }

        if (TryGetCachedTextClassification(text, LobbySlotTextCache, out var cached))
        {
            return cached;
        }

        var slot = text.GetComponentInParent<LobbySlot>(true);
        var result = slot != null && (ReferenceEquals(slot.LobbyName, text) || ReferenceEquals(slot.playerCount, text));
        CacheTextClassification(text, LobbySlotTextCache, result);
        return result;
    }

    private static bool TryGetCachedTextClassification(
        TMP_Text text,
        Dictionary<int, CachedTextClassification> cache,
        out bool value)
    {
        var parentId = GetParentInstanceId(text);
        if (cache.TryGetValue(text.GetInstanceID(), out var cached) && cached.ParentId == parentId)
        {
            value = cached.Value;
            return true;
        }

        value = false;
        return false;
    }

    private static void CacheTextClassification(TMP_Text text, Dictionary<int, CachedTextClassification> cache, bool value)
    {
        if (cache.Count >= TextClassificationCacheLimit)
        {
            cache.Clear();
        }

        cache[text.GetInstanceID()] = new CachedTextClassification(GetParentInstanceId(text), value);
    }

    private static int GetParentInstanceId(TMP_Text text)
    {
        return text.transform.parent == null ? 0 : text.transform.parent.GetInstanceID();
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(bool))]
    [HarmonyPrefix]
    private static void TmpSetTextStringBoolPrefix(TMP_Text __instance, ref string sourceText)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        TmpSetTextPrefix(__instance, ref sourceText);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(string,bool)", sourceText);
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(string), typeof(float))]
    [HarmonyPrefix]
    private static void TmpSetTextStringFloatPrefix(TMP_Text __instance, ref string sourceText)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        TmpSetTextPrefix(__instance, ref sourceText);
        FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(string,float)", sourceText);
    }

    [HarmonyPatch(typeof(TMP_Text), nameof(TMP_Text.SetText), typeof(StringBuilder))]
    [HarmonyPrefix]
    private static void TmpSetTextStringBuilderPrefix(TMP_Text __instance, ref StringBuilder sourceText)
    {
        if (IsInputFieldTextComponent(__instance))
        {
            return;
        }

        if (sourceText == null)
        {
            return;
        }

        var rawText = sourceText.ToString();
        if (IsLobbySlotDynamicText(__instance))
        {
            FontFallbackService.ApplyFallback(__instance, rawText);
            return;
        }

        FontFallbackService.ApplyFallback(__instance, rawText);
        ApplyBootSplashTypography(__instance, rawText);
        if (TranslationService.TryTranslate(rawText, out var translated))
        {
            sourceText = new StringBuilder(translated);
            FontFallbackService.ApplyFallback(__instance, translated);
            ApplyBootSplashTypography(__instance, translated);
            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(StringBuilder)", translated);
            Plugin.ReportTranslationHit();
        }
        else
        {
            RuntimeTextCollector.Record(__instance, rawText);
            FontFallbackService.ApplySystemOnlineProbeFix(__instance, "TMP_Text.SetText(StringBuilder)", rawText);
        }
    }

    private static void ApplyBootSplashTypography(TMP_Text? text, string? value)
    {
        if (text == null || !TranslationService.IsBootSplashText(value))
        {
            return;
        }

        text.richText = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
    }

    private static void SyncRelayTitleHooks(TMP_Text text, string stage)
    {
        if (text == null || HUDManager.Instance == null)
        {
            return;
        }

        AlertTextureReplacementService.SyncEnteringAtmosphereOverlayState(text, stage);
    }

}
