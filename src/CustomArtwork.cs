using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

static class CustomArtwork {
 static string Content(Game game){return game.Rom!=""?game.Rom:game.PortData!=""?game.PortData:game.Target;}
 internal static bool Supports(Game game){
  return game.Rom!=""||game.StoreId==""&&Path.IsPathRooted(game.Target)&&File.Exists(game.Target);
 }
 internal static Artwork.Result Find(Game game){
  var asset=new Artwork.Result{Id=game.Id};
  if(game.Cover==""){
   var file=Content(game);
   foreach(var location in new[]{file,game.Target}.Where(Path.IsPathRooted).Distinct(StringComparer.OrdinalIgnoreCase)){
    var stem=Path.Combine(Path.GetDirectoryName(location),Path.GetFileNameWithoutExtension(location));
    var paths=CoverImages.Extensions.Select(ext=>stem+ext)
     .Concat(CoverImages.Extensions.Select(ext=>Path.Combine(Path.GetDirectoryName(location),"cover"+ext)));
    foreach(var candidate in paths.Where(File.Exists)){
     asset.Cover=Artwork.SaveFile(candidate,false);
     if(asset.Cover!="")break;
    }
    if(asset.Cover!="")break;
   }
   if(asset.Cover=="")asset.Cover=Artwork.Download(VerifiedCovers(game),false);
   if(asset.Cover==""&&(game.Rom!=""||game.PortData!=""))asset.Cover=FindRom(game);
   if(asset.Cover==""&&game.Rom=="")asset.Cover=FindSteamCover(game.Name);
  }
  if(game.Rom==""&&game.Icon==""){
   using(var icon=SourceIcons.FromFile(game.Target))if(icon!=null)asset.Icon=InstalledArt.Save(icon);
  }
  return asset;
 }
 static IEnumerable<string> Systems(Game game){
  string ext=Path.GetExtension(Content(game)).ToLowerInvariant();
  switch(ext){
   case ".gba":return new[]{"Nintendo - Game Boy Advance"};
   case ".gbc":return new[]{"Nintendo - Game Boy Color"};
   case ".gb":return new[]{"Nintendo - Game Boy"};
   case ".nds":case ".dsi":return new[]{"Nintendo - Nintendo DS"};
   case ".3ds":case ".cci":case ".cxi":case ".zcci":case ".zcxi":case ".3dsx":case ".z3dsx":case ".bcci":case ".bcxi":return new[]{"Nintendo - Nintendo 3DS"};
   case ".nes":case ".fds":return new[]{"Nintendo - Nintendo Entertainment System"};
   case ".sfc":case ".smc":return new[]{"Nintendo - Super Nintendo Entertainment System"};
   case ".n64":case ".z64":case ".v64":return new[]{"Nintendo - Nintendo 64"};
   case ".rvz":case ".gcz":case ".gcm":case ".wbfs":
    return ext==".wbfs"?new[]{"Nintendo - Wii"}:new[]{"Nintendo - GameCube","Nintendo - Wii"};
   case ".wua":case ".wud":case ".wux":case ".rpx":return new[]{"Nintendo - Wii U"};
   case ".gg":return new[]{"Sega - Game Gear"};
   case ".sms":return new[]{"Sega - Master System - Mark III"};
   case ".md":case ".gen":case ".smd":return new[]{"Sega - Mega Drive - Genesis"};
   case ".cdi":case ".gdi":return new[]{"Sega - Dreamcast"};
  }
  var platform=game.Platform??"";
  if(platform=="Game Boy / GBA")return new[]{"Nintendo - Game Boy Advance","Nintendo - Game Boy Color","Nintendo - Game Boy"};
  if(platform=="GameCube / Wii")return new[]{"Nintendo - GameCube","Nintendo - Wii"};
  if(platform=="Nintendo 3DS"||platform=="Nintendo 64"||platform=="Super Nintendo")return new[]{"Nintendo - "+(platform=="Super Nintendo"?"Super Nintendo Entertainment System":platform)};
  if(platform=="PlayStation")return new[]{"Sony - PlayStation"};
  if(platform=="PlayStation 2")return new[]{"Sony - PlayStation 2"};
  if(platform=="PSP")return new[]{"Sony - PlayStation Portable"};
  if(platform=="PlayStation 3"||platform=="PlayStation 4"||platform=="PlayStation 5")return new[]{"Sony - "+platform};
  return new string[0];
 }
 const string Boxarts="https://thumbnails.libretro.com/";
 static readonly ConcurrentDictionary<string,Lazy<string[]>> Indexes=new ConcurrentDictionary<string,Lazy<string[]>>();
 static string FolderUrl(string system){return Boxarts+Uri.EscapeDataString(system)+"/Named_Boxarts/";}
 static string FileTitle(string title){return Regex.Replace(title??"",@"^\d{3,5}\s*[-_.]\s*","").Trim();}
 internal static string Clean(string title){
  return Regex.Replace(FileTitle(title),@"\s*\((?:USA|Europe|World|Japan|Brazil|Australia|Korea|China|Taiwan|Canada|Asia|En[,)]|Rev\s|v\d)[^)]*\).*$","",RegexOptions.IgnoreCase).Trim();
 }
 static string Key(string title){
  title=Clean(title);
  title=Regex.Replace(title,@",\s*(The|An|A)(?=\s*[-:]|$)","",RegexOptions.IgnoreCase);
  title=Regex.Replace(title,@"^(The|An|A)\s+","",RegexOptions.IgnoreCase);
  title=Regex.Replace(title.Normalize(NormalizationForm.FormD),@"\p{Mn}","").Normalize(NormalizationForm.FormC);
  return GameProgress.TitleKey(title);
 }
 static string EscapeName(string title){
  return Uri.EscapeDataString(Regex.Replace(title,@"[&*/:`<>?\\|""]","_"))+".png";
 }

