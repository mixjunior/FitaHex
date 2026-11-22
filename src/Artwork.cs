using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

static class Artwork {
 internal class Result { public string Id, Cover="", Banner="", Icon=""; }
 const string Cdn="https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/";
 static Dictionary<string,object> Object(object value){return value as Dictionary<string,object>??new Dictionary<string,object>();}
 static object Get(Dictionary<string,object> obj,string key){object value;return obj.TryGetValue(key,out value)?value:null;}
 internal static string AppId(Game game){
  var match=Regex.Match(game.StoreId??"",@"^steam:(\d+)$");if(match.Success)return match.Groups[1].Value;
  if(game.Source==XboxGames.Source&&String.Equals(game.Name.Trim(),"Hollow Knight",StringComparison.OrdinalIgnoreCase))return "367520";
  if(!String.Equals(game.Source,"Epic Games",StringComparison.OrdinalIgnoreCase))return "";
  var name=Stores.EpicName(game.Name,game.Install);
  if(Regex.IsMatch(name,@"^(Grand Theft Auto V|GTA\s*V)[\s:–-]+Enhanced$",RegexOptions.IgnoreCase))return "3240220";
  if(Regex.IsMatch(name,@"^(Grand Theft Auto V|GTA\s*V)([\s:–-]+(Legacy|Premium Edition))?$",RegexOptions.IgnoreCase))return "271590";
  return "";
 }
 internal static byte[] Fetch(string url,int limit){
  var request=(HttpWebRequest)WebRequest.Create(url);
  request.Timeout=4500;request.ReadWriteTimeout=4500;request.AllowAutoRedirect=false;
  request.UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) FitaHex/1.2";request.Accept="image/jpeg,image/png,application/json,*/*;q=0.2";request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
  using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=new MemoryStream()){
   var buffer=new byte[8192];int read;
   while((read=input.Read(buffer,0,buffer.Length))>0){if(output.Length+read>limit)throw new IOException("Resposta grande demais.");output.Write(buffer,0,read);}
   return output.ToArray();
  }
 }
 internal static Dictionary<string,Dictionary<string,object>> ParseAssets(string json){
  var response=new JavaScriptSerializer().DeserializeObject(json);
  var entries=Get(Object(Get(Object(response),"response")),"store_items") as object[]??new object[0];
  var result=new Dictionary<string,Dictionary<string,object>>();
  foreach(var entry in entries){
   var item=Object(entry);var id=Convert.ToString(Get(item,"appid"));
   if(Regex.IsMatch(id,@"^\d+$"))result[id]=Object(Get(item,"assets"));
  }
  return result;
 }
 static Dictionary<string,Dictionary<string,object>> Metadata(string[] ids){
  if(ids.Length==0)return new Dictionary<string,Dictionary<string,object>>();
  var query=new JavaScriptSerializer().Serialize(new {ids=ids.Select(id=>new {appid=UInt32.Parse(id)}).ToArray(),context=new {language="english",country_code="BR",steam_realm=1},data_request=new {include_assets=true}});
  var url="https://api.steampowered.com/IStoreBrowseService/GetItems/v1/?input_json="+Uri.EscapeDataString(query);
  return ParseAssets(System.Text.Encoding.UTF8.GetString(Fetch(url,2*1024*1024)));
 }
 internal static string Url(string id,Dictionary<string,object> data,string key){
  var value=Convert.ToString(Get(data,key));
  if(!Regex.IsMatch(value,@"^(?:[0-9a-f]{16,64}/)?[A-Za-z0-9_]+\.(?:jpg|png)$",RegexOptions.IgnoreCase))return "";
  return Cdn+id+"/"+value;
 }
 static string Local(string id,bool cover,string[] roots){
  var stems=cover?new[]{"library_600x900_2x","library_600x900","library_capsule_2x","library_capsule"}:new[]{"library_hero_2x","library_hero"};
  foreach(var root in roots){
   var cache=Path.Combine(root,"appcache","librarycache");var app=Path.Combine(cache,id);
   foreach(var stem in stems)foreach(var ext in new[]{".jpg",".png",".jpeg"}){
    foreach(var file in new[]{Path.Combine(cache,id+"_"+stem+ext),Path.Combine(app,stem+ext)})
     if(File.Exists(file)){var saved=SaveFile(file,cover);if(saved!="")return saved;}
    if(!Directory.Exists(app))continue;
    foreach(var hash in Directory.EnumerateDirectories(app)){
     var file=Path.Combine(hash,stem+ext);
     if(File.Exists(file)){var saved=SaveFile(file,cover);if(saved!="")return saved;}
    }
   }
  }
  return "";
 }
 internal static string SaveFile(string path,bool cover){
  try{using(var stream=File.OpenRead(path))return SaveImage(stream,cover);}catch{return "";}
 }
 static string SaveImage(Stream stream,bool cover){
  using(var image=CoverImages.Decode(stream)){
   if(image.Width<100||image.Height<100||image.Width>6000||image.Height>6000||(cover&&image.Height<=image.Width))return "";
   var relative=Path.Combine("images",Guid.NewGuid().ToString("N")+".jpg");var path=Path.Combine(Storage.Root,relative);
   Directory.CreateDirectory(Path.GetDirectoryName(path));image.Save(path,ImageFormat.Jpeg);return relative;
  }
 }
 internal static string Download(IEnumerable<string> urls,bool cover){
  foreach(var url in urls.Where(x=>x!="").Distinct())try{using(var stream=new MemoryStream(Fetch(url,12*1024*1024))){var saved=SaveImage(stream,cover);if(saved!="")return saved;}}catch(WebException){}catch(ArgumentException){}catch(IOException){}catch(System.Runtime.InteropServices.ExternalException){}
  return "";
 }
 internal static bool StoreCoverSupported(Game game){
  return Regex.IsMatch(game.StoreId??"",@"^gog:[0-9]+$")||RiotGames.CoverUrls(game.StoreId).Any();
 }
 internal static IEnumerable<string> GogCoverUrls(string json){
  var product=Object(new JavaScriptSerializer().DeserializeObject(json));
  var images=Object(Get(product,"images"));
  foreach(var key in new[]{"logo2x","logo","boxArt","background"}){
   string value=Convert.ToString(Get(images,key));
   if(value.StartsWith("//",StringComparison.Ordinal))value="https:"+value;
   Uri uri;
   if(Uri.TryCreate(value,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&
    Regex.IsMatch(uri.Host,@"^images(?:-[0-9]+)?\.gog\.com$",RegexOptions.IgnoreCase))yield return uri.AbsoluteUri;
  }
 }
 static string StoreCover(Game game){
  if(game.Source==RiotGames.Source)return Download(RiotGames.CoverUrls(game.StoreId),false);
  var id=game.StoreId.Substring(4);
  try{
   var endpoint="https://api.gog.com/products/"+id+"?locale=en-US";
   var json=System.Text.Encoding.UTF8.GetString(Fetch(endpoint,2*1024*1024));
   var cover=Download(GogCoverUrls(json),false);
   if(cover!="")return cover;
  }catch(WebException){}catch(IOException){}catch(ArgumentException){}catch(InvalidOperationException){}
  return CustomArtwork.FindSteamCover(game.Name);
 }
 internal static List<Result> Find(Game[] games){
  var roots=Stores.SteamRoots().ToArray();var found=new List<Result>();var epicCover=new HashSet<string>();
  Parallel.ForEach(games.Where(g=>g.Cover==""&&String.Equals(g.Source,"Epic Games",StringComparison.OrdinalIgnoreCase)),new ParallelOptions{MaxDegreeOfParallelism=4},g=>{
   var cover=EpicArtwork.Find(g);
   if(cover!="")lock(found){found.Add(new Result{Id=g.Id,Cover=cover});epicCover.Add(g.Id);}
  });
  Parallel.ForEach(games.Where(CustomArtwork.Supports),new ParallelOptions{MaxDegreeOfParallelism=4},game=>{
   var custom=CustomArtwork.Find(game);
   if(custom.Cover!=""||custom.Icon!="")lock(found)found.Add(custom);
  });
  Parallel.ForEach(games.Where(game=>game.Cover==""&&StoreCoverSupported(game)),new ParallelOptions{MaxDegreeOfParallelism=4},game=>{
   var cover=StoreCover(game);
   if(cover!="")lock(found)found.Add(new Result{Id=game.Id,Cover=cover});
  });
  var remote=new Dictionary<string,List<Tuple<Game,Result>>>();
  foreach(var game in games){
   var id=AppId(game);if(id=="")continue;
   var result=new Result{Id=game.Id,Cover="",Banner=""};
   if(epicCover.Contains(game.Id))game.Cover=found.First(x=>x.Id==game.Id).Cover;
   if(game.Cover=="")result.Cover=Local(id,true,roots);
   if(!game.Missing&&game.Banner=="")result.Banner=Local(id,false,roots);
   if(result.Cover!=""||result.Banner!="")found.Add(result);
   if((game.Cover!=""||result.Cover!="")&&(game.Missing||game.Banner!=""||result.Banner!=""))continue;
   List<Tuple<Game,Result>> group;
   if(!remote.TryGetValue(id,out group)){group=new List<Tuple<Game,Result>>();remote[id]=group;}
   group.Add(Tuple.Create(game,result));
  }
  var ids=remote.Keys.ToArray();
  for(int offset=0;offset<ids.Length;offset+=40){
   var batch=ids.Skip(offset).Take(40).ToArray();Dictionary<string,Dictionary<string,object>> metadata;
   try{metadata=Metadata(batch);}catch(WebException){metadata=new Dictionary<string,Dictionary<string,object>>();}catch(ArgumentException){metadata=new Dictionary<string,Dictionary<string,object>>();}catch(IOException){metadata=new Dictionary<string,Dictionary<string,object>>();}
   Parallel.ForEach(batch,new ParallelOptions{MaxDegreeOfParallelism=4},id=>{
    var group=remote[id];Dictionary<string,object> data;if(!metadata.TryGetValue(id,out data))data=new Dictionary<string,object>();
    var cover=group.Any(x=>x.Item1.Cover==""&&x.Item2.Cover=="")?Download(new[]{Url(id,data,"library_capsule_2x"),Url(id,data,"library_capsule"),Cdn+id+"/library_600x900.jpg","https://cdn.cloudflare.steamstatic.com/steam/apps/"+id+"/library_600x900.jpg"},true):"";
    var banner=group.Any(x=>!x.Item1.Missing&&x.Item1.Banner==""&&x.Item2.Banner=="")?Download(new[]{Url(id,data,"library_hero"),Cdn+id+"/library_hero.jpg"},false):"";
    foreach(var entry in group){
     var result=new Result{Id=entry.Item1.Id,Cover=entry.Item1.Cover==""&&entry.Item2.Cover==""?cover:"",Banner=!entry.Item1.Missing&&entry.Item1.Banner==""&&entry.Item2.Banner==""?banner:""};
     if(result.Cover==""&&result.Banner=="")continue;
     lock(found)found.Add(result);

    }
   });
  }
  foreach(var game in games.Where(InstalledArt.Supported)){
   var art=InstalledArt.Find(game);
   if(art.Cover!=""||art.Banner!=""||art.Icon!="")found.Insert(0,art);
  }
  return found;
 }
}




