using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using TMPro;
using UnityEngine;

namespace V81TestChn;

internal static partial class TextPatches
{
    private const string AdvancedFeaturesGradeLabelLocalized = "\u8bc4\u7ea7";
    private const string TooManyEmotesSyncTipEnglish = "[E] Sync emote";
    private const string TooManyEmotesSyncTipLocalized = "[E] \u540c\u6b65\u52a8\u4f5c";
    private static readonly WaitForSeconds AdvancedFeaturesGradeRepairFirstDelay = new(2.35f);
    private static readonly WaitForSeconds AdvancedFeaturesGradeRepairSecondDelay = new(1.25f);
    private static readonly List<TMP_Text> AdvancedFeaturesTmpBuffer = new(64);
    private static readonly HashSet<int> AdvancedFeaturesGradeTextIds = new();

    private static void LethalConfigConfigMenuOpenPostfix(object __instance)
    {
        if (__instance is Component component)
        {
            ExternalEnglishCompatibilityUiService.TranslateRoot(component.gameObject, includeInactive: true, "LethalConfig.ConfigMenu.Open");
        }
    }

    private static void LethalConfigNotificationSetContentPrefix(ref string text, ref string button)
    {
        if (ExternalEnglishCompatibilityService.TryTranslateFast(text, out var translatedText))
        {
            text = translatedText;
        }

        if (ExternalEnglishCompatibilityService.TryTranslateFast(button, out var translatedButton))
        {
            button = translatedButton;
        }
    }

    private static void LethalConfigNotificationOpenPostfix(object __instance)
    {
        if (__instance is Component component)
        {
            ExternalEnglishCompatibilityUiService.TranslateRoot(component.gameObject, includeInactive: true, "LethalConfig.ConfigMenuNotification.Open");
        }
    }

    private static void OpenBodyCamsOverlayUpdateTextPostfix(TMP_Text ___textRenderer)
    {
        ExternalEnglishCompatibilityUiService.TranslateTmpTextKnownNonInput(___textRenderer, "OpenBodyCams.OverlayManager.UpdateText");
    }

    private static void TranslateTooManyEmotesMenu(HUDManager hud)
    {
        var menuRoot = hud.HUDContainer?.transform.parent?.Find("EmotesRadialMenu");
        if (menuRoot != null)
        {
            ExternalEnglishCompatibilityUiService.TranslateRoot(
                menuRoot.gameObject,
                includeInactive: true,
                "HUDManager.Start.TooManyEmotesMenu");
        }
    }

