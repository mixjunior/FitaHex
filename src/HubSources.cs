using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public partial class Hub {
 const string EmulatorFilter="emulator:";
 const string WithoutLauncher="Sem launcher";
 readonly HashSet<string> chosenSources=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> chosenCollections=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 readonly HashSet<string> chosenStates=new HashSet<string>();
 string sourceFilter{get{return chosenSources.Count==1?chosenSources.First():"";}set{SetOnly(chosenSources,value);}}
 string collectionFilter{get{return chosenCollections.Count==1?chosenCollections.First():"";}set{SetOnly(chosenCollections,value);}}
 static void SetOnly(HashSet<string> set,string value){set.Clear();if(value!="")set.Add(value);}
 void RemoveCollectionFilter(string value){chosenCollections.Remove(value);UpdateFilterLabel();}
 static readonly string[] Launchers={"Steam","Epic Games",XboxGames.Source,RiotGames.Source,"GOG","EA app","Ubisoft Connect","Battle.net"};
 readonly Dictionary<string,Image> sourceIcons=new Dictionary<string,Image>(StringComparer.OrdinalIgnoreCase);
 string ActiveSource{get{return sourceFilter==WithoutLauncher?"":sourceFilter;}}
 static string Origin(Game game){
  if(game.Rom!="")return "Emulador";
  var source=(game.Source??"").Trim();
  if(source=="")return "Manual";
  return Launchers.FirstOrDefault(value=>value.Equals(source,StringComparison.CurrentCultureIgnoreCase))??source;
 }
 static bool MatchesLauncher(Game game,string source){
  if(source.StartsWith(EmulatorFilter,StringComparison.Ordinal))return game.Rom!=""&&String.Equals(game.Target,source.Substring(EmulatorFilter.Length),StringComparison.OrdinalIgnoreCase);
  var origin=Origin(game);
  if(source==WithoutLauncher)return !Launchers.Contains(origin,StringComparer.CurrentCultureIgnoreCase);
  return String.Equals(origin,source,StringComparison.CurrentCultureIgnoreCase);
 }
 void SetupFilters(){
  sourceButton=Action(toolbar,"Filtros      ",ShowFilters);
  sourceButton.Glyph="filter";sourceButton.Width=97;sourceButton.AutoSize=false;
  sourceButton.AccessibleName="Filtros de pesquisa";
 }
 IEnumerable<KeyValuePair<string,string>> LauncherFilters(){
  foreach(var source in Launchers.Concat(new[]{"Emulador",WithoutLauncher}))
   yield return new KeyValuePair<string,string>(source,source=="Emulador"?"Emuladores":source);
  var extras=data.Games.Select(Origin).Distinct(StringComparer.OrdinalIgnoreCase).Where(x=>!Launchers.Contains(x,StringComparer.OrdinalIgnoreCase)&&x!="Manual"&&x!="Port"&&x!="Emulador");
  foreach(var source in extras)yield return new KeyValuePair<string,string>(source,source);
  var emulators=data.Emulators.Where(x=>x.Target!="").Select(x=>new {x.Target,x.Name})
   .Concat(data.Games.Where(x=>x.Rom!=""&&x.Target!="").Select(x=>new {x.Target,Name=RomLibrary.Detect(x.Target).Name}))
   .GroupBy(x=>x.Target,StringComparer.OrdinalIgnoreCase).Select(x=>x.First()).OrderBy(x=>x.Name);
  foreach(var emulator in emulators)yield return new KeyValuePair<string,string>(EmulatorFilter+emulator.Target,emulator.Name);
 }
 void ShowFilters(){
  var dropdown=new ToolStripDropDown{Padding=new Padding(1),BackColor=surface};
  int width=Math.Min(630,Math.Max(475,ClientSize.Width-56));
  var panel=new SoftPanel{Size=new Size(width,365),BackColor=surface,ForeColor=ForeColor,Padding=new Padding(12),Font=Font};
  var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};
  columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,37));
  columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
  columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));
  var resets=new List<Action>();
  AddFilterColumn(columns,0,"Plataformas",LauncherFilters(),chosenSources,resets);
  AddFilterColumn(columns,1,"Estado",new[]{"Instalados","Não instalados"}.Select(x=>new KeyValuePair<string,string>(x,x)),chosenStates,resets);
  AddFilterColumn(columns,2,"Coleções",data.Shelves.OrderBy(x=>x.Name).Select(x=>new KeyValuePair<string,string>(x.Name,x.Name)),chosenCollections,resets);
  var footer=new SoftFlow{Dock=DockStyle.Bottom,Height=42};
  Action(footer,"Limpar filtros",()=>{
   chosenSources.Clear();chosenCollections.Clear();chosenStates.Clear();
   foreach(var reset in resets)reset();
   RefreshFilters();
  });
  Action(footer,"Concluir",()=>dropdown.Close());
  panel.Controls.Add(columns);panel.Controls.Add(footer);
  dropdown.Items.Add(new ToolStripControlHost(panel){AutoSize=false,Size=panel.Size,Padding=Padding.Empty,Margin=Padding.Empty});
  dropdown.Closed+=(s,e)=>BeginInvoke(new Action(dropdown.Dispose));
  dropdown.Show(sourceButton,new Point(0,sourceButton.Height+6));
 }
 void AddFilterColumn(TableLayoutPanel columns,int column,string name,IEnumerable<KeyValuePair<string,string>> options,HashSet<string> values,List<Action> resets){
  var box=new SoftPanel{Dock=DockStyle.Fill,Padding=new Padding(4)};
  var title=new Label{Text=name,Dock=DockStyle.Top,Height=31,Font=Skin.HeadingFont(11)};
  var choices=new SoftFlow{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
  foreach(var item in options){
   var option=item;HubButton checkbox=null;
   checkbox=Action(choices,option.Value,()=>{
    if(!values.Remove(option.Key))values.Add(option.Key);
    checkbox.Active=values.Contains(option.Key);checkbox.Glyph=checkbox.Active?"check":"box";checkbox.Invalidate();
    RefreshFilters();
   });
   checkbox.Sidebar=true;checkbox.AutoSize=false;checkbox.Margin=new Padding(0,1,0,1);checkbox.Height=30;checkbox.Width=137;
   choices.Resize+=(s,e)=>checkbox.Width=Math.Max(100,choices.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-3);
   checkbox.Active=values.Contains(option.Key);checkbox.Glyph=checkbox.Active?"check":"box";checkbox.AccessibleName=option.Value;
   resets.Add(()=>{checkbox.Active=false;checkbox.Glyph="box";checkbox.Invalidate();});
  }
  box.Controls.Add(choices);box.Controls.Add(title);columns.Controls.Add(box,column,0);
 }
 void RefreshFilters(){page=150;UpdateFilterLabel();RefreshList();}
 sealed class FilterColors : ProfessionalColorTable {
  readonly Color fill, highlight;
  internal FilterColors(Color background,Color accent){UseSystemColors=false;fill=background;highlight=Skin.Mix(background,accent,.33f);}
  public override Color ImageMarginGradientBegin{get{return fill;}}
  public override Color ImageMarginGradientMiddle{get{return fill;}}
  public override Color ImageMarginGradientEnd{get{return fill;}}
  public override Color ToolStripDropDownBackground{get{return fill;}}
  public override Color MenuItemSelected{get{return highlight;}}
  public override Color MenuItemBorder{get{return highlight;}}
  public override Color MenuBorder{get{return highlight;}}
  public override Color CheckBackground{get{return highlight;}}
 }
 void UpdateFilterLabel(){
  int count=chosenSources.Count+chosenCollections.Count+chosenStates.Count;
  sourceButton.Text=count==0?"Filtros":"Filtros · "+count;
  Clear(filterChips);
  if(count>0){
   var names=LauncherFilters().ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.OrdinalIgnoreCase);
   foreach(var source in chosenSources.ToArray()){
    var value=source;string label;
    AddFilterChip(chosenSources,value,names.TryGetValue(value,out label)?label:value);
   }
   foreach(var collection in chosenCollections.ToArray())AddFilterChip(chosenCollections,collection,collection);
   foreach(var state in chosenStates.ToArray())AddFilterChip(chosenStates,state,state);
  }
  filterChips.Visible=count>0;
 }
 void AddFilterChip(HashSet<string> values,string value,string label){
  var chip=Action(filterChips,label+"  ×",()=>{values.Remove(value);RefreshFilters();});
  chip.AccessibleName="Remover filtro "+label;
 }
 static bool BelongsTo(Game g,Shelf shelf){
  if(!shelf.Smart)return shelf.Ids.Contains(g.Id);
  return Match(g,shelf.Query)&&(!shelf.Installed||Installed(g))&&(!shelf.Favorites||g.Favorite)&&(!shelf.Unplayed||!g.Last.HasValue)
   &&(shelf.Source==""||shelf.Source.Equals(g.Source,StringComparison.OrdinalIgnoreCase))&&(shelf.Platform==""||shelf.Platform.Equals(g.Platform,StringComparison.OrdinalIgnoreCase));
 }
 void SelectCollection(string name){collectionFilter=name;page=150;UpdateFilterLabel();RefreshList();}
 void SelectSource(string source){
  sourceFilter=source;page=150;UpdateFilterLabel();RefreshList();
 }
 Image CachedSourceIcon(string key,Func<Image> read){
  Image image;
  if(sourceIcons.TryGetValue(key,out image))return image;
  try{image=read();}catch(IOException){image=null;}catch(UnauthorizedAccessException){image=null;}catch(System.Security.SecurityException){image=null;}catch(System.Runtime.InteropServices.ExternalException){image=null;}
  sourceIcons[key]=image;return image;
 }
 Image SourceImage(Game game,string source){
  if(game.Badge!="")return CachedSourceIcon("custom:"+game.Badge,()=>Storage.Picture(game.Badge));
  if(source=="Steam"||source==RiotGames.Source)return CachedSourceIcon("brand:"+source,()=>LauncherMarks.Create(source));
  var brandMark=CachedSourceIcon("embedded:"+source,()=>LauncherMarks.Embedded(source));
  if(brandMark!=null)return brandMark;
  if(source==XboxGames.Source){
   var xbox=CachedSourceIcon("launcher:xbox",()=>InstalledArt.ShellImage(XboxGames.Target("Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App")));
   if(xbox!=null)return xbox;
   if(game.Icon!="")return CachedSourceIcon("game:"+game.Icon,()=>Storage.Picture(game.Icon));
   return null;
  }
  if(source!="Manual"&&source!="Port"&&source!="Emulador"){
   var key=source==RiotGames.Source?source+":"+game.Target:source;
   var launcher=CachedSourceIcon("launcher:"+key,()=>SourceIcons.FromFile(SourceIcons.Launcher(source,game)));
   if(launcher!=null)return launcher;
  }
  if(game.Rom==""&&game.Icon!="")return CachedSourceIcon("game:"+game.Icon,()=>Storage.Picture(game.Icon));
  return game.Target==""?null:CachedSourceIcon("target:"+game.Target,()=>SourceIcons.FromFile(game.Target));
 }
 void DrawOrigin(Graphics g,string source,Rectangle badge,Game game){
  var image=SourceImage(game,source);
  if(image==null||image.Width<1||image.Height<1)return;
  var state=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var outline=Skin.Round(badge,5))using(var shade=new SolidBrush(Color.FromArgb(185,12,13,21)))g.FillPath(shade,outline);
  g.InterpolationMode=InterpolationMode.HighQualityBicubic;
  var scale=Math.Min((badge.Width-4f)/image.Width,(badge.Height-4f)/image.Height);
  int width=Math.Max(1,(int)(image.Width*scale)),height=Math.Max(1,(int)(image.Height*scale));
  g.DrawImage(image,new Rectangle(badge.X+(badge.Width-width)/2,badge.Y+(badge.Height-height)/2,width,height));
  g.Restore(state);
 }
 internal void TestSources(){
  if(navigation.ContainsKey("Steam")||navigation.ContainsKey("Epic Games"))throw new InvalidOperationException("Origens continuam no menu lateral.");
  var sample="{\"title\":\"Example Game\",\"keyImages\":[{\"type\":\"DieselGameBoxTall\",\"url\":\"https://cdn2.epicgames.com/example.jpg\"},{\"type\":\"DieselGameBoxTall\",\"url\":\"https://evil.example/example.jpg\"}]}";
  if(EpicArtwork.Portraits(sample,"Example Game").Count()!=1||EpicArtwork.Portraits(sample,"Outro jogo").Any())throw new InvalidOperationException("Arte Epic.");
  if(SourceIcons.CommandFile("\"C:\\Example Files\\Launcher.exe\" \"%1\"")!=@"C:\Example Files\Launcher.exe")throw new InvalidOperationException("Ícone do launcher.");
  using(var icon=SourceIcons.FromFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe")))if(icon==null)throw new InvalidOperationException("Ícone de executável.");
  using(var steam=LauncherMarks.Create("Steam"))using(var riot=LauncherMarks.Create(RiotGames.Source)){
   if(steam.GetPixel(24,5).A==0||riot.GetPixel(24,19).A==0)throw new InvalidOperationException("Marcas Steam e Riot.");
  }
  var legacy=new Library();
  var old=new Game{Name="Jogo antigo",Category="RPG, Portáteis"};legacy.Games.Add(old);
  if(!legacy.MigrateCategories()||legacy.MigrateCategories()||legacy.Shelves.Count!=2||legacy.Shelves.Any(s=>!s.Ids.Contains(old.Id)))throw new InvalidOperationException("Migração de categorias.");
  if(CanRemove(new Game{StoreId="steam:123"})||!CanRemove(new Game{StoreId="rom:saved",Rom="game.gba"})||!CanRemove(new Game()))throw new InvalidOperationException("Proteção para jogos automáticos.");
  if(NexusTheme.All.Length<8||NexusTheme.Find("Light").Background==NexusTheme.Find("NEXUS Dark").Background)throw new InvalidOperationException("Temas.");

  var previous=data.Games.ToList();
  try{
   data.Games.Add(new Game{Name="Filtro Epic",Source="Epic Games"});
   data.Games.Add(new Game{Name="Port",Source="Port"});
   Navigate("Jogos");SelectSource("Epic Games");
   if(!sourceButton.Visible||list.Items.Count==0||list.Items.Cast<ListViewItem>().Any(item=>Origin((Game)item.Tag)!="Epic Games")||!list.Items.Cast<ListViewItem>().Any(item=>item.Text=="Filtro Epic"))throw new InvalidOperationException("Filtro de origem.");
   var rom=new Game{Name="ROM de filtro",Rom=@"C:\ROMs\x.n64",Target=@"C:\emu\Project64.exe",Source="Emulador"};data.Games.Add(rom);
   SelectSource(EmulatorFilter+rom.Target);
   if(navigation.ContainsKey("Emuladores")||list.Items.Count!=1||list.Items[0].Tag!=rom)throw new InvalidOperationException("Filtro de emulador.");
   SelectSource(WithoutLauncher);
   if(list.Items.Cast<ListViewItem>().Any(item=>Launchers.Contains(Origin((Game)item.Tag)))||!list.Items.Cast<ListViewItem>().Any(item=>item.Text=="Port"))throw new InvalidOperationException("Filtro de port.");
   SelectSource("");
   chosenSources.UnionWith(new[]{"Steam","Epic Games"});
   chosenStates.Add("Instalados");RefreshFilters();
   if(list.Items.Count==0||list.Items.Cast<ListViewItem>().Any(row=>!Installed((Game)row.Tag)||!chosenSources.Contains(Origin((Game)row.Tag))))
    throw new InvalidOperationException("Filtros combinados.");
   chosenSources.Clear();chosenStates.Clear();RefreshFilters();
   var newest=new Game{Name="Mais recente",Last=DateTime.Now.AddDays(1)};
   data.Games.Add(newest);
   var fixtureShelf=new Shelf{Name="Coleção de teste"};fixtureShelf.Ids.Add(newest.Id);data.Shelves.Add(fixtureShelf);
   try{
    SelectCollection(fixtureShelf.Name);
    if(list.Items.Count!=1||list.Items[0].Tag!=newest||!recentGame.Text.Contains(newest.Name))throw new InvalidOperationException("Coleções e jogo recente.");
   }finally{data.Shelves.Remove(fixtureShelf);SelectCollection("");}
  }finally{data.Games.Clear();data.Games.AddRange(previous);RefreshList();}
 }
}










