using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace V81TestChn;

internal static class ChatEmojiSpriteService
{
    private const string AtlasFileName = "ChatEmojiAtlas.png";
    private const int AtlasSize = 2048;
    private const int CellSize = 32;
    private const int AtlasGridSize = 64;
    private const int BindingPruneInterval = 64;
    private const int TypewriterAliasesPerBank = 10;
    private const int TypewriterBankCount = 16;
    private const int DiagnosticLogLimit = 160;
    private const string ChatOutputEmojiSeparator = "<space=0.5em>";
    private const uint TypewriterPrivateUseStart = 0xF000;
    private const uint LayoutPaddingPrivateUse = ChatEmojiCatalog.RenderPrivateUseStart + (uint)ChatEmojiCatalog.EntryCount;

    private static readonly Dictionary<int, TextBinding> TextBindings = new();
    private static readonly List<int> StaleBindingIds = new();
    private static readonly HashSet<string> DiagnosticLogKeys = new(StringComparer.Ordinal);
    private static string? _pluginDir;
    private static TMP_SpriteAsset? _spriteAsset;
    private static Texture2D? _atlasTexture;
    private static Material? _spriteMaterial;
    private static TMP_SpriteCharacter[]? _typewriterAliasCharacters;
    private static bool _loadFailed;
    private static int _bindingsSinceLastPrune;
    private static int _nextTypewriterBank;
    private static bool _diagnosticsEnabled;
    private static int _diagnosticLogCount;

    private sealed class TextBinding
    {
        public TextBinding(
            TMP_Text text,
            TMP_SpriteAsset? originalSpriteAsset,
            ITextPreprocessor? originalTextPreprocessor,
            EmojiTextPreprocessor emojiTextPreprocessor)
        {
            Text = text;
            OriginalSpriteAsset = originalSpriteAsset;
            OriginalTextPreprocessor = originalTextPreprocessor;
            EmojiTextPreprocessor = emojiTextPreprocessor;
        }

        public TMP_Text Text { get; }
        public TMP_SpriteAsset? OriginalSpriteAsset { get; }
        public ITextPreprocessor? OriginalTextPreprocessor { get; }
        public EmojiTextPreprocessor EmojiTextPreprocessor { get; }
    }

    private sealed class EmojiTextPreprocessor : ITextPreprocessor
    {
        private readonly ITextPreprocessor? _inner;
        private readonly TMP_Text _text;
        private bool _layoutDiagnosticsSubscribed;

        public bool ExpandEmojiSeparators { get; set; }

        public EmojiTextPreprocessor(TMP_Text text, ITextPreprocessor? inner)
        {
            _text = text;
            _inner = inner;
        }

        public string PreprocessText(string text)
        {
            var source = _inner?.PreprocessText(text) ?? text;
            var expandedSeparatorCount = 0;
            var rendered = ExpandEmojiSeparators
                ? ReplaceEmojiSequencesForChatOutput(source, out expandedSeparatorCount)
                : ReplaceEmojiSequencesForRendering(source);

            if (_diagnosticsEnabled && !ReferenceEquals(source, rendered))
            {
                LogPreprocessDiagnostic(_text, source, rendered, expandedSeparatorCount);
            }

            return rendered;
        }

        public void EnableLayoutDiagnostics()
        {
            if (!_diagnosticsEnabled || _layoutDiagnosticsSubscribed)
            {
                return;
            }

            _text.OnPreRenderText += OnPreRenderText;
            _layoutDiagnosticsSubscribed = true;
        }

        public void DisableLayoutDiagnostics()
        {
            if (!_layoutDiagnosticsSubscribed || _text == null)
            {
                return;
            }

            _text.OnPreRenderText -= OnPreRenderText;
            _layoutDiagnosticsSubscribed = false;
        }

        private void OnPreRenderText(TMP_TextInfo textInfo)
        {
            LogChatOutputLayoutDiagnostic(_text, textInfo);
        }
    }

