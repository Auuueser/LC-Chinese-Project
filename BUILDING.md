# 构建发布包

需要 Windows x64、.NET 8 SDK、Visual Studio 2022 的 C++ 桌面开发工具（含 CMake）、Lethal Company V81 和 BepInEx 5。

将 BepInEx 的 `BepInEx.dll`、`0Harmony.dll` 放入工程根目录的 `lib/`。

在工程根目录运行：

```powershell
./scripts/build-speech-release.ps1 -GameDir 'D:\Steam\steamapps\common\Lethal Company'
```

生成的完整 ZIP 和 SHA-256 校验文件位于 `dist/`。

- 首次构建会联网下载固定版本的语音模型、依赖和许可文件，并校验 SHA-256。
- 语音工作程序源码未改且组件已备齐时，可加 `-SkipPrepare` 复用。
- 可加 `-DistDir './dist/custom-build'` 指定输出目录；已有同名 ZIP 不会被覆盖。

发布包自带 C++ 语音工作程序、离线识别引擎和模型，玩家无需联网或另装 .NET。
