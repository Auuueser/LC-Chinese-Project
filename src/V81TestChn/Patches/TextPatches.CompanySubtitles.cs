using HarmonyLib;
using UnityEngine;

namespace V81TestChn;

internal static partial class TextPatches
{
    [HarmonyPatch(typeof(DepositItemsDesk), "Start")]
    [HarmonyPostfix]
    private static void DepositItemsDeskStartPostfix(DepositItemsDesk __instance)
    {
        CompanySubtitleService.RegisterCompanyDesk(__instance);
    }

    [HarmonyPatch(typeof(TVScript), "OnEnable")]
    [HarmonyPostfix]
    private static void TvScriptOnEnablePostfix(TVScript __instance)
    {
        CompanySubtitleService.RegisterTv(__instance);
    }

    [HarmonyPatch(typeof(TVScript), "OnDisable")]
    [HarmonyPrefix]
    private static void TvScriptOnDisablePrefix(TVScript __instance)
    {
        CompanySubtitleService.UnregisterTv(__instance);
    }

    [HarmonyPatch(typeof(TVScript), "TurnTVOnOff")]
    [HarmonyPrefix]
    private static void TvScriptTurnTvOnOffPrefix(TVScript __instance)
    {
        CompanySubtitleService.RegisterTv(__instance);
    }

    [HarmonyPatch(typeof(TVScript), "TurnTVOnOff")]
    [HarmonyPostfix]
    private static void TvScriptTurnTvOnOffPostfix(TVScript __instance, bool on)
    {
        CompanySubtitleService.OnTvPowerChanged(__instance, on);
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play))]
    [HarmonyPrefix]
    private static void AudioSourcePlayPrefix(AudioSource __instance)
    {
        CompanySubtitleService.OnAudioSourcePlay(__instance);
    }

    [HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShot), typeof(AudioClip), typeof(float))]
    [HarmonyPrefix]
    private static void AudioSourcePlayOneShotPrefix(AudioSource __instance, AudioClip clip)
    {
        CompanySubtitleService.OnAudioSourcePlayOneShot(__instance, clip);
    }

    [HarmonyPatch(typeof(StartOfRound), "DisableShipSpeakerLocalClient")]
    [HarmonyPrefix]
    private static void StartOfRoundDisableShipSpeakerLocalClientPrefix(StartOfRound __instance)
    {
        CompanySubtitleService.OnShipSpeakerDisabled(__instance);
    }
}