    public static void Initialize(string pluginDir, ConfigFile config)
    {
        _pluginDir = pluginDir;
        _loadFailed = false;
        _diagnosticsEnabled = config.Bind(
            ConfigSections.DiagnosticsGeneral,
            "EnableEmojiDiagnostics",
            false,
            "Write a bounded set of Emoji atlas, TMP binding, preprocessing, and lookup diagnostics. Enable only while investigating display failures.").Value;
        _diagnosticLogCount = 0;
        DiagnosticLogKeys.Clear();
        if (_diagnosticsEnabled)
        {
            Plugin.Log.LogInfo($"EmojiDiag enabled; logLimit={DiagnosticLogLimit}; anchors=U+{ChatEmojiCatalog.RenderPrivateUseStart:X4}-U+{ChatEmojiCatalog.RenderPrivateUseStart + ChatEmojiCatalog.EntryCount - 1:X4}; padding=U+{LayoutPaddingPrivateUse:X4}.");
        }
    }

    public static void ApplyToHud(HUDManager? hud)
    {
        if (hud == null || Plugin.IsRuntimeShuttingDown || !EnsureSpriteAsset())
        {
            return;
        }

        PruneDestroyedBindings();
        BindText(hud.chatText, expandEmojiSeparators: true);
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

    internal static bool OwnsTextBinding(TMP_Text? text)
    {
        if (text == null || _spriteAsset == null ||
            !TextBindings.TryGetValue(text.GetInstanceID(), out var binding) ||
            !ReferenceEquals(binding.Text, text))
        {
            return false;
        }

        return ReferenceEquals(text.spriteAsset, _spriteAsset) &&
               ReferenceEquals(text.textPreprocessor, binding.EmojiTextPreprocessor);
    }

    public static void ApplyToQuickMenuLobbyHeader(QuickMenuManager? quickMenu)
    {
        if (quickMenu?.menuContainer == null || Plugin.IsRuntimeShuttingDown)
        {
            return;
        }

        var header = quickMenu.menuContainer.transform.Find("PlayerList/Image/Header");
        if (header != null)
        {
            ApplyToText(header.GetComponentInChildren<TMP_Text>(true));
        }
    }

    public static void OnSceneUnloaded()
    {
        PruneDestroyedBindings();
    }

    public static string EncodeForSignalTranslatorTypewriter(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            _spriteAsset == null ||
            _typewriterAliasCharacters == null ||
            _typewriterAliasCharacters.Length != TypewriterAliasesPerBank * TypewriterBankCount)
        {
            return value ?? string.Empty;
        }

        var bank = _nextTypewriterBank++ % TypewriterBankCount;
        var bankStart = bank * TypewriterAliasesPerBank;
        var aliasCount = 0;
        StringBuilder? builder = null;
        var copyFrom = 0;
        for (var index = 0; index < value.Length;)
        {
            if (aliasCount < TypewriterAliasesPerBank &&
                ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out var glyphIndex))
            {
                var aliasIndex = bankStart + aliasCount++;
                var aliasCharacter = _typewriterAliasCharacters[aliasIndex];
                aliasCharacter.glyph = _spriteAsset.spriteGlyphTable[glyphIndex];
                // TMP resolves a raw sprite Unicode to character.glyphIndex and
                // later uses that value as the sprite-character table index.
                // Updating only glyph leaves every alias at its initial index 0.
                aliasCharacter.glyphIndex = (uint)glyphIndex;
                builder ??= new StringBuilder(value.Length);
                builder.Append(value, copyFrom, index - copyFrom);
                builder.Append((char)(TypewriterPrivateUseStart + (uint)aliasIndex));
                index += consumedLength;
                copyFrom = index;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }

        if (builder == null)
        {
            return value;
        }

        builder.Append(value, copyFrom, value.Length - copyFrom);
        var encoded = builder.ToString();
        if (_diagnosticsEnabled)
        {
            LogTypewriterDiagnostic(value, encoded, bank, aliasCount);
        }

        return encoded;
    }

