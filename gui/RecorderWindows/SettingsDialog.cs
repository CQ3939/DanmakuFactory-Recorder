using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

partial class Settings {
 public static readonly string[] Blocks={"R2L","L2R","Top","Bottom","Special","Color","Repeat"};
 public static string SavedPath {get{return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"弹幕设置.json");}}
 public Settings Copy(){return (Settings)MemberwiseClone();}
 public void Save(){
  string temp=SavedPath+"."+Guid.NewGuid()+".tmp";
  try{File.WriteAllText(temp,ConvertFile.J.Serialize(this),new UTF8Encoding(false));
   if(File.Exists(SavedPath))File.Replace(temp,SavedPath,null);else File.Move(temp,SavedPath);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
 public static Settings Load(string path,Settings original){
  var draft=original.Copy();var defaults=new Settings();
  draft.GuardDuration=0;draft.GuardX=defaults.GuardX;draft.GuardY=defaults.GuardY;
  draft.GuardWidth=defaults.GuardWidth;draft.GuardHeight=defaults.GuardHeight;
  draft.GuardFont=defaults.GuardFont;draft.GuardOpacity=defaults.GuardOpacity;draft.GuardBackground=defaults.GuardBackground;
  var data=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
  string raw=File.ReadAllText(path);
  if(raw.TrimStart().StartsWith("{")){
   // The project's CLI writer also emits unquoted identifiers inside these two arrays.
   raw=Regex.Replace(raw,"(\"(?:blockmode|statmode)\"\\s*:\\s*\\[)([^\\]]*)(\\])",m=>
    m.Groups[1].Value+Regex.Replace(m.Groups[2].Value,@"(?<![""\w])(r2l|l2r|top|bottom|special|color|repeat|table|histogram)(?![""\w])","\"$1\"",RegexOptions.IgnoreCase)+"]",RegexOptions.IgnoreCase);
   raw=Regex.Replace(raw,@",\s*\}\s*$","}");
   foreach(var pair in ConvertFile.J.Deserialize<Dictionary<string,object>>(raw))data[pair.Key]=pair.Value;
  }else foreach(string entry in raw.Split(';')){
   string[] kv=entry.Split(new[]{':'},2);if(kv.Length==2)data[kv[0].Trim()]=kv[1].Trim();
  }
  var aliases=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {"textShadow","Shadow"},{"textOpacity","Opacity"},{"textOutline","Outline"},{"isBold","Bold"},
   {"danmakuScrollTime","Scroll"},{"danmakuFixedTime","Fixed"},{"danmakuDensity","Density"},
   {"danmakuScrollArea","ScrollArea"},{"danmakuAllArea","Area"},{"resolutionWidth","ResX"},{"resolutionHeight","ResY"},
   {"displayarea","Area"},{"scrolltime","Scroll"},{"fixtime","Fixed"},{"fontsize","TextSize"},{"showusernames","ShowUsers"},
   {"showmsgbox","Boxes"},{"msgboxfontsize","Font"},{"msgboxduration","Duration"},{"giftminprice","MinPrice"},
   {"msgboxposx","X"},{"msgboxposy","Y"},{"msgboxsizewidth","Width"},{"msgboxsizeheight","Height"},
   {"stattable","Table"},{"stathistogram","Histogram"},{"blockbtm","Bottom"},{"timeshift","Offset"}};
  foreach(string key in Blocks)aliases["block"+key]=key;
  int applied=0;
  foreach(var pair in data){
   string key=pair.Key.ToLowerInvariant();
   if(key=="danmakublockmodelist")key="blockmode";
   if(key=="blockmode"||key=="statmode"){
    var values=pair.Value as IList;
    var tokens=values==null?Convert.ToString(pair.Value).Split('-'):values.Cast<object>().Select(Convert.ToString).ToArray();
    foreach(string name in key=="blockmode"?Blocks:new[]{"Table","Histogram"})
     typeof(Settings).GetField(name).SetValue(draft,tokens.Contains(name,StringComparer.OrdinalIgnoreCase));
   }else if(key=="resolution"||key=="msgboxpos"||key=="msgboxsize"){
    var v=(IList)pair.Value;int a=Convert.ToInt32(v[0]),b=Convert.ToInt32(v[1]);
    if(key=="resolution"){draft.ResX=a;draft.ResY=b;}else if(key=="msgboxpos"){draft.X=a;draft.Y=b;}else{draft.Width=a;draft.Height=b;}
   }else{
    string name;if(!aliases.TryGetValue(key,out name))name=key;
    var field=typeof(Settings).GetFields().FirstOrDefault(f=>!f.IsStatic&&string.Equals(f.Name,name,StringComparison.OrdinalIgnoreCase));
    if(field==null)continue;
    field.SetValue(draft,Convert.ChangeType(pair.Value,field.FieldType,System.Globalization.CultureInfo.InvariantCulture));
   }applied++;
  }
  if(applied==0)throw new FormatException("未找到可识别的设置项。");
  return draft;
 }
}

