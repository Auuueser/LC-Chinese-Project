using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace V81TestChn;

internal static class UiTranslator
{
    public static (int tmpTranslated, int uiTranslated, int tmpSeen, int uiSeen) TranslateLoadedScene()
    {
        // This is a scene-load sweep. High-frequency setter hooks stay on the fast exact path.
        FontFallbackService.ApplyFallbackGlobally();
        var rootResult = TranslateLoadedSceneRoots();
        rootResult.uiTranslated += GameResourceTranslator.TranslateLoadedResources();
        return rootResult;
    }

    private static (int tmpTranslated, int uiTranslated, int tmpSeen, int uiSeen) TranslateLoadedSceneRoots()
    {
        var tmpTranslated = 0;
        var uiTranslated = 0;
        var tmpSeen = 0;
        var uiSeen = 0;

        for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            var scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text == null)
                    {
                        continue;
                    }

                    tmpSeen++;
                    if (!TranslationGuard.ShouldTranslateGlobalText(text, text.text))
                    {
                        if (TranslationGuard.ShouldTouchGlobalTextStyle(text))
                        {
                            FontFallbackService.ApplyFallback(text, text.text);
                            AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.TMP.Guarded");
                        }

                        continue;
                    }

                    if (TranslationService.TryTranslate(text.text, out var translated))
                    {
                        text.text = translated;
                        FontFallbackService.ApplyFallback(text, translated);
                        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.TMP");
                        tmpTranslated++;
                    }
                    else
                    {
                        FontFallbackService.ApplyFallback(text, text.text);
                        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.TMP");
                        RuntimeTextCollector.Record(text, text.text);
                    }
                }

                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text == null)
                    {
                        continue;
                    }

                    uiSeen++;
                    if (!TranslationGuard.ShouldTranslateGlobalText(text, text.text))
                    {
                        continue;
                    }

                    if (TranslationService.TryTranslate(text.text, out var translated))
                    {
                        text.text = translated;
                        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.UI.Text");
                        uiTranslated++;
                    }
                    else
                    {
                        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.UI.Text");
                        RuntimeTextCollector.Record(text, text.text);
                    }
                }

                foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                {
                    if (text == null)
                    {
                        continue;
                    }

                    uiSeen++;
                    if (!TranslationGuard.ShouldTranslateGlobalText(text, text.text))
                    {
                        continue;
                    }

                    if (TranslationService.TryTranslate(text.text, out var translated))
                    {
                        text.text = translated;
                        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.TextMesh");
                        uiTranslated++;
                    }
                    else
                    {
                        AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.Roots.TextMesh");
                    }
                }
            }
        }

        return (tmpTranslated, uiTranslated, tmpSeen, uiSeen);
    }
}
