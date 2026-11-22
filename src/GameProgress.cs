using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public sealed class AchievementProgress {
 public int Unlocked, Total;
 public List<string> Names=new List<string>();
 public List<AchievementEntry> Entries=new List<AchievementEntry>();
}

public sealed class AchievementEntry {
 public string Name="", Description="", Icon="";
 public bool Unlocked, Hidden;
 public string Id="";
 public override string ToString(){return Name;}
}

static class GameProgress {
 internal static string TitleKey(string name){
  var text=(name??"").Trim().Replace("™","").Replace("®","").Normalize(NormalizationForm.FormKC).ToUpperInvariant();
  return Regex.Replace(text,@"[^\p{L}\p{Nd}]+"," ").Trim();
 }
 internal static bool Related(Game first,Game second){
  var key=TitleKey(first.Name);
  return key!=""&&key==TitleKey(second.Name);
 }
 internal static void Verify(){
  const string data="[[\"achievements\",{\"data\":{\"nTotal\":25,\"nAchieved\":3,\"vecHighlight\":[{\"strID\":\"7\",\"strName\":\"Primeiro passo\",\"bAchieved\":true}],\"vecUnachieved\":[{\"strID\":\"8\",\"strName\":\"Próximo passo\",\"strImage\":\"https://shared.steamstatic.com/achievement.jpg\"}]}}]]";
  var sample=ParseSteam(data);
  if(sample==null||sample.Unlocked!=3||sample.Total!=25||sample.Entries.Count!=2||
     sample.Entries.Single(x=>!x.Unlocked).Icon!="https://shared.steamstatic.com/achievement.jpg"||
     sample.Entries.Single(x=>x.Unlocked).Name!="Primeiro passo")throw new IOException("Cache de conquistas Steam.");
  if(!Related(new Game{Name="Jogo™"},new Game{Name="Jogo"})||Related(new Game{Name="Jogo"},new Game{Name="Jogo Deluxe"}))
   throw new IOException("Versões dos jogos.");
 }
 internal static string SteamAccount(string root){
  var login=Path.Combine(root,"config","loginusers.vdf");
  if(!File.Exists(login))return "";
  var users=Regex.Matches(File.ReadAllText(login),@"(?s)""([0-9]{17})""\s*\{([^{}]*)\}").Cast<Match>().ToArray();
  var recent=users.Where(m=>Regex.IsMatch(m.Groups[2].Value,@"""MostRecent""\s*""1""",RegexOptions.IgnoreCase)).ToArray();
  var auto=users.Where(m=>Regex.IsMatch(m.Groups[2].Value,@"""AutoLogin""\s*""1""",RegexOptions.IgnoreCase)).ToArray();
  var user=recent.Length==1?recent[0]:auto.Length==1?auto[0]:users.Length==1?users[0]:null;
  ulong identity;
  if(user==null||!UInt64.TryParse(user.Groups[1].Value,out identity)||identity<=76561197960265728UL)return "";
  return (identity-76561197960265728UL).ToString(CultureInfo.InvariantCulture);
 }
 internal static Dictionary<string,AchievementProgress> Steam(IEnumerable<string> requested,bool expand=false){
  var result=new Dictionary<string,AchievementProgress>(StringComparer.OrdinalIgnoreCase);
  var appIds=requested.Distinct(StringComparer.OrdinalIgnoreCase).Where(id=>Regex.IsMatch(id??"",@"^steam:[0-9]+$")).ToArray();
  if(appIds.Length==0)return result;
  foreach(var root in Stores.SteamRoots()){
   try{
    var account=SteamAccount(root);
    if(account=="")continue;
    var cache=Path.Combine(root,"userdata",account,"config","librarycache");
    var stats=Path.Combine(root,"appcache","stats");
    if(!Directory.Exists(cache)&&!(expand&&Directory.Exists(stats)))continue;
    foreach(var id in appIds){
     var file=new FileInfo(Path.Combine(cache,id.Substring(6)+".json"));
     try{
      var progress=file.Exists&&file.Length<=4*1024*1024?ParseSteam(File.ReadAllText(file.FullName)):null;
      if(expand){
       var full=SteamAchievementFiles.Progress(stats,account,id.Substring(6));
       if(full!=null&&(progress==null||full.Total==progress.Total&&full.Unlocked==progress.Unlocked))progress=full;
      }
      if(progress!=null)result[id]=progress;
     }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(InvalidOperationException){}
    }
    break;
   }catch(IOException){}catch(UnauthorizedAccessException){}
  }
  return result;
 }
 static object Get(Dictionary<string,object> data,string key){
  object item;return data!=null&&data.TryGetValue(key,out item)?item:null;
 }
 internal static AchievementProgress ParseSteam(string source){
  var root=new JavaScriptSerializer{MaxJsonLength=4*1024*1024}.DeserializeObject(source) as object[];
  if(root==null)return null;
  foreach(var row in root.OfType<object[]>()){
   if(row.Length!=2||!Equals(row[0],"achievements"))continue;
   var data=Get(row[1] as Dictionary<string,object>,"data") as Dictionary<string,object>;
   int total,unlocked;
   if(!Int32.TryParse(Convert.ToString(Get(data,"nTotal")),out total)||total<=0||
      !Int32.TryParse(Convert.ToString(Get(data,"nAchieved")),out unlocked)||unlocked<0||unlocked>total)return null;
   var entries=new Dictionary<string,AchievementEntry>(StringComparer.OrdinalIgnoreCase);
   foreach(var key in new[]{"vecUnachieved","vecHighlight","vecAchievedHidden","vecAchieved"}){
    foreach(var entry in (Get(data,key) as object[]??new object[0]).OfType<Dictionary<string,object>>()){
     var name=Get(entry,"strName") as string;
     if(String.IsNullOrWhiteSpace(name))continue;
     var id=Get(entry,"strID") as string;
     if(String.IsNullOrWhiteSpace(id))id=name;
     bool done=Equals(Get(entry,"bAchieved"),true)||(key=="vecAchieved"||key=="vecAchievedHidden")&&Get(entry,"bAchieved")==null;
     AchievementEntry previous;
     if(entries.TryGetValue(id,out previous)&&previous.Unlocked&&!done)continue;
     entries[id]=new AchievementEntry{Id=id,Name=name,Description=Get(entry,"strDescription") as string??"",
      Icon=Get(entry,"strImage") as string??"",Unlocked=done};
    }
   }
   return new AchievementProgress{Total=total,Unlocked=unlocked,Entries=entries.Values.ToList()};
  }
  return null;
 }
}