class SettingsDialog:Form {
 public Settings Draft;
 readonly TabControl tabs=new TabControl{Dock=DockStyle.Fill};
 readonly CheckBox save=new CheckBox{Text="保存设置，下次启动载入",AutoSize=true};
 FlowLayoutPanel page,row;readonly List<Panel> previews=new List<Panel>(); NumericUpDown limit;
 readonly Dictionary<string,NumericUpDown> numbers=new Dictionary<string,NumericUpDown>();
 public SettingsDialog(Settings current){
  Draft=current.Copy();Text="弹幕与消息框设置";Font=new Font("Microsoft YaHei UI",9);
  ClientSize=new Size(714,470);MinimumSize=new Size(710,500);StartPosition=FormStartPosition.CenterParent;
  Controls.Add(tabs);
  var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=46,Padding=new Padding(6),FlowDirection=FlowDirection.RightToLeft};
  var ok=new Button{Text="确定"};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel};
  var import=new Button{Text="读取配置…",AutoSize=true};footer.Controls.AddRange(new Control[]{ok,cancel,save,import});Controls.Add(footer);
  AcceptButton=ok;CancelButton=cancel;save.Checked=File.Exists(Settings.SavedPath);
  ok.Click+=(s,e)=>{try{if(Draft.Width<=Draft.Font||Draft.Height<=Draft.Font)throw new ArgumentException("消息框宽、高须大于框内字号。");Draft.ValidateGuard();if(save.Checked)Draft.Save();DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message);}};
  import.Click+=(s,e)=>{using(var d=new OpenFileDialog{Filter="项目设置和预设|*.json;*.txt|所有文件|*.*"})if(d.ShowDialog()==DialogResult.OK){
   var old=Draft;try{Draft=Settings.Load(d.FileName,Draft);Build();}catch(Exception ex){Draft=old;Build();MessageBox.Show(this,"读取配置失败："+ex.Message);}
  }};
  Build();
 }
 void Tab(string name){page=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};var t=new TabPage(name);t.Controls.Add(page);tabs.TabPages.Add(t);Row();}
 void Row(){row=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Margin=new Padding(0,4,0,4)};page.Controls.Add(row);}
 void Label(string text){row.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(4,6,3,3)});}
 NumericUpDown Num(string field,string title,decimal min,decimal max,int digits=0,decimal scale=1){
  Label(title);var f=typeof(Settings).GetField(field);
  var n=new NumericUpDown{DecimalPlaces=digits,Minimum=min,Maximum=max,Increment=digits==0?1:0.1M,Width=76};
  n.Value=decimal.Round(Convert.ToDecimal(f.GetValue(Draft))*scale,digits);
  n.ValueChanged+=(s,e)=>{f.SetValue(Draft,Convert.ChangeType(n.Value/scale,f.FieldType));foreach(var preview in previews)preview.Invalidate();};
  numbers[field]=n;row.Controls.Add(n);return n;
 }
 void Check(string field,string title){
  var f=typeof(Settings).GetField(field);var c=new CheckBox{Text=title,AutoSize=true,Checked=(bool)f.GetValue(Draft)};
  c.CheckedChanged+=(s,e)=>{f.SetValue(Draft,c.Checked);foreach(var preview in previews)preview.Invalidate();};row.Controls.Add(c);
 }
 void Build(){
  previews.Clear();numbers.Clear();while(tabs.TabCount>0)tabs.TabPages[0].Dispose();
  Tab("文字与时间");Label("文字样式和密度仅用于 ASS");Row();
  Num("TextSize","字号",1,500);Num("Shadow","阴影",0,4);Num("Outline","描边",0,4);Check("Bold","粗体");Row();
  Num("Opacity","不透明度（%）",0.4M,100,1,100M/255);Row();
  Label("密度");var mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=120};mode.Items.AddRange(new[]{"无限","不重叠","按条数限制"});row.Controls.Add(mode);
  Label("同屏条数上限");limit=new NumericUpDown{Minimum=1,Maximum=1000000,Value=Math.Max(1,Draft.Density>0?Draft.Density:50)};row.Controls.Add(limit);
  mode.SelectedIndex=Draft.Density<0?1:Draft.Density==0?0:2;limit.Enabled=mode.SelectedIndex==2;
  mode.SelectedIndexChanged+=(s,e)=>{limit.Enabled=mode.SelectedIndex==2;Draft.Density=limit.Enabled?(int)limit.Value:-mode.SelectedIndex;};
  limit.ValueChanged+=(s,e)=>{if(limit.Enabled)Draft.Density=(int)limit.Value;};Row();
  Label("上限统计仍在屏幕上的普通和特殊弹幕。达到上限时过滤新到的普通弹幕。");Row();
  Num("Scroll","滚动通过（秒）",0.01M,3600,2);Num("Fixed","固定停留（秒）",0.01M,3600,2);Row();
  Num("Offset","时间轴偏移（秒）",-86400,86400,3);Row();Label("偏移适用于四种输出。正值延后，负值提前；早于零点的事件会过滤并计数。");
  Tab("屏蔽与画面 · ASS");Label("勾选即屏蔽");Row();
  var labels=new[]{"右→左滚动","左→右滚动","顶部固定","底部固定","特殊弹幕","非白色","重复内容"};
  for(int i=0;i<labels.Length;i++)Check(Settings.Blocks[i],labels[i]);Row();Label("重复内容：同屏仍有完全相同文字的普通弹幕时，屏蔽后到的一条。");Row();
  Num("ResX","分辨率  宽",1,16384);Num("ResY","高",1,16384);Row();
  Num("ScrollArea","滚动区域（%）",0.1M,100,1,100);Num("Area","全部区域（%）",0.1M,100,1,100);Row();
  Check("Table","统计表");Check("Histogram","直方图");Row();Label("调试图直接写入 ASS 字幕。播放视频并加载输出的 ASS 即可查看。");Row();Check("ShowUsers","普通弹幕显示用户名（有记录时）");
  Tab("消息框 · ASS");Check("Boxes","启用消息框");Check("Gifts","显示礼物");Check("Chats","显示 SC");Row();
  Preview();
  Num("X","左上角 X（像素）",0,32767);Num("Y","Y",0,32767);Num("Width","宽",1,32767);Num("Height","高",1,32767);Row();
  Num("Font","框内字号",1,500);Num("Duration","消息框时长（秒，0 自动）",0,3600,2);Row();
  Num("MinPrice","礼物最低总价（元）",0,10000000,2);Row();Label("0 元允许无价格礼物；大于 0 时单独统计并过滤总价不明的礼物。SC、上舰单独处理。");
  Tab("上舰 · ASS");Check("Guards","显示上舰（独立开关）");Row();Preview();
  Num("GuardX","左上角 X（像素）",0,32767);Num("GuardY","Y",0,32767);Num("GuardWidth","宽",1,32767);Num("GuardHeight","高",1,32767);Row();
  Num("GuardFont","字号",8,500);Num("GuardDuration","显示时长（秒）",0,86400,2);Num("GuardOpacity","背景不透明度（%）",0,100);Row();
  var color=new Button{Text="背景颜色 "+Draft.GuardBackground,AutoSize=true};
  color.Click+=(s,e)=>{using(var d=new ColorDialog{FullOpen=true,Color=ColorTranslator.FromHtml(Draft.GuardBackground)})if(d.ShowDialog(this)==DialogResult.OK){Draft.GuardBackground="#"+(d.Color.ToArgb()&0xffffff).ToString("X6");color.Text="背景颜色 "+Draft.GuardBackground;}};row.Controls.Add(color);Row();
  Label("0 秒沿用消息框时长；消息框也为 0 时，舰长 198 秒、提督 1998 秒、总督 19998 秒，其他 18 秒。");Row();
  Label("空间不足时会报告发生时间和所需高度，停止该文件的导出。");
 }
 void Preview(){
  var p=new Panel{Width=645,Height=148,BackColor=SystemColors.ControlLight};
  previews.Add(p);p.Paint+=PaintPreview;row.Controls.Add(p);Row();
 }
 void PaintPreview(object sender,PaintEventArgs e){
  var g=e.Graphics;float scale=Math.Min(320f/Draft.ResX,126f/Draft.ResY);
  var frame=new RectangleF(14,10,Draft.ResX*scale,Draft.ResY*scale);
  g.FillRectangle(Brushes.White,frame);g.SetClip(frame);
  Action<int,int,int,int,Color,bool> box=(x,y,w,h,c,on)=>{
   var r=new RectangleF(frame.X+x*scale,frame.Y+y*scale,w*scale,h*scale);
   using(var brush=new SolidBrush(Color.FromArgb(on?60:12,c)))g.FillRectangle(brush,r);
   using(var pen=new Pen(c,1.5f)){if(!on)pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}
  };
  box(Draft.X,Draft.Y,Draft.Width,Draft.Height,Color.SteelBlue,Draft.Boxes);
  box(Draft.GuardX,Draft.GuardY,Draft.GuardWidth,Draft.GuardHeight,Color.Crimson,Draft.Guards);
  g.ResetClip();g.DrawRectangle(Pens.DimGray,frame.X,frame.Y,frame.Width,frame.Height);
  float left=frame.Right+12;
  g.DrawString("输出画面 "+Draft.ResX+" × "+Draft.ResY,Font,Brushes.Black,left,15);
  g.DrawString("消息框  ("+Draft.X+", "+Draft.Y+")   "+Draft.Width+" × "+Draft.Height,Font,Brushes.SteelBlue,left,45);
  g.DrawString("上舰框  ("+Draft.GuardX+", "+Draft.GuardY+")   "+Draft.GuardWidth+" × "+Draft.GuardHeight,Font,Brushes.Crimson,left,75);
 }
}