    public static void Shutdown()
    {
        foreach (var binding in TextBindings.Values)
        {
            var text = binding.Text;
            binding.EmojiTextPreprocessor.DisableLayoutDiagnostics();
            if (text != null && ReferenceEquals(text.spriteAsset, _spriteAsset))
            {
                text.spriteAsset = binding.OriginalSpriteAsset;
            }

            if (text != null && ReferenceEquals(text.textPreprocessor, binding.EmojiTextPreprocessor))
            {
                text.textPreprocessor = binding.OriginalTextPreprocessor;
            }
        }

        TextBindings.Clear();
        StaleBindingIds.Clear();
        _bindingsSinceLastPrune = 0;
        _nextTypewriterBank = 0;
        _typewriterAliasCharacters = null;
        _diagnosticsEnabled = false;
        _diagnosticLogCount = 0;
        DiagnosticLogKeys.Clear();

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

            for (var index = 0; index < ChatEmojiCatalog.EntryCount; index++)
            {
                var name = $"emoji_{index}";
                var column = index % AtlasGridSize;
                var rowFromTop = index / AtlasGridSize;
                var x = column * CellSize;
                var y = AtlasSize - ((rowFromTop + 1) * CellSize);
                var rect = new GlyphRect(x, y, CellSize, CellSize);
                var metrics = new GlyphMetrics(CellSize, CellSize, 0f, CellSize, CellSize);

                // TMP renders sprite glyphs from the atlas rect and material;
                // a Unity Sprite object per glyph is unnecessary runtime state.
                var glyph = new TMP_SpriteGlyph((uint)index, metrics, rect, 1f, 0, null!);
                var character = new TMP_SpriteCharacter(
                    ChatEmojiCatalog.RenderPrivateUseStart + (uint)index,
                    spriteAsset,
                    glyph)
                {
                    name = name
                };
                spriteAsset.spriteGlyphTable.Add(glyph);
                spriteAsset.spriteCharacterTable.Add(character);
            }

            // Preserve the raw UTF-16 string indexes used by TMP_InputField. Each
            // render anchor is one BMP code unit; the rest of the source sequence
            // is represented by zero-width, zero-advance padding characters. TMP
            // divides by the sprite metric height while generating its mesh, so
            // the invisible padding must retain a non-zero height.
            var paddingGlyphIndex = (uint)spriteAsset.spriteGlyphTable.Count;
            var paddingGlyph = new TMP_SpriteGlyph(
                paddingGlyphIndex,
                new GlyphMetrics(0f, CellSize, 0f, CellSize, 0f),
                new GlyphRect(0, 0, 0, 0),
                1f,
                0,
                null!);
            var paddingCharacter = new TMP_SpriteCharacter(
                LayoutPaddingPrivateUse,
                spriteAsset,
                paddingGlyph)
            {
                name = "emoji_layout_padding"
            };
            spriteAsset.spriteGlyphTable.Add(paddingGlyph);
            spriteAsset.spriteCharacterTable.Add(paddingCharacter);

            // TMP's generator uses glyphIndex as a character-table index in this
            // game build. Base anchors and the padding entry therefore remain
            // one-to-one with glyphs. Signal messages reuse rotating BMP aliases
            // whose glyph references are updated before the coroutine runs.
            _typewriterAliasCharacters = new TMP_SpriteCharacter[TypewriterAliasesPerBank * TypewriterBankCount];
            var initialGlyph = spriteAsset.spriteGlyphTable[0];
            for (var aliasIndex = 0; aliasIndex < _typewriterAliasCharacters.Length; aliasIndex++)
            {
                var typewriterCharacter = new TMP_SpriteCharacter(
                    TypewriterPrivateUseStart + (uint)aliasIndex,
                    spriteAsset,
                    initialGlyph)
                {
                    name = $"emoji_typewriter_{aliasIndex}"
                };
                _typewriterAliasCharacters[aliasIndex] = typewriterCharacter;
                spriteAsset.spriteCharacterTable.Add(typewriterCharacter);
            }

            spriteAsset.UpdateLookupTables();
            Plugin.Log.LogInfo($"Loaded scoped HUD emoji atlas: {ChatEmojiCatalog.EntryCount} glyphs, {AtlasSize}x{AtlasSize}.");
            LogSpriteAssetDiagnostic(spriteAsset, texture, material);
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

    private static void BindText(TMP_Text? text, bool expandEmojiSeparators = false)
    {
        if (text == null || _spriteAsset == null)
        {
            return;
        }

        var id = text.GetInstanceID();
        if (!TextBindings.TryGetValue(id, out var binding))
        {
            var original = text.spriteAsset;
            var originalPreprocessor = text.textPreprocessor;
            var emojiPreprocessor = new EmojiTextPreprocessor(text, originalPreprocessor);
            binding = new TextBinding(text, original, originalPreprocessor, emojiPreprocessor);
            TextBindings.Add(id, binding);
            if (original != null &&
                !ReferenceEquals(original, _spriteAsset) &&
                !_spriteAsset.fallbackSpriteAssets.Contains(original))
            {
                _spriteAsset.fallbackSpriteAssets.Add(original);
            }
        }

        if (expandEmojiSeparators)
        {
            binding.EmojiTextPreprocessor.ExpandEmojiSeparators = true;
            binding.EmojiTextPreprocessor.EnableLayoutDiagnostics();
        }

        text.spriteAsset = _spriteAsset;
        if (!ReferenceEquals(text.textPreprocessor, binding.EmojiTextPreprocessor))
        {
            text.textPreprocessor = binding.EmojiTextPreprocessor;
        }

        LogTextBindingDiagnostic(text, binding);
    }

    private static void LogSpriteAssetDiagnostic(TMP_SpriteAsset spriteAsset, Texture2D texture, Material material)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var probes = new[] { 0, 142, 336, 2555, ChatEmojiCatalog.EntryCount - 1 };
        var builder = new StringBuilder(384);
        foreach (var glyphIndex in probes)
        {
            var unicode = ChatEmojiCatalog.RenderPrivateUseStart + (uint)glyphIndex;
            var lookup = spriteAsset.GetSpriteIndexFromUnicode(unicode);
            var character = lookup >= 0 && lookup < spriteAsset.spriteCharacterTable.Count
                ? spriteAsset.spriteCharacterTable[lookup]
                : null;
            builder.Append($" U+{unicode:X4}->lookup={lookup},charGlyph={(character == null ? -1 : (int)character.glyphIndex)};");
        }

        var paddingLookup = spriteAsset.GetSpriteIndexFromUnicode(LayoutPaddingPrivateUse);
        LogDiagnostic(
            "asset",
            $"assetId={spriteAsset.GetInstanceID()} glyphs={spriteAsset.spriteGlyphTable.Count} chars={spriteAsset.spriteCharacterTable.Count} lookupChars={spriteAsset.spriteCharacterLookupTable.Count} paddingLookup={paddingLookup} texture={texture.name}#{texture.GetInstanceID()}:{texture.width}x{texture.height} material={material.name}#{material.GetInstanceID()} shader={material.shader?.name ?? "<null>"} mainTexture={material.mainTexture?.name ?? "<null>"}; probes:{builder}");
    }

