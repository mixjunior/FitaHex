using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle(ProductIdentity.Name)]
[assembly: System.Reflection.AssemblyProduct(ProductIdentity.Name)]
[assembly: System.Reflection.AssemblyDescription("Launcher portátil de jogos para Windows")]

internal static class ProductIdentity {
 internal const string Name="FitaHex";
}

public class Game {
 public string Id=Guid.NewGuid().ToString("N"), Name="", Alias="", Platform="Windows", Source="Manual", Category="", Target="", Args="", Folder="", Cover="", Banner="", Icon="", Badge="", Description="", Developer="", Year="", Genre="", Tags="", Environment="", Install="", StoreId="", Rom="", PortData="";
 public string DiscordCoverUrl="";
 public bool Favorite, Missing, ManualInstall, Completed, Archived, WasInstalled, RefreshCover;
 public AchievementProgress Achievements;
 public DateTime Added=DateTime.Now;
 public DateTime? Last;
 public long Seconds, PlatformSeconds;
 public long? PlatformLocalSecondsAtSync;
 internal long PlayedSeconds {
  get {
   var extra=Math.Max(0,Seconds-(PlatformLocalSecondsAtSync??Seconds));
   var platformTotal=PlatformSeconds+Math.Min(extra,Int64.MaxValue-PlatformSeconds);
   return Math.Max(Seconds,platformTotal);
  }
 }
 internal void ImportPlaytime(long platformSeconds){
  if(platformSeconds<=PlatformSeconds)return;
  PlatformLocalSecondsAtSync=Seconds-Math.Max(0,PlayedSeconds-platformSeconds);
  PlatformSeconds=platformSeconds;
 }
 internal void RecordSession(DateTime start,DateTime end){
  if(!PlatformLocalSecondsAtSync.HasValue)PlatformLocalSecondsAtSync=Seconds;
  Sessions.Add(new Session{Start=start,End=end});
  Seconds+=(long)Math.Max(0,(end-start).TotalSeconds);
 }
 public List<Session> Sessions=new List<Session>();
}
public class Session { public DateTime Start, End; }
public class Emulator {
 public string Name="", Platform="", Target="", Args="\"{rom_path}\"", Folder="", Extensions="", Working="";
}
public class Shelf { public string Name="", Query="", Source="", Platform=""; public bool Smart, Favorites, Installed, Unplayed; public List<string> Ids=new List<string>(); }
public class Library {
 public List<Game> Games=new List<Game>();
 public List<Game> ArchivedGames=new List<Game>();
 public List<Emulator> Emulators=new List<Emulator>();
 public List<Shelf> Shelves=new List<Shelf>();
 public List<string> Categories=new List<string>();
 public string Theme="NEXUS Dark", Profile="Local";
 public bool Fullscreen, Minimize, ListView;
 public int? WindowMode, LibraryView;
 public bool SidebarCollapsed, DiscordEnabled=true;
 public bool AutoScan=true;
 public bool WindowsSetupShown, TourShown;
 public string Language="";
 public bool RestoreArchivedGames(){
  if(ArchivedGames.Count==0)return false;
  var ids=new HashSet<string>(Games.Select(game=>game.Id),StringComparer.OrdinalIgnoreCase);
  foreach(var game in ArchivedGames.Where(game=>ids.Add(game.Id))){
   game.Archived=true;game.Missing=true;game.WasInstalled=true;Games.Add(game);
  }
  ArchivedGames.Clear();return true;
 }
 public bool MigrateCategories(){
  var names=Categories.Concat(Games.SelectMany(game=>(game.Category??"").Split(',')).Select(value=>value.Trim()))
   .Where(name=>!String.IsNullOrWhiteSpace(name)).Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
  if(names.Length==0)return false;
  foreach(var name in names){
   var collectionName=name;
   for(int index=1;Shelves.Any(s=>s.Name.Equals(collectionName,StringComparison.CurrentCultureIgnoreCase));index++)
    collectionName=name+" (categoria "+index+")";
   Shelves.Add(new Shelf{Name=collectionName,Ids=Games.Where(game=>(game.Category??"").Split(',').Any(value=>value.Trim().Equals(name,StringComparison.CurrentCultureIgnoreCase))).Select(game=>game.Id).ToList()});
  }
  Categories.Clear();foreach(var game in Games)game.Category="";
  return true;
 }
}
static class CoverImages {
 internal static readonly string[] Extensions={".png",".jpg",".jpeg",".jfif",".webp",".avif",".heic",".heif",".bmp",".gif",".tif",".tiff",".ico",".jxr",".wdp"};
 internal static readonly string Filter="Imagens|"+String.Join(";",Extensions.Select(ext=>"*"+ext))+"|Todos os arquivos|*.*";
 internal static Bitmap Open(string file){
  using(var stream=File.OpenRead(file))return Decode(stream);
 }
 internal static Bitmap Decode(Stream stream){
  long start=stream.Position;
  try{using(var image=Image.FromStream(stream,true,true))return new Bitmap(image);}
  catch(ArgumentException){}catch(OutOfMemoryException){}
  stream.Position=start;
  try{
   var frame=System.Windows.Media.Imaging.BitmapDecoder.Create(stream,
    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad).Frames.First();
   if(frame.PixelWidth>8192||frame.PixelHeight>8192||(long)frame.PixelWidth*frame.PixelHeight>40000000)
    throw new IOException("A imagem excede o tamanho permitido.");
   var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
   encoder.Frames.Add(frame);
   using(var png=new MemoryStream()){
    encoder.Save(png);png.Position=0;
    using(var image=Image.FromStream(png,true,true))return new Bitmap(image);
   }
  }catch(NotSupportedException error){throw new IOException("Formato de imagem indisponível. Para WebP, AVIF ou HEIF, verifique as extensões de imagem do Windows.",error);}
  catch(System.Runtime.InteropServices.COMException error){throw new IOException("Não foi possível decodificar a imagem.",error);}
  catch(System.FormatException error){throw new IOException("A imagem está inválida.",error);}
 }
}
public static class Storage {
 public static string Root {get;private set;}
 public static string FileName {get{return Path.Combine(Root,"library.json");}}
 static Storage(){Root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),ProductIdentity.Name);}
 internal static void UseTestRoot(){Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,ProductIdentity.Name);}
 internal static void MigrateLegacy(){
  var legacy=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"NexusData");
  MigrateLegacy(legacy,Root);
 }
 internal static bool MigrateLegacy(string legacy,string destination){
  var oldFile=Path.Combine(legacy,"library.json");
  var newFile=Path.Combine(destination,"library.json");
  if(File.Exists(newFile)||!File.Exists(oldFile))return false;
  var oldLock=Path.Combine(legacy,"nexus.lock");
  using(var handle=File.Exists(oldLock)?File.Open(oldLock,FileMode.Open,FileAccess.Read,FileShare.None):null){
   if(Read(oldFile)==null)throw new IOException("Não foi possível ler a biblioteca da pasta NexusData.");
   Directory.CreateDirectory(destination);
   foreach(var folder in new[]{"images","Social","AchievementIcons"})
    CopyLegacyDirectory(Path.Combine(legacy,folder),Path.Combine(destination,folder));
   foreach(var name in new[]{"library.json.bak","nexus.log"}){
    var file=Path.Combine(legacy,name);
    if(File.Exists(file))CopyLegacyFile(file,Path.Combine(destination,name));
   }
   CopyLegacyFile(oldFile,newFile);
  }
  return true;
 }
 static void CopyLegacyDirectory(string source,string target){
  if(!Directory.Exists(source)||(File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0)return;
  Directory.CreateDirectory(target);
  foreach(var file in Directory.EnumerateFiles(source)){
   if((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)CopyLegacyFile(file,Path.Combine(target,Path.GetFileName(file)));
  }
  foreach(var child in Directory.EnumerateDirectories(source))
   CopyLegacyDirectory(child,Path.Combine(target,Path.GetFileName(child)));
 }
 static void CopyLegacyFile(string source,string target){
  if(File.Exists(target))return;
  var temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.Copy(source,temporary);File.Move(temporary,target);}
  finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 static JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
 public static Library Read(string file){return Json.Deserialize<Library>(File.ReadAllText(file));}
 public static void Write(Library data) {
  Directory.CreateDirectory(Root);
  var tmp=FileName+".tmp";
  File.WriteAllText(tmp,Json.Serialize(data));
  if(File.Exists(FileName)) File.Replace(tmp,FileName,FileName+".bak"); else File.Move(tmp,FileName);
 }
 public static string Asset(string value) {
  if(String.IsNullOrWhiteSpace(value)) return "";
  if(!Path.IsPathRooted(value)) { if(!IsAsset(value))throw new IOException("Selecione um arquivo de imagem.");return value; }
  if(!File.Exists(value)) throw new IOException("Imagem não encontrada: "+value);
  Directory.CreateDirectory(Path.Combine(Root,"images"));
  var relative=Path.Combine("images",Guid.NewGuid().ToString("N")+".png");
  using(var image=CoverImages.Open(value))image.Save(Path.Combine(Root,relative),System.Drawing.Imaging.ImageFormat.Png);
  return relative;
 }
 static bool IsAsset(string value){return Regex.IsMatch(value??"", @"^images[\\/][a-fA-F0-9]{32}\.[a-zA-Z0-9]+$");}
 public static Image Picture(string value) {
  if(!IsAsset(value))return null;
  try{return CoverImages.Open(Path.Combine(Root,value));}catch{return null;}
 }
 public static void Export(string path) {
  var full=Path.GetFullPath(path);
  if(full.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Escolha um destino fora da pasta de dados do FitaHex.");
  var temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   using(var zip=System.IO.Compression.ZipFile.Open(temporary,System.IO.Compression.ZipArchiveMode.Create)){
    System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip,FileName,"library.json");
    var images=Path.Combine(Root,"images");
    if(Directory.Exists(images))foreach(var asset in Directory.EnumerateFiles(images)){
     var name="images/"+Path.GetFileName(asset);
     if(IsAsset(name))System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(zip,asset,name);
    }
   }
   if(File.Exists(full))File.Replace(temporary,full,null);else File.Move(temporary,full);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 public static Library Import(string path) {
  using(var zip=System.IO.Compression.ZipFile.OpenRead(path)){
   var entry=zip.GetEntry("library.json");
   if(entry==null)throw new IOException("Biblioteca ausente no backup.");
   Library data;
   using(var r=new StreamReader(entry.Open()))data=Json.Deserialize<Library>(r.ReadToEnd());
   if(data==null||data.Games==null||data.Emulators==null||data.Shelves==null||data.Categories==null)throw new IOException("Biblioteca inválida.");
   Directory.CreateDirectory(Path.Combine(Root,"images"));
   foreach(var asset in zip.Entries){
    var name=asset.FullName.Replace('\\','/');
    if(!Regex.IsMatch(name,@"^images/[a-fA-F0-9]{32}\.[a-zA-Z0-9]+$"))continue;
    var output=Path.Combine(Root,"images",asset.Name);
    if(File.Exists(output))continue;
    using(var input=asset.Open())using(var dest=File.Create(output))input.CopyTo(dest);
   }
   return data;
  }
 }
}
public static class Stores {
 static string Unescape(string value){return value.Replace(@"\\",@"\").Replace("\\\"","\"");}
 static string Vdf(string text,string key){
  var m=Regex.Match(text,"\""+Regex.Escape(key)+"\"\\s+\"((?:\\\\.|[^\"\\\\])*)\"",RegexOptions.IgnoreCase);
  return m.Success?Unescape(m.Groups[1].Value):"";
 }
 static string Reg(RegistryKey key,string name){return Convert.ToString(key.GetValue(name,""));}
 internal static HashSet<string> SteamRoots(){
  var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))if(k!=null)roots.Add(Reg(k,"SteamPath"));}
  catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
  try{using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))
   using(var k=machine.OpenSubKey(@"Software\Valve\Steam"))if(k!=null)roots.Add(Reg(k,"InstallPath"));}
  catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}
  roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
  roots.RemoveWhere(String.IsNullOrWhiteSpace);
  return roots;
 }
 internal static IEnumerable<Game> Steam(){
  var roots=SteamRoots();
  foreach(var root in roots.ToArray()){
   var file=Path.Combine(root,"steamapps","libraryfolders.vdf");
   if(!File.Exists(file))continue;
   foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"((?:\\\\.|[^\"\\\\])*)\""))roots.Add(Unescape(m.Groups[1].Value));
  }
  foreach(var root in roots.Where(x=>!String.IsNullOrWhiteSpace(x))){
   var dir=Path.Combine(root,"steamapps");
   if(!Directory.Exists(dir))continue;
   foreach(var file in Directory.EnumerateFiles(dir,"appmanifest_*.acf")){
    var acf=File.ReadAllText(file);var id=Vdf(acf,"appid");var name=Vdf(acf,"name");var sub=Vdf(acf,"installdir");
    if(!Regex.IsMatch(id,@"^\d+$")||name==""||sub=="")continue;
    uint flag;
    var state=Vdf(acf,"StateFlags");
    if(UInt32.TryParse(state,out flag)&&(flag&4)==0)continue;
    var install=Path.Combine(dir,"common",sub);
    if(Directory.Exists(install))yield return new Game{Source="Steam",Name=name,StoreId="steam:"+id,Install=install,Target="steam://run/"+id};
   }
  }
 }
 static string Field(Dictionary<string,object> d,string name){object v;return d.TryGetValue(name,out v)?Convert.ToString(v):"";}
 internal static string EpicUri(string value){
  return "com.epicgames.launcher://apps/"+Uri.EscapeDataString(value)+"?action=launch&silent=true";
 }
 internal static string EpicName(string name,string install){
  if(Directory.Exists(install)){
   if(File.Exists(Path.Combine(install,"GTA5_Enhanced.exe")))return "Grand Theft Auto V Enhanced";
   if(File.Exists(Path.Combine(install,"GTA5.exe")))return "Grand Theft Auto V Legacy";
  }
  return name;
 }
 static IEnumerable<Game> Epic(){
  var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic");
  var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  var programFiles=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
  var roots=new[]{profile,programFiles}.Where(p=>!String.IsNullOrWhiteSpace(p)).Select(p=>Path.Combine(p,"Epic Games"));
  return EpicWithFolders(root,roots);
 }
 internal static IEnumerable<Game> EpicWithFolders(string manifestRoot,IEnumerable<string> roots){
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var game in EpicFrom(manifestRoot)){seen.Add(EpicFolderKey(game.Install));yield return game;}
  foreach(var root in roots.Distinct(StringComparer.OrdinalIgnoreCase)){
   if(!Directory.Exists(root))continue;
   string[] folders;
   try{folders=Directory.GetDirectories(root);}catch(IOException){continue;}catch(UnauthorizedAccessException){continue;}
   foreach(var folder in folders){
    var key=EpicFolderKey(folder);
    if(seen.Contains(key))continue;
    var directoryName=new DirectoryInfo(folder).Name;
    var name=EpicName(directoryName,folder);
    if(name==directoryName&&!Directory.Exists(Path.Combine(folder,".egstore")))continue;
    seen.Add(key);
    yield return new Game{Source="Epic Games",Name=name,StoreId="epic-path:"+key,Install=folder,Target=EpicUri(folder.Replace('\\','/'))};
   }
  }
 }
 internal static IEnumerable<Game> EpicFrom(string root){
  var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  var manifests=Path.Combine(root,@"EpicGamesLauncher\Data\Manifests");
  var installed=Path.Combine(root,@"UnrealEngineLauncher\LauncherInstalled.dat");
  var json=new JavaScriptSerializer{MaxJsonLength=int.MaxValue};
  if(Directory.Exists(manifests))foreach(var file in Directory.EnumerateFiles(manifests,"*.item")){
   Dictionary<string,object> item;
   try{item=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(file));}catch(ArgumentException){continue;}catch(IOException){continue;}catch(InvalidOperationException){continue;}
   if(item==null)continue;
   var name=Field(item,"DisplayName");var id=Field(item,"AppName");var install=Field(item,"InstallLocation");
   var main=Field(item,"MainGameAppName");
   if(id==""||install==""||Field(item,"bIsIncompleteInstall")=="True"||(main!=""&&main!=id)||!Directory.Exists(install))continue;
   var ns=Field(item,"CatalogNamespace");var catalog=Field(item,"CatalogItemId");
   if(!folders.Add(EpicFolderKey(install)))continue;
   yield return new Game{Source="Epic Games",Name=EpicName(name==""?new DirectoryInfo(install).Name:name,install),StoreId="epic:"+id,Install=install,Target=EpicUri(ns!=""&&catalog!=""?ns+":"+catalog+":"+id:id)};
  }
  if(!File.Exists(installed))yield break;
  Dictionary<string,object> records=null;
  try{records=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(installed));}catch(ArgumentException){}catch(InvalidOperationException){}catch(IOException){}catch(UnauthorizedAccessException){}
  object entries;
  if(records==null||!records.TryGetValue("InstallationList",out entries))yield break;
  var list=entries as System.Collections.IEnumerable;
  if(list==null)yield break;
  foreach(var item in list.OfType<Dictionary<string,object>>()){
   var id=Field(item,"AppName");if(id=="")id=Field(item,"ArtifactId");
   var install=Field(item,"InstallLocation");
   if(id==""||!Directory.Exists(install)||!folders.Add(EpicFolderKey(install)))continue;
   var ns=Field(item,"NamespaceId");var catalog=Field(item,"ItemId");
   yield return new Game{Source="Epic Games",Name=EpicName(new DirectoryInfo(install).Name,install),StoreId="epic:"+id,Install=install,Target=EpicUri(ns!=""&&catalog!=""?ns+":"+catalog+":"+id:id)};
  }
 }
 internal static string EpicFolderKey(string path){
  return String.IsNullOrWhiteSpace(path)?"":Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
 }
 static IEnumerable<Game> Gog(){
  foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
   foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})
    using(var root=RegistryKey.OpenBaseKey(hive,view))
    using(var games=root.OpenSubKey(@"SOFTWARE\GOG.com\Games")){
     if(games==null)continue;
     foreach(var id in games.GetSubKeyNames())using(var k=games.OpenSubKey(id)){
      if(k==null)continue;
      var name=Reg(k,"gameName");var install=Reg(k,"path");var exe=Reg(k,"exe");
      if(!Regex.IsMatch(id,@"^\d+$")||name==""||!Directory.Exists(install))continue;
      yield return new Game{Source="GOG",Name=name,StoreId="gog:"+id,Install=install,Target=File.Exists(exe)&&Path.GetExtension(exe).Equals(".exe",StringComparison.OrdinalIgnoreCase)?exe:"",Folder=install};
     }
    }
 }
 public static List<Game> Scan(out string failures){
  var found=new List<Game>();var errors=new List<string>();
  foreach(var scan in new Func<IEnumerable<Game>>[]{Steam,Epic,Gog,RiotGames.Scan})try{found.AddRange(scan());}catch(Exception e){errors.Add(scan.Method.Name+": "+e.Message);}
  failures=String.Join("\n",errors);return found;
 }
 public static void Merge(Library library,List<Game> found,string errors){
  var keys=new HashSet<string>(found.Select(x=>x.StoreId),StringComparer.OrdinalIgnoreCase);
  foreach(var game in library.Games.Where(x=>x.StoreId!=""&&!x.StoreId.StartsWith("rom:")&&!x.StoreId.StartsWith(XboxGames.Prefix,StringComparison.OrdinalIgnoreCase))){
   var prefix=game.StoreId.Split(':')[0];
   var provider=prefix=="steam"?"Steam":(prefix=="epic"||prefix=="epic-path")?"Epic":prefix=="riot"?"Scan":"Gog";
   if(!game.Missing)game.WasInstalled=true;
   if(!errors.Contains(provider+":")&&!keys.Contains(game.StoreId)&&!(game.ManualInstall&&Directory.Exists(game.Install)))game.Missing=true;
  }
  var old=library.Games.Where(x=>x.StoreId!="").GroupBy(x=>x.StoreId,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
  foreach(var g in found){
   Game existing;
   if(old.TryGetValue(g.StoreId,out existing)){
    if(!existing.ManualInstall){
     existing.Install=g.Install;
     if(g.Target!="")existing.Target=g.Target;
     if(g.Folder!="")existing.Folder=g.Folder;
    }
    Hub.RecordInstallation(existing);continue;
   }
   if(g.Source=="Epic Games"){
    existing=library.Games.FirstOrDefault(x=>x.Source=="Epic Games"&&x.Install!=""&&String.Equals(EpicFolderKey(x.Install),EpicFolderKey(g.Install),StringComparison.OrdinalIgnoreCase));
    if(existing!=null){
     if(existing.StoreId.StartsWith("epic-path:",StringComparison.OrdinalIgnoreCase)&&g.StoreId.StartsWith("epic:",StringComparison.OrdinalIgnoreCase)&&existing.Target==EpicUri(existing.Install.Replace('\\','/')))existing.Target=g.Target;
     if(!g.StoreId.StartsWith("epic-path:",StringComparison.OrdinalIgnoreCase)||existing.StoreId=="")existing.StoreId=g.StoreId;
     if(!existing.ManualInstall)existing.Install=g.Install;
     Hub.RecordInstallation(existing);old[g.StoreId]=existing;continue;
    }
   }
   Hub.RecordInstallation(g);library.Games.Add(g);old[g.StoreId]=g;
  }
 }
}

















