using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

public partial class Hub {
 bool xboxScanning;
 void SwitchHubPanel(){
  body.SuspendLayout();
  try{
   bool social=InSocial;
   libraryPage.Visible=!social;
   list.Visible=!social&&shown!=0;empty.Visible=!social&&shown==0;
   socialHost.Visible=social;
   if(social)OpenSocial();
  }finally{body.ResumeLayout();}
 }
 void Notify(string text){message.Text=text;}
 async void ScanXbox(bool explicitScan){
  if(xboxScanning||scanning)return;
  xboxScanning=true;
  if(explicitScan)message.Text=UiLanguage.Text("Verificando jogos instalados pelo Windows…");
  try{
   var snapshot=await Task.Run(()=>XboxGames.Scan());
   if(IsDisposed)return;
   var count=XboxGames.Merge(data,snapshot);
   if(platformTimes!=null)platformTimes.Apply(data.Games);
   Save();RefreshList();
   if(count>0)Notify(count+" jogos do Xbox / Microsoft Store adicionados.");
   else if(explicitScan)message.Text=snapshot.Games.Length+" jogos reconhecidos pelo Windows. Nenhuma nova entrada.";
   LoadArt(false);
  }catch(Exception error){
   if(!IsDisposed){Log(error.ToString());if(explicitScan)Notify("Não foi possível atualizar os jogos do Xbox / Microsoft Store.");}
  }finally{xboxScanning=false;}
 }
 async void PickXbox(){
  message.Text=UiLanguage.Text("Lendo os atalhos do menu Iniciar…");
  try{
   var choices=await Task.Run(()=>XboxGames.Read());
   if(IsDisposed)return;
   using(var picker=new Form{Text=UiLanguage.Text("Escolher jogo instalado"),StartPosition=FormStartPosition.CenterParent,Size=new Size(580,470),BackColor=BackColor,ForeColor=ForeColor,Font=Font,Padding=new Padding(14)}){
    var searchBox=new TextBox{Dock=DockStyle.Top,AccessibleName=UiLanguage.Text("Buscar no menu Iniciar")};
    var options=new ListBox{Dock=DockStyle.Fill,BackColor=surface,ForeColor=ForeColor,DisplayMember="Key",IntegralHeight=false};
    var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48};
    var label=new Label{Text=UiLanguage.Text("Selecione o título que você quer cadastrar como jogo."),Dock=DockStyle.Top,Height=35};
    var add=Button("Adicionar como jogo");
    add.Click+=(s,e)=>{if(options.SelectedIndex>=0)picker.DialogResult=DialogResult.OK;};
    options.DoubleClick+=(s,e)=>{if(options.SelectedIndex>=0)picker.DialogResult=DialogResult.OK;};
    footer.Controls.Add(add);picker.AcceptButton=add;
    Action fill=()=>{
     var query=searchBox.Text.Trim();
     options.DataSource=choices.Where(x=>x.Name.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0).OrderBy(x=>x.Name).Select(x=>new KeyValuePair<string,XboxGames.Entry>(x.Name,x)).ToArray();
    };
    searchBox.TextChanged+=(s,e)=>fill();fill();
    picker.Controls.Add(options);picker.Controls.Add(footer);picker.Controls.Add(searchBox);picker.Controls.Add(label);
    if(picker.ShowDialog(this)!=DialogResult.OK)return;
    var entry=((KeyValuePair<string,XboxGames.Entry>)options.SelectedItem).Value;var game=XboxGames.Make(entry,true);
    var existing=data.Games.FirstOrDefault(x=>x.StoreId.Equals(game.StoreId,StringComparison.OrdinalIgnoreCase)||x.Target.Equals(game.Target,StringComparison.OrdinalIgnoreCase));
    if(existing==null)data.Games.Add(game);
    else{existing.Source=XboxGames.Source;existing.StoreId=game.StoreId;existing.Install=game.Install;existing.Missing=false;game=existing;}
    selected=game;Save();RefreshList();Notify(game.Name+" foi adicionado à seção Xbox / Microsoft Store.");LoadArt(false);
   }
  }catch(Exception error){
   if(!IsDisposed){Log(error.ToString());Notify("Não foi possível ler os atalhos de jogos instalados no Windows.");}
  }
 }
 internal static void TestXbox(string fixture){
  var file=Path.Combine(fixture,"MicrosoftGame.config");
  File.WriteAllText(file,"<Game><Identity Name=\"Publisher.TestGame\" /></Game>");
  var identities=new HashSet<string>(StringComparer.OrdinalIgnoreCase);XboxGames.ReadIdentity(file,identities);
  var real=new XboxGames.Entry{Name="Teste Game Pass",Package="Publisher.TestGame",Aumid="Publisher.TestGame_abcd!Game"};
  var tool=new XboxGames.Entry{Name="Aplicativo comum",Package="Publisher.Tool",Aumid="Publisher.Tool_abcd!App"};
  var client=new XboxGames.Entry{Name="Xbox",Package="Microsoft.GamingApp",Aumid="Microsoft.GamingApp_abcd!App",Game=true};
  if(!XboxGames.IsGame(real,identities)||XboxGames.IsGame(tool,identities)||XboxGames.IsGame(client,identities))throw new IOException(UiLanguage.Text("Filtro Xbox."));
  var lib=new Library();var game=XboxGames.Make(real,false);
  var state=new XboxGames.Snapshot{Apps=new[]{real,tool},Games=new[]{game}};
  if(!XboxGames.IsTarget(game.Target)||XboxGames.Merge(lib,state)!=1||XboxGames.Merge(lib,state)!=0)throw new IOException(UiLanguage.Text("Importação Xbox."));
  XboxGames.Merge(lib,new XboxGames.Snapshot{Apps=new[]{tool},Games=new Game[0]});
  if(!game.Missing)throw new IOException(UiLanguage.Text("Reconciliação Xbox."));
  var report=Path.Combine(Storage.Root,"xbox-scan.txt");
  try{var actual=XboxGames.Scan();File.WriteAllText(report,actual.Apps.Length+" atalhos, "+actual.Games.Length+" jogos reconhecidos.");}
  catch(Exception error){File.WriteAllText(report,error.ToString());}
 }
 internal static void TestNewIntegrations(string fixture){
  var epicRoot=Path.Combine(fixture,"Epic Games");
  var title=Path.Combine(epicRoot,"Example Epic Game");
  Directory.CreateDirectory(Path.Combine(title,".egstore"));
  Directory.CreateDirectory(Path.Combine(epicRoot,"Empty folder"));
  var manifestRoot=Path.Combine(fixture,"epic-with-fallback");
  var folderGame=Stores.EpicWithFolders(manifestRoot,new[]{epicRoot}).Single();
  if(folderGame.Name!="Example Epic Game"||!folderGame.Target.Contains("Epic%20Games"))throw new IOException(UiLanguage.Text("Detecção Epic por pasta."));
  var dat=Path.Combine(manifestRoot,"UnrealEngineLauncher","LauncherInstalled.dat");Directory.CreateDirectory(Path.GetDirectoryName(dat));
  File.WriteAllText(dat,"{bad json");
  if(Stores.EpicWithFolders(manifestRoot,new[]{epicRoot}).Count()!=1)throw new IOException(UiLanguage.Text("Fallback Epic com manifesto inválido."));
  var json=new System.Web.Script.Serialization.JavaScriptSerializer();
  File.WriteAllText(dat,json.Serialize(new {InstallationList=new[]{new {InstallLocation=title,AppName="sample-artifact"}}}));
  var registered=Stores.EpicWithFolders(manifestRoot,new[]{epicRoot}).Single();
  var library=new Library();library.Games.Add(folderGame);Stores.Merge(library,new List<Game>{registered},"");
  if(library.Games.Count!=1||folderGame.StoreId!="epic:sample-artifact"||folderGame.Missing||!folderGame.Target.Contains("sample-artifact"))throw new IOException(UiLanguage.Text("Deduplicação Epic."));
  var riotRoot=Path.Combine(fixture,"Riot Games");var account=Path.Combine(fixture,"RiotData");
  var client=Path.Combine(riotRoot,"Riot Client","RiotClientServices.exe");Directory.CreateDirectory(Path.GetDirectoryName(client));File.WriteAllText(client,"fixture");
  var lol=Path.Combine(fixture,"OtherDisk","League of Legends");Directory.CreateDirectory(lol);File.WriteAllText(Path.Combine(lol,"LeagueClient.exe"),"fixture");
  Directory.CreateDirectory(account);
  var associates=new Dictionary<string,string>{{lol,client}};
  File.WriteAllText(Path.Combine(account,"RiotClientInstalls.json"),json.Serialize(new {rc_default=client,associated_client=associates}));
  var riot=RiotGames.From(account,new[]{riotRoot}).Single();
  if(riot.Name!="League of Legends"||riot.Target!=client||riot.Args!="--launch-product=league_of_legends --launch-patchline=live")throw new IOException(UiLanguage.Text("Detecção Riot."));
  var app=new Game{Name="Hollow Knight",Target=XboxGames.Target("Sample.Game_abc!Game"),Source=XboxGames.Source,Install=Path.Combine(fixture,"test-store-art")};
  Directory.CreateDirectory(app.Install);
  using(var logo=new Bitmap(150,150))logo.Save(Path.Combine(app.Install,"tile.scale-200.png"),System.Drawing.Imaging.ImageFormat.Png);
  using(var cover=new Bitmap(180,260))cover.Save(Path.Combine(app.Install,"portrait.png"),System.Drawing.Imaging.ImageFormat.Png);
  var manifest=Path.Combine(app.Install,"AppxManifest.xml");
  File.WriteAllText(manifest,"<Package><Applications><Application Id=\"Game\"><VisualElements Square150x150Logo=\"tile.png\" PortraitLogo=\"portrait.png\" /></Application></Applications></Package>");
  var artwork=InstalledArt.Find(app);
  if(artwork.Cover==""||artwork.Icon==""||Artwork.AppId(app)!="367520")throw new IOException(UiLanguage.Text("Artes Microsoft."));
  try{
   var windows=XboxGames.Scan();
   var real=windows.Games.Select(game=>InstalledArt.Find(game)).Count(art=>art.Icon!=""||art.Cover!="");
   File.AppendAllText(Path.Combine(Storage.Root,"xbox-scan.txt")," "+real+" exibem arte local.");
  }catch(Exception error){File.AppendAllText(Path.Combine(Storage.Root,"xbox-scan.txt")," "+error.Message);}
 }
}




