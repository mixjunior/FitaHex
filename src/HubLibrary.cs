using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

partial class Hub {
 static readonly string[] GroupTitles={"Jogado recentemente","Favoritos","Instalados e adicionados","Não instalados"};
 readonly Dictionary<string,DateTime> installationChecks=new Dictionary<string,DateTime>(StringComparer.OrdinalIgnoreCase);
 bool checkingInstallations;
 Playtime platformTimes;

 DateTime lastInstallationCheck;
 void SetupInstallationRefresh(){
  var timer=new Timer{Interval=18000};
  timer.Tick+=(s,e)=>CheckInstallations(false);
  timer.Start();FormClosed+=(s,e)=>timer.Dispose();
  Activated+=(s,e)=>CheckInstallations(data.Games.Any(g=>g.Missing&&g.Source=="Steam"));
 }
 internal static bool InUninstalledHistory(Game game){
  return !Installed(game)&&(game.Archived||game.WasInstalled||game.Last.HasValue||game.PlayedSeconds>0||game.Completed||
   game.Rom!=""||game.StoreId==""&&game.Target!="");
 }
 internal static void RecordInstallation(Game game){
  if(game.Archived||game.WasInstalled&&game.Missing)game.RefreshCover=true;
  game.Archived=false;game.Missing=false;game.WasInstalled=true;
 }
 internal static Game MatchUninstalled(Library library,Game incoming){
  var candidates=library.Games.Where(game=>InUninstalledHistory(game)&&game.Source==incoming.Source&&
   (game.Rom!="")== (incoming.Rom!="")&&(game.Rom!=""||game.StoreId=="")).ToArray();
  string path=incoming.Rom!=""?incoming.Rom:incoming.PortData!=""?incoming.PortData:incoming.Target;
  var exact=candidates.Where(game=>String.Equals(game.Target,incoming.Target,StringComparison.OrdinalIgnoreCase)&&
   String.Equals(game.Rom,incoming.Rom,StringComparison.OrdinalIgnoreCase)&&
   String.Equals(game.PortData,incoming.PortData,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(exact.Length!=0)return exact.Length==1?exact[0]:null;
  var relocated=candidates.Where(game=>{
   if(!String.Equals(game.Platform,incoming.Platform,StringComparison.OrdinalIgnoreCase))return false;
   var oldPath=game.Rom!=""?game.Rom:game.PortData!=""?game.PortData:game.Target;
   return Path.GetFileName(path).Equals(Path.GetFileName(oldPath),StringComparison.OrdinalIgnoreCase)&&
    (game.Rom!=""||GameProgress.Related(game,incoming));
  }).ToArray();
  return relocated.Length==1?relocated[0]:null;
 }
 internal static int LibraryGroup(Game game,HashSet<string> recent,bool installed){
  if(!installed)return 3;
  if(recent.Contains(game.Id))return 0;
  return game.Favorite?1:2;
 }
 static void OpenLink(string link){using(var started=Process.Start(new ProcessStartInfo(link){UseShellExecute=true})){}}
 internal static string InstallLink(Game game){
  var steam=Regex.Match(game.StoreId??"",@"^steam:([0-9]+)$");
  if(steam.Success)return "steam://install/"+steam.Groups[1].Value;
  if(game.Source=="Epic Games"){
   var epic=Regex.Match(game.Target??"", @"^com\.epicgames\.launcher://apps/([a-zA-Z0-9_.%-]+)\?action=launch",RegexOptions.IgnoreCase);
   if((game.StoreId??"").StartsWith("epic:",StringComparison.OrdinalIgnoreCase)&&epic.Success)return "com.epicgames.launcher://apps/"+epic.Groups[1].Value+"?action=installer";
   return "com.epicgames.launcher://store/library";
  }
  if(game.Source=="GOG"){
   var gog=Regex.Match(game.StoreId??"",@"^gog:([0-9]+)$");
   if(gog.Success){using(var handler=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"goggalaxy\shell\open\command"))if(handler!=null)return "goggalaxy://openGameView/"+gog.Groups[1].Value;}
   return "https://www.gog.com/account";
  }
  return "";
 }
 string DownloadAction(Game game){
  if(Regex.IsMatch(game.StoreId??"",@"^steam:[0-9]+$"))return "Instalar pela Steam";
  if(game.Source=="Epic Games")return InstallLink(game).Contains("action=installer")?"Instalar pela Epic":"Abrir biblioteca Epic";
  if(game.Source=="GOG")return "Instalar pelo GOG Galaxy";
  if(game.Source==XboxGames.Source)return "Abrir aplicativo Xbox";
  if(game.Source==RiotGames.Source)return "Instalar pelo Riot Client";
  if(game.Source=="EA app"||game.Source=="Battle.net")return "Abrir "+game.Source;
  return "Localizar arquivo";
 }
 void OpenInstallation(Game game){
  var link=InstallLink(game);
  if(link!="")OpenLink(link);
  else if(game.Source==XboxGames.Source)XboxGames.Launch(XboxGames.Target("Microsoft.GamingApp_8wekyb3d8bbwe!Microsoft.Xbox.App"),"");
  else if(game.Source=="EA app"||game.Source=="Battle.net"){
   var launcher=SourceIcons.Launcher(game.Source,game);
   if(!File.Exists(launcher))throw new IOException("Abra "+game.Source+" para instalar este jogo.");
   using(var process=Process.Start(new ProcessStartInfo(launcher){UseShellExecute=true})){}
   return;
  }
  else if(game.Source==RiotGames.Source){
   var path=SourceIcons.Launcher(game.Source,game);
   if(!File.Exists(path)||!Path.GetFileName(path).Equals("RiotClientServices.exe",StringComparison.OrdinalIgnoreCase))throw new IOException("Abra o Riot Client para instalar este jogo.");
   using(var process=Process.Start(new ProcessStartInfo(path){Arguments=game.Args,WorkingDirectory=Path.GetDirectoryName(path),UseShellExecute=false})){}
  }else{if(IsCustom(game))EditCustom(game,restore:true);else Edit(game);return;}
  installationChecks[game.Id]=DateTime.UtcNow.AddHours(3);
  message.Text="Conclua a instalação na plataforma. "+ProductIdentity.Name+" vai verificar a biblioteca.";
 }
 async void CheckInstallations(bool refreshSteam){
  if(IsDisposed||checkingInstallations||scanning||xboxScanning)return;
  foreach(var expired in installationChecks.Where(x=>x.Value<DateTime.UtcNow).Select(x=>x.Key).ToArray())installationChecks.Remove(expired);
  if(!refreshSteam&&installationChecks.Count==0)return;
  if(DateTime.UtcNow-lastInstallationCheck<TimeSpan.FromSeconds(12))return;
  lastInstallationCheck=DateTime.UtcNow;
  var requested=data.Games.Where(g=>installationChecks.ContainsKey(g.Id)).ToArray();
  bool xbox=requested.Any(g=>g.Source==XboxGames.Source);
  bool other=requested.Any(g=>g.Source!="Steam"&&g.Source!=XboxGames.Source);
  checkingInstallations=true;
  try{
   var detected=await Task.Run(()=>{
    string ignored;
    var found=other?Stores.Scan(out ignored):Stores.Steam().ToList();
    return Tuple.Create(found,xbox?XboxGames.Scan():null);
   });
   if(IsDisposed)return;
   var before=data.Games.Where(g=>g.Missing).Select(g=>g.Id).ToArray();
   var wasMissing=new HashSet<string>(before);
   var known=new HashSet<string>(data.Games.Select(g=>g.StoreId),StringComparer.OrdinalIgnoreCase);
   var epicFolders=new HashSet<string>(data.Games.Where(g=>g.Source=="Epic Games"&&g.Install!="").Select(g=>Stores.EpicFolderKey(g.Install)),StringComparer.OrdinalIgnoreCase);
   var current=detected.Item1.Where(g=>known.Contains(g.StoreId)||g.Source=="Epic Games"&&epicFolders.Contains(Stores.EpicFolderKey(g.Install))).ToList();
   Stores.Merge(data,current,"Steam: passiva\nEpic: passiva\nGog: passiva\nScan: passiva");
   if(detected.Item2!=null)XboxGames.Merge(data,detected.Item2);
   var now=data.Games.Where(g=>wasMissing.Contains(g.Id)&&!g.Missing&&Installed(g)).ToArray();
   if(now.Length!=0){
    foreach(var game in now)installationChecks.Remove(game.Id);
    Save();RefreshList();Notify(now.Length+" instalações concluídas. Os jogos já podem ser abertos.");LoadArt(false);
   }
  }catch(Exception error){if(!IsDisposed)Log(error.Message);}
  finally{checkingInstallations=false;}
 }
}



