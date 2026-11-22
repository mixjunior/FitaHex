using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

static class RiotGames {
 internal const string Source="Riot Games";
 sealed class Product {
  public string Id, Name, Folder;
  public string[] Markers;
  public Product(string id,string name,string folder,params string[] markers){Id=id;Name=name;Folder=folder;Markers=markers;}
 }
 static readonly Product[] Products={
  new Product("league_of_legends","League of Legends","League of Legends","LeagueClient.exe",@"Game\League of Legends.exe"),
  new Product("valorant","VALORANT","VALORANT",@"live\VALORANT.exe",@"live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe",@"ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe","VALORANT.exe"),
  new Product("bacon","Legends of Runeterra","LoR",@"live\Game\LoR.exe",@"Game\LoR.exe","LoR.exe"),
  new Product("lion","2XKO","2XKO",@"Live\Lion.exe","Lion.exe"),
  new Product("teamfighttactics","Teamfight Tactics","Teamfight Tactics",@"Game\Teamfight Tactics.exe",@"live\Game\Teamfight Tactics.exe","TeamfightTactics.exe","Teamfight Tactics.exe")
 };
 internal static IEnumerable<string> CoverUrls(string storeId){
  const string cdn="https://images.launchbox-app.com/";
  switch(storeId){
   case "riot:valorant":
    yield return cdn+"22d94894-e433-4568-b2b0-f3221357c7d2.jpg";
    yield return cdn+"e554925b-2a7b-4429-b324-eb68b54a9118.png";break;
   case "riot:league_of_legends":yield return cdn+"r2_31ab5dd4-94f3-46f2-ba16-83afb407c32a.jpg";break;
   case "riot:bacon":yield return cdn+"49787abb-449f-44ab-a3d4-19a7a918b2d3.png";break;
   case "riot:teamfighttactics":yield return cdn+"fe5d3ed4-4caa-49a7-8591-c558fb6f1cc0.jpg";break;
  }
 }
 internal static IEnumerable<Game> Available(){
  var client=ClientLocations().Select(ExistingClient).FirstOrDefault(path=>path!="");
  if(client==null)yield break;
  foreach(var product in Products.Where(p=>p.Id!="teamfighttactics"))
   yield return new Game{Name=product.Name,Source=Source,Target=client,Folder=Path.GetDirectoryName(client),Args="--launch-product="+product.Id+" --launch-patchline=live",StoreId="riot:"+product.Id,Missing=true};
 }
 internal static async Task<Process> WaitForGame(Game game,DateTime launched,Func<bool> stop){
  var product=Products.FirstOrDefault(item=>"riot:"+item.Id==game.StoreId);
  if(product==null||!Directory.Exists(game.Install))return null;
  var folder=Path.GetFullPath(game.Install).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  var names=product.Markers.Where(marker=>!marker.Equals("LeagueClient.exe",StringComparison.OrdinalIgnoreCase)&&
    !marker.EndsWith(@"\VALORANT.exe",StringComparison.OrdinalIgnoreCase)&&!marker.Equals("VALORANT.exe",StringComparison.OrdinalIgnoreCase))
   .Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  while(!stop()&&(DateTime.Now-launched).TotalMinutes<8){
   foreach(var name in names){
    Process match=null;
    var processes=Process.GetProcessesByName(name);
    try{
     foreach(var process in processes){
      try{
       if(process.StartTime<launched||!process.MainModule.FileName.StartsWith(folder,StringComparison.OrdinalIgnoreCase))continue;
       match=process;return match;
      }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
     }
    }finally{foreach(var process in processes)if(process!=match)process.Dispose();}
   }
   await Task.Delay(2000).ConfigureAwait(false);
  }
  return null;
 }
 static string ExistingClient(string file){
  try{return Path.IsPathRooted(file)&&Path.GetFileName(file).Equals("RiotClientServices.exe",StringComparison.OrdinalIgnoreCase)&&File.Exists(file)?file:"";}catch(ArgumentException){return "";}
 }
 internal static IEnumerable<string> ClientLocations(){
  var metadata=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Riot Games","RiotClientInstalls.json");
  var registered=new List<string>();
  if(File.Exists(metadata)){
   try{
    var records=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(metadata));
    if(records!=null)foreach(var field in new[]{"rc_default","rc_live"}){
     object value;if(records.TryGetValue(field,out value)){var candidate=ExistingClient(Convert.ToString(value));if(candidate!="")registered.Add(candidate);}
    }
   }catch(ArgumentException){}catch(InvalidOperationException){}catch(IOException){}catch(UnauthorizedAccessException){}
  }
  var standard=DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed)
   .Select(d=>Path.Combine(d.RootDirectory.FullName,"Riot Games","Riot Client","RiotClientServices.exe"));
  return registered.Concat(standard);
 }
 public static IEnumerable<Game> Scan(){
  var common=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Riot Games");
  var roots=DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed).Select(d=>Path.Combine(d.RootDirectory.FullName,"Riot Games")).ToArray();
  return From(common,roots);
 }
 internal static Game[] From(string common,string[] roots){
  var paths=new List<string>();
  var clients=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  var installs=Path.Combine(common,"RiotClientInstalls.json");
  var client="";
  if(File.Exists(installs)){
   Dictionary<string,object> json=null;
   try{json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(installs));}catch(ArgumentException){}catch(InvalidOperationException){}
   if(json!=null){
    object value;
    foreach(var key in new[]{"rc_default","rc_live"}){
     if(json.TryGetValue(key,out value))client=ExistingClient(Convert.ToString(value));
     if(client!="")break;
    }
    if(json.TryGetValue("associated_client",out value)){
     var map=value as Dictionary<string,object>;
     if(map!=null)foreach(var record in map)if(Directory.Exists(record.Key)){
      paths.Add(record.Key);
      var found=ExistingClient(Convert.ToString(record.Value));if(found!="")clients[Clean(record.Key)]=found;
     }
    }
   }
  }
  var metadata=Path.Combine(common,"Metadata");
  if(Directory.Exists(metadata))foreach(var product in Products){
   var name=product.Id+".live";var file=Path.Combine(metadata,name,name+".product_settings.yaml");
   if(!File.Exists(file))continue;
   foreach(Match match in Regex.Matches(File.ReadAllText(file),@"^\s*product_install_(?:full_path|root)\s*:\s*([^\r\n]+)",RegexOptions.Multiline)){
    var path=match.Groups[1].Value.Trim().Trim('"',(char)39);
    if(Directory.Exists(path))paths.Add(path);
   }
  }
  if(client=="")client=roots.Select(root=>ExistingClient(Path.Combine(root,"Riot Client","RiotClientServices.exe"))).FirstOrDefault(p=>p!="")??"";
  var result=new List<Game>();
  foreach(var product in Products){
   var folders=paths.Concat(roots.Select(root=>Path.Combine(root,product.Folder))).Distinct(StringComparer.OrdinalIgnoreCase);
   var install=folders.FirstOrDefault(path=>Marker(product,path)!="");
   if(install==null)continue;
   string specific;
   var launch=clients.TryGetValue(Clean(install),out specific)?specific:client;
   if(launch=="")continue;
   result.Add(new Game{Name=product.Name,Source=Source,Target=launch,Args="--launch-product="+product.Id+" --launch-patchline=live",Folder=Path.GetDirectoryName(launch),Install=install,StoreId="riot:"+product.Id});
  }
  return result.ToArray();
 }
 static string Clean(string path){return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);}
 static string Marker(Product product,string folder){
  if(!Directory.Exists(folder))return "";
  return product.Markers.Select(name=>Path.Combine(folder,name)).FirstOrDefault(File.Exists)??"";
 }
 internal static string IconPath(Game game){
  var product=Products.FirstOrDefault(x=>"riot:"+x.Id==game.StoreId);
  return product==null?"":Marker(product,game.Install);
 }
}

