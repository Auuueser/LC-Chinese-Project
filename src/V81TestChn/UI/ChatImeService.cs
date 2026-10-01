using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace V81TestChn;

internal static class ChatImeService
{
    private static readonly ChatImeSubmitState State = new();
    private static readonly ChatInputActivationState Activation = new();
    private static TMP_InputField? _field;
    private static TMP_InputField? _activationField;
    private static bool _queuedActivationByUs;
    private static bool _previousSessionCanceled;
    private static Keyboard? _keyboard;
    private static bool _eventComposing;
    private static InputActionAsset? _submitAsset;
    private static InputAction? _submitAction;

    internal static bool ShouldBlockActionDispatch(int actionIndex, InputActionMap actionMap, InputActionPhase phase)
    {
        if (phase != InputActionPhase.Performed || Plugin.IsRuntimeShuttingDown) return false;
        var asset = InputSystem.actions;
        if (!ReferenceEquals(asset, _submitAsset))
        {
            _submitAsset = asset;
            _submitAction = asset?.FindAction("SubmitChat", false);
        }
        // Dispatch is upstream of every HUD prefix, including command handlers
        // that execute at int.MaxValue priority. Never reorder their patches or
        // alter their text. Input System still completes its state transition.
        if (_submitAction == null || !ReferenceEquals(actionMap, _submitAction.actionMap) ||
            actionIndex != _submitAction.m_ActionIndexInState) return false;
        var hud = HUDManager.Instance;
        return hud != null && ShouldBlockSubmit(hud);
    }

    internal static void OnChatOpened(HUDManager hud)
    {
        if (!ReferenceEquals(hud, HUDManager.Instance) ||
            GameNetworkManager.Instance?.localPlayerController?.isTypingChat != true) return;
        EndActivationRequest();
        _activationField = hud.chatTextField;
        _previousSessionCanceled = _activationField != null && _activationField.m_WasCanceled;
        Activation.Begin(Time.frameCount);
        // m_WasCanceled can still describe the previous session until TMP's
        // deferred activation runs. The new explicit open supersedes it.
        RecoverActivation();
        Refresh();
    }

    internal static void ObserveLateUpdate(TMP_InputField field)
    {
        if (!ReferenceEquals(field, _activationField)) return;
        RecoverActivation();
        Refresh();
    }

    internal static void ObserveSelectedField(TMP_InputField field)
    {
        if (ReferenceEquals(field, _activationField) && field.isFocused) _previousSessionCanceled = false;
        if (ReferenceEquals(field, HUDManager.Instance?.chatTextField)) Refresh();
    }

    internal static void ReleaseField(TMP_InputField field, bool pointerSelection)
    {
        // EventSystem sends OnDeselect before publishing its next selection.
        // A lifecycle callback alone cannot distinguish a rebuild from another
        // UI taking focus. Check that selection after the event in LateUpdate.
        if (ReferenceEquals(field, _activationField) &&
            (pointerSelection || (field.m_WasCanceled && !_previousSessionCanceled) ||
             GameNetworkManager.Instance?.localPlayerController?.isTypingChat != true))
            EndActivationRequest();
        if (ReferenceEquals(field, _field)) ClearObservation();
    }

    internal static void DisableField(TMP_InputField field)
    {
        // Reparenting under an initially inactive UI container can disable and
        // re-enable the field before its first LateUpdate. Keep only the bounded
        // open request; composition state and keyboard subscriptions are released.
        if (ReferenceEquals(field, _field)) ClearObservation();
    }

    internal static bool ShouldBlockSubmit(HUDManager hud)
        => ReferenceEquals(hud, HUDManager.Instance) && Refresh() &&
           State.ShouldBlock(Time.frameCount, EnterHeld());

    internal static bool ShouldBlockKey(TMP_InputField field, Event evt)
        => (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) &&
           ShouldBlockFieldSubmit(field);

    internal static bool ShouldBlockFieldSubmit(TMP_InputField field)
        => field.isFocused && ReferenceEquals(field, HUDManager.Instance?.chatTextField) &&
           Refresh() && State.ShouldBlock(Time.frameCount, EnterHeld());

    internal static void Clear()
    {
        EndActivationRequest();
        ClearObservation();
        _submitAsset = null;
        _submitAction = null;
    }