 static string FindRom(Game game){
  var saved=Artwork.Download(Urls(game),false);
  if(saved!="")return saved;
  string file=FileTitle(Path.GetFileNameWithoutExtension(Content(game)));
  foreach(var system in Systems(game)){
   var candidates=Indexes.GetOrAdd(system,key=>new Lazy<string[]>(()=>ReadIndex(FolderUrl(key)))).Value;
   var matches=Rank(candidates,file,game.Name).Take(3);
   saved=Artwork.Download(matches.Select(name=>FolderUrl(system)+Uri.EscapeDataString(name)+".png"),false);
   if(saved!="")return saved;
  }
  return Artwork.Download(FallbackUrls(game),false);
 }

 internal static IEnumerable<string> Urls(Game game){
  var names=new[]{FileTitle(Path.GetFileNameWithoutExtension(Content(game))),FileTitle(game.Name)}
   .Where(name=>name.Length>0&&name.Length<170).Distinct(StringComparer.OrdinalIgnoreCase);
  foreach(var system in Systems(game))foreach(var name in names){
   yield return FolderUrl(system)+EscapeName(name);
   yield return "https://raw.githubusercontent.com/libretro-thumbnails/"+Uri.EscapeDataString(system.Replace(' ','_'))+"/master/Named_Boxarts/"+EscapeName(name);
  }
 }
 internal static IEnumerable<string> VerifiedCovers(Game game){
  var keys=new[]{Key(game.Name),Key(Path.GetFileNameWithoutExtension(Content(game)))};
  var systems=Systems(game).ToArray();
  bool port=game.Source=="Port";
  if((port||systems.Contains("Nintendo - Nintendo 3DS"))&&keys.Contains("LEGEND OF ZELDA MAJORA S MASK 3D")){
   yield return "https://images.launchbox-app.com/r2_ff15c1c1-63cb-4ff5-8ab6-8f095cd453fc.jpg";
   yield return "https://art.gametdb.com/3ds/cover/US/AJRE.png";
   yield return "https://images.launchbox-app.com/r2_d470e0e7-a0aa-454f-9823-1a1f7ee1f8cc.png";
  }
  if((port||systems.Contains("Nintendo - Game Boy Advance"))&&keys.Any(key=>key=="POKEMON FIRERED VERSION"||key=="POKEMON FIRERED"||key=="POKEMON FIRE RED")){
   yield return "https://images.launchbox-app.com//a60aefb6-37cb-4452-992a-5d83994e0e5e.jpg";
   yield return FolderUrl("Nintendo - Game Boy Advance")+EscapeName("Pokemon - FireRed Version (USA, Europe)");
  }
 }
 internal static string FindSteamCover(string title){
  if(String.IsNullOrWhiteSpace(title)||title.Length>150)return "";
  try{
   var search="https://store.steampowered.com/api/storesearch/?l=english&cc=BR&term="+Uri.EscapeDataString(title.Trim());
   var id=SteamMatch(Encoding.UTF8.GetString(Artwork.Fetch(search,512*1024)),title);
   return id==""?"":Artwork.Download(new[]{
    "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/"+id+"/library_600x900.jpg",
    "https://cdn.cloudflare.steamstatic.com/steam/apps/"+id+"/library_600x900.jpg"},true);
  }catch(WebException){return "";}catch(IOException){return "";}catch(ArgumentException){return "";}catch(InvalidOperationException){return "";}
 }
 internal static string SteamMatch(string json,string title){
  var payload=new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string,object>;
  object raw;
  if(payload==null||!payload.TryGetValue("items",out raw))return "";
  var wanted=Key(title);
  var ids=new HashSet<string>();
  foreach(var item in (raw as object[]??new object[0]).OfType<Dictionary<string,object>>()){
   object id,name;
   if(!item.TryGetValue("name",out name)||!item.TryGetValue("id",out id)||Key(name as string)!=wanted)continue;
   var value=Convert.ToString(id,System.Globalization.CultureInfo.InvariantCulture);
   if(Regex.IsMatch(value,@"^[0-9]{1,10}$"))ids.Add(value);
  }
  return wanted!=""&&ids.Count==1?ids.First():"";
 }
 internal static IEnumerable<string> FallbackUrls(Game game){
  var clean=Clean(game.Name);
  if(clean.Length==0||clean.Length>150)yield break;
  foreach(var system in Systems(game))foreach(var variant in new[]{clean+" (USA)",clean+" (USA, Europe)",clean+" (Europe)",clean}){
   yield return FolderUrl(system)+EscapeName(variant);
   yield return "https://raw.githubusercontent.com/libretro-thumbnails/"+Uri.EscapeDataString(system.Replace(' ','_'))+"/master/Named_Boxarts/"+EscapeName(variant);
  }
 }
 static string[] ReadIndex(string url){
  try{return ParseIndex(Encoding.UTF8.GetString(Artwork.Fetch(url,8*1024*1024)));}
  catch(WebException){}catch(IOException){}catch(ArgumentException){}
  return new string[0];
 }
 internal static string[] ParseIndex(string html){
  return Regex.Matches(html,@"href\s*=\s*(?:""([^""<>]+\.png)""|'([^'<>]+\.png)')",RegexOptions.IgnoreCase).Cast<Match>()
   .Select(item=>Uri.UnescapeDataString(WebUtility.HtmlDecode((item.Groups[1].Success?item.Groups[1]:item.Groups[2]).Value)))
   .Where(name=>name.Length>4&&name.Length<220&&name.IndexOfAny(new[]{'/','\\','?',':'})<0)
   .Select(name=>name.Substring(0,name.Length-4)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 }
 static string[] Tags(string name){
  return Regex.Matches(name??"",@"\(([^()]+)\)").Cast<Match>().Select(match=>match.Groups[1].Value).ToArray();
 }
 internal static IEnumerable<string> Rank(string[] catalogue,string file,string title){
  var keys=new HashSet<string>(new[]{Key(file),Key(title)}.Where(x=>x!=""),StringComparer.Ordinal);
  var wanted=Tags(file);
  return catalogue.Where(name=>keys.Contains(Key(name)))
   .OrderByDescending(name=>String.Equals(name,file,StringComparison.OrdinalIgnoreCase))
   .ThenByDescending(name=>Tags(name).Intersect(wanted,StringComparer.OrdinalIgnoreCase).Sum(tag=>tag.StartsWith("Rev",StringComparison.OrdinalIgnoreCase)?1:4))
   .ThenBy(name=>name.Length);
 }
}