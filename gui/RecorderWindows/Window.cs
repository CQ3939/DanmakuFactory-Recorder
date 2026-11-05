using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Globalization;
using System.Threading.Tasks;
class Window:Form{
 FlowLayoutPanel bar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=150,AutoScroll=true,Padding=new Padding(6)};
 FlowLayoutPanel bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=44};
 ListView files=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,AllowDrop=true};
 TextBox result=new TextBox{Dock=DockStyle.Bottom,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Height=132,WordWrap=false};
 ComboBox format=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=70};
 TextBox output=new TextBox{Width=270}; Settings current=new Settings();bool running;
 Button ButtonAt(FlowLayoutPanel p,string name,Action click){var b=new Button{Text=name,AutoSize=true};p.Controls.Add(b);b.Click+=(s,e)=>click();return b;}
 void LabelAt(string s){bar.Controls.Add(new Label{Text=s,AutoSize=true,Margin=new Padding(6,7,0,0)});}
 void Add(string[] paths){foreach(var path in paths)if(!files.Items.Cast<ListViewItem>().Any(i=>String.Equals(i.Text,path,StringComparison.OrdinalIgnoreCase)))files.Items.Add(new ListViewItem(new[]{path,"待转换",""}));}
 public Window(){
 Text="弹幕转换 · DanmakuFactory";Font=new Font("Microsoft YaHei UI",9);ClientSize=new Size(980,610);MinimumSize=new Size(820,550);StartPosition=FormStartPosition.CenterScreen;
 files.Columns.Add("文件（可拖入多个）",430);files.Columns.Add("状态",145);files.Columns.Add("输出路径",410);
 Controls.Add(files);Controls.Add(result);Controls.Add(bottom);Controls.Add(bar);
 ButtonAt(bar,"选择文件",()=>{using(var d=new OpenFileDialog{Multiselect=true,Filter="弹幕文件|*.jsonl;*.xml;*.ass;*.json|所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK)Add(d.FileNames);});
 ButtonAt(bar,"清空",()=>{files.Items.Clear();result.Clear();});LabelAt("输出格式");format.Items.AddRange(new[]{"ASS","JSONL","XML","JSON"});format.SelectedIndex=0;bar.Controls.Add(format);
 ButtonAt(bar,"统一输出目录",()=>{using(var d=new FolderBrowserDialog())if(d.ShowDialog()==DialogResult.OK)output.Text=d.SelectedPath;});bar.Controls.Add(output);LabelAt("留空：各源文件旁");bar.SetFlowBreak(bar.Controls[bar.Controls.Count-1],true);
 ButtonAt(bar,"弹幕与消息框设置…",EditSettings);
 bar.Height=86;
 if(File.Exists(Settings.SavedPath))try{current=Settings.Load(Settings.SavedPath,current);}catch(Exception e){result.Text="载入设置失败："+e.Message;}
 ButtonAt(bottom,"开始转换",()=>Start());bottom.Controls.Add(new Label{AutoSize=true,Text="选中队列项可查看该文件的统计和完整输出路径。",Margin=new Padding(10,8,0,0)});
 files.SelectedIndexChanged+=(s,e)=>{if(files.SelectedItems.Count>0)result.Text=files.SelectedItems[0].Tag as string;};
 files.DragEnter+=(s,e)=>e.Effect=!running&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;
 files.DragDrop+=(s,e)=>Add((string[])e.Data.GetData(DataFormats.FileDrop));FormClosing+=(s,e)=>{if(running)e.Cancel=true;};
 }
 void EditSettings(){
  using(var editor=new SettingsDialog(current)){
   if(editor.ShowDialog(this)==DialogResult.OK)current=editor.Draft;
  }
 }
 async void Start(){if(running||files.Items.Count==0)return;string ext=format.Text.ToLowerInvariant(),folder=output.Text.Trim();
 string note=ext=="ass"?"直播间信息会跳过。普通弹幕保留显示内容，时间取到百分之一秒，用户和勋章等附加字段会丢失。普通弹幕按密度及区域排布；关闭的直播事件会过滤。礼物框使用引擎配色，已显示直播事件另存可还原记录。":ext=="json"?"AcFun JSON 仅保存普通弹幕的时间、模式、字号、颜色和文字。直播事件、直播间信息及用户字段会丢失。":ext=="xml"?"XML 使用录播标签，并通过扩展属性保留原记录。":"JSONL 保存可还原的记录及已有字段。";
 if(ext=="ass" && current.MinPrice>0)note+="\n礼物总价不足 "+current.MinPrice+" 元或总价不明时过滤，分别计数。SC 和上舰不参与价格筛选。";
 if(MessageBox.Show(note+"\n外部 ASS 的文字按显示片段读取，绘图跳过，无法还原原礼物金额或身份。","转换前说明",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
 await Batch(ext,folder,current);
 }
 async Task Batch(string ext,string folder,Settings o){
 running=true;bar.Enabled=bottom.Enabled=false;
 try{foreach(ListViewItem i in files.Items){string path=i.Text;i.SubItems[2].Text="";try{var msg=await Task.Run(()=>ConvertFile.Run(path,ext,folder,o,v=>Invoke(new Action(()=>i.SubItems[1].Text=v))));i.Tag=result.Text=msg;i.SubItems[2].Text=msg.Split('\r')[0];i.SubItems[1].Text="完成";}catch(Exception e){i.Tag=result.Text=e.Message;i.SubItems[1].Text="失败："+e.Message;}}}finally{running=false;bar.Enabled=bottom.Enabled=true;}
 }
 [STAThread] static int Main(string[] args){CultureInfo.DefaultThreadCurrentCulture=CultureInfo.InvariantCulture;
 if(args.Length>0&&args[0]=="--native")return ConvertFile.Native(args);
 if(args.Length>=4&&args[0]=="--convert"){try{var o=args.Length>4?ConvertFile.J.Deserialize<Settings>(File.ReadAllText(args[4])):new Settings();Console.WriteLine(ConvertFile.Run(args[1],args[2],args[3],o,s=>{}));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 Application.EnableVisualStyles();Application.Run(new Window());return 0;}
}