    private static void LogTextBindingDiagnostic(TMP_Text text, TextBinding binding)
    {
        if (!_diagnosticsEnabled)
        {
            return;
        }

        var source = text.text ?? string.Empty;
        var hasEmoji = ChatEmojiPasteService.ContainsSupportedEmoji(source);
        var key = hasEmoji
            ? $"bind-emoji:{text.GetInstanceID()}:{source.GetHashCode()}"
            : $"bind:{text.GetInstanceID()}";
        LogDiagnostic(
            key,
            $"bind path={BuildPath(text.transform)} textId={text.GetInstanceID()} active={text.gameObject.activeInHierarchy} enabled={text.enabled} sourceHasEmoji={hasEmoji} sourceUnits={FormatCodeUnits(source)} sprite={DescribeObject(text.spriteAsset)} ownedSprite={ReferenceEquals(text.spriteAsset, _spriteAsset)} preprocessor={text.textPreprocessor?.GetType().FullName ?? "<null>"} ownedPreprocessor={ReferenceEquals(text.textPreprocessor, binding.EmojiTextPreprocessor)} font={DescribeObject(text.font)} textInfoChars={text.textInfo?.characterCount ?? -1}");
    }

    private static void LogPreprocessDiagnostic(
        TMP_Text text,
        string source,
        string rendered,
        int expandedSeparatorCount)
    {
        var firstIndex = FindFirstEmoji(source, out var consumedLength, out var glyphIndex);
        var renderedUnicode = firstIndex >= 0 && firstIndex < rendered.Length ? rendered[firstIndex] : '\0';
        var assignedAsset = text.spriteAsset;
        var lookup = assignedAsset == null || renderedUnicode == '\0'
            ? -1
            : assignedAsset.GetSpriteIndexFromUnicode(renderedUnicode);
        var glyph = lookup >= 0 && assignedAsset != null && lookup < assignedAsset.spriteGlyphTable.Count
            ? assignedAsset.spriteGlyphTable[lookup]
            : null;
        LogDiagnostic(
            $"pre:{text.GetInstanceID()}:{source.GetHashCode()}",
            $"preprocess path={BuildPath(text.transform)} textId={text.GetInstanceID()} firstIndex={firstIndex} consumed={consumedLength} glyph={glyphIndex} rendered=U+{(int)renderedUnicode:X4} lookup={lookup} glyphRect={(glyph == null ? "<null>" : glyph.glyphRect.ToString())} sourceLen={source.Length} renderedLen={rendered.Length} expandedSeparators={expandedSeparatorCount} hasChatGapTag={rendered.Contains(ChatOutputEmojiSeparator)} sourceUnits={FormatCodeUnits(source)} renderedUnits={FormatCodeUnits(rendered)} sprite={DescribeObject(assignedAsset)} ownedSprite={ReferenceEquals(assignedAsset, _spriteAsset)} ownedPreprocessor={text.textPreprocessor is EmojiTextPreprocessor} active={text.gameObject.activeInHierarchy} textInfoChars={text.textInfo?.characterCount ?? -1}");
    }

