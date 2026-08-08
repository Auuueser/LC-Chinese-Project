# Third-Party Licenses And Attribution

This file records third-party content that is bundled or generated into LC Chinese Project releases. LC Chinese Project itself is distributed under the MIT License; see `LICENSE`.

## Bundled Or Generated Font Resources

The runtime package includes the original font and generated TextMeshPro fallback under:

```text
fonts/NotoSansSC-VF.ttf
fonts/zh-cn-tmp-font
```

The original TTF is loaded dynamically at runtime to avoid Unity AssetBundle version coupling. The generated AssetBundle remains only as a final compatibility fallback. These assets provide Simplified Chinese glyph coverage in TextMeshPro UI and are not distributed as standalone font products.

### Noto Sans CJK SC

- Project: Noto Fonts / Noto Sans CJK
- Publisher: Google / Noto project
- License: SIL Open Font License 1.1
- Source: https://github.com/notofonts/noto-cjk
- Notes: The package includes `NotoSansSC-VF.ttf` (SHA-256 `763146584CF0710223441356B4395E279021B0806C196614377A7A0174AE074A`) and a fallback bundle generated from it. The full OFL text is included at `fonts/OFL-1.1.txt`.

### Source Han Sans SC / 思源黑体 SC

- Project: Source Han Sans
- Publisher: Adobe
- License: SIL Open Font License 1.1
- Source: https://github.com/adobe-fonts/source-han-sans
- Notes: Source Han Sans is listed as the upstream family related to Noto Sans CJK. Follow the upstream OFL terms when generating or redistributing derived font assets.

## Bundled Emoji Graphics

### Noto Emoji

- Project: Noto Emoji
- Publisher: Google / Noto project
- License: Apache License 2.0 for the PNG image resources used by this project
- Source: https://github.com/googlefonts/noto-emoji/tree/main/png/128
- Notes: The pinned Noto sources for 3,943 supported fully-qualified Unicode Emoji 17.0 entries are reduced into `ChatEmojiAtlas.png` for supported TextMeshPro fields, including chat and terminal displays. The primary artwork is pinned to commit `8998f5dd683424a73e2314a8c1f1e359c19e8742`; twenty-five legacy gendered family sequences use colorful pre-2023 artwork pinned to commit `546287c772cd64dd2a7350a0727bd68c2e4cf759` instead of the later monochrome plaque redesign. Project-denied sequences are excluded before source download, catalog generation and atlas composition. The atlas is not used as a standalone image product and is loaded only for supported UI instances. The full Apache License 2.0 text is included at `licenses/Apache-2.0.txt`.

### Noto Region Flags

- Project: Noto Emoji third-party region flags
- Source: https://github.com/googlefonts/noto-emoji/tree/main/third_party/region-flags
- License status: Public Domain or otherwise copyright-exempt according to the upstream per-flag audit
- Notes: Country and subdivision flag images used by the complete Emoji atlas come from this pinned upstream directory. The upstream notice and exceptions list are included at `licenses/Noto-region-flags-LICENSE.txt`.

## Generated Texture Assets

The runtime package includes project-owned PNG UI textures under:

```text
assets/textures/zh-CN/Texture
```

These localized textures are project-owned assets created from original drawing instructions and Chinese UI wording. Noto Sans SC is used for glyph rasterization; the full OFL text is included at `fonts/OFL-1.1.txt`.

## Compatibility References Not Bundled

The project includes compatibility handling for the following mods, but does not bundle their code or assets:

- RuntimeIcons
- RuntimeIcons_BetterRotations
- HoneeItemIcons

Compatibility is implemented by preserving vanilla English item keys for icon matching while translating display text separately.

## Distribution Checklist

- Include `LICENSE` for LC Chinese Project.
- Include this `THIRD_PARTY_LICENSES.md` file in GitHub and Thunderstore releases.
- Include the full OFL text when distributing the original font or generated TMP font assets.
- Do not distribute generated font assets as standalone font products.
- Do not claim third-party translation, font, or compatibility work as original project authorship.