partial class Hub {
 IEnumerable<Game> Editions(Game game){
  if(!Launchers.Contains(Origin(game),StringComparer.OrdinalIgnoreCase))return new[]{game};
  return data.Games.Where(candidate=>GameProgress.Related(game,candidate)&&Launchers.Contains(Origin(candidate),StringComparer.OrdinalIgnoreCase));
 }
 bool HasPlayableEdition(Game game){return Installed(game)||Editions(game).Any(Installed);}
 Game[] PlayableEditions(Game game){
  return Editions(game).Where(Installed).GroupBy(Origin,StringComparer.OrdinalIgnoreCase)
   .Select(group=>group.FirstOrDefault(g=>g==game)??group.First()).ToArray();
 }
 void ChoosePlay(Game game){

  var alternatives=PlayableEditions(game);
  if(alternatives.Length==0){OpenInstallation(game);return;}
  if(alternatives.Length==1){Play(alternatives[0]);return;}
  var names=alternatives.Select(Origin).ToArray();
  var chosen=Choose("Jogar "+game.Name+" em qual launcher?",names);
  if(chosen!=null)Play(alternatives[Array.IndexOf(names,chosen)]);
 }
 void ToggleCompleted(Game game){
  bool complete=!game.Completed;
  foreach(var edition in Editions(game))edition.Completed=complete;
  Save();RefreshList();
 }
 void ApplyAchievements(Dictionary<string,AchievementProgress> found){
  foreach(var game in data.Games){
   AchievementProgress progress;
   if(found.TryGetValue(game.StoreId,out progress))game.Achievements=progress;
  }
 }
 async void ShowAchievements(Game game){
  try{
   var ids=Editions(game).Select(g=>g.StoreId).ToArray();
   var refreshed=await Task.Run(()=>GameProgress.Steam(ids,true));
   if(IsDisposed)return;
   if(refreshed.Count>0){ApplyAchievements(refreshed);Save();}
   var editions=Editions(game).ToArray();
   var sources=editions.GroupBy(Origin,StringComparer.OrdinalIgnoreCase).Select(group=>group.FirstOrDefault(item=>item.Achievements!=null)??group.First()).ToArray();
   using(var window=new AchievementWindow(game,sources,NexusTheme.Find(data.Theme))){
    window.ShowDialog(this);
   }
  }catch(Exception error){if(!IsDisposed)Safe(()=>{throw new IOException(error.Message,error);});}
 }
}