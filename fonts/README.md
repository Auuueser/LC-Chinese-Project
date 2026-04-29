# fonts

放置中文 TMP 字体 AssetBundle。

默认文件名：

```text
fonts/zh-cn-tmp-font
```

AssetBundle 内建议包含一个 `TMP_FontAsset`。插件启动后会尝试加载它，并把它加入现有 TMP 字体的 fallback 列表。

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

如果使用 Noto Sans CJK SC 或 Source Han Sans SC / 思源黑体 SC，请随发布包附带 OFL 1.1 授权文本和来源说明。当前 `zh-cn-tmp-font` 由本机 `C:\Windows\Fonts\NotoSansSC-VF.ttf` 生成，发布时需要同时附带 `fonts\OFL-1.1.txt` 和 `THIRD_PARTY_LICENSES.md`。

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
