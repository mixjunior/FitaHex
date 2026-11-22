using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public partial class Hub {

 internal void VerifyEnglishLabels(){
  if(navigation["Jogos"].Text!="Library"||navigation["Configurações"].Text!="Settings"||
   list.Columns[0].Text!="Game"||UiLanguage.Text("Instalados e adicionados")!="Installed and added")
    throw new InvalidOperationException("English interface labels.");
 }
 string SectionTitle(){return section=="Jogos"?UiLanguage.Text("Biblioteca"):section.StartsWith("shelf:")?section.Substring(6):UiLanguage.Text(section);}
 int LibraryView{get{return data.LibraryView??(data.ListView?1:0);}}
 void SetLibraryView(int value){
  if(LibraryView==value)return;
  data.LibraryView=value;data.ListView=value==1;
  ApplyGridSize();Save();RefreshList();
 }
 void ApplyGridSize(){
  var size=LibraryView==2?new Size(92,138):new Size(142,213);
  if(covers.ImageSize==size)return;
  list.LargeImageList=null;covers.ImageSize=size;
  ClearCards();covers.Images.Clear();using(var image=Placeholder())covers.Images.Add(image);
  list.LargeImageList=covers;
 }
 void AddSelector(ComboBox combo,string name){
  var labels=combo.Items.Cast<string>().ToArray();
  var actions=Enumerable.Range(0,labels.Length).Select(index=>(Action)(()=>combo.SelectedIndex=index)).ToArray();
  HubButton button=null;
  button=Action(toolbar,name+" ▾",()=>ShowMenu(button,labels,actions));
  button.AccessibleName=name;
  var hint=new ToolTip();
  Action update=()=>{button.Text=combo.SelectedIndex==0?UiLanguage.Text("Ordenar · A–Z ▾"):combo.SelectedItem+" ▾";hint.SetToolTip(button,name);};
  combo.SelectedIndexChanged+=(s,e)=>update();button.Disposed+=(s,e)=>hint.Dispose();update();
 }
 void ClearCards(){foreach(var image in cardImages)image.Dispose();cardImages.Clear();thumbnails.Clear();}
 void ShowMenu(Control anchor,string[] labels,Action[] actions){
  var menu=StyledMenu();
  for(int i=0;i<labels.Length;i++){
   var action=actions[i];var item=new ToolStripMenuItem(UiLanguage.Text(labels[i])){ForeColor=ForeColor,Padding=new Padding(8,5,16,5)};
   item.Click+=(s,e)=>Safe(action);menu.Items.Add(item);
  }
  menu.Show(anchor,new Point(0,anchor.Height+5));
 }
 void PaintHeroButtonBackdrop(object sender,PaintEventArgs e){
  var control=(Control)sender;
  var offset=hero.PointToClient(control.PointToScreen(Point.Empty));
  var state=e.Graphics.Save();
  try{
   e.Graphics.TranslateTransform(-offset.X,-offset.Y);
   PaintHero(hero,e);
  }finally{e.Graphics.Restore(state);}
 }
 void PaintHero(object sender,PaintEventArgs e){
  var bounds=hero.ClientRectangle;
  if(bounds.Width<2||bounds.Height<2)return;
  var graphics=e.Graphics;
  graphics.SmoothingMode=SmoothingMode.AntiAlias;
  using(var shape=Skin.Round(new Rectangle(0,0,bounds.Width-1,bounds.Height-1),14)){
   var state=graphics.Save();graphics.SetClip(shape);
   using(var fill=new LinearGradientBrush(bounds,surface,Skin.Mix(surface,accent,heroImage==null?.13f:0),12f))graphics.FillRectangle(fill,bounds);
   if(heroImage!=null){
    var art=new Rectangle(bounds.Width*31/100,0,bounds.Width-bounds.Width*31/100,bounds.Height);
    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
    Skin.Crop(graphics,heroImage,art);
    using(var veil=new LinearGradientBrush(art,surface,Color.FromArgb(19,surface),LinearGradientMode.Horizontal)){
     veil.InterpolationColors=new ColorBlend{Colors=new[]{surface,Color.FromArgb(234,surface),Color.FromArgb(35,surface)},Positions=new[]{0f,.26f,1f}};
     graphics.FillRectangle(veil,art);
    }
   }else{
    var mark=new Rectangle(bounds.Right-178,31,146,146);
    Skin.Symbol(graphics,"nexus",mark,Skin.Mix(surface,accent,.36f));
   }
   graphics.Restore(state);
   using(var border=new Pen(Skin.Mix(surface,ForeColor,.11f)))graphics.DrawPath(border,shape);
  }
 }
 void UpdateVisibleArtwork(){
  if(list.View==View.LargeIcon){
   list.BeginUpdate();
   try{
    foreach(ListViewItem row in list.Items){
     var game=row.Tag as Game;
     if(game!=null)row.Name=(game.Cover!=""?Thumbnail(game.Cover):game.Icon!=""?Thumbnail(game.Icon,true):0).ToString();
    }
   }finally{list.EndUpdate();}
   list.Invalidate();
  }
  if(selected!=null){
   var asset=selected.Banner!=""?selected.Banner:selected.Cover!=""?selected.Cover:selected.Icon;
   if(asset!=heroAsset)SetGame(selected);
  }
 }
 int Thumbnail(string asset,bool icon=false){
  var key=(icon?"icon:":"cover:")+asset;int index;if(thumbnails.TryGetValue(key,out index))return index;
  using(var source=Storage.Picture(asset)){
   if(source==null)return 0;
   var bitmap=new Bitmap(covers.ImageSize.Width,covers.ImageSize.Height);
   using(var g=Graphics.FromImage(bitmap)){
    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    if(icon){
     var canvas=new Rectangle(Point.Empty,bitmap.Size);
     using(var shade=new LinearGradientBrush(canvas,surface,Skin.Mix(surface,accent,.35f),45f))g.FillRectangle(shade,canvas);
     float scale=Math.Min(bitmap.Width*.76f/source.Width,bitmap.Height*.69f/source.Height);
     int width=Math.Max(1,(int)(source.Width*scale)),height=Math.Max(1,(int)(source.Height*scale));
     g.DrawImage(source,new Rectangle((bitmap.Width-width)/2,(bitmap.Height-height)/2,width,height));
    }else Skin.Crop(g,source,new Rectangle(Point.Empty,bitmap.Size));
   }
   index=cardImages.Count;cardImages.Add(bitmap);
   thumbnails[key]=index;return index;
  }
 }
 Bitmap Placeholder(){
  var b=new Bitmap(covers.ImageSize.Width,covers.ImageSize.Height);
  using(var g=Graphics.FromImage(b))
  using(var brush=new LinearGradientBrush(new Rectangle(Point.Empty,b.Size),surface.IsEmpty?Color.FromArgb(30,34,53):surface,Skin.Mix(surface.IsEmpty?Color.FromArgb(30,34,53):surface,accent.IsEmpty?Color.MediumPurple:accent,.24f),60f))
  using(var font=new Font("Segoe UI",30,FontStyle.Bold)){
   g.FillRectangle(brush,0,0,b.Width,b.Height);TextRenderer.DrawText(g,ProductIdentity.Name.Substring(0,1),font,new Rectangle(Point.Empty,b.Size),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
  return b;
 }
 void DrawGame(object sender,DrawListViewItemEventArgs e){
  if(list.View==View.Details)return;
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  var bounds=e.Bounds;
  if(bounds.Width<20||bounds.Height<20)return;
  if(e.Item.Name=="empty-collection"){
   TextRenderer.DrawText(g,e.Item.Text,Font,new Rectangle(bounds.X+6,bounds.Y+6,bounds.Width-12,42),ForeColor,TextFormatFlags.WordBreak);
   return;
  }
  var outer=new Rectangle(bounds.X+2,bounds.Y+1,bounds.Width-4,bounds.Height-2);
  bool active=e.Item.Selected;
  if(active)using(var bg=Skin.Round(outer,11))
  using(var tone=new SolidBrush(Skin.Mix(surface,accent,.17f)))
  using(var line=new Pen(Skin.Mix(BackColor,accent,.70f))){
   g.FillPath(tone,bg);g.DrawPath(line,bg);
  }
  var art=CardArt(bounds);
  float hover=e.Item==hoveredCard?cardHover:0;
  int index;
  if(!Int32.TryParse(e.Item.Name,out index))index=0;
  if(art.Height>0&&index>=0&&index<cardImages.Count){
   var image=cardImages[index];
   float scale=Math.Max((float)art.Width/image.Width,(float)art.Height/image.Height)*(1+.034f*hover);
   using(var curve=Skin.Round(art,10))
   using(var texture=new TextureBrush(image,WrapMode.Clamp)){
    texture.TranslateTransform(art.Left+(art.Width-image.Width*scale)/2,art.Top+(art.Height-image.Height*scale)/2);
    texture.ScaleTransform(scale,scale);
    g.FillPath(texture,curve);
    if(e.Item.SubItems.Count>4&&e.Item.SubItems[4].Text!=UiLanguage.Text("Instalado"))
     using(var shade=new SolidBrush(Color.FromArgb(94,5,7,13)))g.FillPath(shade,curve);
   }
  }
  var game=e.Item.Tag as Game;
  if(game!=null&&art.Height>0)DrawOrigin(g,Origin(game),new Rectangle(art.X+6,art.Y+6,25,25),game);
  if(game!=null&&hover>0)DrawCardActions(g,art,game,e.Item.SubItems[4].Text==UiLanguage.Text("Instalado"),hover);
  TextRenderer.DrawText(g,e.Item.Text,Font,new Rectangle(bounds.X+5,art.Bottom+2,bounds.Width-10,Math.Max(1,bounds.Bottom-art.Bottom-3)),ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine);
 }
 NotifyIcon trayIcon;
 bool exitingFromTray;
 void SetupTray(){
  var trayMenu=new ContextMenuStrip();
  trayMenu.Items.Add(UiLanguage.Text("Abrir ")+ProductIdentity.Name,null,(sender,args)=>OpenFromTray());
  trayMenu.Items.Add(new ToolStripSeparator());
  trayMenu.Items.Add(UiLanguage.Text("Encerrar ")+ProductIdentity.Name,null,(sender,args)=>ExitFromTray());
  trayIcon=new NotifyIcon{Icon=Icon??SystemIcons.Application,Text=ProductIdentity.Name,ContextMenuStrip=trayMenu};
  trayIcon.MouseClick+=(sender,args)=>{if(args.Button==MouseButtons.Left)OpenFromTray();};
  Shown+=(sender,args)=>trayIcon.Visible=true;
  FormClosing+=(sender,args)=>{
   if(exitingFromTray||args.CloseReason!=CloseReason.UserClosing)return;
   args.Cancel=true;
   Hide();
  };
  Disposed+=(sender,args)=>{
   trayIcon.Visible=false;
   trayIcon.Dispose();
   trayMenu.Dispose();
  };
 }
 void OpenFromTray(){
  if(exitingFromTray||IsDisposed)return;
  if(!Visible)Show();
  if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
  Activate();
 }
 internal void ExitFromTray(){exitingFromTray=true;Close();}
 internal void VerifyTray(){
  if(trayIcon==null||!trayIcon.Visible)throw new InvalidOperationException(UiLanguage.Text("Ícone da bandeja indisponível."));
  Close();
  if(Visible||IsDisposed||!trayIcon.Visible)throw new InvalidOperationException(UiLanguage.Text("Recolher à bandeja."));
  OpenFromTray();
  if(!Visible||IsDisposed)throw new InvalidOperationException(UiLanguage.Text("Restaurar pela bandeja."));
 }
 void SetupChrome(){
  chrome.Height=39;chrome.Dock=DockStyle.Top;chrome.Visible=false;
  var cap=new Label{Text=ProductIdentity.Name,Dock=DockStyle.Fill,Padding=new Padding(16,0,0,0),TextAlign=ContentAlignment.MiddleLeft};
  var controls=new SoftFlow{Width=132,Dock=DockStyle.Right,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};
  var minimize=Action(controls,"−",()=>WindowState=FormWindowState.Minimized);minimize.AccessibleName=UiLanguage.Text("Minimizar");
  var maximize=Action(controls,"□",FlipMaximize);maximize.AccessibleName=UiLanguage.Text("Maximizar ou restaurar");
  var close=Action(controls,"×",Close);close.AccessibleName=UiLanguage.Text("Fechar");
  foreach(Button button in controls.Controls){button.AutoSize=false;button.Width=39;button.Height=31;button.Margin=new Padding(2,3,2,0);}
  cap.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left&&e.Clicks==1){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
  cap.DoubleClick+=(s,e)=>FlipMaximize();
  chrome.Controls.Add(cap);chrome.Controls.Add(controls);Controls.Add(chrome);
 }
 void FlipMaximize(){if(displayMode==1)WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;}
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp);
 protected override void WndProc(ref Message m){
  base.WndProc(ref m);
  if(m.Msg!=0x84||displayMode!=1||WindowState!=FormWindowState.Normal||m.Result.ToInt32()!=1)return;
  int value=m.LParam.ToInt32();
  var p=PointToClient(new Point(unchecked((short)value),unchecked((short)(value>>16))));
  const int edge=7;
  bool left=p.X<edge,right=p.X>=ClientSize.Width-edge,top=p.Y<edge,bottom=p.Y>=ClientSize.Height-edge;
  if(top)m.Result=new IntPtr(left?13:right?14:12);
  else if(bottom)m.Result=new IntPtr(left?16:right?17:15);
  else if(left||right)m.Result=new IntPtr(left?10:11);
 }

 void SetMode(int mode,bool persist){
  if(!chromeReady)return;
  if(mode<0||mode>2)mode=0;
  if(mode!=displayMode){
   var monitor=Screen.FromControl(this);
   var before=displayMode;
   if(before!=2&&mode==2){
    normalState=WindowState;
    normalBounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
   }
   var bounds=before==2?normalBounds:WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
   var state=before==2?normalState:WindowState;
   SuspendLayout();
   try{
    displayMode=mode;fullscreen=mode==2;WindowState=FormWindowState.Normal;
    FormBorderStyle=mode==0?FormBorderStyle.Sizable:FormBorderStyle.None;
    chrome.Visible=mode==1;Padding=mode==1?new Padding(7):Padding.Empty;TopMost=mode==2;
    MaximizedBounds=mode==1?monitor.WorkingArea:Rectangle.Empty;
    if(fullscreen){Bounds=monitor.Bounds;}
    else{
     if(!bounds.IsEmpty)Bounds=bounds;
     WindowState=state==FormWindowState.Minimized?FormWindowState.Normal:state;
    }
   }finally{ResumeLayout();}
   RefreshList();
  }
  if(persist){data.WindowMode=mode;data.Fullscreen=mode==2;Save();}
 }
 internal void VerifyPagination(){
  var more=list.Items.Cast<ListViewItem>().LastOrDefault();
  if(list.Items.Count!=151||more==null||more.Tag!=null||more.Group==null||more.Group.Header!="Não instalados")
   throw new InvalidOperationException(UiLanguage.Text("Paginação da biblioteca ao iniciar."));
 }
 internal void VerifyVisuals(){
  if(TextRenderer.MeasureText(brand.Text,brand.Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine).Width>brand.Width-brand.Padding.Horizontal)throw new InvalidOperationException(UiLanguage.Text("Marca truncada na barra lateral."));
  if(brand.Text!=ProductIdentity.Name||!Text.StartsWith(ProductIdentity.Name)||!Storage.Root.EndsWith(ProductIdentity.Name))throw new InvalidOperationException(UiLanguage.Text("Identidade e compatibilidade."));
  if(section!="Jogos"||!navigation["Jogos"].Active||navigation.ContainsKey("Início")||navigation.ContainsKey("Todos os jogos"))throw new InvalidOperationException(UiLanguage.Text("Tela inicial da biblioteca."));

  var editionTest=new Game{Name="Teste multiplataforma",Source="Steam",Target="steam://run/71",StoreId="steam:71"};
  var secondEdition=new Game{Name="Teste multiplataforma™",Source="Epic Games",Target=Stores.EpicUri("test-edition"),StoreId="epic:test-edition"};
  data.Games.Add(editionTest);data.Games.Add(secondEdition);
  if(PlayableEditions(editionTest).Length!=2)throw new InvalidOperationException(UiLanguage.Text("Seleção de launcher."));
  secondEdition.Missing=true;
  if(PlayableEditions(secondEdition).Single()!=editionTest)throw new InvalidOperationException(UiLanguage.Text("Edição instalada."));
  data.Games.Remove(editionTest);data.Games.Remove(secondEdition);
  var achievementTest=new Game{Name="Conquistas de teste",Source="Steam",Achievements=new AchievementProgress{Unlocked=1,Total=2,Entries=new System.Collections.Generic.List<AchievementEntry>{
   new AchievementEntry{Name="Primeiro passo",Description=UiLanguage.Text("Conclua a primeira fase."),Unlocked=true},
   new AchievementEntry{Name="Último desafio",Description=UiLanguage.Text("Complete todos os desafios.")}
  }}};
  using(var achievements=new AchievementWindow(achievementTest,new[]{achievementTest},NexusTheme.Find(data.Theme))){
   Exception failure=null;
   achievements.Shown+=(sender,args)=>achievements.BeginInvoke(new Action(()=>{
    try{achievements.VerifySections();}catch(Exception error){failure=error;}finally{achievements.Close();}
   }));
   achievements.ShowDialog(this);
   if(failure!=null)throw failure;
  }
  TestSources();
  TestCollections();
  Navigate("Social");
  if(navigation.ContainsKey("social:conversas")||socialPlatforms.Length!=Launchers.Length||socialPlatforms.Where((platform,index)=>!navigation.ContainsKey("social:"+index)).Any()||!socialHost.Visible||socialOverview==null||!socialOverview.Visible)throw new InvalidOperationException(UiLanguage.Text("Submenu Social."));
  using(var socialShot=new Bitmap(Width,Height)){
   DrawToBitmap(socialShot,new Rectangle(0,0,Width,Height));
   socialShot.Save(System.IO.Path.Combine(Storage.Root,"social.png"));
  }
  Navigate("Jogos");
  var normalSize=Size;
  Size=new Size(840,600);
  data.SidebarCollapsed=true;ApplyRail();BuildNav();
  if(rail.Width>=90||!navigation["Jogos"].IconOnly||galleryArea.Width<=450)throw new InvalidOperationException(UiLanguage.Text("Layout em janela pequena."));
  using(var small=new Bitmap(Width,Height)){
   DrawToBitmap(small,new Rectangle(0,0,Width,Height));
   small.Save(System.IO.Path.Combine(Storage.Root,"compact-window.png"));
  }
  data.SidebarCollapsed=false;ApplyRail();BuildNav();Size=normalSize;
  if(navigation.ContainsKey("social:0"))throw new InvalidOperationException(UiLanguage.Text("Submenu Social recolhido."));

  var initial=navigation["Jogos"];
  Navigate("Jogos");
  if(!toolbar.Visible||!hero.Visible||galleryArea.Top<hero.Bottom)throw new InvalidOperationException(UiLanguage.Text("Layout da biblioteca: ")+toolbar.Visible+" "+hero.Visible+" "+galleryArea.Bounds+" "+hero.Bounds);
  var remote=new Game{Name="Disponível para instalar",Source="Steam",Missing=true,StoreId="steam:99",Target="steam://run/99",
   Cover=data.Games.First(g=>g.Cover!="").Cover};
  data.Games.Add(remote);RefreshList();
  if(list.Items.Cast<ListViewItem>().Single(row=>row.Tag==remote).Group.Header!="Não instalados"||
     list.Items.Cast<ListViewItem>().Any(row=>row.Tag!=remote&&row.Group.Header!="Instalados e adicionados"))
   throw new InvalidOperationException(UiLanguage.Text("Grupos de instalação."));
  list.Update();
  using(var screenshot=new Bitmap(Width,Height)){
   DrawToBitmap(screenshot,new Rectangle(0,0,Width,Height));
   screenshot.Save(System.IO.Path.Combine(Storage.Root,"library.png"));
  }
  data.Games.Remove(remote);RefreshList();
  int cached=cardImages.Count;
  if(cached<2)throw new InvalidOperationException(UiLanguage.Text("Miniatura ausente."));
  Navigate("Favoritos");
  if(!empty.Visible||list.Visible||!navigation["Favoritos"].Active)throw new InvalidOperationException(UiLanguage.Text("Estado vazio."));
  Navigate("Jogos");
  if(!ReferenceEquals(initial,navigation["Jogos"])||cardImages.Count!=cached||!list.Visible)throw new InvalidOperationException(UiLanguage.Text("Navegação ou cache."));
  SetLibraryView(2);
  if(covers.ImageSize.Width!=92||Storage.Read(Storage.FileName).LibraryView!=2)throw new InvalidOperationException(UiLanguage.Text("Vista compacta."));
  SetLibraryView(1);if(list.View!=View.Details)throw new InvalidOperationException(UiLanguage.Text("Vista em lista."));
  SetLibraryView(0);
  var initialBounds=Bounds;
  SetMode(1,true);
  if(FormBorderStyle!=FormBorderStyle.None||!chrome.Visible||Storage.Read(Storage.FileName).WindowMode!=1)throw new InvalidOperationException(UiLanguage.Text("Janela sem borda."));
  SetMode(2,true);
  if(chrome.Visible||Bounds!=Screen.FromControl(this).Bounds||Storage.Read(Storage.FileName).WindowMode!=2)throw new InvalidOperationException(UiLanguage.Text("Tela cheia."));
  SetMode(1,true);
  if(displayMode!=1||!chrome.Visible)throw new InvalidOperationException(UiLanguage.Text("Retorno de tela cheia."));
  SetMode(0,true);
  if(FormBorderStyle!=FormBorderStyle.Sizable||chrome.Visible||Bounds!=initialBounds||Storage.Read(Storage.FileName).WindowMode!=0)throw new InvalidOperationException(UiLanguage.Text("Janela padrão."));
  var savedTheme=data.Theme;
  foreach(var theme in NexusTheme.All){
   data.Theme=theme.Key;ApplyTheme();RefreshList();Update();
   using(var screenshot=new Bitmap(Width,Height)){
    DrawToBitmap(screenshot,new Rectangle(0,0,Width,Height));
    screenshot.Save(System.IO.Path.Combine(Storage.Root,"theme-"+theme.Key+".png"));
   }
  }
  data.Theme=savedTheme;ApplyTheme();RefreshList();
 }
}