    private static void LogChatOutputLayoutDiagnostic(TMP_Text text, TMP_TextInfo textInfo)
    {
        if (!_diagnosticsEnabled ||
            _diagnosticLogCount >= DiagnosticLogLimit ||
            text == null ||
            textInfo == null)
        {
            return;
        }

        var source = text.text ?? string.Empty;
        if (!ChatEmojiPasteService.ContainsSupportedEmoji(source))
        {
            return;
        }

        var key = $"chat-layout:{text.GetInstanceID()}:{source.GetHashCode()}";
        if (DiagnosticLogKeys.Contains(key))
        {
            return;
        }

        var builder = new StringBuilder(640);
        var anchorCount = 0;
        var hasPreviousAnchor = false;
        TMP_CharacterInfo previousAnchor = default;
        for (var index = 0; index < textInfo.characterCount; index++)
        {
            var info = textInfo.characterInfo[index];
            var unicode = info.character;
            if (unicode < ChatEmojiCatalog.RenderPrivateUseStart ||
                unicode >= ChatEmojiCatalog.RenderPrivateUseStart + (uint)ChatEmojiCatalog.EntryCount)
            {
                continue;
            }

            if (hasPreviousAnchor && anchorCount <= 16)
            {
                builder.Append(
                    $" pair{anchorCount}=U+{(int)previousAnchor.character:X4}->U+{(int)unicode:X4}" +
                    $":prevAdvance={previousAnchor.xAdvance:0.###}" +
                    $":nextOrigin={info.origin:0.###}" +
                    $":gap={info.origin - previousAnchor.xAdvance:0.###}" +
                    $":lines={previousAnchor.lineNumber}/{info.lineNumber};");
            }

            previousAnchor = info;
            hasPreviousAnchor = true;
            anchorCount++;
        }

        LogDiagnostic(
            key,
            $"chat-layout path={BuildPath(text.transform)} textId={text.GetInstanceID()} sourceLen={source.Length} textInfoChars={textInfo.characterCount} anchors={anchorCount} font={DescribeObject(text.font)} fontSize={text.fontSize:0.###} wordSpacing={text.wordSpacing:0.###} characterSpacing={text.characterSpacing:0.###} richText={text.richText} pairs={builder}");
    }

    private static void LogTypewriterDiagnostic(string source, string encoded, int bank, int aliasCount)
    {
        var firstAlias = '\0';
        foreach (var ch in encoded)
        {
            if (ch >= TypewriterPrivateUseStart &&
                ch < TypewriterPrivateUseStart + TypewriterAliasesPerBank * TypewriterBankCount)
            {
                firstAlias = ch;
                break;
            }
        }

        var lookup = firstAlias == '\0' || _spriteAsset == null ? -1 : _spriteAsset.GetSpriteIndexFromUnicode(firstAlias);
        LogDiagnostic(
            $"signal:{source.GetHashCode()}:{bank}",
            $"typewriter bank={bank} aliases={aliasCount} firstAlias=U+{(int)firstAlias:X4} lookup={lookup} sourceUnits={FormatCodeUnits(source)} encodedUnits={FormatCodeUnits(encoded)} asset={DescribeObject(_spriteAsset)}");
    }

