# Third Party Licenses

This project may use an external Simplified Chinese font as a TextMeshPro fallback font.

## Candidate Fonts

### Noto Sans CJK SC

- Project: Noto Fonts / Noto Sans CJK
- Publisher: Google / Noto project
- License: SIL Open Font License 1.1
- Notes: Noto documentation states that Noto fonts are licensed under the Open Font License and can be used in products and projects, but cannot be sold on their own.

### Source Han Sans SC / 思源黑体 SC

- Project: Source Han Sans
- Publisher: Adobe
- License: SIL Open Font License 1.1
- Notes: Adobe Fonts identifies Source Han Sans as available under an open source license. Use the upstream GitHub/source distribution license text when bundling font files.

## Distribution Rules

- Include the full OFL 1.1 license text when bundling a font or a generated TMP font asset based on the font.
- Do not sell the font by itself.
- Do not claim the font as original project work.
- If modifying or subsetting the font, follow OFL reserved font name requirements.
- Prefer bundling one Regular-weight Simplified Chinese fallback font only.

## Current Bundle Status

The generated runtime bundle is present at:

```text
fonts\zh-cn-tmp-font
```

It was built from the local system font:

```text
C:\Windows\Fonts\NotoSansSC-VF.ttf
```

The bundle is distributed as a TextMeshPro fallback AssetBundle, not as a standalone font product. Include this file and the full OFL text at `fonts\OFL-1.1.txt` with release packages.

## Embedded Code Attribution

### LC-FontPatcher (partial integration)

- Project: LC-FontPatcher
- Author: LeKAKiD
- Source: https://github.com/lekakid/LC-FontPatcher
- License: MIT
- Notes: LC Chinese Project embeds and adapts part of FontPatcher's runtime font fallback loading/patching behavior.
