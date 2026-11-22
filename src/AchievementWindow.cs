using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class AchievementWindow : Form {
 static readonly SemaphoreSlim downloads=new SemaphoreSlim(3);
 readonly CancellationTokenSource closing=new CancellationTokenSource();
 readonly Dictionary<string,Image> icons=new Dictionary<string,Image>(StringComparer.Ordinal);
 readonly NexusTheme theme;
 readonly ListBox items=new ListBox();
 readonly Label note=new Label();
 readonly AchievementEntry[] entries;
 readonly HubButton earned, pending;
 readonly Font rowTitle=Skin.HeadingFont(11);
 readonly int expectedEarned,expectedPending;

 internal AchievementWindow(Game game,Game[] editions,NexusTheme palette){
  theme=palette;
  var reports=editions.Where(g=>g.Achievements!=null).Select(g=>g.Achievements).ToArray();
  expectedEarned=reports.Select(p=>p.Unlocked).DefaultIfEmpty(0).Max();
  expectedPending=reports.Select(p=>p.Total-p.Unlocked).DefaultIfEmpty(0).Max();
  entries=reports.SelectMany(p=>(p.Entries??new List<AchievementEntry>()).Concat((p.Names??new List<string>()).Select(name=>new AchievementEntry{Name=name,Unlocked=true})))
   .GroupBy(item=>item.Hidden?"hidden:"+item.Id:GameProgress.TitleKey(item.Name)).Select(group=>group.OrderByDescending(item=>item.Unlocked).ThenByDescending(item=>item.Icon!="").First())
   .OrderBy(entry=>entry.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();

  Text="Conquistas · "+game.Name;StartPosition=FormStartPosition.CenterParent;
  ClientSize=new Size(720,550);MinimumSize=new Size(520,415);BackColor=theme.Background;ForeColor=theme.Text;
  Font=Skin.UiFont(10);Padding=new Padding(18);MinimizeBox=false;
  var title=new Label{Text=game.Name,Font=Skin.HeadingFont(21),Dock=DockStyle.Top,Height=51,AutoEllipsis=true};
  var summary=new Label{Text=reports.Length==0?"Nenhuma plataforma forneceu dados de conquistas.":String.Join("   ·   ",editions.Where(g=>g.Achievements!=null)
   .Select(g=>g.Source+"  "+g.Achievements.Unlocked+"/"+g.Achievements.Total)),Dock=DockStyle.Top,Height=33,ForeColor=theme.Muted,AutoEllipsis=true};
  var filters=new SoftFlow{Dock=DockStyle.Top,Height=45};
  earned=Button(filters,reports.Length==0?"Desbloqueadas":"Desbloqueadas · "+expectedEarned,()=>Display(true));
  pending=Button(filters,reports.Length==0?"Pendentes":"Pendentes · "+expectedPending,()=>Display(false));

  items.Dock=DockStyle.Fill;items.BorderStyle=BorderStyle.None;items.IntegralHeight=false;items.BackColor=theme.Background;
  items.ForeColor=theme.Text;items.DrawMode=DrawMode.OwnerDrawFixed;items.ItemHeight=68;items.AccessibleName="Lista de conquistas";
  items.DrawItem+=DrawAchievement;
  note.Dock=DockStyle.Bottom;note.Height=34;note.TextAlign=ContentAlignment.MiddleLeft;note.ForeColor=theme.Muted;note.AutoEllipsis=true;

  var actions=new SoftFlow{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};
  var close=Button(actions,"Fechar",Close);close.DialogResult=DialogResult.Cancel;CancelButton=close;
  var steam=editions.FirstOrDefault(g=>Regex.IsMatch(g.StoreId??"",@"^steam:[0-9]+$"));
  if(steam!=null){
   Button(actions,"Ver lista completa na Steam",()=>{
    try{SocialPlatforms.Open("https://steamcommunity.com/my/stats/"+steam.StoreId.Substring(6)+"/?tab=achievements");}
    catch(System.ComponentModel.Win32Exception){MessageBox.Show(this,"Não foi possível abrir a Steam.","Conquistas");}
   });
  }
  Controls.Add(items);Controls.Add(note);Controls.Add(actions);Controls.Add(filters);Controls.Add(summary);Controls.Add(title);
  Display(entries.Any(x=>x.Unlocked)||!entries.Any(x=>!x.Unlocked));
 }

 HubButton Button(FlowLayoutPanel container,string title,Action action){
  var button=(HubButton)Hub.Button(title);
  button.BackColor=theme.Surface;button.ForeColor=theme.Text;button.Accent=theme.Accent;
  button.Click+=(s,e)=>action();container.Controls.Add(button);return button;
 }

 void Display(bool unlocked){
  earned.Active=unlocked;pending.Active=!unlocked;
  var rows=entries.Where(x=>x.Unlocked==unlocked).ToArray();
  items.BeginUpdate();
  try{items.Items.Clear();items.Items.AddRange(rows.Cast<object>().ToArray());}
  finally{items.EndUpdate();}
  int expected=unlocked?expectedEarned:expectedPending;
  note.Text=expected>rows.Length?"O cache da Steam fornece "+rows.Length+" de "+expected+" nesta seção.":rows.Length==0?"Nenhuma conquista disponível nesta seção.":"";
  note.Visible=note.Text!="";
 }

 internal void VerifySections(){
  Display(true);
  if(items.Items.Count!=1||!((AchievementEntry)items.Items[0]).Unlocked)throw new InvalidOperationException("Conquistas desbloqueadas.");
  Display(false);
  if(items.Items.Count!=1||((AchievementEntry)items.Items[0]).Unlocked)throw new InvalidOperationException("Conquistas pendentes.");
  using(var image=new Bitmap(Width,Height)){
   DrawToBitmap(image,new Rectangle(0,0,Width,Height));
   image.Save(Path.Combine(Storage.Root,"achievements.png"));
  }
 }
 void DrawAchievement(object sender,DrawItemEventArgs e){
  if(e.Index<0||e.Index>=items.Items.Count)return;
  var entry=(AchievementEntry)items.Items[e.Index];
  var graphics=e.Graphics;var rect=Rectangle.Inflate(e.Bounds,-2,-3);
  graphics.SmoothingMode=SmoothingMode.AntiAlias;
  using(var box=Skin.Round(rect,9))using(var fill=new SolidBrush((e.State&DrawItemState.Selected)!=0?Skin.Mix(theme.Surface,theme.Accent,.24f):theme.Surface))
   graphics.FillPath(fill,box);
  var picture=new Rectangle(rect.Left+11,rect.Top+9,44,44);
  Image icon;
  if(icons.TryGetValue(entry.Icon,out icon)&&icon!=null)graphics.DrawImage(icon,picture);
  else{
   using(var frame=Skin.Round(picture,7))using(var shade=new SolidBrush(Skin.Mix(theme.Surface,theme.Accent,.18f)))graphics.FillPath(shade,frame);
   Skin.Symbol(graphics,entry.Unlocked?"check":"lock",Rectangle.Inflate(picture,-12,-12),entry.Unlocked?theme.Accent:theme.Muted);
   if(!icons.ContainsKey(entry.Icon)&&AllowedIcon(entry.Icon)){icons.Add(entry.Icon,null);LoadIcon(entry.Icon);}
  }
  var title=new Rectangle(picture.Right+11,rect.Top+8,Math.Max(0,rect.Right-picture.Right-21),24);
  var detail=new Rectangle(title.X,rect.Top+34,title.Width,20);
  var flags=TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
  TextRenderer.DrawText(graphics,entry.Name,rowTitle,title,theme.Text,flags);
  TextRenderer.DrawText(graphics,entry.Description,Font,detail,theme.Muted,flags);
  if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
 }

 internal static bool AllowedIcon(string link){
  Uri uri;
  return Uri.TryCreate(link,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&
   (uri.Host=="shared.steamstatic.com"||uri.Host=="cdn.cloudflare.steamstatic.com"||uri.Host=="community.cloudflare.steamstatic.com"||
    uri.Host=="steamcdn-a.akamaihd.net"||uri.Host=="shared.fastly.steamstatic.com"||uri.Host=="shared.akamai.steamstatic.com");
 }

 async void LoadIcon(string address){
  Image icon=null;
  try{
   await downloads.WaitAsync(closing.Token);
   try{icon=await Task.Run(()=>FetchIcon(address),closing.Token);}
   finally{downloads.Release();}
   if(IsDisposed||closing.IsCancellationRequested){if(icon!=null)icon.Dispose();return;}
   icons[address]=icon;
   if(icon!=null)items.Invalidate();
  }catch(OperationCanceledException){}catch(IOException){}catch(WebException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}catch(System.Runtime.InteropServices.ExternalException){}catch(OutOfMemoryException){}
 }
 static Image FetchIcon(string url){
  string key;
  using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","");
  var folder=Path.Combine(Storage.Root,"AchievementIcons");
  var file=Path.Combine(folder,key+".png");
  if(File.Exists(file)){
   try{using(var image=Image.FromFile(file))return new Bitmap(image);}
   catch(ArgumentException){}
  }
  using(var input=new MemoryStream(Artwork.Fetch(url,256*1024)))
  using(var image=Image.FromStream(input,true,true)){
   if(image.Width>512||image.Height>512)return null;
   var bitmap=new Bitmap(image,44,44);
   try{
    Directory.CreateDirectory(folder);
    using(var saved=new MemoryStream()){
     bitmap.Save(saved,System.Drawing.Imaging.ImageFormat.Png);
     File.WriteAllBytes(file,saved.ToArray());
    }
    return bitmap;
   }catch{bitmap.Dispose();throw;}
  }
 }
 protected override void Dispose(bool disposing){
  if(disposing&&!closing.IsCancellationRequested){
   closing.Cancel();
   foreach(var icon in icons.Values)if(icon!=null)icon.Dispose();
   rowTitle.Dispose();
  }
  base.Dispose(disposing);
 }
}