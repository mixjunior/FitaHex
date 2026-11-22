using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Skin {
 public static Font UiFont(float size){var font=new Font("Segoe UI Variable Text",size);if(font.Name=="Segoe UI Variable Text")return font;font.Dispose();return new Font("Segoe UI",size);}
 public static Font HeadingFont(float size){return new Font("Segoe UI",size,FontStyle.Bold);}
 internal static void Symbol(Graphics g,string kind,Rectangle bounds,Color color){
  if(String.IsNullOrEmpty(kind))return;
  var state=g.Save();
  g.SmoothingMode=SmoothingMode.AntiAlias;
  g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/20f,bounds.Height/20f);
  using(var pen=new Pen(color,1.65f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
  using(var brush=new SolidBrush(color)){
   switch(kind){
    case "nexus":g.DrawPolygon(pen,new[]{new Point(10,1),new Point(19,6),new Point(19,14),new Point(10,19),new Point(1,14),new Point(1,6)});g.DrawLines(pen,new[]{new Point(7,14),new Point(7,6),new Point(14,6)});g.DrawLine(pen,7,10,12,10);break;
    case "menu":g.DrawRectangle(pen,2,4,16,12);g.DrawLine(pen,7,4,7,16);break;
    case "grid":for(int y=2;y<=11;y+=9)for(int x=2;x<=11;x+=9)g.DrawRectangle(pen,x,y,6,6);break;
    case "clock":g.DrawEllipse(pen,2,2,16,16);g.DrawLines(pen,new[]{new Point(10,5),new Point(10,10),new Point(14,12)});break;
    case "box":g.DrawRectangle(pen,3,3,14,14);break;
    case "lock":g.DrawArc(pen,6,2,8,10,180,180);g.DrawRectangle(pen,4,8,12,9);g.DrawLine(pen,10,11,10,14);break;
    case "check":g.DrawEllipse(pen,2,2,16,16);g.DrawLines(pen,new[]{new Point(6,10),new Point(9,13),new Point(14,7)});break;
    case "star":g.DrawPolygon(pen,new[]{new Point(10,2),new Point(12,7),new Point(18,8),new Point(14,12),new Point(15,18),new Point(10,15),new Point(5,18),new Point(6,12),new Point(2,8),new Point(8,7)});break;
    case "folder":g.DrawPolygon(pen,new[]{new Point(2,5),new Point(8,5),new Point(10,7),new Point(18,7),new Point(18,16),new Point(2,16)});break;
    case "social":g.DrawEllipse(pen,7,2,6,6);g.DrawArc(pen,3,10,14,11,180,180);break;
    case "play":g.FillPolygon(brush,new[]{new Point(5,3),new Point(17,10),new Point(5,17)});break;
    case "download":g.DrawLine(pen,10,2,10,13);g.DrawLines(pen,new[]{new Point(5,9),new Point(10,14),new Point(15,9)});g.DrawLine(pen,3,17,17,17);break;
    case "plus":g.DrawLine(pen,3,10,17,10);g.DrawLine(pen,10,3,10,17);break;
    case "close":g.DrawLine(pen,5,5,15,15);g.DrawLine(pen,15,5,5,15);break;
    case "settings":for(int i=4;i<=16;i+=6)g.DrawLine(pen,3,i,17,i);g.FillEllipse(brush,5,2,4,4);g.FillEllipse(brush,11,8,4,4);g.FillEllipse(brush,7,14,4,4);break;
    case "search":g.DrawEllipse(pen,2,2,11,11);g.DrawLine(pen,12,12,18,18);break;
    case "filter":g.DrawPolygon(pen,new[]{new Point(2,4),new Point(18,4),new Point(12,11),new Point(12,16),new Point(8,18),new Point(8,11)});break;
   }
  }
  g.Restore(state);
 }
 public static Color Mix(Color a,Color b,float t){
  return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));
 }
 static double Linear(byte channel){
  double value=channel/255.0;
  return value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
 }
 internal static Color OnAccent(Color fill){
  double luminance=.2126*Linear(fill.R)+.7152*Linear(fill.G)+.0722*Linear(fill.B);
  return luminance>.179?Color.Black:Color.White;
 }
 public static GraphicsPath Round(Rectangle r,int radius){
  var p=new GraphicsPath();int d=radius*2;
  if(r.Width<d||r.Height<d){p.AddRectangle(r);return p;}
  p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);
  p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
 }
 public static void Crop(Graphics g,Image image,Rectangle r){
  float scale=Math.Max((float)r.Width/image.Width,(float)r.Height/image.Height);
  float w=r.Width/scale,h=r.Height/scale;
  g.DrawImage(image,r,(image.Width-w)/2,(image.Height-h)/2,w,h,GraphicsUnit.Pixel);
 }
}
sealed class BrandLabel : Label {
 internal Color Accent;
 protected override void OnPaint(PaintEventArgs e){
  Skin.Symbol(e.Graphics,"nexus",new Rectangle(5,(Height-23)/2,23,23),Accent);
  var text=new Rectangle(Padding.Left,0,Math.Max(0,Width-Padding.Left-Padding.Right),Height);
  TextRenderer.DrawText(e.Graphics,Text,Font,text,ForeColor,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
 }
}
class SoftPanel : Panel {
 public SoftPanel(){DoubleBuffered=true;ResizeRedraw=true;}
}
class SoftFlow : FlowLayoutPanel {
 public SoftFlow(){DoubleBuffered=true;ResizeRedraw=true;}
}
class HubButton : Button {
 readonly Timer timer=new Timer{Interval=15};
 float glow,target;
 bool open,active;
 internal bool Active {get{return active;}set{if(active!=value){active=value;Invalidate();}}}
 internal bool Sidebar,IconOnly,Primary;
 internal string Glyph="",Counter="";
 internal Image GlyphImage;
 internal PaintEventHandler PaintBackdrop;
 internal Color Accent=Color.FromArgb(147,105,244);
 public HubButton(){
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  SetStyle(ControlStyles.Opaque,false);
  timer.Tick+=(s,e)=>{glow+=(target-glow)*.29f;if(Math.Abs(glow-target)<.025f){glow=target;timer.Stop();}Invalidate();};
 }
 void Hover(float value){target=value;if(!timer.Enabled)timer.Start();}
 protected override void OnMouseEnter(EventArgs e){base.OnMouseEnter(e);Hover(1);}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);Hover(0);}
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);open=true;Invalidate();}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);open=false;Invalidate();}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);open=false;Invalidate();}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaintBackground(PaintEventArgs e){
  if(Parent!=null&&Parent.BackColor.A==255)e.Graphics.Clear(Parent.BackColor);
  else base.OnPaintBackground(e);
 }
 protected override void OnPaint(PaintEventArgs e){
  if(Width<5||Height<5)return;
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  var bounds=new Rectangle(1,1,Width-3,Height-3);
  bool flat=Sidebar||Text=="";
  var basis=Primary?Skin.Mix(BackColor,Accent,.87f):Active?Skin.Mix(BackColor,Accent,.19f):BackColor;
  var fill=Skin.Mix(basis,Primary?ForeColor:Accent,open?.21f:glow*.13f);
  var ink=Enabled?(Primary?Skin.OnAccent(fill):ForeColor):Skin.Mix(ForeColor,BackColor,.45f);
  using(var path=Skin.Round(bounds,8)){
   if(!flat||Primary||Active||glow>0||open){
    using(var brush=new SolidBrush(fill))g.FillPath(brush,path);
    if(!Primary&&!flat)using(var seam=new Pen(Skin.Mix(fill,ForeColor,.12f)))g.DrawPath(seam,path);
   }
   if(Focused&&ShowFocusCues)using(var pen=new Pen(Accent,1.5f))g.DrawPath(pen,path);
  }
  if(Active&&Sidebar)using(var indicator=Skin.Round(new Rectangle(3,Height/2-8,3,16),1))
   using(var brush=new SolidBrush(Accent))g.FillPath(brush,indicator);
  bool icon=Glyph!=""||GlyphImage!=null;
  if(icon){
   var glyph=new Rectangle(IconOnly||Text==""?(Width-18)/2:11,(Height-18)/2,18,18);
   if(GlyphImage!=null){
    float scale=Math.Min(18f/GlyphImage.Width,18f/GlyphImage.Height);
    g.DrawImage(GlyphImage,new RectangleF(glyph.X+(18-GlyphImage.Width*scale)/2,glyph.Y+(18-GlyphImage.Height*scale)/2,GlyphImage.Width*scale,GlyphImage.Height*scale));
   }else Skin.Symbol(g,Glyph,glyph,Active&&Sidebar?Accent:ink);
  }
  if(IconOnly||Text=="")return;
  bool counter=Sidebar&&Counter!="";
  int left=icon?38:Sidebar?15:5;
  TextRenderer.DrawText(g,Text,Font,new Rectangle(left,0,Math.Max(0,Width-left-(counter?39:6)),Height),ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|(Sidebar||icon?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));
  if(counter)TextRenderer.DrawText(g,Counter,Font,new Rectangle(Width-39,0,31,Height),Skin.Mix(ForeColor,BackColor,.37f),TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
 }
 protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
class Gallery : ListView {
 public Gallery(){DoubleBuffered=true;}
 [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr window,string app,string classes);
 internal void ApplyWindowsTheme(){
  if(IsHandleCreated)SetWindowTheme(Handle,BackColor.GetBrightness()<.45f?"DarkMode_Explorer":"Explorer",null);
 }
 const int GetGroupRect=0x1062, GetGroupInfoByIndex=0x1099, GetGroupCount=0x1098;
 const int SetGroupInfo=0x1093, SetIconSpacing=0x1035;
 const int PaintMessage=0x000f, PrintClient=0x0318, VerticalScroll=0x0115, WheelMessage=0x020a;
 [StructLayout(LayoutKind.Sequential)]
 struct GroupInfo{
  public uint Size,Mask;
  public IntPtr Header;
  public int HeaderLength;
  public IntPtr Footer;
  public int FooterLength,Id;
  public uint StateMask,State,Align;
  public IntPtr Subtitle;
  public uint SubtitleLength;
  public IntPtr Task;
  public uint TaskLength;
  public IntPtr DescriptionTop;
  public uint DescriptionTopLength;
  public IntPtr DescriptionBottom;
  public uint DescriptionBottomLength;
  public int TitleImage,ExtendedImage,FirstItem;
  public uint ItemCount;
  public IntPtr SubsetTitle;
  public uint SubsetTitleLength;
 }
 [StructLayout(LayoutKind.Sequential)]
 struct NativeRect{public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll",EntryPoint="SendMessageW")]
 static extern IntPtr SendGroup(IntPtr window,int message,IntPtr index,ref GroupInfo info);
 [DllImport("user32.dll",EntryPoint="SendMessageW")]
 static extern IntPtr SendRect(IntPtr window,int message,IntPtr id,ref NativeRect rect);
 [DllImport("user32.dll",EntryPoint="SendMessageW")]
 static extern IntPtr SendCount(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
 readonly HashSet<string> collapsed=new HashSet<string>();
 readonly List<Tuple<Rectangle,string,int>> groupHeaders=new List<Tuple<Rectangle,string,int>>();
 internal readonly Dictionary<string,int> GroupTotals=new Dictionary<string,int>();
 internal void IconSpacing(Size art){
  if(IsHandleCreated)SendCount(Handle,SetIconSpacing,IntPtr.Zero,(IntPtr)((art.Height+36)<<16|(art.Width+20)));
 }
 internal void ResetGroupHeaders(){groupHeaders.Clear();}
 void SetCollapsed(string name,int id,bool value){
  var info=new GroupInfo{Size=(uint)Marshal.SizeOf(typeof(GroupInfo)),Mask=4,StateMask=9,State=(uint)(value?9:8)};
  SendGroup(Handle,SetGroupInfo,(IntPtr)id,ref info);
  if(value)collapsed.Add(name);else collapsed.Remove(name);
 }
 internal void RestoreCollapsed(){
  if(!IsHandleCreated||collapsed.Count==0||Groups.Count==0)return;
  var buffer=Marshal.AllocHGlobal(2048);
  try{
   int count=SendCount(Handle,GetGroupCount,IntPtr.Zero,IntPtr.Zero).ToInt32();
   for(int index=0;index<count;index++){
    var info=new GroupInfo{Size=(uint)Marshal.SizeOf(typeof(GroupInfo)),Mask=0x11,Header=buffer,HeaderLength=1024};
    if(SendGroup(Handle,GetGroupInfoByIndex,(IntPtr)index,ref info)==IntPtr.Zero||info.Header==IntPtr.Zero)continue;
    string name=Marshal.PtrToStringUni(info.Header);
    if(name!=null&&collapsed.Contains(name))SetCollapsed(name,info.Id,true);
   }
  }finally{Marshal.FreeHGlobal(buffer);}
 }
 protected override void OnMouseDown(MouseEventArgs e){
  if(e.Button==MouseButtons.Left){
   foreach(var group in groupHeaders)if(group.Item1.Contains(e.Location)){
    SetCollapsed(group.Item2,group.Item3,!collapsed.Contains(group.Item2));Invalidate();return;
   }
  }
  base.OnMouseDown(e);
 }
 readonly Font heading=Skin.HeadingFont(11);
 protected override void WndProc(ref Message message){
  base.WndProc(ref message);
  if(message.Msg==VerticalScroll||message.Msg==WheelMessage)Invalidate();
  if((message.Msg!=PaintMessage&&message.Msg!=PrintClient)||!IsHandleCreated||!ShowGroups||Groups.Count==0)return;
  if(message.Msg==PrintClient&&message.WParam==IntPtr.Zero)return;
  using(var graphics=message.Msg==PaintMessage?Graphics.FromHwnd(Handle):Graphics.FromHdc(message.WParam))
  using(var background=new SolidBrush(BackColor))
  using(var line=new Pen(Skin.Mix(BackColor,ForeColor,.13f))){
   if(message.Msg==PaintMessage)groupHeaders.Clear();
   int count=SendCount(Handle,GetGroupCount,IntPtr.Zero,IntPtr.Zero).ToInt32();
   var buffer=Marshal.AllocHGlobal(2048);
   try{
    for(int index=0;index<count;index++){
     Marshal.WriteInt16(buffer,0);
     var info=new GroupInfo{Size=(uint)Marshal.SizeOf(typeof(GroupInfo)),Mask=0x11,Header=buffer,HeaderLength=1024};
     if(SendGroup(Handle,GetGroupInfoByIndex,(IntPtr)index,ref info)==IntPtr.Zero||info.Header==IntPtr.Zero)continue;
     var name=Marshal.PtrToStringUni(info.Header);
     if(String.IsNullOrEmpty(name)||!HasItems(name))continue;
     var rect=new NativeRect{Top=1};
     if(SendRect(Handle,GetGroupRect,(IntPtr)info.Id,ref rect)==IntPtr.Zero)continue;
     var band=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
     if(!band.IntersectsWith(ClientRectangle))continue;
     graphics.FillRectangle(background,band);
     if(message.Msg==PaintMessage)groupHeaders.Add(Tuple.Create(band,name,info.Id));
     int total;
     string caption=(collapsed.Contains(name)?"›  ":"⌄  ")+name+(GroupTotals.TryGetValue(name,out total)?"   ·   "+total:"");
     var label=Rectangle.Inflate(band,-8,0);
     var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding;
     TextRenderer.DrawText(graphics,caption,heading,label,ForeColor,flags);
     int start=label.Left+TextRenderer.MeasureText(graphics,caption,heading,Size.Empty,TextFormatFlags.NoPadding).Width+14;
     if(start<label.Right)graphics.DrawLine(line,start,band.Top+band.Height/2,label.Right,band.Top+band.Height/2);
    }
   }finally{Marshal.FreeHGlobal(buffer);}
  }
 }
 bool HasItems(string name){
  foreach(ListViewGroup group in Groups)if(group.Header==name&&group.Items.Count>0)return true;
  return false;
 }
 protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)heading.Dispose();}
}