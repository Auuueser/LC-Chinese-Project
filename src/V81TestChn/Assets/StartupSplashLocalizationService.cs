using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Rendering;

namespace V81TestChn;

internal static class StartupSplashLocalizationService
{
    private const string TargetTextureName = "flashingLightsWarning";
    private const string LocalizedFileName = "StartupFlashingLightsWarningLocalized.png";
    private const int ExpectedWidth = 800;
    private const int ExpectedHeight = 600;
    private const int MaxStartupAttempts = 24;
    private const int MaxLoggedSprites = 8;
    private static readonly WaitForSecondsRealtime RetryDelay = new(0.05f);
    private static readonly ushort[] FullRectTriangleIndices = { 0, 1, 2, 2, 3, 0 };

    private static MonoBehaviour? _host;
    private static Coroutine? _retryCoroutine;
    private static ConfigEntry<bool>? _diagnosticsEnabled;
    private static byte[]? _localizedPng;
    private static string _localizedPngSha256 = string.Empty;
    private static bool _diagnosticsEnabledFast;
    private static bool _applied;
    private static int _applyAttempt;

    public static void Initialize(MonoBehaviour host, string pluginDir, ConfigFile config)
    {
        Shutdown();
        _diagnosticsEnabled = config.Bind(
            ConfigSections.DiagnosticsGeneral,
            "EnableStartupSplashDiagnostics",
            false,
            "启用一次性启动警告纹理与 Sprite 几何诊断。仅在启动替换时记录，不增加常驻轮询。");
        _diagnosticsEnabledFast = _diagnosticsEnabled.Value;

        var path = Path.Combine(pluginDir, "textures", LocalizedFileName);
        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= 0 || file.Length > 2 * 1024 * 1024)
        {
            throw new InvalidDataException($"Invalid startup splash texture: {path}");
        }

        _host = host;
        _localizedPng = File.ReadAllBytes(path);
        _localizedPngSha256 = ComputeSha256(_localizedPng);
        LogDiagnostic(
            $"stage=initialize pngBytes={_localizedPng.Length} pngSha256={_localizedPngSha256} " +
            $"splashFinished={SplashScreen.isFinished}");
        if (TryApply())
        {
            return;
        }

