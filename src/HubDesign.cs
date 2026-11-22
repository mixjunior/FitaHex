using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

partial class Hub {
 readonly ToolTip uiTips=new ToolTip();
 readonly SoftPanel searchShell=new SoftPanel();
 readonly SoftFlow filterChips=new SoftFlow();
 readonly BrandLabel brand=new BrandLabel();
 HubButton railToggle, clearSearch;
 ListViewItem hoveredCard;
 float cardHover;
 bool hoverPlayable;
 readonly Timer hoverTimer=new Timer{Interval=16};

 void SetupRail(){
  rail.Dock=DockStyle.Left;rail.Padding=new Padding(12,8,12,10);
  var masthead=new SoftPanel{Dock=DockStyle.Top,Height=72};
  brand.Text=ProductIdentity.Name;brand.Padding=new Padding(34,0,0,0);brand.Dock=DockStyle.Fill;
  brand.Font=Skin.HeadingFont(17);brand.TextAlign=ContentAlignment.MiddleLeft;brand.AutoEllipsis=true;
  brand.AccessibleName=ProductIdentity.Name;
  railToggle=(HubButton)Button("");railToggle.Glyph="menu";railToggle.Dock=DockStyle.Right;railToggle.Width=34;railToggle.AutoSize=false;
  railToggle.Click+=(s,e)=>{data.SidebarCollapsed=!data.SidebarCollapsed;ApplyRail();BuildNav();Save();};
  masthead.Controls.Add(brand);masthead.Controls.Add(railToggle);
  nav.Dock=DockStyle.Fill;nav.FlowDirection=FlowDirection.TopDown;nav.WrapContents=false;nav.AutoScroll=true;
  controller.Dock=DockStyle.Bottom;controller.Height=30;controller.Padding=new Padding(6,0,0,0);
  controller.TextAlign=ContentAlignment.MiddleLeft;
  rail.Controls.Add(nav);rail.Controls.Add(controller);rail.Controls.Add(masthead);
  ApplyRail();
 }
 void ApplyRail(){
  rail.Width=data.SidebarCollapsed?68:218;
  brand.Visible=!data.SidebarCollapsed;controller.Visible=!data.SidebarCollapsed;
  railToggle.Width=data.SidebarCollapsed?44:34;
  railToggle.AccessibleName=UiLanguage.Text(data.SidebarCollapsed?"Expandir navegação":"Recolher navegação");
  uiTips.SetToolTip(railToggle,railToggle.AccessibleName);
 }
 void ConfigureNav(HubButton button,string key,string label){
  bool child=key.StartsWith("social:",StringComparison.Ordinal)||key.StartsWith("shelf:",StringComparison.Ordinal);
  button.Width=data.SidebarCollapsed?37:child?160:177;button.Height=child?31:35;
  button.Margin=new Padding(!data.SidebarCollapsed&&child?15:1,2,2,2);
  button.IconOnly=data.SidebarCollapsed;button.Glyph=key.StartsWith("shelf:")?"folder":key.StartsWith("social:")?"social":
   key=="Jogos"?"grid":(key=="Recentes"||key=="Histórico")?"clock":(key=="Instalados"||key=="Zerados")?"check":key=="Favoritos"?"star":key=="Coleções"?"folder":key=="Social"?"social":key=="Adicionar jogo"?"plus":"settings";
  if(key.StartsWith("social:")){
   var platform=socialPlatforms[Int32.Parse(key.Substring(7))];
   button.GlyphImage=SocialImage(platform.Name);
  }
  button.AccessibleName=label;uiTips.SetToolTip(button,label);
 }
 Image SocialImage(string platform){
  var source=platform=="Xbox"?XboxGames.Source:platform;
  return SourceImage(new Game{Source=source},source);
 }
 void UpdateNavCounts(){
  foreach(var key in new[]{"Jogos","Recentes","Instalados","Favoritos","Zerados","Histórico","Coleções"}){
   HubButton button;if(!navigation.TryGetValue(key,out button))continue;
   int count=key=="Jogos"?data.Games.Count:key=="Recentes"?data.Games.Count(g=>g.Last.HasValue):
    key=="Instalados"?data.Games.Count(Installed):key=="Favoritos"?data.Games.Count(g=>g.Favorite):key=="Zerados"?data.Games.Count(g=>g.Completed):key=="Histórico"?data.Games.Count(InUninstalledHistory):data.Shelves.Count;
   button.Counter=count.ToString();button.Invalidate();
  }
 }
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,EntryPoint="SendMessageW")]
 static extern IntPtr SendCue(IntPtr window,int message,IntPtr showOnFocus,string text);
 void SetupSearch(){
  searchShell.Size=new Size(217,36);searchShell.Margin=new Padding(0,3,7,3);searchShell.Padding=new Padding(31,0,30,0);
  searchShell.Paint+=(s,e)=>{
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   using(var shape=Skin.Round(new Rectangle(0,0,searchShell.Width-1,searchShell.Height-1),9))
   using(var brush=new SolidBrush(surface))
   using(var border=new Pen(search.Focused?accent:Skin.Mix(surface,ForeColor,.14f))){
    e.Graphics.FillPath(brush,shape);e.Graphics.DrawPath(border,shape);
   }
   Skin.Symbol(e.Graphics,"search",new Rectangle(10,10,15,15),ForeColor);
  };
  search.BorderStyle=BorderStyle.None;search.AccessibleName=UiLanguage.Text("Buscar na biblioteca");
  search.Location=new Point(31,9);search.Width=153;
  search.HandleCreated+=(s,e)=>SendCue(search.Handle,0x1501,(IntPtr)1,UiLanguage.Text("Buscar na biblioteca…"));
  clearSearch=(HubButton)Button("");clearSearch.Glyph="close";clearSearch.AutoSize=false;
  clearSearch.SetBounds(184,3,29,30);clearSearch.AccessibleName=UiLanguage.Text("Limpar pesquisa");clearSearch.Visible=false;
  clearSearch.Click+=(s,e)=>{search.Clear();search.Focus();};
  search.GotFocus+=(s,e)=>{searchShell.Invalidate();};
  search.LostFocus+=(s,e)=>{searchShell.Invalidate();};
  search.TextChanged+=(s,e)=>{clearSearch.Visible=search.TextLength>0;page=150;RefreshList();};
  searchShell.Controls.Add(search);searchShell.Controls.Add(clearSearch);toolbar.Controls.Add(searchShell);
  filterChips.Dock=DockStyle.Top;filterChips.Height=35;filterChips.Visible=false;filterChips.AutoScroll=true;filterChips.WrapContents=false;
  toolbar.SizeChanged+=(s,e)=>toolbar.Height=toolbar.Width<790?96:55;
 }
 void ApplyDesignTheme(){
  searchShell.BackColor=BackColor;searchShell.Invalidate();
  railToggle.BackColor=rail.BackColor;railToggle.ForeColor=ForeColor;railToggle.Accent=accent;
  clearSearch.BackColor=surface;clearSearch.ForeColor=ForeColor;clearSearch.Accent=accent;
  foreach(var button in navigation.Values.OfType<HubButton>()){
   button.BackColor=rail.BackColor;button.ForeColor=ForeColor;button.Accent=accent;button.Invalidate();
  }
  brand.Accent=accent;brand.Invalidate();
  UpdateFilterLabel();
 }
 void FitHeroButtons(){
  if(detailButtons.ClientSize.Width<=0)return;
  int rows=1,used=0;
  foreach(Control control in detailButtons.Controls){
   int width=control.GetPreferredSize(Size.Empty).Width+control.Margin.Horizontal;
   if(used>0&&used+width>detailButtons.ClientSize.Width){rows++;used=0;}
   used+=width;
  }
  int height=rows*39+3;
  if(detailButtons.Height!=height)detailButtons.Height=height;
  int heroHeight=(libraryPage.ClientSize.Height<600?183:213)+(rows-1)*38;
  if(hero.Height!=heroHeight)hero.Height=heroHeight;
 }
 void SetupCardHover(){
  hoverTimer.Tick+=(s,e)=>{
   float target=hoveredCard==null?0f:1f;
   cardHover+=(target-cardHover)*.24f;
   if(Math.Abs(target-cardHover)<.025f){cardHover=target;hoverTimer.Stop();}
   InvalidateCard(hoveredCard);
  };
  list.MouseMove+=(s,e)=>{
   var hit=list.View==View.LargeIcon?list.HitTest(e.Location).Item:null;
   MoveCardHover(hit!=null&&hit.Tag is Game?hit:null);
  };
  list.MouseLeave+=(s,e)=>MoveCardHover(null);
  list.KeyDown+=(s,e)=>{
   if((e.KeyCode==Keys.Apps||e.Shift&&e.KeyCode==Keys.F10)&&list.SelectedItems.Count>0){
    var row=list.SelectedItems[0];var game=row.Tag as Game;
    if(game!=null){GameMenu(game,new Point(row.Bounds.Left,row.Bounds.Bottom));e.Handled=true;}
   }
  };
  FormClosed+=(s,e)=>{hoverTimer.Dispose();uiTips.Dispose();};
 }
 void InvalidateCard(ListViewItem card){if(card!=null&&card.ListView==list)list.Invalidate(card.Bounds);}
 void MoveCardHover(ListViewItem card){
  if(card==hoveredCard)return;
  var previous=hoveredCard;hoveredCard=card;cardHover=0;hoverPlayable=card!=null&&HasPlayableEdition((Game)card.Tag);InvalidateCard(previous);
  if(card==null)hoverTimer.Stop();else hoverTimer.Start();
 }
 Rectangle CardArt(Rectangle bounds){
  int height=Math.Min(covers.ImageSize.Height,bounds.Height-26);
  int width=Math.Min(bounds.Width-12,height*2/3);
  return new Rectangle(bounds.X+(bounds.Width-width)/2,bounds.Y+5,width,height);
 }
 Rectangle CardPlay(Rectangle art){return new Rectangle(art.Left+7,art.Bottom-40,art.Width-14,32);}
 Rectangle CardFavorite(Rectangle art){return new Rectangle(art.Right-65,art.Top+7,27,27);}
 Rectangle CardMore(Rectangle art){return new Rectangle(art.Right-34,art.Top+7,27,27);}
 void DrawCardActions(Graphics graphics,Rectangle art,Game game,bool installed,float amount){
  if(art.Width<75||amount<=.05f)return;
  installed=installed||hoverPlayable;
  using(var clip=Skin.Round(art,10)){
   var saved=graphics.Save();graphics.SetClip(clip);
   using(var shade=new LinearGradientBrush(art,Color.FromArgb((int)(64*amount),3,5,10),Color.FromArgb((int)(219*amount),3,5,10),LinearGradientMode.Vertical))
    graphics.FillRectangle(shade,art);
   graphics.Restore(saved);
  }
  var play=CardPlay(art);
  var actionColor=Skin.Mix(surface,accent,.87f);
  DrawCardPill(graphics,play,UiLanguage.Text(installed?"Jogar":InstallCaption(game)),actionColor,Skin.OnAccent(actionColor));
  DrawCardPill(graphics,CardFavorite(art),game.Favorite?"★":"☆",Color.FromArgb(218,20,22,32),Color.White);
  DrawCardPill(graphics,CardMore(art),"⋯",Color.FromArgb(218,20,22,32),Color.White);
 }
 void DrawCardPill(Graphics graphics,Rectangle bounds,string text,Color fill,Color textColor){
  using(var path=Skin.Round(bounds,7))using(var brush=new SolidBrush(fill))graphics.FillPath(brush,path);
  TextRenderer.DrawText(graphics,text,Font,bounds,textColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
 }
 string InstallCaption(Game game){
  var action=DownloadAction(game);
  return action.StartsWith("Instalar",StringComparison.Ordinal)?"Instalar":action.StartsWith("Abrir",StringComparison.Ordinal)?"Abrir":"Localizar";
 }
 void GameAction(Game game){if(HasPlayableEdition(game))ChoosePlay(game);else OpenInstallation(game);}
 void FavoriteGame(Game game){game.Favorite=!game.Favorite;Save();RefreshList();}
 void GameMenu(Game game,Point location){
  var menu=StyledMenu();menu.ShowImageMargin=true;
  MenuGlyph(menu,MenuAction(menu.Items,HasPlayableEdition(game)?"Jogar":DownloadAction(game),()=>GameAction(game)),Installed(game)?"play":"download");
  MenuAction(menu.Items,"Editar",()=>{if(IsCustom(game))EditCustom(game);else Edit(game);});
  MenuGlyph(menu,MenuAction(menu.Items,game.Favorite?"Desfavoritar":"Favoritar",()=>FavoriteGame(game)),"star");
  MenuAction(menu.Items,"Conquistas",()=>ShowAchievements(game));
  MenuAction(menu.Items,game.Completed?"Desmarcar zerado":"Marcar como zerado",()=>ToggleCompleted(game));
  var collections=new ToolStripMenuItem(UiLanguage.Text("Coleções")){ForeColor=ForeColor};
  foreach(var shelf in data.Shelves.Where(s=>!s.Smart).OrderBy(s=>s.Name)){
   var option=MenuAction(collections.DropDownItems,shelf.Name,()=>{
    if(!shelf.Ids.Remove(game.Id))shelf.Ids.Add(game.Id);
    Save();RefreshList();
   });
   option.Checked=shelf.Ids.Contains(game.Id);
  }
  if(collections.DropDownItems.Count>0){collections.DropDown.Renderer=menu.Renderer;collections.DropDown.BackColor=surface;menu.Items.Add(collections);}
  MenuGlyph(menu,MenuAction(menu.Items,"Abrir pasta",()=>OpenFolder(game)),"folder");
  if(CanRemove(game))MenuAction(menu.Items,"Remover e manter histórico",()=>RemoveGame(game));
  menu.Show(list,location);
 }
 void MenuGlyph(ContextMenuStrip menu,ToolStripMenuItem item,string glyph){
  var image=new Bitmap(17,17);
  using(var graphics=Graphics.FromImage(image))Skin.Symbol(graphics,glyph,new Rectangle(0,0,17,17),ForeColor);
  item.Image=image;menu.Disposed+=(s,e)=>image.Dispose();
 }
 ContextMenuStrip StyledMenu(){
  var menu=new ContextMenuStrip{BackColor=surface,ForeColor=ForeColor,ShowImageMargin=false,Padding=new Padding(5),Renderer=new ToolStripProfessionalRenderer(new FilterColors(surface,accent))};
  menu.Closed+=(s,e)=>BeginInvoke(new Action(menu.Dispose));
  return menu;
 }
 ToolStripMenuItem MenuAction(ToolStripItemCollection items,string text,Action action){
  var item=new ToolStripMenuItem(UiLanguage.Text(text)){ForeColor=ForeColor,Padding=new Padding(8,5,14,5)};
  item.Click+=(s,e)=>Safe(action);items.Add(item);return item;
 }
}