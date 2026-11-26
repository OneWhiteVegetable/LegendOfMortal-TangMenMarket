using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tangmen.Setup
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if(args.Length==0)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new InstallerForm());
                    return 0;
                }
                if(args.Length==1 && args[0]=="--detect") { Console.WriteLine(GameHost.FindGame()); return 0; }
                if(args.Length<2) throw new ArgumentException("操作需要指定游戏目录。");
                string root=GameHost.CheckRoot(args[1]);
                if(args.Length==2 && args[0]=="--check")
                { Console.WriteLine(GameHost.CheckLoader(root)+" "+Installation.InstalledVersion(root));return 0; }
                Operation mode;
                switch(args[0])
                {
                    case "--install": mode=Operation.Install; break;
                    case "--uninstall": mode=Operation.Uninstall; break;
                    case "--restore": mode=Operation.Restore; break;
                    default: throw new ArgumentException("支持 --detect、--check、--install、--restore、--uninstall。");
                }
                bool download=mode==Operation.Install && args.Length==3 && args[2]=="--download-loader";
                string zip=mode==Operation.Install && args.Length==4 && args[2]=="--loader-zip" ? args[3] : null;
                if(args.Length!=2 && !download && zip==null) throw new ArgumentException("安装选项无效。");
                Console.WriteLine(Installation.Run(root,mode,download || zip!=null,zip,Console.WriteLine));
                return 0;
            }
            catch(Exception e)
            {
                if(args.Length==0) MessageBox.Show(e.Message,"唐门行市",MessageBoxButtons.OK,MessageBoxIcon.Error);
                else Console.Error.WriteLine(e.Message);
                return 1;
            }
        }
    }

    internal sealed class InstallerForm : Form
    {
        private static readonly Color Ink=Color.FromArgb(33,36,33), Paper=Color.FromArgb(247,244,234);
        private static readonly Color Red=Color.FromArgb(175,58,49), Muted=Color.FromArgb(88,94,85);
        private readonly TextBox folder=new TextBox();
        private readonly Label version=new Label(), loader=new Label(), status=new Label();
        private readonly CheckBox bootstrap=new CheckBox();
        private readonly LinkLabel local=new LinkLabel();
        private readonly Button install,restore,uninstall,browse,check;
        private readonly ProgressBar progress=new ProgressBar();
        private bool busy;
        private string localZip;

        internal InstallerForm()
        {
            Text="唐门行市安装器  "+Installation.Version;
            Font=new Font("Microsoft YaHei UI",10F,FontStyle.Regular);
            AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96F,96F);
            StartPosition=FormStartPosition.CenterScreen; BackColor=Paper;ForeColor=Ink;
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            ClientSize=new Size(720,465);MinimumSize=new Size(650,470);
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(25,14,25,18),BackColor=Paper};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(var height in new[]{62F,78F,64F}) body.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
            Controls.Add(body);
            var header=new Panel {Dock=DockStyle.Top,Height=92,BackColor=Ink};
            var stripe=new Panel {Dock=DockStyle.Bottom,Height=4,BackColor=Color.FromArgb(185,153,88)};
            var badge=new Label {Text="商",Font=new Font(Font.FontFamily,21,FontStyle.Bold),ForeColor=Paper,BackColor=Red,
                TextAlign=ContentAlignment.MiddleCenter,Location=new Point(27,19),Size=new Size(51,50)};
            var title=new Label {Text="唐门行市",Font=new Font(Font.FontFamily,23,FontStyle.Bold),ForeColor=Paper,AutoSize=true,Location=new Point(90,19)};
            var revision=new Label {Text="安装器  /  v"+Installation.Version,TextAlign=ContentAlignment.MiddleRight,Dock=DockStyle.Right,Width=200,Padding=new Padding(0,0,27,0),ForeColor=Color.FromArgb(222,199,148)};
            header.Controls.Add(badge);header.Controls.Add(title);header.Controls.Add(revision);header.Controls.Add(stripe);Controls.Add(header);

            var target=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=2,Margin=Padding.Empty};
            target.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            target.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,77));target.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,77));
            target.RowStyles.Add(new RowStyle(SizeType.Absolute,25));target.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var where=Label("《活侠传》游戏目录");target.Controls.Add(where,0,0);target.SetColumnSpan(where,3);
            folder.Dock=DockStyle.Fill;folder.Margin=new Padding(0,2,7,0);folder.ReadOnly=true;folder.BackColor=Color.White;
            target.Controls.Add(folder,0,1);
            browse=Button("选择…");browse.Click+=(s,e)=>PickFolder();target.Controls.Add(browse,1,1);
            check=Button("检测");check.Click+=(s,e)=>Inspect();target.Controls.Add(check,2,1);
            body.Controls.Add(target,0,0);

            var facts=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(0,8,0,6),Padding=new Padding(12,5,10,5),BackColor=Color.FromArgb(234,229,213)};
            facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,38));facts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,62));
            facts.RowStyles.Add(new RowStyle(SizeType.Percent,46));facts.RowStyles.Add(new RowStyle(SizeType.Percent,54));
            facts.Controls.Add(Label("已安装的行市"),0,0);facts.Controls.Add(Label("BepInEx 加载器"),1,0);
            foreach(var v in new[]{version,loader}){v.Dock=DockStyle.Fill;v.Font=new Font(Font.FontFamily,11,FontStyle.Bold);v.TextAlign=ContentAlignment.MiddleLeft;v.AutoEllipsis=true;}
            facts.Controls.Add(version,0,1);facts.Controls.Add(loader,1,1);body.Controls.Add(facts,0,1);

            var options=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Margin=Padding.Empty};
            bootstrap.AutoSize=true;bootstrap.Text="初次安装时从官方获取 BepInEx 6 Mono x64";
            bootstrap.Margin=new Padding(0,8,14,2);options.Controls.Add(bootstrap);
            local.Text="选择本地前置 ZIP";local.AutoSize=true;local.Margin=new Padding(0,9,14,2);local.LinkColor=Red;
            local.LinkClicked+=(s,e)=>PickLoader();options.Controls.Add(local);
            var guide=new LinkLabel{Text="官方前置说明",AutoSize=true,LinkColor=Red,Margin=new Padding(0,9,0,2)};
            guide.LinkClicked+=(s,e)=>Process.Start(new ProcessStartInfo(GameHost.Guide){UseShellExecute=true});
            options.Controls.Add(guide);body.Controls.Add(options,0,2);
            var note=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};
            status.Dock=DockStyle.Fill;status.Padding=new Padding(0,4,0,4);status.ForeColor=Muted;
            progress.Dock=DockStyle.Bottom;progress.Height=5;progress.Visible=false;progress.Style=ProgressBarStyle.Marquee;
            note.Controls.Add(status);note.Controls.Add(progress);body.Controls.Add(note,0,3);

            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,Margin=Padding.Empty};
            install=Button("安装 / 更新");install.Width=155;install.Height=39;install.BackColor=Red;install.ForeColor=Color.White;
            restore=Button("还原上次更改");restore.Width=127;restore.Height=39;
            uninstall=Button("卸载行市");uninstall.Width=100;uninstall.Height=39;
            foreach(var b in new[]{install,restore,uninstall}){b.Dock=DockStyle.None;b.Margin=new Padding(0,2,10,0);actions.Controls.Add(b);}
            install.Click+=(s,e)=>Execute(Operation.Install);
            restore.Click+=(s,e)=>Execute(Operation.Restore);
            uninstall.Click+=(s,e)=>Execute(Operation.Uninstall);
            body.Controls.Add(actions,0,4);
            folder.Text=GameHost.FindGame();Inspect();
            FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};
        }

        private Label Label(string text)
        {return new Label {Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Muted,Margin=Padding.Empty};}
        private Button Button(string text)
        {return new Button {Text=text,Dock=DockStyle.Fill,BackColor=Color.FromArgb(235,229,214),FlatStyle=FlatStyle.Flat,Margin=new Padding(2,0,0,5),UseVisualStyleBackColor=false};}

        private void PickFolder()
        {
            using(var picker=new FolderBrowserDialog {Description="选择含 Mortal.exe 的《活侠传》目录",ShowNewFolderButton=false,SelectedPath=folder.Text})
                if(picker.ShowDialog(this)==DialogResult.OK){folder.Text=picker.SelectedPath;localZip=null;Inspect();}
        }
        private void PickLoader()
        {
            using(var picker=new OpenFileDialog {Title="选择官方 BepInEx Mono x64 #789 原版 ZIP",Filter="ZIP 压缩包 (*.zip)|*.zip"})
                if(picker.ShowDialog(this)==DialogResult.OK){localZip=picker.FileName;bootstrap.Checked=true;status.Text="将从本地校验并安装前置："+localZip;}
        }
        private void Inspect()
        {
            install.Enabled=restore.Enabled=uninstall.Enabled=bootstrap.Enabled=local.Enabled=false;
            try
            {
                string root=GameHost.CheckRoot(folder.Text);
                LoaderState state=GameHost.CheckLoader(root);
                version.Text=Installation.InstalledVersion(root);
                loader.Text=state==LoaderState.Ready?"6  ·  Unity Mono x64":state==LoaderState.Missing?"待安装":"需修复";
                bootstrap.Enabled=local.Enabled=(state==LoaderState.Missing);
                bootstrap.Checked=state==LoaderState.Missing;
                install.Enabled=state!=LoaderState.Incomplete;
                uninstall.Enabled=File.Exists(Path.Combine(root,GameHost.Dll))||File.Exists(Path.Combine(root,GameHost.Manifest));
                restore.Enabled=Directory.Exists(Path.Combine(root,Installation.Store)) &&
                    Directory.EnumerateDirectories(Path.Combine(root,Installation.Store)).Any(x=>File.Exists(Path.Combine(x,"committed.json")));
                status.Text=state==LoaderState.Ready?"安装器将校验并更新本版插件与运行资源。旧文件会备份到游戏目录。":
                    state==LoaderState.Missing?"首次安装可同时准备官方 #789 加载器。已有对应的原版压缩包时，也可以从本地选择。":
                    "检测到残缺、已关闭或版本不符的加载器。请按官方说明修复后点「检测」。";
            }
            catch(Exception e){version.Text=loader.Text="—";status.Text=e.Message;}
        }
        private async void Execute(Operation mode)
        {
            if(busy)return;
            if(mode!=Operation.Install && MessageBox.Show(this,mode==Operation.Uninstall?"从这个游戏目录卸载唐门行市？会在游戏目录保留行市文件备份。":"还原安装器上次更改的行市文件？",
                Text,MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
            string dir=folder.Text, zip=localZip;
            bool withLoader=bootstrap.Checked;
            busy=true; foreach(var btn in new Control[]{install,restore,uninstall,browse,check,bootstrap,local})btn.Enabled=false;
            progress.Visible=true; string message;
            var updates=new Progress<string>(t=>status.Text=t);
            try { message=await Task.Run(()=>Installation.Run(dir,mode,withLoader,zip,((IProgress<string>)updates).Report)); }
            catch(Exception e){message="操作未完成。"+e.GetBaseException().Message;}
            finally{busy=false;progress.Visible=false;browse.Enabled=check.Enabled=true;}
            Inspect();status.Text=message;
        }
    }
}
