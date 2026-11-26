# 将唐门行市发布到 GitHub

发布版本为 **v2.2.0**。本仓库的玩家首页是 [README](../README.md)，截图通过相对路径加载。上传时保留 `docs/images/` 的文件名和层级。

| 拿到的内容 | 放在哪里 | 用途 |
|---|---|---|
| `Repository/` 内的文件 | 仓库根目录 | README、截图、安装说明、行市源码、测试、安装器源码与构建资源 |
| `Release/XingShangMod-Setup-v2.2.0.exe` | GitHub Release 的附件 | 玩家双击安装或更新 |
| `Release/XingShangMod-v2.2.0.zip` | 同一个 Release 的附件 | 已有前置的玩家手动解压 |
| 上述文件各自的 `.sha256` | 同一个 Release 的附件 | 对照下载的字节是否完整 |
| `Release正文.md` | 复制内容到 Release 描述 | 版本介绍、变化与截图 |

如果只更新宣传页，取 `README.md`、`docs/`、`安装说明.txt` 和 `Runtime/licenses/OFL.txt`，保留它们的目录结构。如果同时发布这版的可构建源码，把 `Repository/` 里的内容合并到现有仓库。它包括 `src/`、`tests/`、`tools/`、`installer/`、`XingShangMod.csproj`、已生成的 `Runtime/` 和 Unity 工程的 `Assets/`、`Packages/`、`ProjectSettings/`。字体随附的 [OFL 许可](../Runtime/licenses/OFL.txt) 及 Unity 的 `.meta` 文件一并保留。

在现有仓库提交本次内容后，将 `v2.2.0` 标签指向对应提交，再创建 Release 并附上四个文件。这样版本说明中的截图会从同一个标签读取。输出目录 `dist/GitHub-v2.2.0/` 由打包工具生成；其中的 `Repository` 和 `Release` 是本地分拣目录，上传到 GitHub 时取其内容即可。

## 分发边界

游戏本体、`Mortal_Data/Managed`、其他 Mod、本机实际使用的 BepInEx 配置、玩家存档、侧车、调试日志和 Unity 缓存均不在上传清单内。公开截图是本项目在隔离游戏进程中生成的画面。运行时数据含来自 BaoStock、东方财富及 stock_finance_data 等渠道的历史周线，UI bundle 复用了原游戏背景。仓库内可核实的 OFL 许可适用于字体；现有制作资料未给出行情数据和原游戏美术的统一公开再分发许可。正式公开含这些内容的仓库资源、ZIP 或安装器前，请按你取得素材和数据时的授权核对分发范围。

## 在本地生成这套文件

先生成并校验插件 ZIP，再在仓库根目录依次执行以下命令。[开发与打包](DEVELOPMENT.md)

```powershell
python installer/build_installer.py
python tools/prepare_github_release.py
```

分组脚本会核对 Release 附件的 SHA256、ZIP 内的全部文件、安装器内嵌的 ZIP 和 Markdown 图片路径。Release 正文中的图片地址取自本地 Git 的 `origin` 和当前版本标签。重复执行可刷新生成目录。如果上一次导出的内容被手动编辑过，脚本会把它保留在旁边的 `-previous-` 目录。
