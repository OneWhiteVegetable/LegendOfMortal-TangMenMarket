# 开发与打包

以下命令在含 `XingShangMod.csproj` 的仓库根目录执行。插件以 C# 7.3 / .NET Standard 2.0 构建，Unity 工程使用 2020.3.49f1c1。

**从仓库现有资源构建玩家包**：本地安装《活侠传》和 BepInEx 6 Unity Mono，指定游戏根目录即可验证 `Runtime/` 并打包。游戏、Unity、Harmony、DOTween 等宿主程序集从本机引用。

```powershell
$game = 'D:\Games\LegendOfMortal'
dotnet build .\XingShangMod.csproj -c Release "-p:GameRoot=$game"
dotnet run --project .\tests\CoreAudit\CoreAudit.csproj -c Release "-p:GameRoot=$game"
.\tools\pack.ps1 -GameRoot $game
```

重新生成原始行情和文案时，还需要制作工作区中的原始数据和创作素材；这些上游输入与仓库内已生成的 `Runtime/` 是两套文件。下文保留完整资源重建与界面验收流程。

### 1. 重新生成 Unity UI 与 bundle

使用与工程一致的 **Unity 2020.3.49f1c1**。将 `<Unity.exe 路径>` 替换为本机编辑器路径：

```powershell
$unity = '<Unity.exe 路径>'
& $unity -batchmode -quit -projectPath (Resolve-Path .\UnityUI).Path -executeMethod XingShang.UI.Authoring.UiBuilder.BatchGenerate -logFile -
```

该入口重新导入美术与字体、生成 15 个原生 prefab 和 `UnityUI/Assets/Binding/ui_map.json`，再生成 `UnityUI/Build/xingshang_ui.bundle`。bundle 使用确定性构建和分块压缩，未禁用 TypeTree。Unity 工程与源资源保留在 `UnityUI`，运行包不直接读取其中的散图。

### 2. 重建并校验 Runtime

```powershell
python .\tools\build_runtime_data.py
```

脚本以 `UnityUI/Build/xingshang_ui.bundle`、`UnityUI/Assets/Binding/ui_map.json`、`UnityUI/Assets/Fonts/OFL.txt` 和 `安装说明.txt` 为明确输入，同步到 `Runtime/bundles`、`Runtime/licenses` 与 `Runtime/docs`，并在 `Runtime/build_manifest.json` 记录逐文件及 bundle、绑定表、OFL、安装说明的 SHA256。它会清除旧散装 UI 和物品图标输出，但不会删除 `UnityUI` 源资源。

需要自定义输入位置时，可分别传入 `--ui-bundle`、`--ui-map`、`--font-license`、`--install-guide`；业务数据源参数仍可使用 `--ideas-dir`、`--item-art-dir`、`--news-dir`、`--stock-data-dir`。

只校验现有运行资源而不重建它们：

```powershell
python .\tools\build_runtime_data.py --verify-only
```

### 3. 构建插件

默认引用路径在 `XingShangMod.csproj` 中；本机可使用：

```powershell
dotnet build .\XingShangMod.csproj --configuration Release
```

游戏安装在别处时覆盖 `GameRoot`，必要时也可分别覆盖 `ManagedPath` 与 `BepInExCorePath`：

```powershell
dotnet build .\XingShangMod.csproj --configuration Release "-p:GameRoot=D:\Games\LegendOfMortal"
```

主工程只编译 `src/**/*.cs`，不会把 `UnityUI`、`tests` 或 `obj` 中的源码带入插件。输出为 `bin/Release/XingShangMod.dll`；包括 `UnityEngine.AssetBundleModule` 在内的宿主引用均不随插件复制。项目版本与 `src/Plugin.cs` 的 `PluginVersion` 均为 `2.2.0`。

### 4. Runtime Preview 视觉对照

先生成 UI bundle、重建 Runtime 并完成 Release 构建。Runtime Preview 默认读取 `bin/Release/XingShangMod.dll` 与 `Runtime/bundles/xingshang_ui.bundle`，使用仓库内的 `tests/fixtures/demo-market.json` 实例化正式运行时 View、图表和 Overlay。预览沿用正式插件的资源查找路径，请把克隆的工程目录命名为 `XingShangMod`。指定本机游戏目录后运行：

```powershell
$unity = '<Unity.exe 路径>'
& $unity -automated -projectPath (Resolve-Path .\UnityUI).Path -executeMethod XingShang.UI.Authoring.RuntimePreview.BatchEntry -runtimePreviewGameRoot $game -logFile -
```

本机 Unity 2020.3.49f1c1 在自动验收退出时曾于编辑器自身的 CoreBusinessMetrics 线程崩溃。无人值守预览加上 `-automated` 后，启动日志会显示 `StartBugReporterOnCrash: 0`，仍会保留崩溃日志。验收同时记录报告状态与进程退出码。

