using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace V81TestChn;

internal static class UiTranslator
{
    public static (int tmpTranslated, int uiTranslated, int tmpSeen, int uiSeen) TranslateLoadedScene()
    {
        var tmpTranslated = 0;
        var uiTranslated = 0;
        var tmpSeen = 0;
        var uiSeen = 0;

        FontFallbackService.ApplyFallbackGlobally();
        uiTranslated += GameResourceTranslator.TranslateLoadedResources();

        foreach (var dropdown in Object.FindObjectsOfType<TMP_Dropdown>(true))
        {
            if (dropdown == null || dropdown.options == null)
            {
                continue;
            }

            var changed = false;
            foreach (var option in dropdown.options)
            {
                if (option == null)
                {
                    continue;
                }

                if (TranslationService.TryTranslate(option.text, out var translated))
                {
                    if (translated != option.text)
                    {
                        option.text = translated;
                        tmpTranslated++;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                TargetedUiTranslator.SafeRefreshShownValue(dropdown);
            }
        }

        foreach (var dropdown in Object.FindObjectsOfType<Dropdown>(true))
        {
            if (dropdown == null || dropdown.options == null)
            {
                continue;
            }

            var changed = false;
            foreach (var option in dropdown.options)
            {
                if (option == null)
                {
                    continue;
                }

                if (TranslationService.TryTranslate(option.text, out var translated))
                {
                    if (translated != option.text)
                    {
                        option.text = translated;
                        uiTranslated++;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                TargetedUiTranslator.SafeRefreshShownValue(dropdown);
            }
        }

        foreach (var text in Object.FindObjectsOfType<TMP_Text>(true))
        {
            if (text == null)
            {
                continue;
            }

            tmpSeen++;
            if (TranslationService.TryTranslate(text.text, out var translated))
            {
                text.text = translated;
                FontFallbackService.ApplyFallback(text, translated);
                FontFallbackService.ApplySystemOnlineProbeFix(text, "UiTranslator.TMP", translated);
                AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.TMP");
                tmpTranslated++;
            }
            else
            {
                FontFallbackService.ApplyFallback(text, text.text);
                FontFallbackService.ApplySystemOnlineProbeFix(text, "UiTranslator.TMP", text.text);
                AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.TMP");
                RuntimeTextCollector.Record(text, text.text);
            }
        }

        foreach (var text in Object.FindObjectsOfType<Text>(true))
        {
            if (text == null)
            {
                continue;
            }

            uiSeen++;
            if (TranslationService.TryTranslate(text.text, out var translated))
            {
                text.text = translated;
                FontFallbackService.ApplySystemOnlineProbeFix(text, "UiTranslator.UI.Text", translated);
                AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.UI.Text");
                uiTranslated++;
            }
            else
            {
                FontFallbackService.ApplySystemOnlineProbeFix(text, "UiTranslator.UI.Text", text.text);
                AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.UI.Text");
                RuntimeTextCollector.Record(text, text.text);
            }
        }

        foreach (var text in Object.FindObjectsOfType<TextMesh>(true))
        {
            if (text == null)
            {
                continue;
            }

            uiSeen++;
            if (TranslationService.TryTranslate(text.text, out var translated))
            {
                text.text = translated;
                FontFallbackService.ApplySystemOnlineProbeFix(text, "UiTranslator.TextMesh", translated);
                AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.TextMesh");
                uiTranslated++;
            }
            else
            {
                FontFallbackService.ApplySystemOnlineProbeFix(text, "UiTranslator.TextMesh", text.text);
                AlertTextureReplacementService.TryReplaceSystemOnlineText(text, "UiTranslator.TextMesh");
            }
        }

        if (tmpSeen == 0 && uiSeen == 0)
        {
            var rootResult = TranslateLoadedSceneRoots();
            tmpTranslated += rootResult.tmpTranslated;
            uiTranslated += rootResult.uiTranslated;
            tmpSeen += rootResult.tmpSeen;
            uiSeen += rootResult.uiSeen;
        }

        return (tmpTranslated, uiTranslated, tmpSeen, uiSeen);
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
