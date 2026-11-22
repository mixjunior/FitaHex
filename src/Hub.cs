using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

public class Editor : Form {
 TableLayoutPanel fields=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=3,Padding=new Padding(14)};
 Dictionary<string,TextBox> inputs=new Dictionary<string,TextBox>();
 public Editor(string title){
  Text=UiLanguage.Text(title); Size=new Size(690,630); MinimumSize=new Size(480,380); StartPosition=FormStartPosition.CenterParent;
  BackColor=Color.FromArgb(25,23,34);ForeColor=Color.White;Font=new Font("Segoe UI",10);
  fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,165));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
  var area=new Panel{Dock=DockStyle.Fill,AutoScroll=true};area.Controls.Add(fields);Controls.Add(area);
  var bar=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=55,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(10)};
  var ok=Hub.Button("Salvar");ok.DialogResult=DialogResult.OK;var cancel=Hub.Button("Cancelar");cancel.DialogResult=DialogResult.Cancel;bar.Controls.Add(ok);bar.Controls.Add(cancel);Controls.Add(bar);AcceptButton=ok;CancelButton=cancel;
 }
 public void Add(string label,string value,string picker=""){
  var row=fields.RowCount++;var box=new TextBox{Text=value,Dock=DockStyle.Fill,BackColor=Color.FromArgb(43,39,56),ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(4,5,4,7)};
  if(label.StartsWith("Variáveis")){box.Multiline=true;box.Height=55;}inputs[label]=box;fields.Controls.Add(new Label{Text=UiLanguage.Text(label),AutoSize=true,Anchor=AnchorStyles.Left},0,row);fields.Controls.Add(box,1,row);
  if(picker==""){fields.SetColumnSpan(box,2);return;}
  var browse=Hub.Button("…");browse.Width=40;browse.Height=27;fields.Controls.Add(browse,2,row);
  browse.Click+=(s,e)=>{if(picker=="dir"){using(var d=new FolderBrowserDialog())if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.SelectedPath;}else using(var d=new OpenFileDialog{Filter=picker=="img"?CoverImages.Filter:"Todos os arquivos|*.*"})if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.FileName;};
 }
 public string V(string key){return inputs[key].Text.Trim();}
}
public partial class Hub : Form {
 Library data;
 internal bool ReloadLanguage {get;private set;}
 FlowLayoutPanel nav=new SoftFlow(), toolbar=new SoftFlow(), detailButtons=new SoftFlow();
 ListView list=new Gallery();
 ImageList covers=new ImageList();
 TextBox search=new TextBox();
 ComboBox sort=new ComboBox();
 Label title=new Label(), subtitle=new Label(), details=new Label(), controller=new Label();
 Panel chrome=new SoftPanel(), empty=new SoftPanel(),heroText=new SoftPanel();
 Image heroImage;
 string heroAsset="";
 Label libraryHeading=new Label(), emptyText=new Label();
 Panel libraryBar=new SoftPanel();
 HubButton sourceButton;
 readonly Dictionary<string,int> thumbnails=new Dictionary<string,int>();
 readonly List<Bitmap> cardImages=new List<Bitmap>();
 readonly Dictionary<string,HubButton> navigation=new Dictionary<string,HubButton>();
 readonly Dictionary<string,string> selectionBySection=new Dictionary<string,string>();
 Rectangle normalBounds;
 FormWindowState normalState, gameWindowState;
 int displayMode;
 bool chromeReady;
 StatusStrip status=new StatusStrip();
 ToolStripStatusLabel message=new ToolStripStatusLabel();
 Panel rail=new Panel(), body=new Panel(), hero=new Panel(), libraryPage=new SoftPanel(), galleryArea=new SoftPanel();
 string section="Jogos",previousSection="Jogos";
 Game selected;
 int shown, page=150;
 bool fullscreen, scanning, refreshing, loadingArt, refreshArtRequested;
 UpdateReport latestUpdate;
 readonly ToolStripStatusLabel updateLink=new ToolStripStatusLabel(UiLanguage.Text("Ver resumo")){IsLink=true,Visible=false};
 Color accent, surface;
 readonly HashSet<string> running=new HashSet<string>();
 public static Button Button(string label){
  var b=new HubButton{Text=UiLanguage.Text(label.TrimEnd())+(label.Length>label.TrimEnd().Length?label.Substring(label.TrimEnd().Length):""),AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(31,32,47),ForeColor=Color.FromArgb(237,235,249),Margin=new Padding(3),Padding=new Padding(5,0,5,0),Cursor=Cursors.Hand};
  b.FlatAppearance.BorderSize=0;return b;
 }
 HubButton Action(FlowLayoutPanel panel,string label,Action click){var b=(HubButton)Button(label);if(!accent.IsEmpty){b.Accent=accent;b.BackColor=surface;b.ForeColor=ForeColor;}if(panel==detailButtons)b.PaintBackdrop=PaintHeroButtonBackdrop;b.Click+=(s,e)=>Safe(click);panel.Controls.Add(b);return b;}
 void Safe(Action action){try{action();}catch(Exception e){Log(e.ToString());MessageBox.Show(this,e.Message,ProductIdentity.Name,MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
 void Log(string error){try{Directory.CreateDirectory(Storage.Root);File.AppendAllText(Path.Combine(Storage.Root,"nexus.log"),DateTime.Now+" "+error+Environment.NewLine);}catch{}}
 void Save(){Storage.Write(data);}
 public Hub(Library library,bool enableTray=false,bool offerTour=false){
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  if(enableTray)SetupTray();
  data=library;Text=ProductIdentity.Name+UiLanguage.Text(" — Biblioteca de jogos");MinimumSize=new Size(840,580);Size=new Size(1240,780);StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;Font=Skin.UiFont(10);ForeColor=Color.White;
  SetupRail();
  body.Dock=DockStyle.Fill;body.Padding=new Padding(24,8,24,12);
  toolbar.Dock=DockStyle.Top;toolbar.Height=55;toolbar.Padding=new Padding(0,7,0,4);
  SetupSearch();
  sort.DropDownStyle=ComboBoxStyle.DropDownList;
  sort.Items.AddRange(new[]{"A–Z","Adicionados recentemente","Mais jogados","Últimos jogados","Ordem manual","Z–A"}.Select(UiLanguage.Text).Cast<object>().ToArray());
  sort.SelectedIndex=0;sort.SelectedIndexChanged+=(s,e)=>RefreshList();
  AddSelector(sort,"Ordenar");SetupFilters();
  HubButton view=null;
  view=Action(toolbar,"Exibição ▾",()=>ShowMenu(view,new[]{"Grade","Lista","Compacta"},new Action[]{()=>SetLibraryView(0),()=>SetLibraryView(1),()=>SetLibraryView(2)}));
  Action(toolbar,"Atualizar jogos",()=>Scan(true));
  Action(toolbar,"＋ Adicionar",AddCustom).Primary=true;
  hero.Dock=DockStyle.Top;hero.Height=213;hero.Paint+=PaintHero;
  heroText.Dock=DockStyle.Left;heroText.Padding=new Padding(22,20,12,13);heroText.BackColor=Color.Transparent;
  hero.Resize+=(s,e)=>heroText.Width=Math.Min(hero.ClientSize.Width,Math.Max(700,hero.Width*60/100));
  libraryPage.Resize+=(s,e)=>hero.Height=libraryPage.ClientSize.Height<600?183:213;
  title.Dock=DockStyle.Top;title.Height=55;title.BackColor=Color.Transparent;title.Font=Skin.HeadingFont(26);title.AutoEllipsis=true;
  subtitle.Dock=DockStyle.Top;subtitle.Height=30;subtitle.BackColor=Color.Transparent;subtitle.ForeColor=Color.FromArgb(206,184,251);subtitle.AutoEllipsis=true;
  details.Dock=DockStyle.Fill;details.AutoEllipsis=true;details.BackColor=Color.Transparent;details.ForeColor=Color.FromArgb(188,189,210);
  detailButtons.Dock=DockStyle.Bottom;detailButtons.Height=42;detailButtons.WrapContents=true;detailButtons.AutoScroll=false;
  detailButtons.SizeChanged+=(s,e)=>FitHeroButtons();
  detailButtons.BackColor=Color.Transparent;
  heroText.Controls.Add(details);heroText.Controls.Add(detailButtons);heroText.Controls.Add(subtitle);heroText.Controls.Add(title);hero.Controls.Add(heroText);
  list.Dock=DockStyle.Fill;list.BorderStyle=BorderStyle.None;list.ForeColor=Color.White;list.MultiSelect=false;list.FullRowSelect=true;list.HideSelection=false;list.ShowItemToolTips=true;list.AllowDrop=true;list.OwnerDraw=true;list.DrawItem+=DrawGame;list.DrawColumnHeader+=(s,e)=>{using(var brush=new SolidBrush(surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,Font,e.Bounds,ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);};list.DrawSubItem+=(s,e)=>{if(list.View==View.Details){var fill=e.Item.Selected?Skin.Mix(surface,accent,.25f):BackColor;using(var brush=new SolidBrush(fill))e.Graphics.FillRectangle(brush,e.Bounds);var bounds=e.Bounds;var game=e.Item.Tag as Game;if(e.ColumnIndex==2&&game!=null){DrawOrigin(e.Graphics,Origin(game),new Rectangle(bounds.X+2,bounds.Y+1,16,16),game);bounds.X+=22;bounds.Width-=22;}if(bounds.Width>0)TextRenderer.DrawText(e.Graphics,e.SubItem.Text,Font,bounds,ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}};
  covers.ImageSize=new Size(126,178);covers.ColorDepth=ColorDepth.Depth32Bit;list.LargeImageList=covers;var imageHandle=covers.Handle;using(var sizing=Placeholder())covers.Images.Add(sizing);
  foreach(var c in new[]{"Jogo","Plataforma","Origem","Tempo","Instalação"})list.Columns.Add(UiLanguage.Text(c),c=="Jogo"?300:125);
  list.HandleCreated+=(s,e)=>((Gallery)list).IconSpacing(covers.ImageSize);
  ApplyGridSize();SetupCardHover();
  list.SelectedIndexChanged+=(s,e)=>{if(!refreshing)SetGame(list.SelectedItems.Count==0?null:list.SelectedItems[0].Tag as Game);};
  list.ItemActivate+=(s,e)=>Safe(()=>{if(list.SelectedItems.Count==0)return;var g=list.SelectedItems[0].Tag as Game;if(g==null){if(list.SelectedItems[0].Name!="empty-collection"){page+=150;RefreshList();LoadArt(false);}}else if(HasPlayableEdition(g)&&!RequiresPlayButton(g))ChoosePlay(g);});
  SetupCustomCardActions();
  list.ItemDrag+=(s,e)=>{var item=e.Item as ListViewItem;var g=item==null?null:item.Tag as Game;if(g!=null)list.DoDragDrop(g.Id,DragDropEffects.Copy);};
  libraryHeading.Dock=DockStyle.Fill;libraryHeading.Height=43;libraryHeading.Font=Skin.HeadingFont(15);libraryHeading.TextAlign=ContentAlignment.MiddleLeft;
  libraryBar.Height=43;libraryBar.Dock=DockStyle.Top;libraryBar.Controls.Add(libraryHeading);
  FormClosed+=(s,e)=>{foreach(var icon in sourceIcons.Values)if(icon!=null)icon.Dispose();};
  empty.Dock=DockStyle.Fill;emptyText.Dock=DockStyle.Top;emptyText.Height=86;emptyText.Padding=new Padding(20,0,16,0);emptyText.TextAlign=ContentAlignment.MiddleLeft;emptyText.ForeColor=Color.FromArgb(188,189,210);empty.Controls.Add(emptyText);
  libraryPage.Dock=galleryArea.Dock=DockStyle.Fill;
  galleryArea.Controls.Add(list);galleryArea.Controls.Add(empty);
  libraryPage.Controls.Add(galleryArea);libraryPage.Controls.Add(libraryBar);SetupCollectionShowcase();libraryPage.Controls.Add(hero);SetupRecent();libraryPage.Controls.Add(filterChips);libraryPage.Controls.Add(toolbar);
  body.Controls.Add(libraryPage);
  SetupSocial();
  status.Items.Add(message);status.Items.Add(updateLink);updateLink.Click+=(s,e)=>ShowUpdateReport();status.SizingGrip=false;status.Font=Skin.UiFont(9);Controls.Add(body);Controls.Add(rail);Controls.Add(status);SetupChrome();
  KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.F){if(InSocial)Navigate("Jogos");search.Focus();search.SelectAll();e.Handled=true;}};
  SetupInstallationRefresh();
  ApplyTheme();BuildNav();SwitchHubPanel();RefreshList();
  var timer=new Timer{Interval=105};timer.Tick+=(s,e)=>Poll();timer.Start();FormClosed+=(s,e)=>{timer.Dispose();discordPresence.Dispose();covers.Dispose();ClearCards();sort.Dispose();if(heroImage!=null)heroImage.Dispose();};
  Shown+=(s,e)=>{((Gallery)list).ApplyWindowsTheme();chromeReady=true;SetMode(data.WindowMode.HasValue?data.WindowMode.Value:data.Fullscreen?2:0,false);if(offerTour&&!data.TourShown)BeginInvoke(new Action(()=>{ShowTour();if(data.AutoScan)Scan();}));else if(data.AutoScan)Scan();};
 }
 void BuildNav(){
  nav.SuspendLayout();
  try{
   var names=new[]{"Jogos","Recentes","Instalados","Favoritos","Zerados","Histórico","Coleções","Social","Adicionar jogo","Configurações"};
   var keys=names.SelectMany(name=>name=="Social"&&InSocial?new[]{name}.Concat(socialPlatforms.Select((platform,index)=>"social:"+index)):name=="Coleções"&&InCollections
    ?new[]{name}.Concat(data.Shelves.OrderBy(s=>s.Name,StringComparer.CurrentCultureIgnoreCase).Select(c=>"shelf:"+c.Name)):new[]{name}).ToArray();
   foreach(var key in navigation.Keys.Except(keys).ToArray()){navigation[key].Dispose();navigation.Remove(key);}
   for(int i=0;i<keys.Length;i++){
    var key=keys[i];HubButton button;
    if(!navigation.TryGetValue(key,out button)){
     var label=key=="Jogos"?"Biblioteca":key.StartsWith("shelf:")?key.Substring(6):key.StartsWith("social:")?socialPlatforms[Int32.Parse(key.Substring(7))].Name:key;
     button=Nav(label,key);if(key.StartsWith("shelf:"))button.Text=label;navigation.Add(key,button);
    }
    ConfigureNav(button,key,key=="Jogos"?"Biblioteca":button.Text);
    button.BackColor=rail.BackColor;button.Accent=accent;button.ForeColor=ForeColor;
    if(key=="Coleções")button.Text=UiLanguage.Text(InCollections?"Coleções  ▾":"Coleções  ›");
    if(key=="Social")button.Text=InSocial?"Social  ▾":"Social  ›";
    if(key.StartsWith("shelf:"))button.AllowDrop=!data.Shelves.First(c=>"shelf:"+c.Name==key).Smart;
    if(nav.Controls.GetChildIndex(button)!=i)nav.Controls.SetChildIndex(button,i);
   }
   UpdateNav();UpdateNavCounts();
  }finally{nav.ResumeLayout();}
 }
 void UpdateNav(){foreach(var pair in navigation)pair.Value.Active=pair.Key==section;}
 void Navigate(string key){

  if(section==key)return;
  bool collectionsWereOpen=InCollections,socialWasOpen=InSocial;
  if(selected!=null)selectionBySection[section]=selected.Id;
  previousSection=section;section=key;page=150;
  string id;
  selected=selectionBySection.TryGetValue(key,out id)?data.Games.FirstOrDefault(g=>g.Id==id):null;
  if(collectionsWereOpen!=InCollections||socialWasOpen!=InSocial)BuildNav();else UpdateNav();
  SwitchHubPanel();RefreshList();
 }
 HubButton Nav(string label,string key){
  var b=Action(nav,label,()=>{
   if(key=="Adicionar jogo"){AddCustom();return;}
   if(key=="Configurações"){Settings();return;}
   Navigate(key);
  });
  b.AutoSize=false;b.Width=154;b.Height=32;b.Sidebar=true;b.Margin=new Padding(3,2,3,2);
  if(key.StartsWith("social:")){b.Width=138;b.Margin=new Padding(19,1,3,1);b.Height=29;}
  if(key.StartsWith("shelf:")){
   b.Width=138;b.Margin=new Padding(19,1,3,1);b.Height=29;
   b.DragEnter+=(s,e)=>e.Effect=e.Data.GetDataPresent(typeof(string))?DragDropEffects.Copy:DragDropEffects.None;
   b.DragDrop+=(s,e)=>Safe(()=>{
    var shelf=data.Shelves.First(x=>x.Name==label);var id=e.Data.GetData(typeof(string)) as string;
    if(!shelf.Smart&&data.Games.Any(x=>x.Id==id)&&!shelf.Ids.Contains(id)){shelf.Ids.Add(id);Save();RefreshList();}
   });
  }
  return b;
 }
 static void Clear(Control parent){while(parent.Controls.Count>0)parent.Controls[0].Dispose();}
 static IEnumerable<string> Terms(string value){return (value??"").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>x!="");}
 static bool Match(Game g,string query){
  var text=String.Join(" ",g.Name,g.Alias,g.Platform,g.Source,g.Tags,g.Developer,g.Genre);
  return query.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries).All(q=>System.Globalization.CultureInfo.CurrentCulture.CompareInfo.IndexOf(text,q,System.Globalization.CompareOptions.IgnoreCase|System.Globalization.CompareOptions.IgnoreNonSpace)>=0);
 }
 static bool Installed(Game g){
  if(g.Missing||g.Archived)return false;
  if(XboxGames.IsTarget(g.Target))return true;
  if(g.Install!="")return Directory.Exists(g.Install);
  if(g.Rom!="")return File.Exists(g.Rom)&&File.Exists(g.Target);
  if(g.PortData!="")return File.Exists(g.PortData)&&File.Exists(g.Target);
  return File.Exists(g.Target)||IsUri(g.Target);
 }
 static bool IsUri(string target){return Regex.IsMatch(target??"",@"^[a-zA-Z][a-zA-Z0-9+.-]+:")&&!Path.IsPathRooted(target);}
 void RefreshList(){
  if(list.Parent==null||InSocial)return;
  var entries=data.Games.Where(g=>Match(g,search.Text));
  var shelves=data.Shelves.FirstOrDefault(x=>"shelf:"+x.Name==section);
  if(shelves!=null)entries=entries.Where(g=>BelongsTo(g,shelves));
  else if(section=="Coleções")entries=entries.Where(g=>data.Shelves.Any(s=>BelongsTo(g,s)));
  else if(section=="Favoritos")entries=entries.Where(g=>g.Favorite);
  else if(section=="Zerados")entries=entries.Where(g=>g.Completed);
  else if(section=="Histórico")entries=entries.Where(InUninstalledHistory);
  else if(section=="Recentes")entries=entries.Where(g=>g.Last.HasValue);

  else if(section=="Instalados")entries=entries.Where(Installed);
  if(chosenSources.Count>0)entries=entries.Where(g=>chosenSources.Any(source=>MatchesLauncher(g,source)));
  if(chosenStates.Count==1)entries=entries.Where(g=>Installed(g)==chosenStates.Contains("Instalados"));
  if(chosenCollections.Count>0)entries=entries.Where(g=>data.Shelves.Any(s=>chosenCollections.Contains(s.Name)&&BelongsTo(g,s)));
  if(sort.SelectedIndex==5)entries=entries.OrderByDescending(g=>g.Name,StringComparer.CurrentCultureIgnoreCase);else if(section=="Recentes"&&sort.SelectedIndex==0)entries=entries.OrderByDescending(g=>g.Last);else if(sort.SelectedIndex==1)entries=entries.OrderByDescending(g=>g.Added);else if(sort.SelectedIndex==2)entries=entries.OrderByDescending(g=>g.PlayedSeconds);else if(sort.SelectedIndex==3)entries=entries.OrderByDescending(g=>g.Last).ThenByDescending(g=>g.Added);else if(sort.SelectedIndex==0)entries=entries.OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase);
  var games=entries.ToList();shown=games.Count;
  var installed=new HashSet<string>(games.Where(Installed).Select(g=>g.Id));
  var recent=new HashSet<string>(data.Games.Where(g=>g.Last.HasValue).OrderByDescending(g=>g.Last).Take(1).Select(g=>g.Id));
  var grouped=section=="Jogos";
  var collectionOverview=section=="Coleções";
  var collectionGroups=collectionOverview?data.Shelves.Where(s=>chosenCollections.Count==0||chosenCollections.Contains(s.Name)).OrderBy(s=>s.Name,StringComparer.CurrentCultureIgnoreCase).ToArray():new Shelf[0];
  var collectionRows=collectionGroups.SelectMany(s=>games.Where(g=>BelongsTo(g,s)).Select(g=>Tuple.Create(s,g))).ToArray();
  var totalCards=collectionOverview?collectionRows.Length:shown;
  if(grouped)games=games.OrderBy(g=>LibraryGroup(g,recent,installed.Contains(g.Id))).ThenByDescending(g=>recent.Contains(g.Id)?g.Last:null).ToList();
  var previous=selected??(grouped?games.FirstOrDefault(g=>recent.Contains(g.Id)&&installed.Contains(g.Id)):null);var visibleGames=collectionOverview?collectionRows.Take(page).Select(row=>row.Item2).ToArray():games.Take(page).ToArray();
  var nextSelection=visibleGames.Contains(previous)?previous:visibleGames.FirstOrDefault();
  MoveCardHover(null);refreshing=true;list.BeginUpdate();body.SuspendLayout();
  try{
   var gallery=(Gallery)list;gallery.ResetGroupHeaders();gallery.GroupTotals.Clear();
   list.Items.Clear();list.Groups.Clear();
   var groups=new ListViewGroup[GroupTitles.Length];
   if(grouped)for(int index=0;index<groups.Length;index++){groups[index]=new ListViewGroup(UiLanguage.Text(GroupTitles[index]));list.Groups.Add(groups[index]);}
   if(grouped)foreach(var batch in games.GroupBy(g=>LibraryGroup(g,recent,installed.Contains(g.Id))))gallery.GroupTotals[UiLanguage.Text(GroupTitles[batch.Key])]=batch.Count();
   if(thumbnails.Count>420)ClearCards();
   if(cardImages.Count==0)cardImages.Add(Placeholder());
   list.View=LibraryView==1&&!fullscreen?View.Details:View.LargeIcon;

   ListViewGroup moreGroup=null;
   if(collectionOverview){
    var used=collectionRows.Take(page).ToLookup(row=>row.Item1,row=>row.Item2);
    foreach(var shelf in collectionGroups){
     if(!used.Contains(shelf)&&collectionRows.Any(row=>row.Item1==shelf))continue;
     var group=new ListViewGroup(shelf.Name);list.Groups.Add(group);gallery.GroupTotals[shelf.Name]=collectionRows.Count(row=>row.Item1==shelf);
     if(!used.Contains(shelf)){
      list.Items.Add(new ListViewItem(games.Count==0||search.Text==""?"Nenhum jogo nesta coleção":"Nenhum resultado nesta coleção"){Group=group,Name="empty-collection"});
      continue;
     }
     moreGroup=group;
     foreach(var game in used[shelf])AddGameCard(game,group,installed,nextSelection);
    }
   }else{
    foreach(var game in visibleGames){
     moreGroup=grouped?groups[LibraryGroup(game,recent,installed.Contains(game.Id))]:null;
     AddGameCard(game,moreGroup,installed,nextSelection);
    }
   }
   if(totalCards>page){
    var more=new ListViewItem(UiLanguage.Text("Carregar mais…"),0){Group=moreGroup};
    list.Items.Add(more);
   }
  }finally{list.EndUpdate();body.ResumeLayout();refreshing=false;}
  ((Gallery)list).IconSpacing(covers.ImageSize);((Gallery)list).RestoreCollapsed();
  bool hasRows=list.Items.Count!=0;empty.Visible=!hasRows;list.Visible=hasRows;
  emptyText.Text=UiLanguage.Text(search.Text!=""?"Nenhum jogo corresponde à busca.":section=="Favoritos"?"Favorite um jogo para encontrá-lo aqui.":section=="Zerados"?"Use Marcar como zerado nos detalhes do jogo.":section=="Histórico"?"Jogos removidos ou desinstalados aparecem aqui com o progresso salvo.":section=="Coleções"?"Crie uma coleção ou abra uma pelo submenu para adicionar jogos.":shelves!=null?"Nenhum jogo para mostrar nesta coleção.":"Nenhum jogo nesta seção.");
  UpdateRecent();UpdateNavCounts();
  libraryHeading.Text=section=="Coleções"?UiLanguage.Text("Jogos das coleções")+"   ·   "+shown:SectionTitle()+"   ·   "+shown;
  UpdateCollectionShowcase(collectionGroups);SetGame(nextSelection);
  message.Text=shown+UiLanguage.Text(" exibidos")+"  •  "+data.Games.Count+UiLanguage.Text(" na biblioteca")+"  •  "+Duration(data.Games.Sum(x=>x.PlayedSeconds));
 }
 void AddGameCard(Game game,ListViewGroup group,HashSet<string> installed,Game nextSelection){
  var icon=0;
  if(list.View==View.LargeIcon){
   if(game.Cover!="")icon=Thumbnail(game.Cover);
   else if(game.Icon!="")icon=Thumbnail(game.Icon,true);
  }
  var item=new ListViewItem(new[]{(game.Favorite?"★  ":"")+game.Name+(game.Completed?"  ✓":""),game.Platform,UiLanguage.Text(Origin(game)),Duration(game.PlayedSeconds),UiLanguage.Text(installed.Contains(game.Id)?"Instalado":InUninstalledHistory(game)?"Desinstalado":"Não instalado")},0){
   Group=group,Tag=game,Name=icon.ToString(),ToolTipText=game.Name+"  ·  "+game.Source
  };
  list.Items.Add(item);
  if(game==nextSelection&&list.SelectedItems.Count==0){item.Selected=true;item.Focused=true;}
 }
 static string Duration(long seconds){return (seconds/3600)+"h "+((seconds/60)%60)+"min";}
 void SetGame(Game game){
  playButton=null;
  selected=game;
  var asset=game==null?"":game.Banner!=""?game.Banner:game.Cover!=""?game.Cover:game.Icon;
  if(asset!=heroAsset){heroAsset=asset;if(heroImage!=null)heroImage.Dispose();heroImage=Storage.Picture(asset);hero.Invalidate(true);}
  detailButtons.SuspendLayout();try{
  Clear(detailButtons);
  if(section=="Coleções"){
   title.Text=UiLanguage.Text(section);subtitle.Text=data.Shelves.Count+(UiLanguage.English?(data.Shelves.Count==1?" collection":" collections"):(data.Shelves.Count==1?" coleção":" coleções"));
   Action(detailButtons,"＋ Nova coleção",()=>Collection(null));
  }else{title.Text=game==null?(SectionTitle()):game.Name;subtitle.Text=game==null?UiLanguage.Text("Biblioteca local")+"  /  "+data.Profile:game.Platform+"   /   "+UiLanguage.Text(game.Source)+"   /   "+Duration(game.PlayedSeconds);}
  if(ActiveSource==RiotGames.Source)Action(detailButtons,"Atualizar jogos Riot",()=>Scan(true));
  if(ActiveSource==XboxGames.Source){Action(detailButtons,"Atualizar jogos Xbox",()=>ScanXbox(true));Action(detailButtons,"Escolher jogo instalado…",()=>PickXbox());}
  if(ActiveSource=="Epic Games")Action(detailButtons,"＋ Adicionar por caminho",AddEpic);
  var shelf=data.Shelves.FirstOrDefault(x=>"shelf:"+x.Name==section);
  if(shelf!=null){
   Action(detailButtons,"Renomear coleção",()=>Collection(shelf));
   if(!shelf.Smart)Action(detailButtons,"Gerenciar jogos…",()=>ManageCollection(shelf));
   Action(detailButtons,"Excluir coleção",()=>DeleteCollection(shelf));
  }
  if(game==null){details.Text=UiLanguage.Text(section=="Coleções"?"Abra uma coleção no submenu à esquerda.":shelf!=null?"Os jogos desta coleção aparecem aqui.":ActiveSource==RiotGames.Source?ProductIdentity.Name+" procura instalações locais e abre os jogos pelo Riot Client.":ActiveSource==XboxGames.Source?"Importe jogos instalados ou escolha um título pelo menu Iniciar do Windows.":ActiveSource=="Epic Games"?"Use Atualizar jogos ou escolha a pasta, o executável ou o atalho do jogo.":"Adicione um executável ou importe os jogos das lojas instaladas neste computador.");return;}
  details.Text=String.Join("   •   ",new[]{game.Description,game.Tags,!game.Last.HasValue?"":UiLanguage.Text("Última execução ")+game.Last.Value.ToString("g")}.Where(x=>x!=""));

  if(game.Completed)subtitle.Text+="   ·   "+UiLanguage.Text("Zerado");
  var ready=HasPlayableEdition(game);
  playButton=ready?Action(detailButtons,"▶  Jogar",()=>ChoosePlay(game)):Action(detailButtons,DownloadAction(game),()=>OpenInstallation(game));playButton.Primary=true;
  if(!Installed(game))subtitle.Text+="   /   "+UiLanguage.Text(InUninstalledHistory(game)?"Desinstalado":"Não instalado nesta plataforma");
  Action(detailButtons,"Editar",()=>{if(IsCustom(game))EditCustom(game);else Edit(game);});Action(detailButtons,game.Favorite?"★ Favorito":"☆ Favoritar",()=>{game.Favorite=!game.Favorite;Save();RefreshList();});Action(detailButtons,"Abrir pasta",()=>OpenFolder(game));
  Action(detailButtons,"Conquistas",()=>ShowAchievements(game));
  Action(detailButtons,game.Completed?"Desmarcar zerado":"Marcar como zerado",()=>ToggleCompleted(game));
  if(CanRemove(game))Action(detailButtons,"Remover",()=>RemoveGame(game));
  }finally{detailButtons.ResumeLayout();FitHeroButtons();}
 }
 void OpenFolder(Game g){
  var folder=g.Rom!=""?Path.GetDirectoryName(g.Rom):g.Install!=""?g.Install:g.Folder!=""?g.Folder:File.Exists(g.Target)?Path.GetDirectoryName(g.Target):"";
  if(!Directory.Exists(folder))throw new IOException(UiLanguage.Text("Pasta indisponível."));Process.Start("explorer.exe","\""+folder+"\"");
 }
 void Edit(Game game){
  using(var form=new CustomGameDialog(game,false,BackColor,surface,accent,true)){
   if(form.ShowDialog(this)!=DialogResult.OK)return;
   game.Name=form.GameName;game.Cover=Storage.Asset(form.Cover);game.RefreshCover=false;
   selected=game;Save();RefreshList();
  }
 }
 static bool Yes(string text){return text.Equals("sim",StringComparison.OrdinalIgnoreCase);}
 async void Scan(bool showReport=false){
  if(scanning||xboxScanning)return;
  scanning=true;message.Text=UiLanguage.Text("Atualizando jogos dos launchers…");
  var report=new UpdateReport(Launchers);
  try{
   var knownIds=data.Games.Select(g=>g.StoreId).ToArray();
   var result=await Task.Run(()=>{
    string errors;
    var installed=Stores.Scan(out errors);
    var cacheErrors=new List<string>();
    var owned=OwnedGames.Scan(cacheErrors);
    XboxGames.Snapshot xbox=null;
    string xboxError="";
    try{xbox=XboxGames.Scan();}catch(Exception error){xboxError=error.Message;}
    return new {Installed=installed,Errors=errors,Owned=owned,CacheErrors=cacheErrors,Xbox=xbox,XboxError=xboxError,
     Times=Playtime.Read(),Achievements=GameProgress.Steam(knownIds.Concat(installed.Select(g=>g.StoreId)).Concat(owned.Select(g=>g.StoreId)))};
   });
   if(IsDisposed)return;
   Stores.Merge(data,result.Installed,result.Errors);
   OwnedGames.Add(data,result.Owned);ApplyAchievements(result.Achievements);
   if(result.Xbox!=null)XboxGames.Merge(data,result.Xbox);
   var found=result.Xbox==null?result.Installed:result.Installed.Concat(result.Xbox.Games);
   report.ReadGames(found,result.Owned,data.Games.Where(Installed));
   platformTimes=result.Times;
   platformTimes.Apply(data.Games,(game,origin)=>report.Time(game.Source,origin));
   report.ReadErrors(result.Errors,result.CacheErrors,result.XboxError);
   if(result.Errors!="")Log(result.Errors);
   if(result.XboxError!="")Log(result.XboxError);
   Save();RefreshList();
   while(loadingArt&&!IsDisposed)await Task.Delay(100);
   if(IsDisposed)return;
   await LoadArtCore(false,report);
   if(IsDisposed)return;
   latestUpdate=report;updateLink.Visible=true;
   message.Text=UiLanguage.Text("Atualização concluída")+"  •  "+report.TotalGames+UiLanguage.Text(" jogos encontrados")+"  •  "+report.TotalCovers+UiLanguage.Text(" capas atualizadas");
   if(showReport&&Visible&&WindowState!=FormWindowState.Minimized)ShowUpdateReport();
  }catch(Exception error){if(!IsDisposed)Safe(()=>{throw new IOException(error.Message,error);});}
  finally{scanning=false;}
 }
 void AddEpic(){
  using(var f=new Editor("Adicionar jogo da Epic Games por caminho")){
   f.Add("Nome (opcional)","");
   f.Add("Pasta de instalação","","dir");
   f.Add("Executável ou atalho","","file");
   f.Add("Capa (opcional)","","img");
   if(f.ShowDialog(this)!=DialogResult.OK)return;
   var install=f.V("Pasta de instalação");var file=f.V("Executável ou atalho");
   if(install==""&&file=="")throw new IOException(UiLanguage.Text("Selecione a pasta do jogo ou um executável/atalho."));
   if(install!=""&&!Directory.Exists(install))throw new IOException(UiLanguage.Text("Pasta de instalação inválida."));
   var installSelected=install!="";
   if(file!=""){
    var ext=Path.GetExtension(file).ToLowerInvariant();
    if(!File.Exists(file)||(ext!=".exe"&&ext!=".lnk"&&ext!=".url"))throw new IOException(UiLanguage.Text("Selecione um arquivo .exe, .lnk ou .url existente."));
    if(install==""&&ext==".exe")install=Path.GetDirectoryName(file);
   }
   var name=f.V("Nome (opcional)");
   if(name=="")name=Stores.EpicName(install!=""?new DirectoryInfo(install).Name:Path.GetFileNameWithoutExtension(file),install);
   var target=file!=""?file:Stores.EpicUri(install.Replace('\\','/'));
   var existing=data.Games.FirstOrDefault(g=>g.Source=="Epic Games"&&((install!=""&&g.Install!=""&&String.Equals(Stores.EpicFolderKey(g.Install),Stores.EpicFolderKey(install),StringComparison.OrdinalIgnoreCase))||g.Target.Equals(target,StringComparison.OrdinalIgnoreCase)));
   if(existing!=null&&MessageBox.Show(this,"Atualizar o caminho de "+existing.Name+"?","Epic Games",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
   var cover=Storage.Asset(f.V("Capa (opcional)"));
   if(existing!=null){
    existing.Target=target;existing.Install=install;existing.Folder=installSelected?install:"";existing.ManualInstall=true;RecordInstallation(existing);
    if(cover!="")existing.Cover=cover;
    selected=existing;
   }else{
    selected=new Game{Name=name,Source="Epic Games",Install=install,Folder=installSelected?install:"",Target=target,ManualInstall=true,Cover=cover};
    RecordInstallation(selected);data.Games.Add(selected);
   }
   Save();RefreshList();LoadArt(false);
  }
 }
 async void LoadArt(bool manual){await LoadArtCore(manual,null);}
 async Task LoadArtCore(bool manual,UpdateReport report){
  if(loadingArt){refreshArtRequested=true;return;}
  var visible=new HashSet<string>(list.Items.Cast<ListViewItem>().Select(row=>row.Tag as Game).Where(g=>g!=null).Select(g=>g.Id));
  var missing=data.Games.Where(g=>
   !g.Archived&&(g.Missing?g.Cover==""&&(Artwork.StoreCoverSupported(g)||visible.Contains(g.Id)&&Artwork.AppId(g)!=""):
   (g.Cover==""||g.RefreshCover||g.Banner=="")&&Artwork.AppId(g)!=""||
   InstalledArt.Supported(g)&&(g.Icon==""||g.Source==XboxGames.Source&&(g.Cover==""||g.Banner==""))||
   (g.Source=="Epic Games"||Artwork.StoreCoverSupported(g))&&(g.Cover==""||g.RefreshCover)||CustomArtwork.Supports(g)&&(g.RefreshCover||visible.Contains(g.Id)&&(g.Cover==""||g.Rom==""&&g.Icon==""))))
   .OrderBy(g=>g.Missing).ThenBy(g=>!visible.Contains(g.Id))
   .Select(g=>new Game{Id=g.Id,StoreId=g.StoreId,Name=g.Name,Source=g.Source,Install=g.Install,Target=g.Target,Cover=g.RefreshCover?"":g.Cover,Banner=g.Banner,Icon=g.Icon,Rom=g.Rom,PortData=g.PortData,Platform=g.Platform,Missing=g.Missing}).ToArray();
  if(missing.Length==0){if(manual)message.Text=UiLanguage.Text("Sem capas compatíveis pendentes. Para outras capas, use Editar.");return;}
  loadingArt=true;message.Text=UiLanguage.Text("Buscando capas e banners…");
  try{
   int newCovers=0,newBanners=0,newIcons=0;
   for(int offset=0;offset<missing.Length;offset+=20){
    var batch=missing.Skip(offset).Take(20).ToArray();
    var result=await Task.Run(()=>Artwork.Find(batch));
    if(IsDisposed)return;
    var byId=data.Games.ToDictionary(g=>g.Id);
    foreach(var art in result){
     Game game;if(!byId.TryGetValue(art.Id,out game))continue;
     if(!game.Archived&&(game.Cover==""||game.RefreshCover)&&art.Cover!=""){game.Cover=art.Cover;game.RefreshCover=false;newCovers++;if(report!=null)report.Cover(game.Source);}
     if(game.Banner==""&&art.Banner!=""){game.Banner=art.Banner;newBanners++;}
     if(game.Icon==""&&art.Icon!=""){game.Icon=art.Icon;newIcons++;}
    }
    if(result.Count>0){Save();UpdateVisibleArtwork();}
   }
   if(newCovers+newBanners+newIcons>0){Notify(newCovers+" capas, "+newBanners+" banners e "+newIcons+" ícones adicionados.");}
   else message.Text=UiLanguage.Text("Nenhuma imagem nova disponível. Use Editar para selecionar uma capa.");
  }catch(Exception error){if(!IsDisposed){Log(error.ToString());if(report!=null)report.ArtFailed=true;message.Text=UiLanguage.Text("Falha ao buscar capas. Consulte nexus.log.");}}
  finally{loadingArt=false;if(refreshArtRequested&&!IsDisposed){refreshArtRequested=false;LoadArt(false);}}
 }
 async void Play(Game g){
  try{
   if(running.Contains(g.Id))return;
   if(!Installed(g))throw new IOException(UiLanguage.Text("Este jogo ainda não está instalado ou o arquivo foi movido."));
   if(g.Target=="")throw new IOException(UiLanguage.Text("Configure o executável ou protocolo pelo botão Editar."));
   if(g.Rom!=""&&!File.Exists(g.Rom))throw new IOException(UiLanguage.Text("ROM não encontrada."));
   if(g.PortData!=""&&!File.Exists(g.PortData))throw new IOException(UiLanguage.Text("Arquivo de dados do port não encontrado. Atualize o caminho em Editar."));
   var args=RomLibrary.Arguments(g);
   Process process;
   if(XboxGames.IsTarget(g.Target)){
    if(g.Missing)throw new IOException(UiLanguage.Text("O Windows não encontrou o jogo na última atualização."));
    XboxGames.Launch(g.Target,args);g.Last=DateTime.Now;Save();RefreshList();return;
   }
   if(IsUri(g.Target)){
    process=Process.Start(new ProcessStartInfo(g.Target){UseShellExecute=true});
    if(process!=null)process.Dispose();
    g.Last=DateTime.Now;Save();RefreshList();return;
   }
   if(!File.Exists(g.Target))throw new IOException(UiLanguage.Text("Arquivo não encontrado: ")+g.Target);
   var ext=Path.GetExtension(g.Target).ToLowerInvariant();var script=ext==".cmd"||ext==".bat"||ext==".ps1";
   if(script&&MessageBox.Show(this,"Executar este script local?\n\n"+g.Target,"Confirmar execução",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
   var info=new ProcessStartInfo(g.Target,args){WorkingDirectory=g.Folder!=""?g.Folder:Path.GetDirectoryName(g.Target),UseShellExecute=ext!=".exe"&&!script};
   if(ext==".ps1"){info.FileName=Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe");info.Arguments="-NoProfile -File \""+g.Target+"\" "+args;}
   else if(script){
    if(Regex.IsMatch(args+g.Target,"[&|<>^%!\\r\\n]"))throw new IOException(UiLanguage.Text("Caracteres de controle do CMD não são aceitos em caminhos ou argumentos."));
    info.FileName=Path.Combine(Environment.SystemDirectory,"cmd.exe");info.Arguments="/d /v:off /s /c \"\""+g.Target+"\" "+args+"\"";
   }
   if(!info.UseShellExecute)foreach(var line in g.Environment.Split(new[]{'\r','\n',';'},StringSplitOptions.RemoveEmptyEntries)){
    var eq=line.IndexOf('=');if(eq<1)throw new IOException(UiLanguage.Text("Informe variáveis no formato NOME=valor."));info.EnvironmentVariables[line.Substring(0,eq).Trim()]=line.Substring(eq+1);
   }
   process=Process.Start(info);
   var start=DateTime.Now;g.Last=start;
   if(g.StoreId.StartsWith("riot:",StringComparison.OrdinalIgnoreCase)){
    if(process!=null)process.Dispose();
    Save();RefreshList();
    if(!running.Add(g.Id))return;
    try{
     var gameProcess=await Task.Run(()=>RiotGames.WaitForGame(g,start,()=>IsDisposed));
     if(gameProcess==null)return;
     DateTime actualStart;
     using(gameProcess){
      actualStart=gameProcess.StartTime;
      await Task.Run(()=>gameProcess.WaitForExit());
     }
     if(IsDisposed)return;
     var actualEnd=DateTime.Now;
     g.RecordSession(actualStart,actualEnd);
     Save();RefreshList();
    }finally{running.Remove(g.Id);}
    return;
   }
   if(process==null||info.UseShellExecute||g.StoreId.StartsWith("gog:")){
    if(process!=null)process.Dispose();Save();RefreshList();return;
   }
   running.Add(g.Id);StartDiscordSession(g,start);UpdateRecent();Safe(Save);
   if(g.Rom!=""&&!data.DiscordEnabled)message.Text=UiLanguage.Text("Atividade do Discord desativada. Para ativar, abra Configurações → Social.");
   if(data.Minimize&&WindowState!=FormWindowState.Minimized){gameWindowState=WindowState;WindowState=FormWindowState.Minimized;}
   try{await Task.Run(()=>{using(process)process.WaitForExit();});}
   finally{running.Remove(g.Id);EndDiscordSession(g);}
   if(IsDisposed)return;
   var end=DateTime.Now;g.RecordSession(start,end);Save();RefreshList();
   if(data.Minimize&&running.Count==0&&WindowState==FormWindowState.Minimized)WindowState=gameWindowState;
  }catch(Exception e){if(!IsDisposed)Safe(()=>{throw new IOException(e.Message,e);});}
 }
 [StructLayout(LayoutKind.Sequential)] struct Pad {public uint Packet;public ushort Buttons;public byte Lt,Rt;public short X,Y,Rx,Ry;}
 [DllImport("xinput1_4.dll")] static extern uint XInputGetState(uint index,out Pad state);
 int previousPad, repeats;
 bool padAvailable=true;
 void Poll(){
  if(!padAvailable)return;
  Pad p=new Pad();bool active=false;
  try{for(uint i=0;i<4;i++)if(XInputGetState(i,out p)==0){active=true;break;}}catch(DllNotFoundException){padAvailable=false;return;}
  controller.Text=UiLanguage.Text(active?"Controle conectado":"Sem controle");
  if(!active||Form.ActiveForm!=this){previousPad=0;return;}
  int buttons=p.Buttons;
  if(p.Y>19000)buttons|=1;if(p.Y<-19000)buttons|=2;if(p.X<-19000)buttons|=4;if(p.X>19000)buttons|=8;
  if(p.Lt>120)buttons|=0x10000;if(p.Rt>120)buttons|=0x20000;
  var pressed=buttons&~previousPad;repeats=buttons==previousPad?repeats+1:0;previousPad=buttons;
  if(repeats>=3)pressed|=buttons&0x3000F;
  if((pressed&0x10)!=0){Safe(Settings);return;}
  if((pressed&0x8000)!=0){if(InSocial)Navigate("Jogos");Safe(VirtualSearch);return;}
  if((pressed&0x2000)!=0){Navigate(navigation.ContainsKey(previousSection)?previousSection:"Jogos");return;}
  if((pressed&0x300)!=0){
   string[] sections={"Jogos","Recentes","Instalados","Favoritos","Zerados","Histórico","Coleções","Social"};
   var index=Array.IndexOf(sections,section);Navigate(sections[(index+((pressed&256)!=0?sections.Length-1:1)+sections.Length)%sections.Length]);return;
  }
  if(InSocial)return;
  if((pressed&0x3000F)!=0&&list.Items.Count>0){
   list.Focus();
   if((pressed&0x10000)!=0)SendKeys.Send("{PGUP}");else if((pressed&0x20000)!=0)SendKeys.Send("{PGDN}");
   else if((pressed&1)!=0)SendKeys.Send("{UP}");else if((pressed&2)!=0)SendKeys.Send("{DOWN}");else if((pressed&4)!=0)SendKeys.Send("{LEFT}");else SendKeys.Send("{RIGHT}");
  }
  if((pressed&0x4000)!=0&&selected!=null){selected.Favorite=!selected.Favorite;Safe(()=>{Save();RefreshList();});}
  if((pressed&0x1000)!=0&&list.Items.Count>0){if(list.SelectedItems.Count>0&&list.SelectedItems[0].Tag==null){if(list.SelectedItems[0].Name!="empty-collection"){page+=150;RefreshList();LoadArt(false);}}else if(selected!=null){if((RequiresPlayButton(selected)||!Installed(selected))&&playButton!=null){if(playButton.Focused)playButton.PerformClick();else playButton.Focus();}else ChoosePlay(selected);}}
 }
 void VirtualSearch(){
  using(var f=new Form{Text=UiLanguage.Text("Pesquisar na biblioteca"),StartPosition=FormStartPosition.CenterParent,Size=new Size(520,360),BackColor=surface,ForeColor=Color.White}){
   var input=new TextBox{Dock=DockStyle.Top,Text=search.Text};
   var grid=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12)};
   var keys=new List<Button>();
   foreach(char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")Action(grid,c.ToString(),()=>input.AppendText(c.ToString()));
   Action(grid,"Espaço",()=>input.AppendText(" "));Action(grid,"Apagar",()=>{if(input.TextLength>0)input.Text=input.Text.Substring(0,input.TextLength-1);});Action(grid,"Buscar",()=>{f.DialogResult=DialogResult.OK;});
   keys.AddRange(grid.Controls.Cast<Button>());f.Controls.Add(grid);f.Controls.Add(input);
   f.AcceptButton=keys.Last();
   int index=0,last=0;
   using(var timer=new Timer{Interval=100}){
    timer.Tick+=(s,e)=>{Pad p;int slot=0;while(XInputGetState((uint)slot,out p)!=0&&++slot<4){}if(slot==4)return;int current=p.Buttons;if(p.Y>19000)current|=1;if(p.Y<-19000)current|=2;if(p.X<-19000)current|=4;if(p.X>19000)current|=8;int press=current&~last;last=current;
     if((press&0x2000)!=0){f.Close();return;}if((press&0x1000)!=0){keys[index].PerformClick();return;}
     int move=(press&1)!=0?-8:(press&2)!=0?8:(press&4)!=0?-1:(press&8)!=0?1:0;
     if(move!=0){index=(index+move+keys.Count)%keys.Count;keys[index].Focus();}
    };
    f.Shown+=(s,e)=>{keys[0].Focus();timer.Start();};
    if(f.ShowDialog(this)==DialogResult.OK)search.Text=input.Text;
   }
  }
  list.Focus();
 }
}
static class Program {
 [STAThread] static int Main(string[] args){
  System.Net.ServicePointManager.SecurityProtocol|=System.Net.SecurityProtocolType.Tls12;
  EmbeddedWebView.Register();
  var qa=Path.GetFileName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).StartsWith("qa-");
  if(args.Length==2&&args[0]=="--check-rom"&&qa){
   File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rom-child.txt"),args[1]);return 0;
  }
  if(args.Contains("--self-test")){
   if(!qa)return 2;
   Storage.UseTestRoot();return SelfTest();
  }
  if(args.Contains("--self-test-social")){
   if(!qa)return 2;
   Storage.UseTestRoot();
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   int index=0;
   var testProvider=args.FirstOrDefault(arg=>arg.StartsWith("--provider=",StringComparison.Ordinal));
   if(testProvider!=null&&!Int32.TryParse(testProvider.Substring(11),out index))return 2;
   try{return Hub.TestSocialWeb(args.Contains("--live"),index);}
   catch(Exception error){Directory.CreateDirectory(Storage.Root);File.WriteAllText(Path.Combine(Storage.Root,"webview-error.txt"),error.ToString());return 4;}
  }
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try{
   using(var lockFile=LockData()){
    Storage.MigrateLegacy();
    var library=File.Exists(Storage.FileName)?Storage.Read(Storage.FileName):new Library();
    if(library==null)throw new IOException(UiLanguage.Text("Biblioteca inválida."));
    if(library.RestoreArchivedGames()|library.MigrateCategories())Storage.Write(library);
    UiLanguage.Select(library.Language);
    WindowsShortcuts.FirstRun(library);
    try{WindowsShortcuts.RepairStartup();}
    catch(Exception error){
     if(!(error is IOException||error is UnauthorizedAccessException||error is COMException))throw;
     MessageBox.Show(UiLanguage.Text("Não foi possível atualizar a inicialização do FitaHex. ")+error.Message,
      ProductIdentity.Name,MessageBoxButtons.OK,MessageBoxIcon.Warning);
    }
    bool reload;
    do{
     UiLanguage.Select(library.Language);
     using(var window=new Hub(library,true,true)){
      Application.Run(window);reload=window.ReloadLanguage;
     }
     if(reload)library=Storage.Read(Storage.FileName);
    }while(reload);
   }
   return 0;
  }catch(Exception e){MessageBox.Show(e.Message,ProductIdentity.Name,MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
 }
 static FileStream LockData(){
  Directory.CreateDirectory(Storage.Root);
  try{return new FileStream(Path.Combine(Storage.Root,"fitahex.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
  catch(IOException){throw new IOException(UiLanguage.Text("A biblioteca está em uso por outra instância ou a pasta não está disponível."));}
 }
 static void VerifyLanguages(){
  var br=new System.Globalization.CultureInfo("pt-BR");
  var us=new System.Globalization.CultureInfo("en-US");
  if(UiLanguage.UseEnglish("",br)||!UiLanguage.UseEnglish("",us)||
   !UiLanguage.UseEnglish("en",br)||UiLanguage.UseEnglish("pt-BR",us))throw new IOException("Idioma automático.");
  UiLanguage.Select("en");
  try{
   using(var window=new Hub(new Library{Language="en"}))window.VerifyEnglishLabels();
  }finally{UiLanguage.Select("pt-BR");}
 }
 static void TestLegacyMigration(string fixture){
  var legacy=Path.Combine(fixture,"Legacy");var destination=Path.Combine(fixture,"New");
  Directory.CreateDirectory(Path.Combine(legacy,"images"));
  File.WriteAllText(Path.Combine(legacy,"images","cover.png"),"fixture");
  var saved=new Library{Theme="Sand",TourShown=true};
  saved.Games.Add(new Game{Name="Jogo salvo",Seconds=7200,Completed=true});
  var oldFile=Path.Combine(legacy,"library.json");
  File.WriteAllText(oldFile,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(saved));
  if(!Storage.MigrateLegacy(legacy,destination)||Storage.MigrateLegacy(legacy,destination)||
   !File.Exists(oldFile)||!File.Exists(Path.Combine(destination,"images","cover.png")))
   throw new IOException(UiLanguage.Text("Migração dos dados locais."));
  var restored=Storage.Read(Path.Combine(destination,"library.json"));
  if(restored.Theme!="Sand"||!restored.TourShown||restored.Games.Single().Seconds!=7200||!restored.Games.Single().Completed)
   throw new IOException(UiLanguage.Text("Dados migrados incompletos."));
 }
 static void TestUninstalledHistory(string fixture){
  var library=new Library();
  var game=new Game{Name="Jogo",StoreId="steam:404",Source="Steam",Install=fixture,Seconds=300,Completed=true};
  library.Games.Add(game);
  Stores.Merge(library,new List<Game>(),"");
  if(!game.Missing||!Hub.InUninstalledHistory(game))throw new IOException(UiLanguage.Text("Histórico Steam."));
  Stores.Merge(library,new List<Game>{new Game{StoreId=game.StoreId,Source=game.Source,Install=fixture}},"");
  if(library.Games.Count!=1||game.Missing||!game.RefreshCover||!game.Completed||game.Seconds!=300)throw new IOException(UiLanguage.Text("Reinstalação Steam."));
  var rom=new Game{Name="Nome editado",Rom=Path.Combine(fixture,"old","Puzzle.gba"),Target="mgba.exe",
   Platform="Game Boy / GBA",Source="Emulador",Seconds=90,Completed=true};
  library.ArchivedGames.Add(rom);
  if(!library.RestoreArchivedGames()||!rom.Archived||!Hub.InUninstalledHistory(rom))throw new IOException(UiLanguage.Text("Histórico anterior."));
  var moved=new Game{Rom=Path.Combine(fixture,"new","Puzzle.gba"),Target="mgba.exe",Platform=rom.Platform,Source=rom.Source};
  if(Hub.MatchUninstalled(library,moved)!=rom)throw new IOException(UiLanguage.Text("ROM movida."));
  Hub.RecordInstallation(rom);
  if(rom.Archived||rom.Missing||!rom.Completed||rom.Seconds!=90)throw new IOException(UiLanguage.Text("Progresso da ROM."));
 }
 static void TestRoms(string fixture){
  var folder=Path.Combine(fixture,"ROMs de teste");var nested=Path.Combine(folder,"Outra pasta");
  Directory.CreateDirectory(nested);
  var first=Path.Combine(folder,"Corrida de teste.rvz");var second=Path.Combine(nested,"Jogo 2.iso");
  File.WriteAllText(first,"fixture");File.WriteAllText(second,"fixture");File.WriteAllText(Path.Combine(folder,"notes.txt"),"fixture");
  var configured=RomLibrary.Detect(Path.Combine(fixture,"Dolphin.exe"));
  var files=RomLibrary.Find(folder,configured.Extensions);
  if(files.Length!=2||!files.Contains(second)||!configured.Args.Contains("-e"))throw new IOException(UiLanguage.Text("Importação de ROMs."));
  var deeper=Path.Combine(nested,"3DS","Meus jogos");
  Directory.CreateDirectory(deeper);
  var cartridge=Path.Combine(deeper,"Aventura.3DS");var compressed=Path.Combine(deeper,"Puzzle.zcci");
  File.WriteAllText(cartridge,"fixture");File.WriteAllText(compressed,"fixture");
  File.WriteAllText(Path.Combine(deeper,"Instalador.cia"),"fixture");
  var azahar=RomLibrary.ImportProfile(new Emulator{Target=Path.Combine(fixture,"azahar-qt.exe"),Extensions="iso"});
  var detected3ds=RomLibrary.Find(folder,RomLibrary.Detect(azahar.Target).Extensions);
  if(azahar.Platform!="Nintendo 3DS"||!RomLibrary.Find(folder,azahar.Extensions).Contains(cartridge)||
     detected3ds.Length!=2||!detected3ds.Contains(compressed)||RomLibrary.Arguments(RomLibrary.NewGame(azahar,cartridge))!="\""+cartridge+"\"")
   throw new IOException(UiLanguage.Text("Azahar, formatos e subpastas."));
  var handheld=Path.Combine(deeper,"Jogo.gba");File.WriteAllText(handheld,"fixture");
  var ps=Path.Combine(nested,"Jogo de teste PS5");Directory.CreateDirectory(ps);
  var boot=Path.Combine(ps,"EBOOT.BIN");File.WriteAllText(boot,"fixture");
  File.WriteAllText(Path.Combine(ps,"dados.bin"),"fixture");
  var generic=RomLibrary.Detect(Path.Combine(fixture,"EmuladorNovo.exe"));
  var genericFiles=RomLibrary.Find(folder,generic.Extensions);
  if(!genericFiles.Contains(handheld)||!genericFiles.Contains(second)||!genericFiles.Contains(boot)||
     genericFiles.Any(file=>Path.GetFileName(file)=="dados.bin")||
     RomLibrary.NewGame(generic,boot).Name!="Jogo de teste PS5")
   throw new IOException(UiLanguage.Text("Busca automática para emulador sem configuração."));
  var game=RomLibrary.NewGame(configured,first);var other=RomLibrary.NewGame(configured,second);
  if(game.Name!="Corrida de teste"||game.Cover!=""||game.Rom!=first||game.Target!=configured.Target||game.Source!="Emulador")throw new IOException(UiLanguage.Text("Cadastro de ROM."));
  var moved=Path.Combine(nested,Path.GetFileName(first));File.Copy(first,moved,true);
  game.Rom=RomLibrary.ResolvePath(nested,game.Rom);
  if(game.Rom!=moved||other.Rom!=second||!RomLibrary.Arguments(game).EndsWith("\""+moved+"\""))throw new IOException(UiLanguage.Text("Caminho individual."));
  var port=new Game{Source="Port",PortData=moved,Args="--check-rom {game_path}"};
  if(!Hub.RequiresPlayButton(game)||!Hub.RequiresPlayButton(port)||RomLibrary.Arguments(port)!="--check-rom \""+moved+"\"")throw new IOException(UiLanguage.Text("Abertura de port."));
  port.PortData="";port.Args="";
  if(RomLibrary.Arguments(port)!="")throw new IOException(UiLanguage.Text("Port com jogo embutido."));
  var executable=System.Reflection.Assembly.GetExecutingAssembly().Location;
  game.Args="--check-rom \"{rom_path}\"";
  var info=new ProcessStartInfo(executable,RomLibrary.Arguments(game)){UseShellExecute=false,CreateNoWindow=true};
  using(var child=Process.Start(info)){
   if(!child.WaitForExit(6000)){child.Kill();throw new IOException(UiLanguage.Text("O emulador de teste não encerrou."));}
   if(child.ExitCode!=0||File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rom-child.txt"))!=moved)throw new IOException(UiLanguage.Text("O caminho não chegou ao emulador de teste."));
  }
  using(var dialog=new CustomGameDialog(game,false,Color.Black,Color.FromArgb(35,32,45),Color.MediumPurple)){
   dialog.CreateControl();
   if(dialog.GameName!=game.Name||dialog.Cover!="")throw new IOException(UiLanguage.Text("Editor de ROM."));
  }
 }
 static void TestInstallation(string fixture){
  var game=new Game{StoreId="steam:77",Name="Jogo remoto",Source="Steam",Missing=true};
  var recent=new HashSet<string>{game.Id};
  if(Hub.LibraryGroup(game,recent,false)!=3||Hub.InstallLink(game)!="steam://install/77")throw new IOException(UiLanguage.Text("Instalação Steam."));
  var library=new Library();library.Games.Add(game);
  library.Games.Add(new Game{Source="Epic Games",Name=game.Name,StoreId="epic:another-platform"});
  Stores.Merge(library,new List<Game>{new Game{StoreId="steam:77",Install=fixture,Source="Steam"}},"Epic: ignorada");
  if(game.Missing||Hub.LibraryGroup(game,recent,true)!=0||library.Games.Count!=2)throw new IOException(UiLanguage.Text("Promoção de instalação."));
  var epic=new Game{Source="Epic Games",StoreId="epic:sample",Target=Stores.EpicUri("sample")};
  if(!Hub.InstallLink(epic).EndsWith("?action=installer"))throw new IOException(UiLanguage.Text("Instalação Epic."));
 }
 static int SelfTest(){
  try{
   var library=new Library();
   var game=new Game{Name="Teste",Target="steam://run/42",StoreId="steam:42"};
   Stores.Merge(library,new List<Game>{game,game},"");
   if(library.Games.Count!=1)throw new Exception("Duplicação.");
   game.Name="Personalizado";
   Stores.Merge(library,new List<Game>(),"");
   if(!game.Missing||game.Name!="Personalizado")throw new Exception("Reconciliação.");
   Storage.Write(library);var loaded=Storage.Read(Storage.FileName);
   if(loaded.Games.Count!=1||loaded.Games[0].Name!="Personalizado"||loaded.Games[0].Last.HasValue)throw new Exception("Persistência.");
   Storage.Write(loaded);
   var zip=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backup.zip");using(var testLock=LockData()){Storage.Export(zip);if(Storage.Import(zip).Games.Count!=1)throw new Exception("Backup.");}
   var fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"epic-test");
   var dat=Path.Combine(fixture,"UnrealEngineLauncher","LauncherInstalled.dat");
   var install=Path.Combine(fixture,"GTA V");
   Directory.CreateDirectory(install);Directory.CreateDirectory(Path.GetDirectoryName(dat));
   File.WriteAllText(Path.Combine(install,"GTA5_Enhanced.exe"),"fixture");
   var json=new System.Web.Script.Serialization.JavaScriptSerializer();
   File.WriteAllText(dat,json.Serialize(new {InstallationList=new[]{new {InstallLocation=install,AppName="fixture"}}}));
   var imported=Stores.EpicFrom(fixture).Single();
   if(imported.Name!="Grand Theft Auto V Enhanced"||Artwork.AppId(imported)!="3240220"||!imported.Target.Contains("fixture"))throw new Exception("Fallback Epic.");
   var manual=new Game{Name="Meu GTA",Source="Epic Games",Install=install,Target=Stores.EpicUri(install),ManualInstall=true};
   loaded.Games.Add(manual);Stores.Merge(loaded,new List<Game>{imported},"");
   if(loaded.Games.Count!=2||manual.StoreId!="epic:fixture"||manual.Name!="Meu GTA"||manual.Missing)throw new Exception("Reconciliação Epic.");
   var assets=Artwork.ParseAssets("{\"response\":{\"store_items\":[{\"appid\":42,\"assets\":{\"library_capsule\":\"0123456789abcdef/library_600x900.jpg\"}}]}}");
   if(!Artwork.Url("42",assets["42"],"library_capsule").EndsWith("/42/0123456789abcdef/library_600x900.jpg"))throw new Exception("URL Steam.");
   GameProgress.Verify();
   var cover=Path.Combine(fixture,"cover.png");
   using(var sample=new Bitmap(200,300)){using(var art=Graphics.FromImage(sample))using(var brush=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,0,200,300),Color.DarkSlateBlue,Color.DeepSkyBlue,50f))art.FillRectangle(brush,0,0,200,300);sample.Save(cover,System.Drawing.Imaging.ImageFormat.Png);}
   const string usa="Legend of Zelda, The - Majora's Mask 3D (USA) (En,Fr,Es) (Rev 1)";
   const string europe="Legend of Zelda, The - Majora's Mask 3D (Europe) (En,Fr,De,Es,It)";
   var rom3ds=new Game{Name=usa,Rom=Path.Combine(fixture,usa+".3ds"),Platform="Nintendo 3DS"};
   var coverIndex=CustomArtwork.ParseIndex("<a href=\""+Uri.EscapeDataString(europe)+".png\">EU</a><a href=\""+
    Uri.EscapeDataString(usa)+".png\">US</a><a href=\"../other.png\">outro</a>");
   if(coverIndex.Length!=2||CustomArtwork.Rank(coverIndex,usa,"The Legend of Zelda: Majora's Mask 3D").FirstOrDefault()!=usa||
      !CustomArtwork.Urls(rom3ds).First().StartsWith("https://thumbnails.libretro.com/")||
      !CustomArtwork.VerifiedCovers(rom3ds).Any(url=>url.EndsWith("/AJRE.png")))
    throw new IOException(UiLanguage.Text("Catálogo de capas de Nintendo 3DS: ")+coverIndex.Length+" / "+String.Join("|",CustomArtwork.Rank(coverIndex,usa,rom3ds.Name))+" / "+String.Join("|",CustomArtwork.VerifiedCovers(rom3ds)));
   var fireRed=new Game{Name="Pokémon FireRed",Rom=Path.Combine(fixture,"1636 - Pokemon Fire Red (U)(Squirrels).gba")};
   if(!CustomArtwork.VerifiedCovers(fireRed).Any(url=>url.Contains("a60aefb6"))||
      CustomArtwork.SteamMatch(@"{""items"":[{""id"":42,""name"":""OBS Studio""},{""id"":43,""name"":""OBS Studio DLC""}]}", "OBS Studio")!="42"||
      !new Library().DiscordEnabled||new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Library>(@"{""DiscordEnabled"":false}").DiscordEnabled)
    throw new IOException(UiLanguage.Text("Capas de jogos avulsos e padrão Discord."));
   var tiff=Path.Combine(fixture,"imagem.tiff");
   using(var sample=new Bitmap(123,145))sample.Save(tiff,System.Drawing.Imaging.ImageFormat.Tiff);
   var importedTiff=Storage.Asset(tiff);
   using(var picture=Storage.Picture(importedTiff))
    if(!importedTiff.EndsWith(".png")||picture==null||picture.Height!=145)throw new IOException(UiLanguage.Text("Importação de capa TIFF."));
   var romArt=new Game{Name="Pokémon Emerald",Rom=Path.Combine(fixture,"cover.gba")};
   if(CustomArtwork.Find(romArt).Cover==""||!CustomArtwork.Urls(romArt).First().Contains("Game%20Boy%20Advance"))throw new IOException(UiLanguage.Text("Capa automática de ROM."));
   game=loaded.Games[0];game.Cover=Artwork.SaveFile(cover,true);game.Missing=false;
   if(game.Cover=="")throw new Exception("Capa.");
   using(var picture=Storage.Picture(game.Cover))if(picture==null||picture.Height!=300)throw new Exception("Cache visual.");
   string errors;Stores.Scan(out errors);
   Hub.TestXbox(fixture);
   Hub.TestNewIntegrations(fixture);
   TestInstallation(fixture);
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   if(!RiotGames.CoverUrls("riot:valorant").Any()||
      !Artwork.GogCoverUrls(@"{""images"":{""logo"":""//images.gog.com/test.png""}}").Contains("https://images.gog.com/test.png")||
      OwnedGames.GalaxyOwnedGame("origin_OFFER:42","EA").Source!="EA app"||
      !OwnedGames.GalaxyOwnedGame("battlenet_wow","World of Warcraft").Missing)
    throw new IOException(UiLanguage.Text("Capas e biblioteca de launchers."));
   VerifyLanguages();TestLegacyMigration(fixture);UpdateReport.Verify();Playtime.Verify();TestUninstalledHistory(fixture);TestRoms(fixture);WindowsShortcuts.Verify(fixture);Hub.TestDiscordPresence();FolderPicker.Verify(fixture);
   loaded.AutoScan=false;
   var largeLibrary=new Library{AutoScan=false};
   for(int index=1;index<=380;index++)largeLibrary.Games.Add(new Game{
    Name="Jogo "+index,Source="Steam",StoreId="steam:"+index,Target="steam://run/"+index,Missing=index>25
   });
   using(var largeForm=new Hub(largeLibrary))largeForm.VerifyPagination();
   Exception verification=null;
   using(var form=new Hub(loaded,true)){
    form.Shown+=(s,e)=>form.BeginInvoke(new Action(()=>{using(var screenshot=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(screenshot,new Rectangle(0,0,form.Width,form.Height));screenshot.Save(Path.Combine(Storage.Root,"window.png"));}try{form.VerifyVisuals();form.VerifyTray();}catch(Exception error){verification=error;}form.ExitFromTray();}));
    Application.Run(form);
   }
   if(verification!=null)throw new InvalidOperationException(UiLanguage.Text("Interface."),verification);
   File.WriteAllText(Path.Combine(Storage.Root,"self-test.txt"),"OK");
   return 0;
  }catch(Exception e){Directory.CreateDirectory(Storage.Root);File.WriteAllText(Path.Combine(Storage.Root,"self-test.txt"),e.ToString());return 1;}
 }
}






