该入口需要真实 Editor Game View，不能添加 `-batchmode`，也不能用只实例化 Prefab 的静态 `BatchPreview` 代替。默认输出到 `UnityUI/Build/RuntimePreview`，包含 fixture、逐用例语义 JSON、原始 PNG 和 `report.json`；可用 `-runtimePreviewOutput <目录>` 改输出位置。

截图矩阵依次使用 2560×1440、1920×1080、1280×720、1366×768 四个原生视口。静态用例先拍独立 `market-empty` 空契匣，再拍默认两单 `market`，避免两单 fixture 掩盖空态。每张 PNG 的像素尺寸必须等于对应视口，禁止缩略图、截图后 resize、自动配准或整页缩放；局部 crop 只能用于诊断，并且必须保留原始整屏图。截图前预热字体，并要求布局/语义连续两帧稳定；缺字、错误禁用、异常日志、空白图或尺寸不符均应视为失败。关键组件同时记录屏幕矩形与可见像素探针，不能用节点存在或 RectTransform 尺寸代替视觉验收。

真实动效与交易反馈使用独立入口：

```powershell
& $unity -automated -projectPath (Resolve-Path .\UnityUI).Path -executeMethod XingShang.UI.Authoring.RuntimePreview.BatchInteractions -runtimePreviewGameRoot $game -logFile -
```

该入口输出到 `UnityUI/Build/RuntimePreviewInteractions`，在 2560×1440、1920×1080、1280×720 与 1366×768 下验证指针/焦点、Tooltip、窗口时间线、Toast 堆叠、连续总榜、真实入匣容量边界、正式交易确认与飞签/落印，以及减少动效终态。指针验收从实际屏幕坐标执行 `EventSystem.RaycastAll`，并用 `GraphicRaycaster.Raycast` 复核命中后走 pointer 事件管线；直接回调只能用于诊断且会标记失败，不能冒充真实鼠标通过。除明确执行成交的用例外，浏览不得改变 RunState 或钱包；成交用例必须按正式 Preview 核对账目且双击只能提交一次。达到持仓方向或格位限制时，商品草稿和契匣会及时禁用相应的加量操作。

## 发布打包

需要 PowerShell 5.1 或更高版本。先按上节生成 bundle 和 Runtime，再执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1
```

自定义游戏引用路径可追加 `-GameRoot`、`ManagedPath` 或 `-BepInExCorePath`，例如：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1 -GameRoot 'D:\Games\LegendOfMortal'
```

脚本会实际执行 Release 构建，核对 `Runtime/build_manifest.json` 的业务计数、检查项、文件集合、字节数和逐文件 SHA256，并复核 UI bundle、OFL 与安装说明。它不会重新生成 Unity bundle 或 Runtime。

新包在 `dist` 下的唯一临时 staging 和临时 ZIP 中完成构建及 ZIP 文件集合校验；全部成功后才在同卷内原子替换正式 ZIP 与 `.sha256`。随后将同一个已验证 staging 安全切换为 `dist/pkg`，因此该目录与最终 ZIP 的内容一致。构建或 ZIP 校验失败不会改动旧 `pkg`；目录切换失败会恢复旧目录。临时文件在结束时清理。

输出：

```text
dist/XingShangMod-v<版本>.zip
dist/XingShangMod-v<版本>.zip.sha256
dist/pkg/BepInEx/
```

只有本次打包命令成功后，`dist/pkg` 才是上述版本；失败时保留的上一版目录不能作为新包安装或发布。ZIP 与 `dist/pkg` 的根目录都只有 `BepInEx`，且只允许插件 DLL 与其 `Runtime` 资源，不会包含 `bin`、`obj`、`tools`、反编译文件、游戏 DLL 或其他 Mod。

校验下载包：

```powershell
Get-FileHash '.\dist\XingShangMod-v2.2.0.zip' -Algorithm SHA256
Get-Content '.\dist\XingShangMod-v2.2.0.zip.sha256'
```

两者哈希应一致（不区分大小写）。

## Windows 安装器

在 64 位 Windows 上先打出当前版本的插件 ZIP，再运行 `python installer/build_installer.py`。构建脚本使用本机 .NET Framework 编译器，将同版本 ZIP 作为资源嵌入 EXE，生成安装器及独立的 SHA256 收据。安装器支持查找 Steam 游戏目录、手动选目录、安装或更新、卸载行市和还原上次更改。全新游戏可选用固定版本的官方前置下载或本地官方 ZIP。前置的校验在写入游戏目录之前完成。

## 发布到 GitHub

[上传文件分组、截图和 Release 附件](PUBLISHING.md)