    private static void TooManyEmotesPlayerLateUpdatePrefix(PlayerControllerB __instance)
    {
        if (Plugin.IsRuntimeShuttingDown || __instance == null || __instance != GameNetworkManager.Instance?.localPlayerController)
        {
            return;
        }

        var cursorTip = __instance.cursorTip;
        if (cursorTip == null || !string.Equals(cursorTip.text, TooManyEmotesSyncTipLocalized, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            _restoringLateWriterCursorTipSource = true;
            cursorTip.text = TooManyEmotesSyncTipEnglish;
        }
        finally
        {
            _restoringLateWriterCursorTipSource = false;
        }
    }

    private static void TooManyEmotesPlayerLateUpdatePostfix(PlayerControllerB __instance)
    {
        if (Plugin.IsRuntimeShuttingDown || __instance == null || __instance != GameNetworkManager.Instance?.localPlayerController)
        {
            return;
        }

        var cursorTip = __instance.cursorTip;
        if (cursorTip == null || !string.Equals(cursorTip.text, TooManyEmotesSyncTipEnglish, StringComparison.Ordinal))
        {
            return;
        }

        HudInteractionLocalizationService.ApplyPlayerCursorTip(__instance, "TooManyEmotes.PlayerControllerB.LateUpdate");
    }

    private static void SteamworksShowGamepadTextInputPrefix(ref string description)
    {
        if (string.Equals(description, "Type command", StringComparison.Ordinal))
        {
            description = "\u8f93\u5165\u547d\u4ee4";
        }
    }

    private static void TmpInputFieldOnEnablePostfix(TMP_InputField __instance)
    {
        if (Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        ExternalEnglishCompatibilityUiService.TranslateTmpInputPlaceholder(__instance, "TMP_InputField.OnEnable");
    }

    private static void BetterSavesInitializeBetterSavesPostfix()
    {
        var filesPanel = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel");
        ExternalEnglishCompatibilityUiService.TranslateRoot(filesPanel, includeInactive: true, "BetterSaves.InitializeBetterSaves");
    }

    private static void BetterSavesDeleteFileButtonUpdateFileToDeletePostfix(int ___fileToDelete, TMP_Text ___deleteFileText)
    {
        if (___fileToDelete <= 0 || ___deleteFileText == null)
        {
            return;
        }

        if (ExternalEnglishCompatibilityService.TryTranslateBetterSavesDeleteFilePrompt(___deleteFileText.text, ___fileToDelete, out var translated))
        {
            ApplyTranslatedTmpText(___deleteFileText, translated, "BetterSaves.DeleteFileButton.UpdateFileToDelete");
        }
        else
        {
            ExternalEnglishCompatibilityUiService.TranslateTmpText(___deleteFileText, "BetterSaves.DeleteFileButton.UpdateFileToDelete");
        }

        var confirmationRoot = GameObject.Find("Canvas/MenuContainer/DeleteFileConfirmation");
        ExternalEnglishCompatibilityUiService.TranslateRoot(confirmationRoot, includeInactive: true, "BetterSaves.DeleteFileConfirmation");
    }

    private static void AdvancedFeaturesEndscreenOpenPostfix(GameObject ___Container)
    {
        ExternalEnglishCompatibilityUiService.TranslateRoot(___Container, includeInactive: true, "AdvancedFeatures.Endscreen.Open");
        RegisterAdvancedFeaturesGradeText(TryGetAdvancedFeaturesGradeTextByPrefabPath(___Container));
        StartAdvancedFeaturesGradeRepair(___Container);
    }

    private static void ApplyTranslatedTmpText(TMP_Text text, string translated, string reason)
    {
        if (string.Equals(text.text, translated, System.StringComparison.Ordinal))
        {
            return;
        }

        text.text = translated;
        FontFallbackService.ApplyFallback(text, translated);
        Plugin.ReportTranslationHit();
    }

    private static void StartAdvancedFeaturesGradeRepair(GameObject? container)
    {
        var hud = HUDManager.Instance;
        if (container == null || hud == null)
        {
            return;
        }

        hud.StartCoroutine(RepairAdvancedFeaturesGradeTextDeferred(container));
    }

    private static IEnumerator RepairAdvancedFeaturesGradeTextDeferred(GameObject container)
    {
        yield return AdvancedFeaturesGradeRepairFirstDelay;
        RepairAdvancedFeaturesGradeText(container, "AdvancedFeatures.Endscreen.Open.delayed-grade-1");
        yield return AdvancedFeaturesGradeRepairSecondDelay;
        RepairAdvancedFeaturesGradeText(container, "AdvancedFeatures.Endscreen.Open.delayed-grade-2");
    }

    private static void RepairAdvancedFeaturesGradeText(GameObject? container, string reason)
    {
        if (container == null)
        {
            return;
        }

        var grade = ResolveCurrentEndgameGradeLetter();
        if (grade.Length == 0)
        {
            return;
        }

        var directGradeText = TryGetAdvancedFeaturesGradeTextByPrefabPath(container);
        RegisterAdvancedFeaturesGradeText(directGradeText);
        if (directGradeText != null)
        {
            if (TryBuildAdvancedFeaturesDirectGradeValueText(directGradeText.text, grade, out var repairedDirectValue))
            {
                PrepareAdvancedFeaturesGradeTextForDisplay(directGradeText);
                directGradeText.text = repairedDirectValue;
                Plugin.ReportTranslationHit();
                Plugin.LogTargetedTranslation(reason + ".direct-grade", 1, 1);
                return;
            }

            if (TryNormalizeVanillaEndgameGradeLetter(directGradeText.text, out var existingGrade) &&
                string.Equals(existingGrade, grade, StringComparison.Ordinal))
            {
                PrepareAdvancedFeaturesGradeTextForDisplay(directGradeText);
                return;
            }
        }

        try
        {
            container.GetComponentsInChildren(includeInactive: true, AdvancedFeaturesTmpBuffer);
            foreach (var text in AdvancedFeaturesTmpBuffer)
            {
                if (text == null ||
                    !TryFindNearbyAdvancedFeaturesGradeLabel(text, AdvancedFeaturesTmpBuffer, out var nearbyLabel) ||
                    !TryBuildAdvancedFeaturesGradeValueText(text.text, nearbyLabel, grade, out var repairedValue))
                {
                    continue;
                }

                text.text = repairedValue;
                FontFallbackService.ApplyFallback(text, repairedValue);
                Plugin.ReportTranslationHit();
                Plugin.LogTargetedTranslation(reason, 1, AdvancedFeaturesTmpBuffer.Count);
                return;
            }

            foreach (var text in AdvancedFeaturesTmpBuffer)
            {
                if (text == null ||
                    !TryBuildAdvancedFeaturesGradeDisplayText(text.text, grade, out var repairedDisplay))
                {
                    continue;
                }

                text.text = repairedDisplay;
                FontFallbackService.ApplyFallback(text, repairedDisplay);
                Plugin.ReportTranslationHit();
                Plugin.LogTargetedTranslation(reason, 1, AdvancedFeaturesTmpBuffer.Count);
                return;
            }
        }
        finally
        {
            AdvancedFeaturesTmpBuffer.Clear();
        }
    }

    private static TMP_Text? TryGetAdvancedFeaturesGradeTextByPrefabPath(GameObject container)
    {
        var root = container.transform;
        if (root.childCount <= 2)
        {
            return null;
        }

        var bottom = root.GetChild(2);
        if (bottom.childCount <= 1)
        {
            return null;
        }

        var gradeRow = bottom.GetChild(1);
        if (gradeRow.childCount <= 1)
        {
            return null;
        }

        return gradeRow.GetChild(1).GetComponent<TMP_Text>();
    }

    private static void RegisterAdvancedFeaturesGradeText(TMP_Text? text)
    {
        if (text == null)
        {
            return;
        }

        AdvancedFeaturesGradeTextIds.Add(text.GetInstanceID());
    }

    private static bool TryNormalizeAdvancedFeaturesGradeTextValue(TMP_Text? text, string? value, out string normalized)
    {
        normalized = string.Empty;
        if (text == null || !AdvancedFeaturesGradeTextIds.Contains(text.GetInstanceID()))
        {
            return false;
        }

        if (!TryNormalizeVanillaEndgameGradeLetter(value, out normalized))
        {
            return false;
        }

        if (LooksLikeAdvancedFeaturesGradeText(text))
        {
            return true;
        }

        AdvancedFeaturesGradeTextIds.Remove(text.GetInstanceID());
        return false;
    }

    private static bool LooksLikeAdvancedFeaturesGradeText(TMP_Text text)
    {
        var transform = text.transform;
        return string.Equals(text.name, "Grade", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(transform.parent?.name, "Grade", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(transform.parent?.parent?.name, "Bottom", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrepareAdvancedFeaturesGradeTextForDisplay(TMP_Text text)
    {
        text.gameObject.SetActive(true);
        text.enabled = true;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        if (text.color.a < 0.9f)
        {
            var color = text.color;
            color.a = 1f;
            text.color = color;
        }
    }

    private static string ResolveCurrentEndgameGradeLetter()
    {
        if (TryNormalizeVanillaEndgameGradeLetter(HUDManager.Instance?.statsUIElements?.gradeLetter?.text, out var grade))
        {
            return grade;
        }

        return TryResolveCurrentEndgameGradeLetterFromRoundState(out var calculatedGrade) ? calculatedGrade : string.Empty;
    }

    private static bool TryResolveCurrentEndgameGradeLetterFromRoundState(out string grade)
    {
        grade = string.Empty;

        var roundManager = RoundManager.Instance;
        var startOfRound = StartOfRound.Instance;
        var playersManager = HUDManager.Instance?.playersManager ?? startOfRound;
        var playerScripts = playersManager?.allPlayerScripts;
        if (roundManager == null || startOfRound == null || playerScripts == null)
        {
            return false;
        }

        var playersDead = 0;
        var playersControlled = 0;
        for (var i = 0; i < playerScripts.Length; i++)
        {
            var player = playerScripts[i];
            if (player == null || (!player.disconnectedMidGame && !player.isPlayerDead && !player.isPlayerControlled))
            {
                continue;
            }

            if (player.isPlayerDead)
            {
                playersDead++;
            }
            else if (player.isPlayerControlled)
            {
                playersControlled++;
            }
        }

        return TryBuildAdvancedFeaturesGradeLetterFromRoundState(
            startOfRound.allPlayersDead,
            playersControlled,
            startOfRound.connectedPlayersAmount,
            playersDead,
            roundManager.scrapCollectedInLevel,
            roundManager.totalScrapValueInLevel,
            out grade);
    }

    private static bool TryBuildAdvancedFeaturesGradeLetterFromRoundState(
        bool allPlayersDead,
        int playersControlled,
        int connectedPlayersAmount,
        int playersDead,
        int scrapCollected,
        float totalScrap,
        out string grade)
    {
        grade = string.Empty;
        if (allPlayersDead)
        {
            grade = "F";
            return true;
        }

        var score = 0;
        if (playersControlled == connectedPlayersAmount + 1)
        {
            score++;
        }
        else if (playersDead > 1)
        {
            score--;
        }

        if (totalScrap > 0f)
        {
            var scrapRatio = scrapCollected / totalScrap;
            if (scrapRatio >= 0.99f)
            {
                score += 2;
            }
            else if (scrapRatio >= 0.6f)
            {
                score++;
            }
            else if (scrapRatio <= 0.25f)
            {
                score--;
            }
        }

        switch (score)
        {
            case -1:
                grade = "D";
                return true;
            case 0:
                grade = "C";
                return true;
            case 1:
                grade = "B";
                return true;
            case 2:
                grade = "A";
                return true;
            case 3:
                grade = "S";
                return true;
            default:
                return false;
        }
    }

    private static bool TryFindNearbyAdvancedFeaturesGradeLabel(TMP_Text candidate, List<TMP_Text> texts, out string label)
    {
        label = string.Empty;

        var candidateTransform = candidate.transform;
        var parent = candidateTransform.parent;
        var grandParent = parent?.parent;
        for (var i = 0; i < texts.Count; i++)
        {
            var other = texts[i];
            if (other == null || ReferenceEquals(other, candidate) || !IsAdvancedFeaturesGradeLabelText(other.text))
            {
                continue;
            }

            var otherParent = other.transform.parent;
            var otherGrandParent = otherParent?.parent;
            if (otherParent != parent &&
                otherParent != grandParent &&
                otherGrandParent != parent &&
                otherGrandParent != grandParent)
            {
                continue;
            }

            label = other.text;
            return true;
        }

        return false;
    }

    private static bool TryBuildAdvancedFeaturesGradeDisplayText(string? current, string? grade, out string repaired)
    {
        repaired = current ?? string.Empty;
        if (!TryNormalizeVanillaEndgameGradeLetter(grade, out var gradeValue))
        {
            return false;
        }

        var text = current?.Trim();
        if (string.IsNullOrEmpty(text) || IsVanillaEndgameGradeLetter(text) || !IsAdvancedFeaturesGradeLabelText(text))
        {
            return false;
        }

        repaired = AdvancedFeaturesGradeLabelLocalized + " " + gradeValue;
        return true;
    }

    private static bool TryBuildAdvancedFeaturesGradeValueText(string? current, string? nearbyLabel, string? grade, out string repaired)
    {
        repaired = current ?? string.Empty;
        if (!TryNormalizeVanillaEndgameGradeLetter(grade, out var gradeValue) ||
            !string.IsNullOrWhiteSpace(current) ||
            !IsAdvancedFeaturesGradeLabelText(nearbyLabel))
        {
            return false;
        }

        repaired = gradeValue;
        return true;
    }

    private static bool TryBuildAdvancedFeaturesDirectGradeValueText(string? current, string? grade, out string repaired)
    {
        repaired = current ?? string.Empty;
        if (!TryNormalizeVanillaEndgameGradeLetter(grade, out var gradeValue))
        {
            return false;
        }

        if (string.Equals(current?.Trim(), gradeValue, StringComparison.Ordinal))
        {
            return false;
        }

        repaired = gradeValue;
        return true;
    }

    private static bool IsAdvancedFeaturesGradeLabelText(string? value)
    {
        var text = value?.Trim();
        return string.Equals(text, "Grade", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, "Grade:", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(text, AdvancedFeaturesGradeLabelLocalized, StringComparison.Ordinal) ||
               string.Equals(text, AdvancedFeaturesGradeLabelLocalized + ":", StringComparison.Ordinal) ||
               string.Equals(text, AdvancedFeaturesGradeLabelLocalized + "\uff1a", StringComparison.Ordinal);
    }

    private static bool IsVanillaEndgameGradeLetter(string? value)
    {
        return TryNormalizeVanillaEndgameGradeLetter(value, out _);
    }

    private static bool TryNormalizeVanillaEndgameGradeLetter(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length != 1)
        {
            return false;
        }

        normalized = trimmed[0] switch
        {
            'F' or '\uff26' => "F",
            'D' or '\uff24' => "D",
            'C' or '\uff23' => "C",
            'B' or '\uff22' => "B",
            'A' or '\uff21' => "A",
            'S' or '\uff33' => "S",
            _ => string.Empty
        };

        return normalized.Length != 0;
    }
}
