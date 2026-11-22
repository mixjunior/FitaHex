using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

sealed class SocialPlatform {
 internal readonly string Name, ProfileFolder, Hint, EntryUrl;
 internal readonly Action OpenClient;
 readonly HashSet<string> hosts, alertHosts;
 internal SocialPlatform(string name,string folder,string hint,string entryUrl,string[] trusted,string[] notifications,Action openClient){
  Name=name;ProfileFolder=folder;Hint=hint;EntryUrl=entryUrl;OpenClient=openClient;
  hosts=new HashSet<string>(trusted,StringComparer.OrdinalIgnoreCase);
  alertHosts=new HashSet<string>(notifications,StringComparer.OrdinalIgnoreCase);
 }
 internal bool Allows(Uri url){
  return url.Scheme==Uri.UriSchemeHttps&&url.IsDefaultPort&&url.UserInfo==""&&hosts.Contains(url.IdnHost);
 }
 internal bool AllowsAlert(Uri url){return Allows(url)&&alertHosts.Contains(url.IdnHost);}
}
static class SocialPlatforms {
 internal static SocialPlatform[] Create(Action openRiot){
  const string epicProfile="https://www.epicgames.com/account/personal";
  const string xboxProfile="https://play.xbox.com/pt-BR/play/user";
  return new[]{
   new SocialPlatform("Steam","Steam","", "https://steamcommunity.com/my/",
    new[]{"steamcommunity.com","store.steampowered.com","login.steampowered.com","help.steampowered.com"},new[]{"steamcommunity.com"},()=>Open("steam://open/main")),
   new SocialPlatform("Epic Games","Epic","",
    "https://www.epicgames.com/id/login?redirectUrl="+Uri.EscapeDataString(epicProfile),
    new[]{"store.epicgames.com","www.epicgames.com","epicgames.com","accounts.epicgames.com","account.epicgames.com"},new[]{"store.epicgames.com"},
    ()=>Open("com.epicgames.launcher://store/library")),
   new SocialPlatform("Xbox","Xbox","",
    "https://www.xbox.com/pt-BR/auth/msa?action=logIn&returnUrl="+Uri.EscapeDataString(xboxProfile),
    new[]{"www.xbox.com","xbox.com","play.xbox.com","account.xbox.com","login.live.com","account.live.com","account.microsoft.com","login.microsoftonline.com","sisu.xboxlive.com","www.microsoft.com"},new[]{"www.xbox.com","play.xbox.com"},
    ()=>XboxGames.Launch(XboxGames.Target("Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App"),"")),
   new SocialPlatform("Riot Games","Riot","","https://account.riotgames.com/",
    new[]{"account.riotgames.com","auth.riotgames.com","authenticate.riotgames.com","www.riotgames.com","support.riotgames.com"},new string[0],openRiot),
   Account("GOG","GOG","https://www.gog.com/u",
    new[]{"www.gog.com","gog.com","login.gog.com","auth.gog.com"},@"GOG Galaxy\GalaxyClient.exe"),
   Account("EA app","EA","https://myaccount.ea.com/cp-ui/aboutme/index",
    new[]{"myaccount.ea.com","accounts.ea.com","signin.ea.com","www.ea.com","help.ea.com"},@"Electronic Arts\EA Desktop\EA Desktop\EADesktop.exe"),
   Account("Ubisoft Connect","Ubisoft","https://account.ubisoft.com/",
    new[]{"account.ubisoft.com","account-uplay.ubi.com","connect.ubisoft.com","www.ubisoft.com"},@"Ubisoft\Ubisoft Game Launcher\UbisoftConnect.exe"),
   Account("Battle.net","BattleNet","https://account.battle.net/overview",
    new[]{"account.battle.net","battle.net","us.battle.net","eu.battle.net","account.blizzard.com","oauth.battle.net"},@"Battle.net\Battle.net Launcher.exe")
  };
 }
 static SocialPlatform Account(string name,string folder,string address,string[] trusted,string relativePath){
  return new SocialPlatform(name,folder,name=="GOG"?"Entre na sua conta e selecione Seu perfil no menu do GOG.":"",address,trusted,new string[0],()=>{
   string executable="";
   try{executable=SourceIcons.Launcher(name,new Game());}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
   if(!File.Exists(executable)){
    executable=new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86}
     .Select(root=>Path.Combine(Environment.GetFolderPath(root),relativePath)).FirstOrDefault(File.Exists);
   }
   if(executable==null)throw new FileNotFoundException(name);
   using(var process=Process.Start(new ProcessStartInfo(executable){WorkingDirectory=Path.GetDirectoryName(executable),UseShellExecute=true})){}
  });
 }
 internal static void Open(string address){
  using(var process=Process.Start(new ProcessStartInfo(address){UseShellExecute=true})){}
 }
}
sealed class PlatformPane : UserControl {
 readonly SocialPlatform platform;
 readonly Panel viewport=new SoftPanel();
 readonly Label status=new Label();
 readonly FlowLayoutPanel bar=new SoftFlow();

