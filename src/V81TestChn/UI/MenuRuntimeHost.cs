using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace V81TestChn;

// Independent of the BepInEx plugin component. Some startup mods destroy that
// component while leaving installed Harmony patches alive.
internal static class MenuRuntimeHost
{
    private static MenuRuntimeDriver? _driver;
    private static bool _subscribed, _stopped, _creating;

    internal static void Initialize()
    {
        _stopped = false;
        if (!_subscribed)
        {
            SceneManager.sceneLoaded += SceneLoaded;
            Application.onBeforeRender += EnsureBeforeRender;
            Application.quitting += Shutdown;
            _subscribed = true;
        }
        Ensure();
    }

    internal static MonoBehaviour? Ensure()
    {
        if (_stopped || Plugin.IsRuntimeShuttingDown || _creating) return null;
        if (_driver != null && _driver.isActiveAndEnabled) return _driver;
        _creating = true;
        try
        {
            if (_driver != null) Object.Destroy(_driver.gameObject);
            var root = new GameObject("V81TestChn.MenuRuntime");
            Object.DontDestroyOnLoad(root);
            _driver = root.AddComponent<MenuRuntimeDriver>();
            QuickMenuPreparationService.AttachHost(_driver);
            QuickMenuTimingService.AttachHost(_driver);
            Plugin.Log.LogInfo("[MenuRuntime] independent host created/recovered.");
            return _driver;
        }
        finally { _creating = false; }
    }

    // One null/active check per rendered frame; no polling of menus or texts.
    private static void EnsureBeforeRender() { if (_driver == null || !_driver.isActiveAndEnabled) Ensure(); }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode) => Ensure();
    internal static void Destroyed(MenuRuntimeDriver driver)
    {
        if (ReferenceEquals(_driver, driver))
        {
            _driver = null;
            SpeechInputService.Cancel();
        }
    }
    internal static void Shutdown()
    {
        _stopped = true;
        if (_subscribed)
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            Application.onBeforeRender -= EnsureBeforeRender;
            Application.quitting -= Shutdown;
            _subscribed = false;
        }
        SpeechInputService.Shutdown();
        if (_driver != null) Object.Destroy(_driver.gameObject);
        _driver = null;
    }
}

internal sealed class MenuRuntimeDriver : MonoBehaviour
{
    private void Update()
    {
        // The plugin component can disappear during startup. Keep input and
        // cancellation on this surviving main-thread host, with one pump owner.
        SpeechInputService.Pump();
        EnvironmentTextureLocalizationService.PumpPendingLevelScan();
    }
    private void OnDestroy() => MenuRuntimeHost.Destroyed(this);
}
