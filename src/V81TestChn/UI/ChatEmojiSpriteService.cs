using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace V81TestChn;

internal static class ChatEmojiSpriteService
{
    private const string AtlasFileName = "ChatEmojiAtlas.png";
    private const int AtlasSize = 1024;
    private const int CellSize = 64;
    private const int BindingPruneInterval = 64;
    private const uint TypewriterPrivateUseStart = 0xE000;

    private static readonly Dictionary<int, TextBinding> TextBindings = new();
    private static readonly List<int> StaleBindingIds = new();
    private static string? _pluginDir;
    private static TMP_SpriteAsset? _spriteAsset;
    private static Texture2D? _atlasTexture;
    private static Material? _spriteMaterial;
    private static bool _loadFailed;
    private static int _bindingsSinceLastPrune;

    private readonly struct TextBinding
    {
        public TextBinding(TMP_Text text, TMP_SpriteAsset? originalSpriteAsset)
        {
            Text = text;
            OriginalSpriteAsset = originalSpriteAsset;
        }

        public TMP_Text Text { get; }
        public TMP_SpriteAsset? OriginalSpriteAsset { get; }
    }

    public static void Initialize(string pluginDir)
    {
        _pluginDir = pluginDir;
        _loadFailed = false;
    }

    public static void ApplyToHud(HUDManager? hud)
    {
        if (hud == null || Plugin.IsRuntimeShuttingDown || !EnsureSpriteAsset())
        {
            return;
        }

        PruneDestroyedBindings();
        BindText(hud.chatText);
        BindText(hud.chatTextField?.textComponent);
        BindText(hud.signalTranslatorText);
    }

    public static void ApplyToSignalTranslator(HUDManager? hud)
    {
        if (hud == null || Plugin.IsRuntimeShuttingDown || !EnsureSpriteAsset())
        {
            return;
        }

        PruneDestroyedBindings();
        BindText(hud.signalTranslatorText);
    }

    public static void ApplyToText(TMP_Text? text)
    {
        if (text == null || Plugin.IsRuntimeShuttingDown || !EnsureSpriteAsset())
        {
            return;
        }

        BindText(text);
        if (++_bindingsSinceLastPrune >= BindingPruneInterval)
        {
            PruneDestroyedBindings();
        }
    }

    public static void OnSceneUnloaded()
    {
        PruneDestroyedBindings();
    }

    public static string EncodeForSignalTranslatorTypewriter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        StringBuilder? builder = null;
        var copyFrom = 0;
        for (var index = 0; index + 1 < value.Length; index++)
        {
            if (!char.IsHighSurrogate(value[index]) || !char.IsLowSurrogate(value[index + 1]))
            {
                continue;
            }

            var unicode = (uint)char.ConvertToUtf32(value[index], value[index + 1]);
            var emojiIndex = Array.IndexOf(ChatEmojiCatalog.UnicodeValues, unicode);
            if (emojiIndex < 0)
            {
                index++;
                continue;
            }

            builder ??= new StringBuilder(value.Length);
            builder.Append(value, copyFrom, index - copyFrom);
            builder.Append((char)(TypewriterPrivateUseStart + (uint)emojiIndex));
            index++;
            copyFrom = index + 1;
        }

        if (builder == null)
        {
            return value;
        }

