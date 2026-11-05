using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Web.Script.Serialization;
using D=System.Collections.Generic.Dictionary<string,object>;

partial class Settings {
 public int ResX=1920, ResY=1080, TextSize=38, Shadow=1, Outline=0, Opacity=180;
 public decimal Fixed=5, Offset=0, ScrollArea=1, MinPrice=0;
 public bool Bold, ShowUsers, Table, Histogram, R2L,L2R,Top,Bottom,Special,Color,Repeat;
 public bool Boxes=true;
 public int GuardX=24,GuardY=24,GuardWidth=360,GuardHeight=480,GuardFont=28,GuardOpacity=88;
 public string GuardBackground="#203348";
 public void ValidateGuard(){
  if(!Guards)return;
  if(GuardX<0||GuardY<0||GuardWidth<1||GuardHeight<1||(long)GuardX+GuardWidth>ResX||(long)GuardY+GuardHeight>ResY)
   throw new ArgumentException("上舰框超出输出画面，请调整上舰框坐标、尺寸或输出分辨率。");
  if(GuardFont<8||GuardFont>500||GuardOpacity<0||GuardOpacity>100||!Regex.IsMatch(GuardBackground??"","^#[0-9a-fA-F]{6}$"))
   throw new ArgumentException("上舰字号、背景颜色或不透明度无效。");
  if(GuardDuration<0||GuardDuration>86400||decimal.Round(GuardDuration,2)!=GuardDuration)
   throw new ArgumentException("上舰时长须为 0 或 0.01 至 86400 秒，最多两位小数。");
 }
 public string FontName="Microsoft YaHei";
 public int Density=0, Width=500, Height=1080, X=20, Y=0, Font=38;
 public decimal Area=1, Scroll=12, Duration=0, GuardDuration=0;
 public bool Gifts=true, Chats=true, Guards=true;
}
class ConvertFile {
 public static readonly JavaScriptSerializer J=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
 static string S(D r,string k,string fallback=""){return r.ContainsKey(k)?Convert.ToString(r[k],CultureInfo.InvariantCulture):fallback;}
 static string Kind(D r){return S(r,"type");}
 static string Encode(D r){return Convert.ToBase64String(Encoding.UTF8.GetBytes(J.Serialize(r)));}
 static D Decode(string s){return J.Deserialize<D>(Encoding.UTF8.GetString(Convert.FromBase64String(s)));}
 static readonly string[] keys={"ts","mode","font_size","color"};
 static D Normal(string[] parts,string text){
  if(parts.Length<4)throw new FormatException("弹幕参数不足");
  var r=new D{{"type","danmaku"},{"text",text}};
  for(int i=0;i<4;i++)r[keys[i]]=decimal.Parse(parts[i],CultureInfo.InvariantCulture);
  return r;
 }
 static void JsonItems(object node,List<D> rows){
  var d=node as D;
  if(d!=null){var a=S(d,"c").Split(','); rows.Add(Normal(new[]{a[0],a[2],a[3],a[1]},S(d,"m")));return;}
  var arr=node as object[]; if(arr==null)throw new FormatException("需要 AcFun c/m 数组");foreach(var item in arr)JsonItems(item,rows);
 }
 static string[] liveKeys={"ts","user","uid","gift_name","gift_count","level","count","price","time"};
 static string Attr(string k){return k=="gift_name"?"giftname":k=="gift_count"?"giftcount":k;}
 static List<D> Read(string path){
  var rows=new List<D>();int line=0;
  try{switch(Path.GetExtension(path).ToLowerInvariant()){
  case ".jsonl":foreach(string s in File.ReadLines(path)){line++;if(s.Trim().Length>0)rows.Add(J.Deserialize<D>(s));}break;
  case ".json":JsonItems(J.DeserializeObject(File.ReadAllText(path)),rows);break;
  case ".xml":foreach(var e in XDocument.Load(path).Root.Elements()){
   if(e.Attribute("recorder")!=null){rows.Add(Decode((string)e.Attribute("recorder")));continue;}
   string t=e.Name.LocalName;
   if(t=="d"){var d=Normal(((string)e.Attribute("p")).Split(','),e.Value);foreach(string k in new[]{"user","uid"})if(e.Attribute(k)!=null)d[k]=(string)e.Attribute(k);rows.Add(d);}
   else if(t=="gift"||t=="guard"||t=="sc"){var d=new D{{"type",t=="sc"?"super_chat":t}};foreach(string k in liveKeys)if(e.Attribute(Attr(k))!=null){string v=(string)e.Attribute(Attr(k));decimal n; d[k]=k!="user"&&k!="gift_name"&&decimal.TryParse(v,out n)?(object)n:v;}if(t=="sc")d["message"]=e.Value;rows.Add(d);}
   else if(t=="BililiveRecorder" && e.HasElements){var d=new D{{"type","meta"}};foreach(var v in e.Elements())d[v.Name.LocalName]=v.Value;rows.Add(d);}
  }break;
  case ".ass":
   var styles=new Dictionary<string,string[]>();bool packed=false;
   foreach(string s in File.ReadLines(path)){
    line++;
    if(s=="; Recorder live archive")packed=true;
    if(s.StartsWith(";@event "))rows.Add(Decode(s.Substring(8)));
    if(s.StartsWith("Style:")){var a=s.Substring(6).Trim().Split(',');styles[a[0]]=a;}
    if(!s.StartsWith("Dialogue:",StringComparison.OrdinalIgnoreCase))continue;
    var f=s.Substring(9).TrimStart().Split(new[]{','},10);if(f[3]=="danmakuFactory_stat" || ((f[3]=="MSG"||f[3]=="Guard")&&packed))continue;
    if(Regex.IsMatch(f[9],@"\\p[1-9]")){rows.Add(new D{{"type","ass_drawing"}});continue;}
    string mode=f[3]=="TOP"?"5":f[3]=="BTM"?"4":f[3]=="L2R"?"6":"1";
    string[] st;styles.TryGetValue(f[3],out st);var fs=Regex.Match(f[9],@"\\fs(\d+)");var c=Regex.Match(f[9],@"\\(?:1?c)&H([0-9a-fA-F]{6})");
    string hex=c.Success?c.Groups[1].Value:st!=null?st[3].Trim().TrimEnd('&'):"FFFFFF"; hex=hex.Substring(hex.Length-6);
    string rgb=hex.Substring(4)+hex.Substring(2,2)+hex.Substring(0,2);
    var h=f[1].Split(':');decimal sec=decimal.Parse(h[0])*3600+decimal.Parse(h[1])*60+decimal.Parse(h[2]);
    rows.Add(Normal(new[]{sec.ToString(),mode,fs.Success?fs.Groups[1].Value:st!=null?st[2]:"25",Convert.ToInt32(rgb,16).ToString()},Regex.Replace(f[9],@"\{[^}]*\}","").Replace(@"\N","\n").Replace(@"\h"," ")));
   }break;
  default:throw new FormatException("不支持的文件扩展名");}
  foreach(var r in rows){if(String.IsNullOrEmpty(Kind(r)))throw new FormatException("缺少 type");if(new[]{"danmaku","gift","guard","super_chat"}.Contains(Kind(r))){decimal t;if(!decimal.TryParse(S(r,"ts"),out t)||t<0)throw new FormatException("无效 ts");}}
  return rows;}catch(Exception ex){throw new InvalidDataException((line>0?"第 "+line+" 行附近：":"")+ex.Message,ex);}
 }
 static string Param(D r){return string.Join(",",S(r,"ts"),S(r,"mode","1"),S(r,"font_size","25"),S(r,"color","16777215"),"0,0,0,0");}
 static XElement Element(D r){
  string t=Kind(r);if(t=="danmaku"){
   var d=new XElement("d",new XAttribute("p",Param(r)),S(r,"text"));
   foreach(string key in new[]{"user","uid"})if(r.ContainsKey(key))d.SetAttributeValue(key,S(r,key));
   return d;
  }
  var e=new XElement(t=="super_chat"?"sc":t);foreach(string k in liveKeys)if(r.ContainsKey(k))e.SetAttributeValue(Attr(k),S(r,k));if(t=="super_chat")e.Value=S(r,"message");return e;
 }
 static string Counts(IEnumerable<D> rows){var a=rows.GroupBy(Kind).Select(g=>g.Key+" "+g.Count()).ToArray();return a.Length==0?"0":string.Join("，",a);}
 static bool Known(D r){return new[]{"meta","danmaku","super_chat","gift","guard"}.Contains(Kind(r));}
 public static string Run(string path,string ext,string folder,Settings opt,Action<string> status){
  status("读取中");var rows=Read(path);var skipped=new List<D>();var filtered=new List<D>();var exported=new List<D>();
  folder=string.IsNullOrEmpty(folder)?Path.GetDirectoryName(Path.GetFullPath(path)):folder;
  string tmp=Path.Combine(folder,"."+Guid.NewGuid()+".tmp");
  try{
   int unknown=0, low=0, early=0;var items=new List<D>();foreach(var r in rows){string t=Kind(r);
    if(opt.Offset!=0 && r.ContainsKey("ts") && Known(r)){
     decimal ts=Convert.ToDecimal(r["ts"])+opt.Offset;
     if(ts<0){early++;filtered.Add(r);continue;}r["ts"]=ts;
    }
    if(ext=="ass" && t=="gift" && opt.Boxes && opt.Gifts && opt.MinPrice>0){
     decimal total;
     if(!TryGiftTotal(r,out total)){unknown++;filtered.Add(r);continue;}
     if(total<opt.MinPrice){low++;filtered.Add(r);continue;}
    }
    if(ext=="jsonl"&&t!="ass_drawing")items.Add(r);
    else if(!Known(r)||(ext=="json"&&t!="danmaku")||(ext=="ass"&&t=="meta"))skipped.Add(r);
    else if(ext=="ass"&&((!opt.Boxes&&(t=="gift"||t=="super_chat"))||(t=="gift"&&!opt.Gifts)||(t=="guard"&&!opt.Guards)||(t=="super_chat"&&!opt.Chats)))filtered.Add(r);
    else items.Add(r);}
   status("正在生成 "+ext.ToUpperInvariant());
   if(ext=="ass")Render(items,tmp,opt,exported,filtered,skipped,status);
   else{exported.AddRange(items);
    if(ext=="jsonl")File.WriteAllLines(tmp,items.Select(J.Serialize),new UTF8Encoding(false));
    else if(ext=="xml")new XDocument(new XElement("i",new XElement("BililiveRecorder"),items.Select(r=>{var e=Element(r);e.SetAttributeValue("recorder",Encode(r));return e;}))).Save(tmp);
    else if(ext=="json")File.WriteAllText(tmp,J.Serialize(new object[]{new object[0],new object[0],items.Select(r=>new {c=string.Join(",",S(r,"ts"),S(r,"color","16777215"),S(r,"mode","1"),S(r,"font_size","25"),",,"),m=S(r,"text")})}));
    else throw new FormatException("未知目标格式");}
   string stem=Path.Combine(folder,Path.GetFileNameWithoutExtension(path));
   for(int n=0;;n++){string dest=stem+(n==0?"":" ("+n+")")+"."+ext;
    try{File.Move(tmp,dest);}catch(IOException){if(File.Exists(dest))continue;throw;}
    return dest+"\r\n读取 "+Counts(rows)+"\r\n设置过滤 "+Counts(filtered)+"（无明确总价礼物 "+unknown+"，低于最低价礼物 "+low+"，偏移后早于零点 "+early+"）；跳过 "+Counts(skipped)+"\r\n导出 "+Counts(exported);}
  }finally{if(File.Exists(tmp))File.Delete(tmp);}
 }
 // Only explicit totals carry enough information for a per-record price comparison.
 static bool TryGiftTotal(D row,out decimal value){
  if(decimal.TryParse(S(row,"total_price_cny"),out value)&&value>=0)return true;
  if(S(row,"coin_type")=="gold" && decimal.TryParse(S(row,"total_coin"),out value)&&value>=0){value/=1000;return true;}
  return false;
 }
 [DllImport("danmakufactory_core.dll",CallingConvention=CallingConvention.Cdecl)]static extern int runCommand(int argc,[In,MarshalAs(UnmanagedType.LPArray,ArraySubType=UnmanagedType.LPStr)]string[] argv);
 public static int Native(string[] args){return runCommand(args.Length,args);}
 static void RunEngine(string work,string args){
  using(var p=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,args){WorkingDirectory=work,UseShellExecute=false,CreateNoWindow=true})){
   p.WaitForExit();if(p.ExitCode!=0)throw new Exception("转换引擎退出码 "+p.ExitCode);
  }
 }
 static string Safe(string s){return s.Replace("{","｛").Replace("}","｝").Replace("\\","＼").Replace("\r\n","\n").Replace("\r","\n").Replace("\n",@"\N");}
 sealed class GuardLayout {
  sealed class Card {
   public D Row;public long Start,End;public int Top,Height;public List<string> Lines;
  }
  readonly List<Card> cards=new List<Card>();
  readonly Settings settings;
  readonly int padding,gap,lineHeight;
  static long Centiseconds(decimal seconds){return (long)decimal.Round(seconds*100,0,MidpointRounding.AwayFromZero);}
  static string Stamp(long cs){return string.Format("{0}:{1:00}:{2:00}.{3:00}",cs/360000,cs/6000%60,cs/100%60,cs%100);}
  static decimal AutoSeconds(string level,Settings o){
   if(o.Duration>0)return o.Duration;
   return level=="1"?19998:level=="2"?1998:level=="3"?198:18;
  }
  static List<string> Wrap(string text,System.Drawing.Graphics g,System.Drawing.Font font,int width){
   var result=new List<string>();
   foreach(string paragraph in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n')){
    var line=new StringBuilder();float used=0;
    var elements=StringInfo.GetTextElementEnumerator(paragraph.Replace("\t","    "));
    while(elements.MoveNext()){
     string ch=elements.GetTextElement();
     float advance=g.MeasureString(ch,font,int.MaxValue,System.Drawing.StringFormat.GenericTypographic).Width*1.12f;
     if(advance>width)throw new ArgumentException("上舰框太窄，请增大宽度以容纳文字。");
     if(line.Length>0 && used+advance>width){result.Add(line.ToString());line.Clear();used=0;}
     line.Append(ch);used+=advance;
    }
    result.Add(line.ToString());
   }return result;
  }
  public GuardLayout(List<D> items,Settings o){
   settings=o;
   var rows=items.Where(r=>Kind(r)=="guard").OrderBy(r=>Convert.ToDecimal(r["ts"])).ToList();
   if(rows.Count==0)return;
   o.ValidateGuard();padding=Math.Max(4,o.GuardFont/3);gap=Math.Max(4,o.GuardFont/5);
   lineHeight=(int)Math.Ceiling(o.GuardFont*1.45);
   var active=new List<Card>();
   using(var bitmap=new System.Drawing.Bitmap(1,1))
   using(var g=System.Drawing.Graphics.FromImage(bitmap))
   using(var font=new System.Drawing.Font(o.FontName,o.GuardFont,System.Drawing.GraphicsUnit.Pixel)){
    foreach(var r in rows){
     long start=Centiseconds(Convert.ToDecimal(r["ts"]));string level=S(r,"level");
     string title=level=="1"?"总督":level=="2"?"提督":level=="3"?"舰长":"上舰";
     string user=S(r,"user");
     string action="开通了"+title;
     int textWidth=o.GuardWidth-2*padding;
     var lines=Wrap(user,g,font,textWidth);
     int last=lines.Count-1;
     if(Wrap(lines[last]+action,g,font,textWidth).Count==1)lines[last]+=action;
     else{
      if(lines[last].Length==0)lines.RemoveAt(last);
      lines.AddRange(Wrap(action,g,font,textWidth));
     }
     int height=2*padding+lines.Count*lineHeight;
     active.RemoveAll(c=>c.End<=start);
     int y=0;foreach(var busy in active.OrderBy(c=>c.Top)){
      if(y+height+gap<=busy.Top)break;
      y=busy.Top+busy.Height+gap;
     }
     if(y+height>o.GuardHeight)throw new ArgumentException("上舰框在 "+Stamp(start)+" 放不下「"+user+"」，需要至少 "+(y+height)+" 像素高（当前 "+o.GuardHeight+"）。请增大框高、加宽框以减少换行，或缩短显示时长后重试。");
     var card=new Card{Row=r,Start=start,End=start+Centiseconds(o.GuardDuration>0?o.GuardDuration:AutoSeconds(level,o)),Top=y,Height=height,Lines=lines};
     cards.Add(card);active.Add(card);
    }
   }
  }
  public void Write(StreamWriter w,List<D> exported){
   var o=settings;
   foreach(var card in cards){
    int x=o.GuardX,y=o.GuardY+card.Top;
    string line=Stamp(card.Start)+","+Stamp(card.End)+",Guard,,0,0,0,,";
    string rgb=o.GuardBackground.Substring(1);
    string bgr=rgb.Substring(4)+rgb.Substring(2,2)+rgb.Substring(0,2);
    string alpha=((int)Math.Round(255*(1-o.GuardOpacity/100.0))).ToString("X2");
    string clip=string.Format(@"\an7\q2\shad0\clip({0},{1},{2},{3})",x,y,x+o.GuardWidth,y+card.Height);
    w.WriteLine("Dialogue: 20,"+line+"{"+clip+@"\pos("+x+","+y+@")\bord0\1c&H"+bgr+@"&\1a&H"+alpha+@"&\p1}m 0 0 l "+o.GuardWidth+" 0 "+o.GuardWidth+" "+card.Height+" 0 "+card.Height);
    for(int i=0;i<card.Lines.Count;i++){
     string pos=string.Format(@"\pos({0},{1})",x+padding,y+padding+i*lineHeight);
     w.WriteLine("Dialogue: 21,"+line+"{"+clip+pos+@"\bord1.4\3c&H171717&\1c&HFFFFFF&}"+Safe(card.Lines[i]));
    }
    exported.Add(card.Row);w.WriteLine(";@event "+Encode(card.Row));
   }
  }
 }
 static void Render(List<D> items,string output,Settings o,List<D> exported,List<D> filtered,List<D> skipped,Action<string> status){
  string work=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"dm-"+Guid.NewGuid())).FullName;
  try{
   var root=new XElement("i",new XElement("BililiveRecorder"));var live=new Dictionary<string,D>();var normals=new List<D>();
   var guards=new GuardLayout(items,o);
   foreach(var r in items){string t=Kind(r);
    if(t=="guard")continue;
    var e=Element(r);
    if(t=="danmaku"){if(!new[]{"1","2","3","4","5","6","7"}.Contains(S(r,"mode","1"))){skipped.Add(r);continue;} e.SetAttributeValue("user",o.ShowUsers&&r.ContainsKey("user")?Safe(S(r,"user")):null);e.SetAttributeValue("uid",null);if(S(r,"mode")!="7")e.Value=Safe(e.Value);normals.Add(r);}
    else{if((t=="gift"&&(!r.ContainsKey("gift_count")||!r.ContainsKey("gift_name")))||(t=="super_chat"&&!r.ContainsKey("price"))){skipped.Add(r);continue;}
     string id="DFU"+live.Count.ToString("D7")+"Z";live[id]=r;e.SetAttributeValue("user",id);e.SetAttributeValue("uid",live.Count); if(t=="gift")e.SetAttributeValue("giftname",Safe(S(r,"gift_name")));
     if(t=="super_chat")e.Value=Safe(e.Value);
    }root.Add(e);}
   if(!root.Elements().Skip(1).Any())root.Add(new XElement("d",new XAttribute("p","0,1,25,16777215,0,0,0,0"),"DFEMPTY"));
   new XDocument(root).Save(Path.Combine(work,"in.xml"));
   string args=string.Format("--native -i in.xml -o out.ass --force --ignore-warnings --giftminprice 0 -d {0} --displayarea {1} -s {2} --msgboxsize {3}x{4} --msgboxpos {5}x{6} --msgboxfontsize {7} --msgboxduration {8}",o.Density,o.Area,o.Scroll,o.Width,o.Height,o.X,o.Y,o.Font,o.Duration);
   args+=string.Format(" -r {0}x{1} -S {2} -D {3} -L {4} -O {5} -B {6} -f {7} --scrollarea {8} --showusernames {9} --showmsgbox {10}",o.ResX,o.ResY,o.TextSize,o.Shadow,o.Outline,o.Opacity,o.Bold.ToString().ToLowerInvariant(),o.Fixed,o.ScrollArea,o.ShowUsers.ToString().ToLowerInvariant(),o.Boxes.ToString().ToLowerInvariant());
   string blocks=string.Join("-",Settings.Blocks.Where(k=>(bool)typeof(Settings).GetField(k).GetValue(o)).Select(k=>k.ToUpperInvariant()));
   bool splitLive=blocks.Length>0 && live.Count>0 && o.Boxes;
   if(splitLive){
    var liveXml=new XElement("i",root.Elements().Where(e=>e.Name!="d"));new XDocument(liveXml).Save(Path.Combine(work,"live.xml"));
    RunEngine(work,args.Replace("in.xml","live.xml").Replace("out.ass","live.ass"));
    root.Elements().Where(e=>e.Name!="d"&&e.Name!="BililiveRecorder").Remove();
    if(!root.Elements("d").Any())root.Add(new XElement("d",new XAttribute("p","0,1,25,16777215,0,0,0,0"),"DFEMPTY"));
    new XDocument(root).Save(Path.Combine(work,"in.xml"));
   }
   if(blocks.Length>0)args+=" -b "+blocks;
   RunEngine(work,args);
   bool debug=o.Table||o.Histogram;
   if(debug && normals.Count>0){
    // Native statistics accept ordinary/special danmaku only. Render this layer separately.
    root.Elements().Where(n=>n.Name!="d").Remove();
    new XDocument(root).Save(Path.Combine(work,"stats.xml"));
    string mode=o.Table&&o.Histogram?"TABLE-HISTOGRAM":o.Table?"TABLE":"HISTOGRAM";
    RunEngine(work,args.Replace("in.xml","stats.xml").Replace("out.ass","stats.ass")+" --statmode "+mode);
   }
   status("核对字幕并写入");var seen=new HashSet<string>();int visible=0;
   using(var w=new StreamWriter(output,false,new UTF8Encoding(true))){
    w.WriteLine("; Recorder live archive");foreach(string s in File.ReadLines(Path.Combine(work,"out.ass")).Concat(splitLive?File.ReadLines(Path.Combine(work,"live.ass")).Where(v=>v.StartsWith("Dialogue:")&&v.Contains(",MSG,")):Enumerable.Empty<string>())){
     if(s=="[Events]" && o.Guards)w.WriteLine("Style: Guard,"+o.FontName+","+o.GuardFont+",&H00FFFFFF,&H00FFFFFF,&H00171717,&H00000000,0,0,0,0,100,100,0,0,1,1,0,7,0,0,0,1");
     if(debug && s=="[Events]"){
      if(normals.Count==0)w.WriteLine("Style: danmakuFactory_stat,Microsoft YaHei,24,&H00FFFFFF,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,1,0,7,0,0,0,1");
      else foreach(string style in File.ReadLines(Path.Combine(work,"stats.ass")).Where(v=>v.StartsWith("Style: danmakuFactory_stat,")))w.WriteLine(style);
     }
     if(s.StartsWith("Dialogue:",StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(s,@"^Dialogue:[^,]*,[^,]*,[^,]*,(R2L|L2R|TOP|BTM|SP),")){if(normals.Count==0)continue;visible++;}
     if(s.StartsWith("Style:")&&o.FontName!="Microsoft YaHei"){w.WriteLine(s.Replace(",Microsoft YaHei,",","+o.FontName+","));continue;}
     w.WriteLine(s.Contains(",MSG,")?Regex.Replace(s,@"DFU\d{7}Z",m=>{D r;if(!live.TryGetValue(m.Value,out r))return m.Value;seen.Add(m.Value);return Safe(S(r,"user"));}):s);
    }
    if(debug){
     if(normals.Count==0)w.WriteLine(@"Dialogue: 9,0:00:00.00,0:00:05.00,danmakuFactory_stat,,0,0,0,,{\an7\pos(20,20)}统计：弹幕 0 条");
     else foreach(string stat in File.ReadLines(Path.Combine(work,"stats.ass")).Where(v=>v.StartsWith("Dialogue:")&&v.Contains(",danmakuFactory_stat,")))w.WriteLine(stat);
    }
    guards.Write(w,exported);
    foreach(var pair in live){if(seen.Contains(pair.Key)){exported.Add(pair.Value);w.WriteLine(";@event "+Encode(pair.Value));}else skipped.Add(pair.Value);}
   }
   exported.AddRange(normals.Take(visible));filtered.AddRange(normals.Skip(visible));
  }finally{Directory.Delete(work,true);}
 }
}

