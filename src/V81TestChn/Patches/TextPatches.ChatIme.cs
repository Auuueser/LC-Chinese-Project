using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace V81TestChn;

internal static partial class TextPatches
{
    private static bool ChatActionDispatchImePrefix(int actionIndex, InputActionMap actionMap, InputActionPhase phase)
        => !ChatImeService.ShouldBlockActionDispatch(actionIndex, actionMap, phase);

    private static bool HudSubmitChatImePrefix(HUDManager __instance)
        => !ChatImeService.ShouldBlockSubmit(__instance);

    private static void HudEnableChatImePostfix(HUDManager __instance)
    {
        if (__instance.localPlayer != null && __instance.localPlayer.isTypingChat) SpeechInputService.Cancel();
        ChatImeService.OnChatOpened(__instance);
    }

    private static void TmpChatImeLateUpdatePrefix(TMP_InputField __instance)
        => ChatImeService.ObserveLateUpdate(__instance);

    private static void TmpChatImeUpdatePrefix(TMP_InputField __instance)
        => ChatImeService.ObserveSelectedField(__instance);

    private static void TmpChatImeDeselectPostfix(TMP_InputField __instance, BaseEventData eventData)
        => ChatImeService.ReleaseField(__instance, eventData is PointerEventData);

    private static void TmpChatImeDisablePostfix(TMP_InputField __instance)
        => ChatImeService.DisableField(__instance);

    private static bool TmpChatImeKeyPressedPrefix(TMP_InputField __instance, Event evt,
        ref TMP_InputField.EditState __result)
    {
        if (!ChatImeService.ShouldBlockKey(__instance, evt)) return true;
        __result = TMP_InputField.EditState.Continue;
        return false;
    }

    private static bool TmpChatImeSubmitPrefix(TMP_InputField __instance)
        => !ChatImeService.ShouldBlockFieldSubmit(__instance);
}
