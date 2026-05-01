# Changelog

## 0.1.7 - Signal Translator HUD update

- Added a cached and throttled Signal Translator HUD localization path to reduce repeated runtime text hierarchy traversal.
- Enlarged the localized receiving-signal prompt while preserving original font sizes for later signal text.
- Added or completed recent Cruiser, ship magnet, Signal Translator, and order-status terminal translations.
- Updated the packaged `V81TestChn.dll`, manifest, and release resources from the current runtime build.

## 0.1.6 - V81 status text and lobby warning update

- Added translations for V81 cadaver infection and air-filter status messages.
- Added a configurable infection temperature unit. Celsius is the default; Fahrenheit can be selected in the generated plugin config.
- Localized the join-lobby modified-host warning and adjusted its line breaks for cleaner in-game layout.
- Updated the packaged `V81TestChn.dll`, manifest, and release resources from the current runtime build.

## 0.1.5 - terminal confirmation input fix

- Fixed translated terminal page rewrites so vanilla terminal input tracking is preserved.
- Restored `c` / `confirm` behavior on translated terminal purchase and route confirmation pages, including cruiser purchase confirmation.
- Updated the packaged `V81TestChn.dll` and release resources from the current runtime build.
- Updated installation and troubleshooting notes for resource-root and package-layout issues.

## 0.1.4 - runtime icon compatibility update

- Added compatibility handling for RuntimeIcons, RuntimeIcons_BetterRotations, and HoneeItemIcons by preserving vanilla English item keys for icon matching while translating display text separately.
- Added display-only item-name translation for drop prompts and related HUD text without mutating the underlying item definition.
- Updated the packaged `V81TestChn.dll` to the compatibility build.
- Documented that this package does not bundle, reference, or copy third-party icon mod code or assets.
- Kept compatibility diagnostics commented by default to avoid repeated runtime log noise.

## 0.1.3 - v81 packaging update

- Changed the release layout so `thunderstore/` is the pre-zip release root.
- Moved build and validation scripts out of the release root into `thunderstore-tools/`.
- Updated runtime resource lookup to load assets from the `V81TestChn.dll` directory, improving compatibility with mod-manager nested install paths.
- Added release validation to prevent common broken layouts such as an extra `package/` folder or `BepInEx/plugins/package`.
- Added installation notes for mod-manager installs, manual installs, and old broken-package remnants.

## 0.1.2

- Added a Thunderstore-ready package layout.
- Updated the release package to use the standalone LC Chinese Project name.
- Included the current V81TestChn plugin build output, translation catalog, localized textures, and font fallback assets.
- Kept GameTranslator out of the runtime dependency chain.

## 0.1.0

- Created the initial BepInEx localization framework.
- Added exact-match and targeted runtime translation support.
- Added TMP Chinese font fallback loading.
- Added localized UI texture replacement support.
