# fonts

放置中文原始字体和旧版 TMP 字体 AssetBundle 兜底资源。

运行时加载顺序：

```text
fonts/NotoSansSC-VF.ttf
系统中文字体
fonts/zh-cn-tmp-font（旧版最终兜底）
```

插件优先从随包 TTF 动态创建 `TMP_FontAsset`，避免 AssetBundle 与 Unity 运行时版本耦合。候选字体必须通过代表性简体中文字形验证才会注册为 fallback。

## 字符集

运行：

```text
python tools\extract_font_charset.py
```

会生成：

```text
fonts\zh-cn-charset.txt
fonts\zh-cn-charset-report.md
```

`zh-cn-charset.txt` 用于 Unity TextMeshPro Font Asset Creator 的 Custom Characters。当前字符集从 `translations\zh-CN.json` 提取，并额外包含 ASCII 与常用中文标点。

## 授权

随包 `NotoSansSC-VF.ttf` 及旧 `zh-cn-tmp-font` 均源自 Noto Sans SC，采用 SIL Open Font License 1.1。发布时必须同时附带 `fonts\OFL-1.1.txt` 和 `THIRD_PARTY_LICENSES.md`。

当前原始字体：

```text
fonts/NotoSansSC-VF.ttf
SHA-256: 763146584CF0710223441356B4395E279021B0806C196614377A7A0174AE074A
```

详细流程见：

```text
docs\font-assetbundle-guide.md
THIRD_PARTY_LICENSES.md
```

生成 AssetBundle 后运行：

```text
python tools\validate_font_bundle.py
```

当前仓库已生成 `zh-cn-tmp-font` AssetBundle，并已通过 `tools\validate_font_bundle.py` 校验。
