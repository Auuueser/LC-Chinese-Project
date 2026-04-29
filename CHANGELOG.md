# Changelog

## 0.1.6 - V81 status text and lobby warning update

- Added V81 cadaver infection and air-filter status translations.
- Added configurable infection temperature display; Celsius is used by default, with Fahrenheit available through the plugin config.
- Localized the join-lobby modified-host warning and manually wrapped it to better match the vanilla tooltip layout.
- Updated release metadata and rebuilt the Thunderstore payload from the current runtime build.

## 0.1.5 - terminal confirmation input fix

- Fixed terminal screen translation rewrites so vanilla input tracking is preserved after translated terminal pages load.
- Restored confirmation shortcut behavior for terminal purchase and route confirmation pages, including `c` / `confirm` flows.
- Added static regression coverage for terminal screen rewrites and player input tracking.
- Rebuilt the Thunderstore payload from the current runtime build.

## 0.1.4 - runtime icon compatibility update

- Added compatibility handling for RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons by preserving vanilla English item keys for icon matching while translating display text separately.
- Added display-only item-name translation for drop prompts and related HUD text without mutating the underlying item definition.
- Documented that the compatibility layer does not bundle, reference, or copy third-party mod code or assets.
- Kept compatibility diagnostics commented by default to avoid repeated runtime log noise.

## 0.1.3 - v81 packaging update

- Documented the required Thunderstore zip root layout to avoid accidental `package/` nesting.
- Documented manual install and mod-manager install differences.
- Documented the resource-root requirement: runtime assets must be resolved from the `V81TestChn.dll` directory because mod managers may use nested install folders.
- Added troubleshooting notes for `TranslationService loaded 0 exact + 0 regex entries from 0 source(s).`
- Added cleanup notes for old broken-package remnants such as `BepInEx/plugins/package`.

## 0.1.2

- Added Thunderstore release metadata.
- Included the V81TestChn plugin source, translation cfg catalog, localized textures, and Chinese font fallback assets.
- Kept GameTranslator out of the runtime dependency chain.

## 0.1.0

- Created the initial BepInEx localization framework.
- Added exact-match and targeted runtime translation support.
- Added TMP Chinese font fallback loading.
- Added localized UI texture replacement support.
