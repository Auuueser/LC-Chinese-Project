using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Video;

namespace V81TestChn;

internal static partial class TextPatches
{
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

        EnvironmentTextureLocalizationService.ApplyShipEnvironment(__instance);
        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.Start exit inShipPhase={__instance.inShipPhase} currentLevel={LevelName(__instance)}");
        // Plugin.Log.LogInfo($"Patch entry StartOfRound.Start shipHasLanded={__instance.shipHasLanded} inShipPhase={__instance.inShipPhase} currentLevel={__instance.currentLevel?.name ?? "<null>"}");
    }

    private static void StartOfRoundSceneManagerOnLoadCompletePostfix()
    {
        AlertTextureReplacementService.TryApplyEnteringAtmosphereOverlayFromLoadingScreen(
            HUDManager.Instance,
            "StartOfRound.SceneManager_OnLoadComplete1");
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

        MapScreenLocalizationService.ApplyDescriptionTranslation(__instance.screenLevelDescription, "StartOfRound.ChangePlanet.screen");
        if (HUDManager.Instance != null)
        {
            TargetedUiTranslator.TranslateHudPlanetInfo(HUDManager.Instance, "StartOfRound.ChangePlanet.planet-info");
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

        MapScreenLocalizationService.ApplyDescriptionTranslation(__instance.screenLevelDescription, "StartOfRound.SetMapScreenInfoToCurrentLevel.screen");
        if (HUDManager.Instance != null)
        {
            TargetedUiTranslator.TranslateHudPlanetInfo(HUDManager.Instance, "StartOfRound.SetMapScreenInfoToCurrentLevel.planet-info");
        }
        // Plugin.Log.LogInfo($"RoomCreateProbe StartOfRound.SetMapScreenInfoToCurrentLevel exit currentLevel={LevelName(__instance)} screenText={TextInfo(__instance.screenLevelDescription)}");
    }

    [HarmonyPatch(typeof(StartOfRound), "SwitchMapMonitorPurpose")]
    [HarmonyPostfix]
    private static void StartOfRoundSwitchMapMonitorPurposePostfix(StartOfRound __instance, bool displayInfo = false)
    {
        if (__instance == null || !displayInfo)
        {
            return;
        }

        MapScreenLocalizationService.ApplyDescriptionTranslation(__instance.screenLevelDescription, "StartOfRound.SwitchMapMonitorPurpose.screen");
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
}
