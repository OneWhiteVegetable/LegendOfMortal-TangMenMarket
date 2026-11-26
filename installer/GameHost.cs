using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Tangmen.Setup
{
    internal enum LoaderState { Ready, Missing, Incomplete }

    internal static class GameHost
    {
        internal const string Guide = "https://docs.bepinex.dev/master/articles/user_guide/installation/unity_mono.html";
        internal const string LoaderUrl = "https://builds.bepinex.dev/projects/bepinex_be/789/BepInEx-Unity.Mono-win-x64-6.0.0-be.789%2B6cea286.zip";
        internal const string LoaderSha = "e7ab96aa80f77119335fca0e99e978460d422f4a76befb4deb9c2d36380614d0";
        internal const string Dll = "BepInEx/plugins/XingShangMod.dll";
        internal const string Runtime = "BepInEx/plugins/XingShangMod/Runtime/";
        internal const string Manifest = Runtime + "build_manifest.json";

        internal static string CheckRoot(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new InvalidDataException("请选择包含 Mortal.exe 的《活侠传》游戏目录。");
            string root = Path.GetFullPath(path.Trim().Trim('"'));
            if (!IsX64(Path.Combine(root, "Mortal.exe")) || !IsX64(Path.Combine(root, "UnityPlayer.dll")) ||
                !IsAssembly(Path.Combine(root, "Mortal_Data/Managed/Mortal.Core.dll"), "Mortal.Core"))
                throw new InvalidDataException("此处缺少可验证的 64 位 Mortal.exe、UnityPlayer.dll 或 Mortal.Core.dll。请在 Steam 的游戏管理菜单中浏览本地文件并选择该目录。");
            return root;
        }

        private static bool IsX64(string file)
        {
            if (!File.Exists(file)) return false;
            using (var input = new BinaryReader(File.OpenRead(file)))
            {
                if (input.BaseStream.Length < 64 || input.ReadUInt16() != 0x5A4D) return false;
                input.BaseStream.Position = 60;
                int offset = input.ReadInt32();
                if (offset < 64 || offset > input.BaseStream.Length - 6) return false;
                input.BaseStream.Position = offset;
                return input.ReadUInt32() == 0x4550 && input.ReadUInt16() == 0x8664;
            }
        }

        internal static bool IsAssembly(string path, string expected, int? major = null)
        {
            if (!File.Exists(path)) return false;
            try
            {
                var refName = AssemblyName.GetAssemblyName(path);
                return refName.Name == expected && (!major.HasValue || refName.Version.Major == major.Value);
            }
            catch (BadImageFormatException) { return false; }
            catch (FileLoadException) { return false; }
        }

        internal static LoaderState CheckLoader(string root)
        {
            string core = Path.GetFullPath(Path.Combine(root, "BepInEx/core"));
            string cfg = Path.Combine(root, "doorstop_config.ini");
            if (IsAssembly(Path.Combine(core, "BepInEx.Core.dll"), "BepInEx.Core", 6) &&
                IsAssembly(Path.Combine(core, "BepInEx.Unity.Mono.dll"), "BepInEx.Unity.Mono", 6) &&
                IsAssembly(Path.Combine(core, "BepInEx.Unity.Mono.Preloader.dll"), "BepInEx.Unity.Mono.Preloader", 6) &&
                File.Exists(Path.Combine(core, "0Harmony.dll")) && IsX64(Path.Combine(root, "winhttp.dll")) &&
                File.Exists(cfg))
            {
                string[] lines = File.ReadAllLines(cfg);
                bool general = false, enabled = false, loader = false;
                foreach (string raw in lines)
                {
                    string line = raw.Split('#',';')[0].Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal)) { general = line.Equals("[General]", StringComparison.OrdinalIgnoreCase); continue; }
                    if (!general) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim(), value = line.Substring(eq+1).Trim().Trim('"');
                    if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase)) enabled = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                    if (key.Equals("target_assembly", StringComparison.OrdinalIgnoreCase))
                        loader = String.Equals(Path.GetFullPath(Path.Combine(root, value)), Path.Combine(core, "BepInEx.Unity.Mono.Preloader.dll"), StringComparison.OrdinalIgnoreCase);
                }
                if (enabled && loader) return LoaderState.Ready;
            }
            return File.Exists(cfg) || File.Exists(Path.Combine(root, "winhttp.dll")) || File.Exists(Path.Combine(root, ".doorstop_version")) ||
                (Directory.Exists(core) && Directory.EnumerateFileSystemEntries(core).Any()) ? LoaderState.Incomplete : LoaderState.Missing;
        }

        internal static void RequireClosed(string root)
        {
            string game = Path.Combine(root, "Mortal.exe");
            foreach (var proc in Process.GetProcessesByName("Mortal")) using (proc)
            {
                try
                {
                    if (!proc.HasExited && String.Equals(proc.MainModule.FileName, game, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("请先退出这个目录中的《活侠传》，再执行安装或还原。");
                }
                catch (System.ComponentModel.Win32Exception)
                { throw new IOException("无法确认运行中的 Mortal.exe 所在位置。请退出游戏再试。"); }
                catch (InvalidOperationException) { /* Process exited during the check. */ }
            }
        }

        internal static string FindGame()
        {
            var candidates = new List<string> { AppDomain.CurrentDomain.BaseDirectory };
            var steamRoots = new List<string>();
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                    try
                    {
                        using (var key = RegistryKey.OpenBaseKey(hive, view))
                        using (var steam = key.OpenSubKey(@"Software\Valve\Steam"))
                            if (steam != null)
                                foreach (string name in new[] { "SteamPath", "InstallPath" })
                                {
                                    string value = steam.GetValue(name) as string;
                                    if (!String.IsNullOrWhiteSpace(value)) steamRoots.Add(value);
                                }
                    }
                    catch (System.Security.SecurityException) { }
            steamRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            var libraries = new HashSet<string>(steamRoots, StringComparer.OrdinalIgnoreCase);
            foreach (string steam in steamRoots)
            {
                string file = Path.Combine(steam, "steamapps/libraryfolders.vdf");
                if (!File.Exists(file)) continue;
                try
                {
                    foreach (Match m in Regex.Matches(File.ReadAllText(file), "(?im)^\\s*\"(?:path|[0-9]+)\"\\s+\"([^\"]+)\""))
                    {
                        string lib = m.Groups[1].Value.Replace(@"\\", @"\");
                        if (Path.IsPathRooted(lib)) libraries.Add(lib);
                    }
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            foreach (string lib in libraries)
            {
                string apps = Path.Combine(lib, "steamapps");
                string manifest = Path.Combine(apps, "appmanifest_1859910.acf");
                if (!File.Exists(manifest)) continue;
                try
                {
                    Match m = Regex.Match(File.ReadAllText(manifest), "(?im)^\\s*\"installdir\"\\s+\"([^\"]+)\"");
                    if (m.Success && m.Groups[1].Value == Path.GetFileName(m.Groups[1].Value))
                        candidates.Add(Path.Combine(apps, "common", m.Groups[1].Value));
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            foreach (string candidate in candidates)
                try { return CheckRoot(candidate); } catch (InvalidDataException) { } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
            return "";
        }
    }
}