    private static void EndActivationRequest()
    {
        // A cancel/other UI selection can occur after our request but before
        // TMP consumes it. Revoke only the request we supplied, never TMP's or
        // another mod's pre-existing activation request.
        if (_queuedActivationByUs && _activationField != null && !_activationField.isFocused)
            _activationField.m_ShouldActivateNextUpdate = false;
        _queuedActivationByUs = false;
        _previousSessionCanceled = false;
        Activation.Reset();
        _activationField = null;
    }

    private static void ClearObservation()
    {
        if (_keyboard != null) _keyboard.onIMECompositionChange -= OnCompositionChanged;
        _keyboard = null;
        _field = null;
        _eventComposing = false;
        State.Reset();
    }

    private static void RecoverActivation()
    {
        var field = _activationField;
        if (field != null && (field.isFocused || !field.m_WasCanceled)) _previousSessionCanceled = false;
        if (_queuedActivationByUs && (field == null || field.isFocused || !field.m_ShouldActivateNextUpdate))
            _queuedActivationByUs = false;
        var events = EventSystem.current;
        var player = GameNetworkManager.Instance?.localPlayerController;
        var available = !Plugin.IsRuntimeShuttingDown && Application.isFocused &&
            field != null && ReferenceEquals(field, HUDManager.Instance?.chatTextField) &&
            field.isActiveAndEnabled && field.IsInteractable() && !field.readOnly && events != null;
        var selected = events != null ? events.currentSelectedGameObject : null;
        if (Activation.ShouldActivate(Time.frameCount, player != null && player.isTypingChat,
            available, selected != null && (field == null || !ReferenceEquals(selected, field.gameObject)),
            !_previousSessionCanceled && field != null && field.m_WasCanceled,
            field != null && field.isFocused, field != null && field.m_ShouldActivateNextUpdate))
        {
            // Use TMP's own activation path, preserving IME, caret, selection,
            // validation and all third-party input/submit listeners.
            field!.ActivateInputField();
            _queuedActivationByUs = field.m_ShouldActivateNextUpdate;
        }
        if (!Activation.Pending) EndActivationRequest();
    }

    private static bool Refresh()
    {
        var field = HUDManager.Instance?.chatTextField;
        var player = GameNetworkManager.Instance?.localPlayerController;
        if (Plugin.IsRuntimeShuttingDown || !Application.isFocused || field == null || player == null ||
            !player.isTypingChat || !field.isActiveAndEnabled ||
            (!field.isFocused && (EventSystem.current == null ||
             !ReferenceEquals(EventSystem.current.currentSelectedGameObject, field.gameObject))))
        {
            if (_field != null || _keyboard != null) ClearObservation();
            return false;
        }

        if (!ReferenceEquals(field, _field))
        {
            ClearObservation();
            _field = field;
            State.Open(Time.frameCount, EnterHeld());
        }
        var keyboard = Keyboard.current;
        if (!ReferenceEquals(keyboard, _keyboard))
        {
            if (_keyboard != null) _keyboard.onIMECompositionChange -= OnCompositionChanged;
            _keyboard = keyboard;
            _eventComposing = false;
            if (_keyboard != null) _keyboard.onIMECompositionChange += OnCompositionChanged;
        }

        // Read the same BaseInput as TMP, including any UI input override.
        var input = EventSystem.current?.currentInputModule?.input;
        var composition = input != null ? input.compositionString : Input.compositionString;
        State.Observe(Time.frameCount, _eventComposing || !string.IsNullOrEmpty(composition), EnterHeld(), EnterReleased());
        return true;
    }

    private static void OnCompositionChanged(IMECompositionString composition)
    {
        _eventComposing = composition.Count > 0;
        // Preserve the end-of-composition frame even if it precedes the action
        // callback and TMP already reports an empty composition string.
        State.Observe(Time.frameCount, _eventComposing, EnterHeld(), EnterReleased());
    }

    private static bool EnterReleased()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasReleasedThisFrame || keyboard.numpadEnterKey.wasReleasedThisFrame);
    }

    private static bool EnterHeld()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.isPressed || keyboard.numpadEnterKey.isPressed);
    }
}
