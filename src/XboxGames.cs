using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;

static class XboxGames {
 public const string Source="Xbox / Microsoft Store";
 public const string Prefix="xbox:";
 public class Entry {
  public string Name, Aumid, Package;
  public string Folder="";
  public bool Game;
 }
 static readonly HashSet<string> Known=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
  "Microsoft.MinecraftUWP","Microsoft.MinecraftWindowsBeta","Microsoft.MicrosoftSolitaireCollection",
  "Microsoft.MicrosoftMahjong","Microsoft.MicrosoftMinesweeper","Microsoft.MicrosoftSudoku","Microsoft.MicrosoftJigsaw",
  "Microsoft.MicrosoftUltimateWordGames","Microsoft.MicrosoftTreasureHunt","Microsoft.MicrosoftNumberPuzzle"
 };
 static bool SystemApp(string package){
  if(String.IsNullOrEmpty(package))return true;
  return Regex.IsMatch(package,@"^Microsoft\.(?:GamingApp|GamingServices|Xbox.*|WindowsStore|StorePurchaseApp|MinecraftLauncher|MinecraftEducationEdition)$",RegexOptions.IgnoreCase);
 }
 internal static bool ValidAppId(string id){return Regex.IsMatch(id??"",@"^[A-Za-z0-9.-]+_[A-Za-z0-9]+![A-Za-z0-9._-]+$");}
 internal static string Target(string id){return @"shell:AppsFolder\"+id;}
 internal static Game Make(Entry record,bool manual){
  if(!ValidAppId(record.Aumid))throw new IOException("Identificador de aplicativo inválido.");
  return new Game{Name=record.Name,Source=Source,StoreId=Prefix+record.Aumid,Install=record.Folder??"",Target=Target(record.Aumid),ManualInstall=manual};
 }
 internal static bool IsGame(Entry entry,HashSet<string> gdk){
  return !SystemApp(entry.Package)&&(entry.Game||Known.Contains(entry.Package)||gdk.Contains(entry.Package));
 }
 static HashSet<string> ConfiguredGames(){
  var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var disk in DriveInfo.GetDrives()){
   if(disk.DriveType!=DriveType.Fixed||!disk.IsReady)continue;
   var folder=Path.Combine(disk.RootDirectory.FullName,"XboxGames");
   try{
    if(!Directory.Exists(folder))continue;
    foreach(var game in Directory.EnumerateDirectories(folder)){
     foreach(var root in new[]{game,Path.Combine(game,"Content")}){
      var config=Path.Combine(root,"MicrosoftGame.config");
      try{if(File.Exists(config))ReadIdentity(config,names);}catch(IOException){}catch(UnauthorizedAccessException){}catch(XmlException){}
     }
    }
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
  return names;
 }
 internal static void ReadIdentity(string path,HashSet<string> names){
  var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024};
  using(var reader=XmlReader.Create(path,settings)){
   if(reader.MoveToContent()!=XmlNodeType.Element||reader.LocalName!="Game")return;
   while(reader.Read())if(reader.NodeType==XmlNodeType.Element&&reader.Depth==1&&reader.LocalName=="Identity"){
    var name=reader.GetAttribute("Name");if(!String.IsNullOrWhiteSpace(name))names.Add(name);return;
   }
  }
 }
 const string Inventory=@"
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false)
$starts=@(Get-StartApps)
$packages=@{}
try {
 Get-AppxPackage -PackageTypeFilter Main | ForEach-Object {
  if(!$_.IsFramework -and !$_.IsResourcePackage -and $_.PackageFamilyName){$packages[$_.PackageFamilyName]=$_}
 }
}catch{}
$metadata=@{}
$rows=@(foreach($app in $starts) {
 if($app.AppID -notmatch '^([a-zA-Z0-9.-]+)_[a-zA-Z0-9]+![a-zA-Z0-9._-]+$'){continue}
 $name=$Matches[1]
 $family=($app.AppID -split '!',2)[0]
 if(!$metadata.ContainsKey($family)){
  $pkg=$packages[$family];$gdk=$false;$loc=''
  if($pkg){
   $loc=$pkg.InstallLocation
   if($loc){
    $gdk=Test-Path -LiteralPath (Join-Path $loc 'MicrosoftGame.config') -PathType Leaf -ErrorAction SilentlyContinue
    if(!$gdk){$gdk=Test-Path -LiteralPath (Join-Path $loc 'Content\MicrosoftGame.config') -PathType Leaf -ErrorAction SilentlyContinue}
   }
   try{
    $manifest=Get-AppxPackageManifest -Package $pkg.PackageFullName -ErrorAction Stop
    $marker=$manifest.SelectSingleNode('//*[local-name()=""Capability"" and (translate(@Name,""ABCDEFGHIJKLMNOPQRSTUVWXYZ"",""abcdefghijklmnopqrstuvwxyz"")=""xboxlive"")] | //*[local-name()=""Extension"" and (@Category=""windows.gameConfig"" or @Category=""windows.game"")]')
    if($marker){$gdk=$true}
   }catch{}
  }
  $metadata[$family]=@{Folder=$loc;Game=[bool]$gdk}
 }
 $entry=$metadata[$family]
 [PSCustomObject]@{Name=$app.Name;Aumid=$app.AppID;Folder=$entry.Folder;Package=$name;Game=$entry.Game}
})
ConvertTo-Json -InputObject $rows -Compress -Depth 3
";
 internal static Entry[] Read(){
  var command=Convert.ToBase64String(Encoding.Unicode.GetBytes(Inventory));
  var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe"),"-NoLogo -NoProfile -NonInteractive -EncodedCommand "+command){
   UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8
  };
  using(var process=Process.Start(info)){
   var read=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
   if(!process.WaitForExit(20000)){try{process.Kill();}catch(InvalidOperationException){}throw new IOException("O Windows não respondeu à consulta de aplicativos instalados.");}
   Task.WaitAll(read,error);
   if(process.ExitCode!=0)throw new IOException("Não foi possível consultar os aplicativos do Windows.");
   var entries=new JavaScriptSerializer{MaxJsonLength=int.MaxValue}.Deserialize<Entry[]>(read.Result.Trim());
   return (entries??new Entry[0]).Where(x=>x!=null&&ValidAppId(x.Aumid)&&!String.IsNullOrWhiteSpace(x.Name)&&(!SystemApp(x.Package)||String.Equals(x.Package,"Microsoft.MinecraftLauncher",StringComparison.OrdinalIgnoreCase))).ToArray();
  }
 }
 internal sealed class Snapshot {
  public Entry[] Apps;
  public Game[] Games;
 }
 public static Snapshot Scan(){
  var entries=Read();var gdk=ConfiguredGames();
  return new Snapshot{Apps=entries,Games=entries.Where(e=>IsGame(e,gdk)).GroupBy(e=>e.Aumid,StringComparer.OrdinalIgnoreCase).Select(e=>Make(e.First(),false)).ToArray()};
 }
 internal static int Merge(Library library,Snapshot snapshot){
  var apps=new HashSet<string>(snapshot.Apps.Select(entry=>Prefix+entry.Aumid),StringComparer.OrdinalIgnoreCase);
  var current=library.Games.Where(g=>g.StoreId.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase)).GroupBy(g=>g.StoreId,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
  foreach(var game in current.Values){
   if(!game.Missing)game.WasInstalled=true;
   if(apps.Contains(game.StoreId))Hub.RecordInstallation(game);
   else game.Missing=true;
  }
  int added=0;
  foreach(var game in snapshot.Games){
   Game old;
   if(current.TryGetValue(game.StoreId,out old)){old.Install=game.Install;continue;}
   old=library.Games.FirstOrDefault(g=>g.Target.Equals(game.Target,StringComparison.OrdinalIgnoreCase));
   if(old!=null){old.StoreId=game.StoreId;old.Source=Source;old.Install=game.Install;Hub.RecordInstallation(old);}
   else{Hub.RecordInstallation(game);library.Games.Add(game);added++;}
   current[game.StoreId]=old??game;
  }
  return added;
 }
 [ComImport,Guid("2e941141-7f97-4756-ba1d-9decde894a3d"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ActivationManager {
  void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string aumid,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint processId);
 }
 const string ShellPrefix=@"shell:AppsFolder\";
 internal static bool IsTarget(string value){return value.StartsWith(ShellPrefix,StringComparison.OrdinalIgnoreCase)&&ValidAppId(value.Substring(ShellPrefix.Length));}
 internal static void Launch(string value,string arguments){
  if(!IsTarget(value))throw new IOException("Atalho da Microsoft Store inválido.");
  var type=Type.GetTypeFromCLSID(new Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c"));
  var manager=(ActivationManager)Activator.CreateInstance(type);
  try{uint pid;manager.ActivateApplication(value.Substring(ShellPrefix.Length),arguments,0,out pid);}
  finally{Marshal.FinalReleaseComObject(manager);}
 }
}


