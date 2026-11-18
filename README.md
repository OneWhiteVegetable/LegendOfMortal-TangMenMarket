# 唐门行市（XingShangMod）

《活侠传》的 BepInEx 6 Unity Mono 插件。唐门行市使用随包历史数据快照和本地规则模拟商品行情、交易、货囊与做空；不会连接交易所、券商或行情服务。

**所有行情均为纯离线游戏模拟，仅供娱乐，不构成任何投资建议，也不能用于真实交易决策。**

## 玩法与入口

- 每次完成原版“行商”养成事件后打开交易行市；开局随机出现 6 件商品，门派升级和设施开发可增加页数。
- 养成界面右上角“系统”中的“唐门行市”只打开货囊与资产分析，不能交易，也不能跳转回行商。
- 每消耗 3 个行动点推进一个市场周；低行动点周目在月末补足月份，高行动点周目不会重复补周。
- 商品由随机个股与每年更换的唐门指数共同驱动，包含低波动弹性、行情异动、普通数据末端横盘与真实退市处理。
- 买入、卖出、借货做空与还货共用同一货囊格位；单格堆叠数量由轻功、内力和商品重量决定。做空实收 97%，24 个市场周到期自动平仓。
- 同一周目的随机结果和仓位随对应原版存档版本保存；读取存档不会重新抽取，新周目才重新随机。

## 运行依赖

- 《活侠传》游戏本体。
- **BepInEx 6 的 Unity Mono 版本**，已核对的宿主版本为 `6.0.0-be.692`；不能使用 IL2CPP 版本，也未以 BepInEx 5 作为目标。
- 插件目标框架为 `.NET Standard 2.0`，构建时需要本机游戏目录中的程序集引用。

发布 ZIP 是**插件-only** 包：不含 BepInEx 本体、游戏 DLL、DOTween、其他 Mod、反编译工程或构建工具。请先独立安装并配置好 BepInEx 6 Unity Mono。

## 安装

1. 关闭游戏，备份重要存档。
2. 将 `XingShangMod-v<版本>.zip` 解压到游戏根目录，即包含 `Mortal.exe` 和 `BepInEx` 的目录。
3. 不要覆盖 `BepInEx/core`、`BepInEx/config` 或其他插件。正确安装后只有以下插件路径：

```text
BepInEx/
└── plugins/
    ├── XingShangMod.dll
    └── XingShangMod/
        └── Runtime/
            ├── build_manifest.json
            ├── items.json
            ├── news.json
            ├── selection_catalog.json
            ├── data/
            ├── items/
            └── ui/
```

升级时先关闭游戏，再替换 `XingShangMod.dll` 和整个 `XingShangMod/Runtime` 目录；不要只覆盖部分资源。详细步骤见 `安装说明.txt`。

## 存档、侧车与卸载

本 Mod 不改写原版 `.dat` 的结构或内容。行情、持仓、周目标识等 Mod 状态保存为独立 JSON 侧车，位置为：

```text
<Unity Application.persistentDataPath>/XingShangMod/Sidecars/
└── p-<用户 Profile>/
    └── s-<存档槽位>/
        └── <GameSave.TimeTick>.json
```

Windows 下 `Application.persistentDataPath` 通常位于 `%USERPROFILE%/AppData/LocalLow/<发行商>/<游戏名>`，以游戏实际使用的目录为准；侧车与原版存档位于同一持久化数据根目录下。

卸载时关闭游戏，删除：

- `BepInEx/plugins/XingShangMod.dll`
- `BepInEx/plugins/XingShangMod/`

**卸载不会影响原版 `.dat` 存档继续独立游玩，但原版游戏不会显示或结算 Mod 的行情和持仓。** 默认保留侧车，重新安装匹配版本后可继续识别对应存档；如确定不再使用，可先备份再删除持久化数据目录中的 `XingShangMod/Sidecars`。删除侧车不等于删除原版存档。

## 日志与诊断

主要日志：`游戏根目录/BepInEx/LogOutput.log`。

排查时记录游戏版本、BepInEx 版本、Mod 版本、复现步骤及对应时段的日志，搜索 `XingShangMod`、`Mod initialization failed`、`Patch class` 和 `Sidecar`。若侧车缺失或损坏，请保留相关文件和日志用于诊断，不要修改原版 `.dat` 来“修复”侧车问题。

## 从源码构建

在 `XingShangMod` 项目根目录执行。默认引用路径在 `XingShangMod.csproj` 中；本机可使用：

```powershell
& 'C:\allsoft\dotnet\dotnet.exe' build .\XingShangMod.csproj --configuration Release
```

游戏安装在别处时覆盖 `GameRoot`，必要时也可分别覆盖 `ManagedPath` 与 `BepInExCorePath`：

```powershell
& 'C:\allsoft\dotnet\dotnet.exe' build .\XingShangMod.csproj --configuration Release "-p:GameRoot=D:\Games\LegendOfMortal"
```

插件输出为 `bin/Release/XingShangMod.dll`。宿主引用均不随插件复制。

只校验现有运行资源而不重建它们：

```powershell
python .\tools\build_runtime_data.py --verify-only
```

## 发布打包

需要 PowerShell 5.1 或更高版本。在项目根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1 -DotnetPath 'C:\allsoft\dotnet\dotnet.exe'
```

自定义游戏引用路径可追加 `-GameRoot`、`ManagedPath` 或 `-BepInExCorePath`，例如：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\pack.ps1 -DotnetPath 'C:\allsoft\dotnet\dotnet.exe' -GameRoot 'D:\Games\LegendOfMortal'
```

脚本会实际执行 Release 构建，核对 `Runtime/build_manifest.json` 的计数、检查项、文件集合、字节数和逐文件 SHA256，然后重建干净的 `dist/pkg/BepInEx/plugins`。输出：

```text
dist/XingShangMod-v<版本>.zip
dist/XingShangMod-v<版本>.zip.sha256
```

ZIP 根目录只有 `BepInEx`，且只允许插件 DLL 与其 `Runtime` 资源，不会打入 `bin`、`obj`、`tools`、反编译文件、游戏 DLL 或其他 Mod。`dist/pkg` 是 staging 目录，不需要单独上传。

校验下载包：

```powershell
Get-FileHash '.\dist\XingShangMod-v1.0.0.zip' -Algorithm SHA256
Get-Content '.\dist\XingShangMod-v1.0.0.zip.sha256'
```

两者哈希应一致（不区分大小写）。
