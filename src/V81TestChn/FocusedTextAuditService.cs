using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace V81TestChn;

internal static class FocusedTextAuditService
{
    private static int _logBudget = 180;

    public static void AuditLoadedScene(string stage)
    {
        if (_logBudget <= 0)
        {
            return;
        }

        foreach (var text in Object.FindObjectsOfType<TMP_Text>(true))
        {
            if (text == null || !ShouldAudit(text.text))
            {
                continue;
            }

            var sharedFace = "N/A";
            var shared = text.fontSharedMaterial;
            if (shared != null && shared.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                sharedFace = shared.GetColor(ShaderUtilities.ID_FaceColor).ToString();
            }

            var fontFace = "N/A";
            var fontMat = text.fontMaterial;
            if (fontMat != null && fontMat.HasProperty(ShaderUtilities.ID_FaceColor))
            {
                fontFace = fontMat.GetColor(ShaderUtilities.ID_FaceColor).ToString();
            }

            Log(stage, "TMP", text.name, text.color, sharedFace, fontFace, text.text);
            if (_logBudget <= 0)
            {
                return;
            }
        }

        foreach (var text in Object.FindObjectsOfType<Text>(true))
        {
            if (text == null || !ShouldAudit(text.text))
            {
                continue;
            }

            Log(stage, "UGUI.Text", text.name, text.color, "N/A", "N/A", text.text);
            if (_logBudget <= 0)
            {
                return;
            }
        }

        foreach (var text in Object.FindObjectsOfType<TextMesh>(true))
        {
            if (text == null || !ShouldAudit(text.text))
            {
                continue;
            }

            Log(stage, "TextMesh", text.name, text.color, "N/A", "N/A", text.text);
            if (_logBudget <= 0)
            {
                return;
            }
        }
    }

    private static void Log(string stage, string type, string name, Color color, string sharedFace, string fontFace, string text)
    {
        if (_logBudget <= 0)
        {
            return;
        }

        _logBudget--;
        Plugin.Log.LogWarning(
            $"FocusedSceneAudit[{stage}] type={type}, name={name}, color={color}, sharedFace={sharedFace}, fontFace={fontFace}, text='{Trim(text)}'");
    }

    private static bool ShouldAudit(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsOrdinalIgnoreCase(text, "SYSTEMS ONLINE") ||
               ContainsOrdinalIgnoreCase(text, "joined the ship") ||
               ContainsOrdinalIgnoreCase(text, "started the ship") ||
               ContainsOrdinalIgnoreCase(text, "ENTERING THE ATMOSPHERE") ||
               ContainsOrdinalIgnoreCase(text, "\u7cfb\u7edf\u5728\u7ebf") ||
               ContainsOrdinalIgnoreCase(text, "\u7cfb\u7edf\u4e0a\u7ebf") ||
               ContainsOrdinalIgnoreCase(text, "\u6b63\u5728\u8fdb\u5165\u5927\u6c14\u5c42") ||
               ContainsOrdinalIgnoreCase(text, "\u8fdb\u5165\u5927\u6c14\u5c42");
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string needle)
    {
        return source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Trim(string input)
    {
        input = input.Replace('\n', ' ');
        return input.Length <= 120 ? input : input.Substring(0, 120);
    }
}