        _retryCoroutine = host.StartCoroutine(RetryDuringStartup());
    }

    public static void Shutdown()
    {
        if (_retryCoroutine != null && _host != null)
        {
            try
            {
                _host.StopCoroutine(_retryCoroutine);
            }
            catch
            {
                // The BepInEx host may already be tearing down.
            }
        }

        _retryCoroutine = null;
        _host = null;
        _diagnosticsEnabled = null;
        _localizedPng = null;
        _localizedPngSha256 = string.Empty;
        _diagnosticsEnabledFast = false;
        _applied = false;
        _applyAttempt = 0;
    }

    private static IEnumerator RetryDuringStartup()
    {
        for (var attempt = 0; attempt < MaxStartupAttempts; attempt++)
        {
            yield return RetryDelay;
            if (TryApply())
            {
                _retryCoroutine = null;
                yield break;
            }

            if (attempt >= 2 && SplashScreen.isFinished)
            {
                break;
            }
        }

        _retryCoroutine = null;
        Plugin.Log.LogWarning(
            "Startup splash localization could not find flashingLightsWarning before the native splash finished.");
    }

    private static bool TryApply()
    {
        if (_applied || _localizedPng == null)
        {
            return _applied;
        }

        _applyAttempt++;
        var loadedTextures = Resources.FindObjectsOfTypeAll<Texture2D>();
        LogDiagnostic(
            $"stage=search attempt={_applyAttempt} loadedTextures={loadedTextures.Length} " +
            $"splashFinished={SplashScreen.isFinished}");

        foreach (var texture in loadedTextures)
        {
            if (texture == null ||
                !string.Equals(NormalizeName(texture.name), TargetTextureName, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                LogTextureAndReferencingSprites(texture, "before-load");
                var loadSucceeded = ImageConversion.LoadImage(texture, _localizedPng, true);
                LogDiagnostic(
                    $"stage=after-load loadSucceeded={loadSucceeded} pngSha256={_localizedPngSha256} " +
                    DescribeTexture(texture));
                if (!loadSucceeded ||
                    texture.width != ExpectedWidth ||
                    texture.height != ExpectedHeight)
                {
                    Plugin.Log.LogWarning(
                        $"Startup splash localization produced an invalid texture size: {texture.width}x{texture.height}.");
                    return false;
                }

                LogTextureAndReferencingSprites(texture, "after-load");
                if (!TryOverrideReferencingSpriteGeometry(texture))
                {
                    Plugin.Log.LogWarning(
                        "Startup splash localization could not expand the original Tight Sprite mesh to a full rectangle.");
                    return false;
                }

                LogTextureAndReferencingSprites(texture, "after-geometry");
                texture.name = TargetTextureName;
                _localizedPng = null;
                _applied = true;
                Plugin.Log.LogInfo(
                    "Localized Unity startup flashing-lights warning texture in memory with full-rectangle Sprite geometry.");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"Startup splash localization failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        LogDiagnostic($"stage=search-result attempt={_applyAttempt} matchingTextures=0");
        return false;
    }

    private static bool TryOverrideReferencingSpriteGeometry(Texture2D texture)
    {
        var matchingSpriteCount = 0;
        var overriddenSpriteCount = 0;
        foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite == null)
            {
                continue;
            }

            try
            {
                var spriteTexture = sprite.texture;
                if (spriteTexture == null || spriteTexture.GetInstanceID() != texture.GetInstanceID())
                {
                    continue;
                }

                matchingSpriteCount++;
                var rect = sprite.rect;
                if (rect.width <= 0f || rect.height <= 0f ||
                    float.IsNaN(rect.width) || float.IsNaN(rect.height) ||
                    float.IsInfinity(rect.width) || float.IsInfinity(rect.height))
                {
                    Plugin.Log.LogWarning(
                        $"Startup splash Sprite has invalid full-rectangle geometry inputs: " +
                        $"sprite={sprite.name} rect={FormatRect(rect)}.");
                    continue;
                }

                // OverrideGeometry expects Sprite.rect pixel space (0..rect.size).
                // Unity applies the pivot offset and pixels-per-unit conversion.
                var vertices = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, rect.height),
                    new Vector2(rect.width, rect.height),
                    new Vector2(rect.width, 0f),
                };

                var beforeVertexCount = sprite.vertices.Length;
                var beforeTriangleIndexCount = sprite.triangles.Length;
                sprite.OverrideGeometry(vertices, FullRectTriangleIndices);

                var updatedVertices = sprite.vertices;
                var updatedTriangles = sprite.triangles;
                if (updatedVertices.Length != 4 || updatedTriangles.Length != 6)
                {
                    Plugin.Log.LogWarning(
                        $"Startup splash Sprite full-rectangle verification failed: sprite={sprite.name} " +
                        $"vertices={updatedVertices.Length} triangleIndices={updatedTriangles.Length}.");
                    continue;
                }

                overriddenSpriteCount++;
                LogDiagnostic(
                    $"stage=geometry-override sprite={sprite.name} spriteId={sprite.GetInstanceID()} " +
                    $"beforeVertices={beforeVertexCount} beforeTriangleIndices={beforeTriangleIndexCount} " +
                    $"afterVertices={updatedVertices.Length} afterTriangleIndices={updatedTriangles.Length} " +
                    $"vertices={FormatVector2Array(updatedVertices)} " +
                    $"triangles={FormatUShortArray(updatedTriangles)} uv={FormatVector2Array(sprite.uv)}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"Startup splash Sprite full-rectangle override failed: " +
                    $"sprite={sprite.name} error={ex.GetType().Name}:{ex.Message}");
            }
        }

        if (matchingSpriteCount == 0)
        {
            Plugin.Log.LogWarning(
                "Startup splash localization found the target texture but no referencing Sprite to expand.");
            return false;
        }

        if (overriddenSpriteCount != matchingSpriteCount)
        {
            Plugin.Log.LogWarning(
                $"Startup splash localization expanded {overriddenSpriteCount}/{matchingSpriteCount} referencing Sprites.");
            return false;
        }

        return true;
    }

    private static void LogTextureAndReferencingSprites(Texture2D texture, string stage)
    {
        if (!_diagnosticsEnabledFast)
        {
            return;
        }

        Plugin.Log.LogInfo($"StartupSplashDiag stage={stage} {DescribeTexture(texture)}");

        var matchingSpriteCount = 0;
        var loggedSpriteCount = 0;
        foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite == null)
            {
                continue;
            }

            Texture2D? spriteTexture;
            try
            {
                spriteTexture = sprite.texture;
            }
            catch (Exception ex)
            {
                LogDiagnostic(
                    $"stage={stage} sprite={sprite.name} action=texture-read-failed " +
                    $"error={ex.GetType().Name}:{ex.Message}");
                continue;
            }

            if (spriteTexture == null || spriteTexture.GetInstanceID() != texture.GetInstanceID())
            {
                continue;
            }

            matchingSpriteCount++;
            if (loggedSpriteCount >= MaxLoggedSprites)
            {
                continue;
            }

            loggedSpriteCount++;
            try
            {
                var vertices = sprite.vertices;
                var triangles = sprite.triangles;
                var uv = sprite.uv;
                var geometry = vertices.Length == 4 && triangles.Length == 6
                    ? "quad"
                    : "non-quad-tight-candidate";
                Plugin.Log.LogInfo(
                    $"StartupSplashDiag stage={stage} spriteIndex={matchingSpriteCount - 1} " +
                    $"spriteName={sprite.name} spriteId={sprite.GetInstanceID()} packed={sprite.packed} " +
                    $"packingMode={SafePackingMode(sprite)} packingRotation={SafePackingRotation(sprite)} " +
                    $"rect={FormatRect(sprite.rect)} textureRect={SafeTextureRect(sprite)} " +
                    $"pivot={FormatVector2(sprite.pivot)} pixelsPerUnit={sprite.pixelsPerUnit:0.###} " +
                    $"bounds={FormatBounds(sprite.bounds)} geometry={geometry} " +
                    $"vertices={FormatVector2Array(vertices)} triangles={FormatUShortArray(triangles)} " +
                    $"uv={FormatVector2Array(uv)}");
            }
            catch (Exception ex)
            {
                LogDiagnostic(
                    $"stage={stage} sprite={sprite.name} action=geometry-read-failed " +
                    $"error={ex.GetType().Name}:{ex.Message}");
            }
        }

        LogDiagnostic(
            $"stage={stage} matchingSprites={matchingSpriteCount} loggedSprites={loggedSpriteCount} " +
            $"maxLoggedSprites={MaxLoggedSprites}");
    }

    private static string DescribeTexture(Texture2D texture)
    {
        return
            $"textureName={texture.name} textureId={texture.GetInstanceID()} " +
            $"size={texture.width}x{texture.height} format={texture.format} " +
            $"mipmaps={texture.mipmapCount} readable={texture.isReadable} " +
            $"filter={texture.filterMode} wrap={texture.wrapMode} aniso={texture.anisoLevel}";
    }

    private static string SafeTextureRect(Sprite sprite)
    {
        try
        {
            return FormatRect(sprite.textureRect);
        }
        catch (Exception ex)
        {
            return $"<unavailable:{ex.GetType().Name}>";
        }
    }

    private static string SafePackingMode(Sprite sprite)
    {
        try
        {
            return sprite.packingMode.ToString();
        }
        catch (Exception ex)
        {
            return $"<unavailable:{ex.GetType().Name}>";
        }
    }

    private static string SafePackingRotation(Sprite sprite)
    {
        try
        {
            return sprite.packingRotation.ToString();
        }
        catch (Exception ex)
        {
            return $"<unavailable:{ex.GetType().Name}>";
        }
    }

    private static string FormatRect(Rect rect)
    {
        return $"({rect.x:0.###},{rect.y:0.###},{rect.width:0.###},{rect.height:0.###})";
    }

    private static string FormatBounds(Bounds bounds)
    {
        return $"center={FormatVector3(bounds.center)},size={FormatVector3(bounds.size)}";
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.###},{value.y:0.###})";
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
    }

    private static string FormatVector2Array(Vector2[] values)
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            builder.Append(FormatVector2(values[i]));
        }

        return builder.Append(']').ToString();
    }

    private static string FormatUShortArray(ushort[] values)
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(values[i]);
        }

        return builder.Append(']').ToString();
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty);
    }

    private static void LogDiagnostic(string message)
    {
        if (_diagnosticsEnabledFast)
        {
            Plugin.Log.LogInfo($"StartupSplashDiag {message}");
        }
    }

    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        const string instanceSuffix = " (Instance)";
        return name.EndsWith(instanceSuffix, StringComparison.OrdinalIgnoreCase)
            ? name.Substring(0, name.Length - instanceSuffix.Length)
            : name;
    }
}
