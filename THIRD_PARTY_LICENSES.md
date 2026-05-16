# Third-Party Licenses And Attribution

This file records third-party content that is bundled, adapted, generated from,
or used as a reference by LC Chinese Project. The project itself is distributed
under the MIT License; see `LICENSE`.

## Bundled Or Generated Font Assets

The runtime package includes generated TextMeshPro font fallback assets under:

```text
fonts/zh-cn-tmp-font
fonts/fontpatcher/
```

These assets are distributed to provide Simplified Chinese glyph coverage in
TextMeshPro UI. They are not sold as standalone font products.

### Noto Sans CJK SC

- Project: Noto Fonts / Noto Sans CJK
- Publisher: Google / Noto project
- License: SIL Open Font License 1.1
- Source: https://github.com/notofonts/noto-cjk
- Notes: The local fallback bundle was generated from a Noto Sans SC font file.
  The full OFL text is included at `fonts/OFL-1.1.txt`.

### Source Han Sans SC / 思源黑体 SC

- Project: Source Han Sans
- Publisher: Adobe
- License: SIL Open Font License 1.1
- Source: https://github.com/adobe-fonts/source-han-sans
- Notes: Source Han Sans is listed here as the upstream family related to Noto
  Sans CJK. Follow the upstream OFL terms when generating or redistributing
  derived font assets.

## Adapted Or Referenced MIT Content

### LethalCompany_Chinese_Localized_Translation

- Project: LethalCompany_Chinese_Localized_Translation
- Source: https://github.com/CoolLKKPS/LethalCompany_Chinese_Localized_Translation
- License: MIT
- Usage: Portions of the Simplified Chinese translation catalog and selected
  localization assets were imported, reviewed, and maintained in this project.
  Runtime loading and replacement logic is implemented by LC Chinese Project.

### LC-FontPatcher

- Project: LC-FontPatcher
- Author: LeKAKiD
- Source: https://github.com/lekakid/LC-FontPatcher
- License: MIT
- Usage: FontPatcher-style runtime font bundle loading, matching, and fallback
  injection behavior is partially adapted in `EmbeddedFontPatcherService` and
  the `fonts/fontpatcher/default/` resources.

### GameTranslator

- Project: GameTranslator
- Source: https://github.com/CoolLKKPS/GameTranslator
- License: MIT
- Usage: Reviewed as a conceptual reference for localization asset workflows.
  LC Chinese Project does not bundle, reference, copy, or require GameTranslator
  runtime code.

## Compatibility References Not Bundled

The project includes compatibility handling for the following mods, but does not
bundle their code or assets:

- RuntimeIcons
- RuntimeIcons_BetterRotations
- HoneeItemIcons

Compatibility is implemented by preserving vanilla English item keys for icon
matching while translating display text separately.

## Distribution Checklist

- Include `LICENSE` for LC Chinese Project.
- Include this `THIRD_PARTY_LICENSES.md` file in GitHub and Thunderstore
  releases.
- Include the full OFL text when distributing generated TMP font assets.
- Do not distribute generated font assets as standalone font products.
- Do not claim third-party translation, font, or compatibility work as original
  project authorship.