 readonly bool testing, offlineTest;
 WebView2 browser;

 bool starting;
 internal Task Initialization{get;private set;}
 internal bool Ready{get{return browser!=null&&browser.CoreWebView2!=null;}}
 internal event Action<bool> PageLoaded;
 internal PlatformPane(SocialPlatform service,Color surface,Color foreground,Color accent,bool test=false,bool offline=false){
  testing=test;offlineTest=offline;platform=service;
  Dock=DockStyle.Fill;ForeColor=foreground;
  bar.Dock=DockStyle.Top;bar.Height=44;bar.WrapContents=false;
  Add("Abrir launcher",()=>Run(platform.OpenClient,"Não foi possível abrir o launcher. Verifique se está instalado."),surface,accent);
  status.Dock=DockStyle.Top;status.Height=31;status.AutoEllipsis=true;status.Padding=new Padding(3,4,3,0);
  viewport.Dock=DockStyle.Fill;viewport.BackColor=surface;
  Controls.Add(viewport);Controls.Add(status);Controls.Add(bar);
 }
 HubButton Add(string text,Action action,Color surface,Color accent){
  var button=(HubButton)Hub.Button(text);button.BackColor=surface;button.ForeColor=ForeColor;button.Accent=accent;
  button.Click+=(s,e)=>action();bar.Controls.Add(button);return button;
 }
 void Run(Action action,string failure){
  try{action();}catch(Exception){Report(failure);}
 }
 void Report(string text){status.Text=UiLanguage.Text(text);status.Visible=text!="";}
 internal void Go(){
  if(Ready){Navigate(platform.EntryUrl);return;}
  if(!starting)Initialization=Start();
 }
 async Task Start(){
  starting=true;Report("Carregando "+platform.Name+"…");
  try{
   var profile=Path.Combine(Storage.Root,"Social",platform.ProfileFolder,"WebView");
   Directory.CreateDirectory(profile);
   var environment=await CoreWebView2Environment.CreateAsync(null,profile);
   if(IsDisposed)return;
   browser=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=viewport.BackColor};
   viewport.Controls.Add(browser);
   await browser.EnsureCoreWebView2Async(environment);
   if(IsDisposed)return;
   var core=browser.CoreWebView2;
   core.Settings.AreDevToolsEnabled=false;
   core.Settings.AreHostObjectsAllowed=false;
   core.Settings.IsWebMessageEnabled=false;
   core.Settings.IsPasswordAutosaveEnabled=false;
   core.Settings.IsGeneralAutofillEnabled=false;
   core.NavigationStarting+=(s,e)=>{
    Uri uri;
    if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)||(!platform.Allows(uri)&&!(offlineTest&&e.Uri=="about:blank"))){
     e.Cancel=true;Report("Link externo ao painel. Para continuar, use o launcher.");
    }
   };
   core.NewWindowRequested+=(s,e)=>{
    e.Handled=true;
    Uri uri;
    if(Uri.TryCreate(e.Uri,UriKind.Absolute,out uri)&&platform.Allows(uri))Navigate(uri.AbsoluteUri);
    else Report("Link externo ao painel. Para continuar, use o launcher.");
   };
   core.NavigationCompleted+=(s,e)=>{
    if(e.WebErrorStatus==CoreWebView2WebErrorStatus.OperationCanceled&&!e.IsSuccess)return;
    Report(e.IsSuccess?platform.Hint:"Não foi possível carregar "+platform.Name+". Reabra a aba Social ou use o launcher.");
    if(testing)File.AppendAllText(Path.Combine(Storage.Root,"webview-navigation.txt"),platform.Name+" "+(e.IsSuccess?"OK":e.WebErrorStatus.ToString())+Environment.NewLine);
    var loaded=PageLoaded;if(loaded!=null)loaded(e.IsSuccess);
   };
   Navigate(platform.EntryUrl);
  }catch(Exception error){
   if(IsDisposed)return;
   if(browser!=null){browser.Dispose();browser=null;}
   Report("O painel "+platform.Name+" não abriu. Use Abrir launcher.");
   if(testing)File.AppendAllText(Path.Combine(Storage.Root,"webview-error.txt"),platform.Name+" "+error.ToString()+Environment.NewLine);
  }finally{starting=false;}
 }
 void Navigate(string address){
  try{browser.CoreWebView2.Navigate(offlineTest?"about:blank":address);}
  catch(Exception){Report("Não foi possível abrir esta página. Use Abrir launcher.");}
 }
}

