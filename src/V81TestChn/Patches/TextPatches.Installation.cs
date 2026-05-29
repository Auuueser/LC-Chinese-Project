using HarmonyLib;
using GameNetcodeStuff;
using System;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static int InstallTextPatches(Harmony harmony)
    {
        var patched = 0;

        PatchPostfix(harmony, typeof(MenuManager), "OnEnable", nameof(MenuManagerOnEnablePostfix), ref patched);
        PatchPostfix(harmony, typeof(MenuManager), "EnableUIPanel", nameof(MenuManagerEnableUIPanelPostfix), ref patched);
        PatchPrefix(harmony, typeof(MenuManager), "DisplayMenuNotification", nameof(MenuManagerDisplayMenuNotificationPrefix), ref patched);
        PatchPostfix(harmony, typeof(DeleteFileButton), "SetFileToDelete", nameof(DeleteFileButtonSetFileToDeletePostfix), ref patched);
        PatchPostfix(harmony, typeof(SaveFileUISlot), "OnEnable", nameof(SaveFileUISlotOnEnablePostfix), ref patched);

        PatchPostfix(harmony, typeof(PreInitSceneScript), "Start", nameof(PreInitSceneScriptStartPostfix), ref patched);
        PatchPostfix(harmony, typeof(PreInitSceneScript), "SetLaunchPanelsEnabled", nameof(PreInitSceneScriptSetLaunchPanelsEnabledPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "OpenQuickMenu", nameof(QuickMenuManagerOpenPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "EnableUIPanel", nameof(QuickMenuManagerEnableUIPanelPostfix), ref patched);
        PatchPostfix(harmony, typeof(QuickMenuManager), "LeaveGame", nameof(QuickMenuManagerLeaveGamePostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "Start", nameof(StartOfRoundStartPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "Start", nameof(StartOfRoundStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "AutoSaveShipData", nameof(StartOfRoundAutoSaveShipDataPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "ChangeLevel", nameof(StartOfRoundChangeLevelPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "ChangePlanet", nameof(StartOfRoundChangePlanetPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "ChangePlanet", nameof(StartOfRoundChangePlanetPostfix), ref patched);
        PatchPrefix(harmony, typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel", nameof(StartOfRoundSetMapScreenInfoPrefix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "SetMapScreenInfoToCurrentLevel", nameof(StartOfRoundSetMapScreenInfoPostfix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "SwitchMapMonitorPurpose", nameof(StartOfRoundSwitchMapMonitorPurposePostfix), ref patched);
        PatchPostfix(harmony, typeof(StartOfRound), "FirePlayersAfterDeadlineClientRpc", nameof(StartOfRoundFirePlayersAfterDeadlineClientRpcPostfix), ref patched);
        PatchPrefix(harmony, typeof(GameNetworkManager), "SaveGame", nameof(GameNetworkManagerSaveGamePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "Start", nameof(HudManagerStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "UseSignalTranslatorClientRpc", nameof(HudManagerUseSignalTranslatorClientRpcPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UseSignalTranslatorClientRpc", nameof(HudManagerUseSignalTranslatorClientRpcPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UpdateScanNodes", nameof(HudManagerUpdateScanNodesPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayCreditsEarning", nameof(HudManagerDisplayCreditsEarningPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayNewScrapFound", nameof(HudManagerDisplayNewScrapFoundPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayNewDeadline", nameof(HudManagerDisplayNewDeadlinePostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "DisplayDaysLeft", nameof(HudManagerDisplayDaysLeftPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "SetShipLeaveEarlyVotesText", nameof(HudManagerSetShipLeaveEarlyVotesTextPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ReadDialogue", nameof(HudManagerReadDialoguePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ReadDialogue", nameof(HudManagerReadDialoguePostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "UpdateBoxesSpectateUI", nameof(HudManagerUpdateBoxesSpectateUiPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "SetSpectatingTextToPlayer", nameof(HudManagerSetSpectatingTextToPlayerPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "FillEndGameStats", nameof(HudManagerFillEndGameStatsPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ApplyPenalty", nameof(HudManagerApplyPenaltyPostfix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ShowPlayersFiredScreen", nameof(HudManagerShowPlayersFiredScreenPostfix), ref patched);
        PatchPostfix(
            harmony,
            AccessTools.Method(typeof(HUDManager), "AddChatMessage", new[] { typeof(string), typeof(string), typeof(int), typeof(bool) }),
            nameof(HudManagerAddChatMessagePostfix),
            ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "AddTextToChatOnServer", nameof(HudManagerAddTextToChatOnServerPostfix), ref patched);
        PatchPostfix(harmony, typeof(ChallengeLeaderboardSlot), "SetSlotValues", nameof(ChallengeLeaderboardSlotSetSlotValuesPostfix), ref patched);
        PatchPostfix(harmony, typeof(LobbySlot), "SetModdedIcon", nameof(LobbySlotSetModdedIconPostfix), ref patched);
        PatchPostfix(harmony, typeof(HangarShipDoor), "Start", nameof(HangarShipDoorStartPostfix), ref patched);

        PatchPrefix(harmony, typeof(HUDManager), "DisplayTip", nameof(HudManagerDisplayTipPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "DisplayStatusEffect", nameof(HudManagerDisplayStatusEffectPrefix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ChangeControlTip", nameof(HudManagerChangeControlTipPrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ChangeControlTip", nameof(HudManagerChangeControlTipPostfix), ref patched);
        PatchPrefix(harmony, typeof(HUDManager), "ChangeControlTipMultiple", nameof(HudManagerChangeControlTipMultiplePrefix), ref patched);
        PatchPostfix(harmony, typeof(HUDManager), "ChangeControlTipMultiple", nameof(HudManagerChangeControlTipMultiplePostfix), ref patched);

        PatchPostfix(harmony, typeof(GrabbableObject), "Start", nameof(GrabbableObjectStartPostfix), ref patched);
        PatchPrefix(harmony, typeof(GrabbableObject), "SetControlTipsForItem", nameof(GrabbableObjectSetControlTipsPrefix), ref patched);
        PatchPostfix(harmony, typeof(GrabbableObject), "SetControlTipsForItem", nameof(GrabbableObjectSetControlTipsPostfix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(StunGrenadeItem), "SetControlTipForGrenade"), nameof(StunGrenadeItemSetControlTipForGrenadePostfix), ref patched);
        PatchPostfix(harmony, AccessTools.Method(typeof(PlayerControllerB), "SetHoverTipAndCurrentInteractTrigger"), nameof(PlayerControllerBSetHoverTipAndCurrentInteractTriggerPostfix), ref patched);
        PatchPostfix(harmony, typeof(VehicleController), "Start", nameof(VehicleControllerStartPostfix), ref patched);
        PatchPostfix(harmony, typeof(RoundManager), "GenerateNewLevelClientRpc", nameof(RoundManagerGenerateNewLevelClientRpcPostfix), ref patched);

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
        PatchPostfix(harmony, typeof(Terminal), "PlayBroadcastCodeEffect", nameof(TerminalPlayBroadcastCodeEffectPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "loadTextAnimation", nameof(TerminalLoadTextAnimationPostfix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "BeginUsingTerminal", nameof(TerminalBeginUsingPostfix), ref patched);
        PatchPrefix(harmony, typeof(Terminal), "SetItemSales", nameof(TerminalSetItemSalesPrefix), ref patched);
        PatchPostfix(harmony, typeof(Terminal), "SetItemSales", nameof(TerminalSetItemSalesPostfix), ref patched);
        PatchPrefix(harmony, typeof(VideoPlayer), nameof(VideoPlayer.Play), nameof(VideoPlayerPlayPrefix), ref patched);
        PatchPostfix(harmony, typeof(VideoPlayer), nameof(VideoPlayer.Play), nameof(VideoPlayerPlayPostfix), ref patched);

        // Cover whole text-source SetText overloads only. Numeric formatting, char[] buffer,
        // and range-based StringBuilder overloads stay on the global fast path to avoid dynamic counters and input slices.
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
}