    private static int FindFirstEmoji(string value, out int consumedLength, out int glyphIndex)
    {
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out consumedLength, out glyphIndex))
            {
                return index;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }

        consumedLength = 0;
        glyphIndex = -1;
        return -1;
    }

    private static void LogDiagnostic(string key, string message)
    {
        if (!_diagnosticsEnabled ||
            _diagnosticLogCount >= DiagnosticLogLimit ||
            !DiagnosticLogKeys.Add(key))
        {
            return;
        }

        _diagnosticLogCount++;
        Plugin.Log.LogInfo($"EmojiDiag[{_diagnosticLogCount}/{DiagnosticLogLimit}] {message}");
    }

    private static string FormatCodeUnits(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }

        const int limit = 48;
        var builder = new StringBuilder(Math.Min(value.Length, limit) * 6);
        for (var index = 0; index < value.Length && index < limit; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append($"{(int)value[index]:X4}");
        }

        if (value.Length > limit)
        {
            builder.Append($",...(+{value.Length - limit})");
        }

        return builder.ToString();
    }

    private static string DescribeObject(UnityEngine.Object? value)
    {
        return value == null ? "<null>" : $"{value.name}#{value.GetInstanceID()}";
    }

    private static string BuildPath(Transform? transform)
    {
        if (transform == null)
        {
            return "<null>";
        }

        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string ReplaceEmojiSequencesForRendering(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        StringBuilder? builder = null;
        var copyFrom = 0;
        for (var index = 0; index < value.Length;)
        {
            if (ChatEmojiCatalog.TryMatchLongest(value, index, out var consumedLength, out var glyphIndex))
            {
                builder ??= new StringBuilder(value.Length);
                builder.Append(value, copyFrom, index - copyFrom);
                builder.Append((char)(ChatEmojiCatalog.RenderPrivateUseStart + (uint)glyphIndex));

                for (var paddingIndex = 1; paddingIndex < consumedLength; paddingIndex++)
                {
                    builder.Append((char)LayoutPaddingPrivateUse);
                }

                index += consumedLength;
                copyFrom = index;
                continue;
            }

            index += ChatEmojiCatalog.ReadCodePoint(value, index, out _);
        }

        if (builder == null)
        {
            return value;
        }

        builder.Append(value, copyFrom, value.Length - copyFrom);
        return builder.ToString();
    }

    private static string ReplaceEmojiSequencesForChatOutput(
        string source,
        out int expandedSeparatorCount)
    {
        expandedSeparatorCount = 0;
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        StringBuilder? builder = null;
        var copyFrom = 0;
        for (var index = 0; index < source.Length;)
        {
            if (!ChatEmojiCatalog.TryMatchLongest(source, index, out var consumedLength, out var glyphIndex))
            {
                index += ChatEmojiCatalog.ReadCodePoint(source, index, out _);
                continue;
            }

            builder ??= new StringBuilder(source.Length + 32);
            builder.Append(source, copyFrom, index - copyFrom);
            builder.Append((char)(ChatEmojiCatalog.RenderPrivateUseStart + (uint)glyphIndex));

            var gapStart = index + consumedLength;
            var nextEmoji = gapStart;
            while (nextEmoji < source.Length && source[nextEmoji] == ' ')
            {
                nextEmoji++;
            }

            if (nextEmoji > gapStart &&
                ChatEmojiCatalog.TryMatchLongest(source, nextEmoji, out _, out _))
            {
                for (var gapIndex = gapStart; gapIndex < nextEmoji; gapIndex++)
                {
                    builder.Append(ChatOutputEmojiSeparator);
                    expandedSeparatorCount++;
                }

                index = nextEmoji;
                copyFrom = nextEmoji;
                continue;
            }

            index = gapStart;
            copyFrom = gapStart;
        }

        if (builder == null)
        {
            return source;
        }

        builder.Append(source, copyFrom, source.Length - copyFrom);
        return builder.ToString();
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