        builder.Append(value, copyFrom, value.Length - copyFrom);
        return builder.ToString();
    }

    public static void Shutdown()
    {
        foreach (var binding in TextBindings.Values)
        {
            var text = binding.Text;
            if (text != null && ReferenceEquals(text.spriteAsset, _spriteAsset))
            {
                text.spriteAsset = binding.OriginalSpriteAsset;
            }
        }

        TextBindings.Clear();
        StaleBindingIds.Clear();
        _bindingsSinceLastPrune = 0;

        if (_spriteAsset != null)
        {
            foreach (var glyph in _spriteAsset.spriteGlyphTable)
            {
                if (glyph?.sprite != null)
                {
                    UnityEngine.Object.Destroy(glyph.sprite);
                }
            }

            UnityEngine.Object.Destroy(_spriteAsset);
            _spriteAsset = null;
        }

        if (_spriteMaterial != null)
        {
            UnityEngine.Object.Destroy(_spriteMaterial);
            _spriteMaterial = null;
        }

        if (_atlasTexture != null)
        {
            UnityEngine.Object.Destroy(_atlasTexture);
            _atlasTexture = null;
        }

        _pluginDir = null;
        _loadFailed = false;
    }

    private static bool EnsureSpriteAsset()
    {
        if (_spriteAsset != null)
        {
            return true;
        }

        if (_loadFailed || string.IsNullOrEmpty(_pluginDir))
        {
            return false;
        }

        var atlasPath = Path.Combine(_pluginDir, "V81TestChn", "textures", AtlasFileName);
        if (!File.Exists(atlasPath))
        {
            atlasPath = Path.Combine(_pluginDir, "textures", AtlasFileName);
        }

        if (!File.Exists(atlasPath))
        {
            _loadFailed = true;
            Plugin.Log.LogWarning($"Chat emoji atlas not found: {atlasPath}");
            return false;
        }

        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "V81TestChn_ChatEmojiAtlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!texture.LoadImage(File.ReadAllBytes(atlasPath), true) ||
                texture.width != AtlasSize ||
                texture.height != AtlasSize)
            {
                UnityEngine.Object.Destroy(texture);
                _loadFailed = true;
                Plugin.Log.LogWarning($"Chat emoji atlas must be {AtlasSize}x{AtlasSize}: {atlasPath}");
                return false;
            }

            var shader = Shader.Find("TextMeshPro/Sprite");
            if (shader == null)
            {
                UnityEngine.Object.Destroy(texture);
                _loadFailed = true;
                Plugin.Log.LogWarning("TextMeshPro/Sprite shader was not found; chat emoji support is disabled.");
                return false;
            }

            var material = new Material(shader)
            {
                name = "V81TestChn_ChatEmojiMaterial",
                mainTexture = texture,
                hideFlags = HideFlags.HideAndDontSave
            };

            _atlasTexture = texture;
            _spriteMaterial = material;

            var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            spriteAsset.name = "V81TestChn_ChatEmojiSpriteAsset";
            spriteAsset.hideFlags = HideFlags.HideAndDontSave;
            spriteAsset.spriteSheet = texture;
            spriteAsset.material = material;
            spriteAsset.spriteInfoList = new List<TMP_Sprite>();
            spriteAsset.fallbackSpriteAssets = new List<TMP_SpriteAsset>();
            _spriteAsset = spriteAsset;

            // A runtime-created TMP_SpriteAsset has no legacy spriteInfoList.
            // Prime the current lookup-table format while the list is empty;
            // otherwise TMP tries to upgrade a null legacy list on first access.
            spriteAsset.UpdateLookupTables();

            var typewriterCharacters = new List<TMP_SpriteCharacter>(ChatEmojiCatalog.UnicodeValues.Length);
            for (var index = 0; index < ChatEmojiCatalog.UnicodeValues.Length; index++)
            {
                var unicode = ChatEmojiCatalog.UnicodeValues[index];
                var name = $"emoji_u{unicode:x}";
                var column = index % 16;
                var rowFromTop = index / 16;
                var x = column * CellSize;
                var y = AtlasSize - ((rowFromTop + 1) * CellSize);
                var rect = new GlyphRect(x, y, CellSize, CellSize);
                var metrics = new GlyphMetrics(CellSize, CellSize, 0f, CellSize, CellSize);
                var sprite = Sprite.Create(
                    texture,
                    new Rect(x, y, CellSize, CellSize),
                    new Vector2(0.5f, 0.5f),
                    CellSize,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = name;

                var glyph = new TMP_SpriteGlyph((uint)index, metrics, rect, 1f, 0, sprite);
                var character = new TMP_SpriteCharacter(unicode, spriteAsset, glyph)
                {
                    name = name
                };
                var typewriterCharacter = new TMP_SpriteCharacter(TypewriterPrivateUseStart + (uint)index, spriteAsset, glyph)
                {
                    name = $"{name}_typewriter"
                };
                spriteAsset.spriteGlyphTable.Add(glyph);
                spriteAsset.spriteCharacterTable.Add(character);
                typewriterCharacters.Add(typewriterCharacter);
            }

            // TMP returns glyphIndex as a sprite-character table index while
            // generating text. Keep the first 256 character entries aligned
            // one-to-one with their glyph indices; aliases must come after them.
            foreach (var typewriterCharacter in typewriterCharacters)
            {
                spriteAsset.spriteCharacterTable.Add(typewriterCharacter);
            }

            spriteAsset.UpdateLookupTables();
            Plugin.Log.LogInfo($"Loaded scoped HUD emoji atlas: {ChatEmojiCatalog.UnicodeValues.Length} glyphs, {AtlasSize}x{AtlasSize}.");
            return true;
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            Plugin.Log.LogWarning($"Chat emoji atlas loading failed: {ex.GetType().Name}: {ex.Message}");
            Shutdown();
            _loadFailed = true;
            return false;
        }
    }

    private static void BindText(TMP_Text? text)
    {
        if (text == null || _spriteAsset == null || ReferenceEquals(text.spriteAsset, _spriteAsset))
        {
            return;
        }

        var id = text.GetInstanceID();
        if (!TextBindings.ContainsKey(id))
        {
            var original = text.spriteAsset;
            TextBindings.Add(id, new TextBinding(text, original));
            if (original != null &&
                !ReferenceEquals(original, _spriteAsset) &&
                !_spriteAsset.fallbackSpriteAssets.Contains(original))
            {
                _spriteAsset.fallbackSpriteAssets.Add(original);
            }
        }

        text.spriteAsset = _spriteAsset;
    }

    private static void PruneDestroyedBindings()
    {
        _bindingsSinceLastPrune = 0;

        if (TextBindings.Count == 0)
        {
            _spriteAsset?.fallbackSpriteAssets?.Clear();
            return;
        }

        StaleBindingIds.Clear();
        foreach (var pair in TextBindings)
        {
            if (pair.Value.Text != null)
            {
                continue;
            }

            StaleBindingIds.Add(pair.Key);
        }

        foreach (var staleId in StaleBindingIds)
        {
            TextBindings.Remove(staleId);
        }

        StaleBindingIds.Clear();
        var fallbackAssets = _spriteAsset?.fallbackSpriteAssets;
        if (fallbackAssets == null)
        {
            return;
        }

        fallbackAssets.Clear();
        foreach (var binding in TextBindings.Values)
        {
            var original = binding.OriginalSpriteAsset;
            if (original != null &&
                !ReferenceEquals(original, _spriteAsset) &&
                !fallbackAssets.Contains(original))
            {
                fallbackAssets.Add(original);
            }
        }
    }
}
