# 唐门行市 Windows 安装器

单文件 WinForms 程序适用于 Windows 10/11 的 .NET Framework 4.5 及以上。发行时提供 `dist/XingShangMod-Setup-v<版本>.exe` 和同名 `.sha256`。当前使用项目中的原版商号图形作为图标。

## 构建

在 Mod 仓库根目录先完成 Unity bundle 和 Runtime 生成，再运行：

```powershell
./tools/pack.ps1 -DotnetPath 'C:/allsoft/dotnet/dotnet.exe'
python installer/build_installer.py
```

脚本读取项目版本，校验 ZIP 随附的 SHA256，并逐字节比较发布 ZIP 内的 DLL 和 Runtime 清单与当前构建产物。编译器来自 Windows 自带的 .NET Framework 4.x。EXE 直接嵌入同一 ZIP，安装前再校验资源整体哈希。未使用代码签名证书。

## 操作

在 Steam 正常安装的《活侠传》会通过 `appmanifest_1859910.acf` 自动定位。自定义目录可手选。检测验证游戏 EXE、UnityPlayer.dll 的 x64 PE 头、Mortal.Core 的程序集身份，以及加载器的程序集与 Doorstop 配置。

已安装并启用 BepInEx 6 Unity Mono x64 时，安装器直接部署自身资源。全新目录可选择从官方获取固定前置包或手选对应原始 ZIP。程序先校验整个 ZIP 的 SHA256，再写入加载器文件；共享加载器发生冲突时会停止。原版发布 ZIP 的 `changelog.txt` 放置为 `BepInEx/BepInEx-changelog.txt`，以免与游戏根目录下的其他说明同名。加载器来自上游原始发布，更新和卸载行市时保留。

- 官方构建：[BepInEx #789 Windows x64 Mono 原始 ZIP](https://builds.bepinex.dev/projects/bepinex_be/789/BepInEx-Unity.Mono-win-x64-6.0.0-be.789%2B6cea286.zip)
- SHA256：`e7ab96aa80f77119335fca0e99e978460d422f4a76befb4deb9c2d36380614d0`
- 上游安装说明与许可证：[Mono Unity 安装](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_mono.html)、[BepInEx 源码与许可](https://github.com/BepInEx/BepInEx/tree/6cea286d204eb3754d3b8363a5a1fd6640164a49)

升级会备份实际改动的行市文件；旧清单中的淘汰文件仅在其内容仍匹配旧清单时移走。卸载会保留用户额外放入的文件。备份和事务记录位于游戏目录的 `.XingShangModInstaller`，与 BepInEx 插件扫描目录分开存放。发生文件占用或写入失败时按记录反向回滚。异常退出后的未完成记录会在下一次安装、还原或卸载时先行恢复。若文件在事后被手工修改，程序会要求先备份移走冲突文件。

## 命令行

```text
XingShangMod-Setup-v2.2.0.exe --detect
XingShangMod-Setup-v2.2.0.exe --check <游戏目录>
XingShangMod-Setup-v2.2.0.exe --install <游戏目录>
XingShangMod-Setup-v2.2.0.exe --install <游戏目录> --download-loader
XingShangMod-Setup-v2.2.0.exe --install <游戏目录> --loader-zip <官方前置ZIP>
XingShangMod-Setup-v2.2.0.exe --uninstall <游戏目录>
XingShangMod-Setup-v2.2.0.exe --restore <游戏目录>
```

命令行失败返回 1，成功返回 0。`--detect` 仅输出自动识别到的目录，`--check` 只检查指定目录。PowerShell 脚本调用图形子系统 EXE 时可用 `Start-Process -Wait -PassThru` 读取 ExitCode。
