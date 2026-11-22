using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

sealed class Playtime {
 static readonly Dictionary<string,string> GalaxySources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
  {"gog","GOG"},{"steam","Steam"},{"epic","Epic Games"},{"xboxone",XboxGames.Source},{"winstore",XboxGames.Source},
  {"riot",RiotGames.Source},{"origin","EA app"},{"uplay","Ubisoft Connect"},{"battlenet","Battle.net"}
 };
 readonly Dictionary<string,string> origins=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,long> exact=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Dictionary<string,long>> titles=new Dictionary<string,Dictionary<string,long>>(StringComparer.OrdinalIgnoreCase);

 internal static Playtime Read(){
  var times=new Playtime();
  foreach(var root in Stores.SteamRoots()){
   try{
    var account=GameProgress.SteamAccount(root);
    if(account=="")continue;
    var config=new FileInfo(Path.Combine(root,"userdata",account,"config","localconfig.vdf"));
    if(!config.Exists||config.Length>32*1024*1024)continue;
    times.ReadSteam(File.ReadAllText(config.FullName));
    break;
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
  var galaxy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"GOG.com\Galaxy\storage\galaxy-2.0.db");
  if(File.Exists(galaxy)){
   try{OwnedGames.ReadGalaxyTimes(galaxy,times);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
  }
  times.ReadXboxCache(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
   @"Playnite\ExtensionsData\7e4fbb5e-2ae3-48d4-8ba0-6b30e7a4e287\PfnInfoCache"));
  return times;
 }

 void ReadXboxCache(string directory){
  if(!Directory.Exists(directory))return;
  try{
   foreach(var path in Directory.EnumerateFiles(directory,"*.json")){
    try{
     if(new FileInfo(path).Length<=1024*1024)ReadXboxTitle(File.ReadAllText(path));
    }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
   }
  }catch(IOException){}catch(UnauthorizedAccessException){}
 }
 void ReadXboxTitle(string json){
  var record=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
  object pfn,minutes;
  uint total;
  if(record==null||!record.TryGetValue("pfn",out pfn)||!record.TryGetValue("minutesPlayed",out minutes)||
   !Regex.IsMatch(Convert.ToString(pfn),@"^[a-zA-Z0-9_.-]+_[a-zA-Z0-9]+$")||
   !UInt32.TryParse(Convert.ToString(minutes),out total)||total==0)return;
  var key="xbox:"+pfn;
  long previous;
  if(!exact.TryGetValue(key,out previous)||previous<total*60L){exact[key]=total*60L;origins[key]="Playnite";}
 }
 internal void ReadSteam(string vdf){
  var tokens=Regex.Matches(vdf,@"//[^\r\n]*|""((?:\\.|[^""\\])*)""|[{}]");
  var scopes=new List<string>();
  for(int index=0;index<tokens.Count;index++){
   var token=tokens[index];
   if(token.Value=="}"){if(scopes.Count>0)scopes.RemoveAt(scopes.Count-1);continue;}
   if(!token.Groups[1].Success)continue;
   string key=token.Groups[1].Value;
   while(++index<tokens.Count&&tokens[index].Value.StartsWith("//",StringComparison.Ordinal)){}
   if(index==tokens.Count)break;
   var value=tokens[index];
   if(value.Value=="{"){scopes.Add(key);continue;}
   uint appId,minutes;
   if(scopes.Count!=6||!String.Equals(String.Join("/",scopes.Take(5)),"UserLocalConfigStore/Software/Valve/Steam/apps",StringComparison.OrdinalIgnoreCase)||
    !key.Equals("Playtime",StringComparison.OrdinalIgnoreCase)||!value.Groups[1].Success||
    !UInt32.TryParse(scopes[5],out appId)||appId==0||!UInt32.TryParse(value.Groups[1].Value,out minutes))continue;
   exact["steam:"+appId]=minutes*60L;origins["steam:"+appId]="Steam";
  }
 }

 internal void AddGalaxy(string release,string name,long seconds){
  int split=release.IndexOf('_');
  if(split<1||split==release.Length-1)return;
  string platform=release.Substring(0,split),source;
  if(!GalaxySources.TryGetValue(platform,out source))return;
  if(platform=="steam"||platform=="gog"||platform=="epic"){
   string key=platform+":"+release.Substring(split+1);
   long old;
   if(!exact.TryGetValue(key,out old)||seconds>old){exact[key]=seconds;origins[key]="GOG Galaxy";}
  }
  string title=GameProgress.TitleKey(name);
  if(title=="")return;
  string group=source+"\n"+title;
  Dictionary<string,long> entries;
  if(!titles.TryGetValue(group,out entries))titles[group]=entries=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
  entries[release]=seconds;
 }

 internal int Apply(IEnumerable<Game> games,Action<Game,string> onUpdate=null){
  var supported=games.Where(game=>game.Rom==""&&GalaxySources.ContainsValue(game.Source)).ToArray();
  var byTitle=supported.ToLookup(game=>game.Source+"\n"+GameProgress.TitleKey(game.Name),StringComparer.OrdinalIgnoreCase);
  int updated=0;
  foreach(var game in supported){
   long seconds;
   string lookup=game.StoreId,origin;
   if(!exact.TryGetValue(lookup,out seconds)&&game.Source==XboxGames.Source&&game.StoreId.StartsWith(XboxGames.Prefix,StringComparison.OrdinalIgnoreCase))
    lookup="xbox:"+game.StoreId.Substring(XboxGames.Prefix.Length).Split('!')[0];
   if(!exact.TryGetValue(lookup,out seconds)){
    string key=game.Source+"\n"+GameProgress.TitleKey(game.Name);
    Dictionary<string,long> entries;
    if(byTitle[key].Count()!=1||!titles.TryGetValue(key,out entries)||entries.Count!=1)continue;
    seconds=entries.Values.First();origin="GOG Galaxy";
   }else origin=origins[lookup];
   if(seconds>game.PlatformSeconds){game.ImportPlaytime(seconds);updated++;if(onUpdate!=null)onUpdate(game,origin);}
  }
  return updated;
 }

 internal static void Verify(){
  var times=new Playtime();
  times.ReadSteam(@" ""UserLocalConfigStore"" { ""Software"" { ""Valve"" { ""Steam"" { ""apps"" {
   ""42"" { ""Playtime2wks"" ""3"" ""Playtime"" ""100"" } } } } } }");
  times.AddGalaxy("gog_99","Jogo GOG",1200);
  times.AddGalaxy("epic_fixture","Jogo Epic",3000);
  var steam=new Game{Name="Steam",StoreId="steam:42",Source="Steam",Seconds=120};
  var gog=new Game{StoreId="gog:99",Source="GOG",Seconds=1800};
  var epic=new Game{Name="Jogo Epic",StoreId="epic-path:sample",Source="Epic Games"};
  var xbox=new Game{Source=XboxGames.Source,StoreId=XboxGames.Prefix+"Example.Game_ab123!App",
   Target=XboxGames.Target("Example.Game_ab123!App")};
  times.ReadXboxTitle(@"{""pfn"":""Example.Game_ab123"",""minutesPlayed"":""63""}");
  var games=new[]{steam,gog,epic,xbox};
  if(times.Apply(games)!=4||xbox.PlayedSeconds!=3780||steam.PlayedSeconds!=6000||gog.PlayedSeconds!=1800||epic.PlayedSeconds!=3000||times.Apply(games)!=0)
   throw new IOException("Importação de tempo jogado.");
  var start=DateTime.UtcNow;
  steam.RecordSession(start,start.AddMinutes(30));
  if(steam.PlayedSeconds!=7800)throw new IOException("Sessão após sincronização.");
  steam.ImportPlaytime(7800);
  if(steam.PlayedSeconds!=7800)throw new IOException("Dupla contagem após sincronização.");
  var legacy=new Game{Seconds=1800,PlatformSeconds=36000};
  legacy.RecordSession(start,start.AddMinutes(30));
  if(legacy.PlayedSeconds!=37800)throw new IOException("Tempo legado e sessão nova.");
 }
}
