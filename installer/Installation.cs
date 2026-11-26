using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Tangmen.Setup
{
    internal enum Operation { Install, Uninstall, Restore }
    internal sealed class FileEdit { public string path {get;set;} public string before {get;set;} public string after {get;set;} }
    internal sealed class Receipt { public string operation {get;set;} public string version {get;set;} public bool compactPaths {get;set;} public List<FileEdit> files {get;set;} }
    internal sealed class RuntimeFile { public string path {get;set;} public string sha256 {get;set;} }
    internal sealed class RuntimeList { public int schemaVersion {get;set;} public List<RuntimeFile> files {get;set;} }

    internal static class Installation
    {
        internal const string Store = ".XingShangModInstaller";
        internal static string Version {get {return Assembly.GetExecutingAssembly().GetName().Version.ToString(3);}}
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 };
        // Exact file names in the pinned official Windows x64 Mono archive.
        private static readonly HashSet<string> Framework = new HashSet<string>(new[] {
            "winhttp.dll", ".doorstop_version", "doorstop_config.ini", "BepInEx/BepInEx-changelog.txt",
            "BepInEx/core/0Harmony.dll", "BepInEx/core/AssetRipper.Primitives.dll", "BepInEx/core/SemanticVersioning.dll",
            "BepInEx/core/Mono.Cecil.dll", "BepInEx/core/Mono.Cecil.Mdb.dll", "BepInEx/core/Mono.Cecil.Pdb.dll",
            "BepInEx/core/Mono.Cecil.Rocks.dll", "BepInEx/core/MonoMod.RuntimeDetour.dll", "BepInEx/core/MonoMod.Utils.dll",
            "BepInEx/core/BepInEx.Core.dll", "BepInEx/core/BepInEx.Core.xml", "BepInEx/core/BepInEx.Preloader.Core.dll",
            "BepInEx/core/BepInEx.Preloader.Core.xml", "BepInEx/core/BepInEx.Unity.Common.dll", "BepInEx/core/BepInEx.Unity.Common.xml",
            "BepInEx/core/BepInEx.Unity.Mono.dll", "BepInEx/core/BepInEx.Unity.Mono.xml",
            "BepInEx/core/BepInEx.Unity.Mono.Preloader.dll", "BepInEx/core/BepInEx.Unity.Mono.Preloader.xml"
        }, StringComparer.OrdinalIgnoreCase);

        private static bool ModFile(string path)
        {
            return path.Equals(GameHost.Dll, StringComparison.OrdinalIgnoreCase) || path.StartsWith(GameHost.Runtime,StringComparison.OrdinalIgnoreCase);
        }

        private static bool ValidHash(string value)
        {
            return value != null && value.Length == 64 && value.All(Uri.IsHexDigit);
        }

        private static string Under(string root, string relative)
        {
            string current = root;
            foreach (string part in relative.Split('/'))
            {
                if (String.IsNullOrEmpty(part) || part=="." || part==".." || part.EndsWith(".") || part.EndsWith(" ") ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                    throw new InvalidDataException("安装清单包含无效路径：" + relative);
                current=Path.Combine(current,part);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint)!=0)
                    throw new IOException("该路径为文件系统链接，无法安全替换：" + current);
            }
            return current;
        }

        internal static string Hash(Stream stream)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }

        // A fully qualified game file can already be close to MAX_PATH. The
        // transaction must not prepend a second copy of its whole directory tree.
        private static string TransactionFile(string directory, string relative, bool compactPaths)
        {
            if (!compactPaths) return Under(directory, relative);
            using (var data = new MemoryStream(Encoding.UTF8.GetBytes(relative.ToUpperInvariant())))
                return Under(directory, Hash(data).Substring(0, 32));
        }

        private static string HashFile(string file)
        {
            if (!File.Exists(file)) return null;
            using(var input=File.OpenRead(file)) return Hash(input);
        }

        private static void MakeParent(string file) { Directory.CreateDirectory(Path.GetDirectoryName(file)); }

        private static Receipt ReadReceipt(string path)
        {
            Receipt data = Json.Deserialize<Receipt>(File.ReadAllText(path, Encoding.UTF8));
            if (data==null || data.files==null || data.files.Any(f=>f==null || f.path==null ||
                (!ModFile(f.path) && !Framework.Contains(f.path)) || (f.before==null && f.after==null) ||
                (f.before!=null && !ValidHash(f.before)) || (f.after!=null && !ValidHash(f.after))) ||
                data.files.Select(f=>f.path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=data.files.Count)
                throw new InvalidDataException("安装备份记录无法校验：" + path);
            return data;
        }

        internal static string InstalledVersion(string root)
        {
            string dll=Path.Combine(root,GameHost.Dll);
            if (!File.Exists(dll)) return "未安装";
            return GameHost.IsAssembly(dll,"XingShangMod") ? AssemblyName.GetAssemblyName(dll).Version.ToString(3) : "待核验";
        }

        private static Dictionary<string,string> PreviousManifest(string root)
        {
            var items = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string file=Under(root,GameHost.Manifest);
            if (!File.Exists(file)) return items;
            RuntimeList data=Json.Deserialize<RuntimeList>(File.ReadAllText(file,Encoding.UTF8));
            if (data==null || data.schemaVersion != 1 || data.files==null) throw new InvalidDataException("现有运行资源清单无法读取，请先备份当前行市目录。");
            foreach (var f in data.files)
            {
                if (f==null || String.IsNullOrEmpty(f.path) || !ValidHash(f.sha256)) throw new InvalidDataException("现有行市清单含无效条目。");
                string relative=GameHost.Runtime+f.path;
                Under(root,relative);
                items.Add(relative,f.sha256.ToLowerInvariant());
            }
            return items;
        }

        private static string PackageHash()
        {
            using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("package.sha256"))
            using(var reader=new StreamReader(input)) return reader.ReadToEnd().Trim();
        }

        private static void StageZip(Stream input, string expected, string staging, Dictionary<string,string> files, bool loader)
        {
            if (!String.Equals(Hash(input),expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP 的 SHA256 不符，已停止写入。");
            input.Position=0;
            using (var archive = new ZipArchive(input,ZipArchiveMode.Read,true))
            {
                foreach (var entry in archive.Entries)
                {
                    if(entry.FullName.EndsWith("/",StringComparison.Ordinal)) continue;
                    string rel=entry.FullName;
                    if(loader && rel=="changelog.txt") rel="BepInEx/BepInEx-changelog.txt";
                    if (!(loader ? Framework.Contains(rel) : ModFile(rel))) throw new InvalidDataException("包中出现范围外文件："+rel);
                    string outPath=TransactionFile(staging,rel,true);
                    MakeParent(outPath);
                    using(var dst=new FileStream(outPath,FileMode.CreateNew,FileAccess.Write))
                    using(var src=entry.Open()) src.CopyTo(dst);
                    files.Add(rel,HashFile(outPath));
                }
            }
            if(loader && !Framework.All(files.ContainsKey)) throw new InvalidDataException("官方前置包缺少所需文件。");
            if(!loader && (!files.ContainsKey(GameHost.Dll) || !files.ContainsKey(GameHost.Manifest) || !files.ContainsKey(GameHost.Runtime+"bundles/xingshang_ui.bundle")))
                throw new InvalidDataException("插件包不完整。");
        }

        private static void StageLoader(string work, string stage, string localZip, Dictionary<string,string> files, Action<string> progress)
        {
            if(String.IsNullOrEmpty(localZip))
            {
                progress("正在从 BepInEx 官方构建站获取 Windows x64 Mono 前置…");
                localZip=Path.Combine(work,"official-loader.zip");
                var request=(HttpWebRequest)WebRequest.Create(GameHost.LoaderUrl);
                request.UserAgent="Tangmen-Exchange-Setup/"+Version;
                request.Timeout=30000; request.ReadWriteTimeout=30000;
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var response=(HttpWebResponse)request.GetResponse())
                using (var src=response.GetResponseStream())
                using (var output=File.Create(localZip))
                {
                    byte[] buf=new byte[32768]; int n;
                    while ((n=src.Read(buf,0,buf.Length))!=0)
                    {
                        if(output.Length+n>4*1024*1024) throw new InvalidDataException("前置下载大小不符。");
                        output.Write(buf,0,n);
                    }
                }
            }
            using(var input=File.OpenRead(localZip)) StageZip(input,GameHost.LoaderSha,stage,files,true);
        }

        private static void Rollback(string root,string work,Receipt data)
        {
            for(int i=data.files.Count-1;i>=0;i--)
            {
                FileEdit f=data.files[i];
                string dst=Under(root,f.path);
                string old=TransactionFile(Under(work,"old"),f.path,data.compactPaths);
                string staged=TransactionFile(Under(work,"stage"),f.path,data.compactPaths);
                bool hasBackup=File.Exists(old);
                if(!hasBackup && (f.before!=null || File.Exists(staged) || !File.Exists(dst))) continue;
                if(hasBackup && HashFile(old)!=f.before) throw new IOException("旧文件备份校验失败："+old);
                if(File.Exists(dst))
                {
                    if(HashFile(dst)!=f.after) throw new IOException("文件已被其他程序修改，请检查安装备份："+dst);
                    File.Delete(dst);
                }
                if(hasBackup) { MakeParent(dst); File.Move(old,dst); }
            }
        }

        private static void ClearStage(string work)
        {
            // Only the installer's generated, checked transaction subtree is temporary.
            string stage=Under(work,"stage");
            if(Directory.Exists(stage)) DeleteOwnedTree(stage);
            string download=Under(work,"official-loader.zip");
            if(File.Exists(download)) File.Delete(download);
        }

        private static void DeleteOwnedTree(string dir)
        {
            foreach(var f in Directory.GetFiles(dir))
            {
                if((File.GetAttributes(f)&FileAttributes.ReparsePoint)!=0) throw new IOException("临时文件为链接："+f);
                File.Delete(f);
            }
            foreach(var child in Directory.GetDirectories(dir))
            {
                if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0) throw new IOException("临时目录为链接："+child);
                DeleteOwnedTree(child);
            }
            Directory.Delete(dir);
        }

        private static void CleanStageAfterCommit(string work,Action<string> progress)
        {
            try { ClearStage(work); }
            catch(IOException) { progress("操作已完成，临时文件仍位于 "+work); }
            catch(UnauthorizedAccessException) { progress("操作已完成，临时文件仍位于 "+work); }
        }

        internal static string Run(string folder, Operation mode, bool allowLoader, string localLoader, Action<string> progress)
        {
            string root=GameHost.CheckRoot(folder);
            GameHost.RequireClosed(root);
            string history=Under(root,Store);
            Directory.CreateDirectory(history);
            using(var lockFile=new FileStream(Under(history,"setup.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            {
                foreach(var dir in Directory.GetDirectories(history))
                {
                    string checkedDir=Under(history,Path.GetFileName(dir));
                    string pending=Under(checkedDir,"pending.json");
                    if(!File.Exists(pending)) continue;
                    progress("正在恢复上次中断的安装…");
                    Rollback(root,checkedDir,ReadReceipt(pending));
                    File.Move(pending,Path.Combine(checkedDir,"rolled-back.json"));
                    CleanStageAfterCommit(checkedDir,progress);
                }
                return Execute(root,history,mode,allowLoader,localLoader,progress);
            }
        }

        private static string Execute(string root,string history,Operation mode,bool allowLoader,string localLoader,Action<string> progress)
        {
            DateTime stamp=DateTime.UtcNow;
            string work=Path.Combine(history,stamp.ToString("yyyyMMdd-HHmmssfffffff"));
            while (Directory.Exists(work) || File.Exists(work))
            {
                stamp=stamp.AddTicks(1);
                work=Path.Combine(history,stamp.ToString("yyyyMMdd-HHmmssfffffff"));
            }
            string stage=Path.Combine(work,"stage");
            Directory.CreateDirectory(stage);
            var desired=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string journal=Path.Combine(work,"pending.json"), committed=Path.Combine(work,"committed.json");
            var plan=new Receipt { operation=mode.ToString(), version=Version, compactPaths=true, files=new List<FileEdit>() };
            try
            {
                if(mode==Operation.Install)
                {
                    LoaderState loader=GameHost.CheckLoader(root);
                    if(loader==LoaderState.Incomplete) throw new InvalidDataException("检测到不完整或不兼容的加载器。请按官方说明修复 BepInEx 6 Unity Mono x64，然后重试。");
                    if(loader==LoaderState.Missing && !allowLoader) throw new InvalidDataException("尚未安装 BepInEx 6 Unity Mono x64。可勾选安装官方前置，或选择本地官方前置 ZIP。");
                    if(loader==LoaderState.Missing) StageLoader(work,stage,localLoader,desired,progress);
                    progress("正在校验内置资源并准备安装…");
                    using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("package.zip"))
                        StageZip(input,PackageHash(),stage,desired,false);
                }
                if(mode==Operation.Restore)
                {
                    string previous=Directory.GetDirectories(history).OrderByDescending(x=>Path.GetFileName(x),StringComparer.Ordinal)
                        .Select(x=>Under(Under(history,Path.GetFileName(x)),"committed.json")).FirstOrDefault(File.Exists);
                    if(previous==null) throw new InvalidOperationException("此目录还没有可还原的安装记录。");
                    string prior=Path.GetDirectoryName(previous);
                    Receipt oldPlan=ReadReceipt(previous);
                    foreach(var f in oldPlan.files.Where(x=>ModFile(x.path)))
                    {
                        if(HashFile(Under(root,f.path))!=f.after) throw new IOException("安装后此文件又被改动，请先备份或移走它再还原："+f.path);
                        if(f.before!=null)
                        {
                            string backup=TransactionFile(Under(prior,"old"),f.path,oldPlan.compactPaths);
                            if(HashFile(backup)!=f.before) throw new IOException("旧文件备份缺失或校验失败："+f.path);
                            string target=TransactionFile(stage,f.path,true);MakeParent(target);File.Copy(backup,target);
                        }
                        desired.Add(f.path,f.before);
                    }
                }
                else
                {
                    if(File.Exists(Under(root,GameHost.Dll)) && !GameHost.IsAssembly(Under(root,GameHost.Dll),"XingShangMod"))
                        throw new InvalidDataException("现有 XingShangMod.dll 身份不符，请先备份该同名文件。");
                    var previous=PreviousManifest(root);
                    foreach(var item in previous)
                        if(!desired.ContainsKey(item.Key) && HashFile(Under(root,item.Key))==item.Value) desired.Add(item.Key,null);
                    if(mode==Operation.Uninstall) { desired[GameHost.Dll]=null; desired[GameHost.Manifest]=null; }
                }
                progress("正在比较安装文件…");
                foreach(var pair in desired)
                {
                    string before=HashFile(Under(root,pair.Key));
                    if(before==pair.Value) continue;
                    if(!ModFile(pair.Key) && before!=null) throw new IOException("安装位置已有其他前置文件："+pair.Key);
                    plan.files.Add(new FileEdit {path=pair.Key,before=before,after=pair.Value});
                }
                if(plan.files.Count==0)
                {
                    ClearStage(work);Directory.Delete(work);
                    return mode==Operation.Uninstall ? "此目录没有需要卸载的行市文件。" : "已核对文件，当前内容一致。";
                }
                plan.files=plan.files.OrderBy(x=>x.path.Equals(GameHost.Dll,StringComparison.OrdinalIgnoreCase)?1:0).ToList();
                string tempJournal=Path.Combine(work,"plan.tmp");
                File.WriteAllText(tempJournal,Json.Serialize(plan),new UTF8Encoding(false));
                GameHost.RequireClosed(root);
                File.Move(tempJournal,journal);
                int index=0;
                foreach(var f in plan.files)
                {
                    string dst=Under(root,f.path);
                    string old=TransactionFile(Under(work,"old"),f.path,plan.compactPaths);
                    string next=TransactionFile(Under(work,"stage"),f.path,plan.compactPaths);
                    if(f.before!=null){MakeParent(old);File.Move(dst,old);}
                    if(f.after!=null){MakeParent(dst);File.Move(next,dst);}
                    if(++index%500==0) progress("正在写入 "+index+" / "+plan.files.Count);
                }
                File.Move(journal,committed);
                CleanStageAfterCommit(work,progress);
                return String.Format("已完成，共更改 {0:N0} 个文件。备份和记录位于 {1}",plan.files.Count,work);
            }
            catch
            {
                if(File.Exists(journal))
                {
                    Rollback(root,work,plan);
                    File.Move(journal,Path.Combine(work,"rolled-back.json"));
                }
                CleanStageAfterCommit(work,progress);
                if(!Directory.EnumerateFileSystemEntries(work).Any()) Directory.Delete(work);
                throw;
            }
        }
    }
}