partial class Hub {
 Panel socialHost;
 Panel socialOverview;
 readonly Dictionary<int,PlatformPane> profiles=new Dictionary<int,PlatformPane>();
 SocialPlatform[] socialPlatforms;
 bool InSocial{get{return section=="Social"||section.StartsWith("social:",StringComparison.Ordinal);}}
 void SetupSocial(){
  socialPlatforms=SocialPlatforms.Create(OpenRiotSocial);
  socialHost=new SoftPanel{Dock=DockStyle.Fill,Visible=false};body.Controls.Add(socialHost);
 }
 void OpenRiotSocial(){
  var target=data.Games.Where(g=>g.Source==RiotGames.Source).Select(g=>g.Target).Concat(RiotGames.ClientLocations())
   .FirstOrDefault(path=>!String.IsNullOrWhiteSpace(path)&&File.Exists(path)&&Path.GetFileName(path).Equals("RiotClientServices.exe",StringComparison.OrdinalIgnoreCase));
  if(target==null)throw new IOException(UiLanguage.Text("Riot Client não encontrado."));
  using(var process=Process.Start(new ProcessStartInfo(target){WorkingDirectory=Path.GetDirectoryName(target),UseShellExecute=false})){}
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 void ShowSocialProfile(int index){
  PlatformPane pane;
  if(!profiles.TryGetValue(index,out pane)){
   var provider=socialPlatforms[index];
   pane=new PlatformPane(provider,surface,ForeColor,accent);profiles.Add(index,pane);
   socialHost.Controls.Add(pane);pane.Go();
  }
  foreach(var pair in profiles)pair.Value.Visible=pair.Key==index;
  pane.BringToFront();
 }
 void ShowSocialOverview(){
  if(socialOverview==null){
   socialOverview=new SoftPanel{Dock=DockStyle.Fill,Padding=new Padding(2,8,2,12),AutoScroll=true};
   var heading=new Label{Text=UiLanguage.Text("Seus perfis"),Dock=DockStyle.Top,Height=76,Font=Skin.HeadingFont(28),TextAlign=ContentAlignment.MiddleLeft};
   var choices=new TableLayoutPanel{Dock=DockStyle.Top};
   var descriptions=new[]{"Perfil da comunidade","Conta Epic Games","Perfil Xbox e gamertag","Conta e Riot ID","Perfil da comunidade","Conta e EA ID","Conta Ubisoft e nome de usuário","Conta e BattleTag"};
   var cards=new List<Control>();
   for(int i=0;i<socialPlatforms.Length;i++){
    int index=i;var platform=socialPlatforms[i];
    var card=new SoftPanel{Dock=DockStyle.Fill,BackColor=surface,Margin=new Padding(0,0,12,12),Padding=new Padding(14,12,14,11)};
    var iconHost=new SoftPanel{Dock=DockStyle.Top,Height=34};
    var icon=new PictureBox{Dock=DockStyle.Left,Width=34,SizeMode=PictureBoxSizeMode.Zoom,Image=SocialImage(platform.Name)};iconHost.Controls.Add(icon);
    var title=new Label{Text=platform.Name,Dock=DockStyle.Top,Height=37,Font=Skin.HeadingFont(14),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    var description=new Label{Text=UiLanguage.Text(descriptions[index]),Dock=DockStyle.Fill,ForeColor=NexusTheme.Find(data.Theme).Muted,AutoEllipsis=true};
    var button=(HubButton)Button("Abrir perfil  ›");
    button.AccessibleName=UiLanguage.Text("Abrir perfil ")+platform.Name;
    button.BackColor=surface;button.ForeColor=ForeColor;button.Accent=accent;button.Dock=DockStyle.Bottom;
    button.Click+=(s,e)=>Navigate("social:"+index);
    card.Controls.Add(description);card.Controls.Add(button);card.Controls.Add(title);card.Controls.Add(iconHost);
    cards.Add(card);
   }
   Action reflow=()=>{
    int columns=socialOverview.ClientSize.Width>=1220?4:socialOverview.ClientSize.Width>=820?3:2;
    if(choices.ColumnCount==columns)return;
    choices.SuspendLayout();
    try{
     choices.Controls.Clear();choices.ColumnStyles.Clear();choices.RowStyles.Clear();
     choices.ColumnCount=columns;choices.RowCount=(cards.Count+columns-1)/columns;
     choices.Height=choices.RowCount*159;
     for(int column=0;column<columns;column++)choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/columns));
     for(int row=0;row<choices.RowCount;row++)choices.RowStyles.Add(new RowStyle(SizeType.Percent,100f/choices.RowCount));
     for(int index=0;index<cards.Count;index++)choices.Controls.Add(cards[index],index%columns,index/columns);
    }finally{choices.ResumeLayout();}
   };
   socialOverview.SizeChanged+=(s,e)=>reflow();reflow();
   socialOverview.Controls.Add(choices);socialOverview.Controls.Add(heading);socialHost.Controls.Add(socialOverview);
  }
  socialOverview.Visible=true;socialOverview.BringToFront();
 }
 void OpenSocial(){
  foreach(Control pane in socialHost.Controls)pane.Visible=false;
  if(section=="Social"){ShowSocialOverview();return;}
  int index;
  if(!Int32.TryParse(section.Substring(7),out index)||index<0||index>=socialPlatforms.Length)return;
  try{EmbeddedWebView.Prepare();ShowSocialProfile(index);}
  catch(Exception error){Log(error.Message);status.Items[0].Text=UiLanguage.Text("Não foi possível abrir o perfil desta plataforma.");}
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 internal static int TestSocialWeb(bool live,int index){
  var platforms=SocialPlatforms.Create(()=>{});
  if(index<0||index>=platforms.Length||platforms.Any(p=>!p.Allows(new Uri(p.EntryUrl))))return 1;
  EmbeddedWebView.Prepare();int outcome=2;
  using(var form=new Form{Size=new Size(900,650)})
  using(var watchdog=new Timer{Interval=22000}){
   var pane=new PlatformPane(platforms[index],Color.FromArgb(15,16,25),Color.White,Color.MediumPurple,true,!live);
   form.Controls.Add(pane);
   pane.PageLoaded+=success=>{outcome=success?0:5;form.BeginInvoke(new Action(form.Close));};
   watchdog.Tick+=(s,e)=>form.Close();
   form.Shown+=async(s,e)=>{watchdog.Start();pane.Go();await pane.Initialization;if(!form.IsDisposed&&!pane.Ready){outcome=3;form.Close();}};
   Application.Run(form);
  }
  return outcome;
 }
}

