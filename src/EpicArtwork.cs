using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

static class EpicArtwork {
 static string Known(string id){
  if(id=="3240220")return "https://store-images.s-microsoft.com/image/apps.34539.13827124301078315.8451c006-d969-493e-b6bd-27c25b136528.934f20c2-9f4d-467a-9f30-7d3a14f2b26b";
  if(id=="271590")return "https://store-images.s-microsoft.com/image/apps.8135.66515090704019777.7fa547c1-c211-4229-a4d3-3ceef762e0a4.0bb0ac0a-9d63-4d91-8e53-f2e39a040bcd";
  return "";
 }
 static string Slug(string name){
  var plain=new string(name.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark&&c!='™'&&c!='®').ToArray());
  return Regex.Replace(plain.ToLowerInvariant(),"[^a-z0-9]+","-").Trim('-');
 }
 static bool Allowed(string link){
  Uri uri;
  if(!Uri.TryCreate(link,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo!="")return false;
  return uri.Host=="cdn1.epicgames.com"||uri.Host=="cdn2.epicgames.com"||uri.Host=="media-cdn.epicgames.com"||uri.Host=="cdn2.unrealengine.com";
 }
 static IEnumerable<Dictionary<string,object>> Objects(object data){
  var obj=data as Dictionary<string,object>;
  if(obj!=null){
   yield return obj;
   foreach(var child in obj.Values)foreach(var nested in Objects(child))yield return nested;
  }else{
   var list=data as object[];
   if(list!=null)foreach(var child in list)foreach(var nested in Objects(child))yield return nested;
  }
 }
 static string Value(Dictionary<string,object> data,string key){object value;return data.TryGetValue(key,out value)?value as string??"":"";}
 internal static IEnumerable<string> Portraits(string json,string title){
  var serializer=new JavaScriptSerializer{MaxJsonLength=4*1024*1024,RecursionLimit=65};
  var objects=Objects(serializer.DeserializeObject(json)).ToArray();
  var normalized=Slug(title);
  if(!objects.Any(o=>new[]{"title","productName","displayName"}.Any(key=>Slug(Value(o,key))==normalized)))return new string[0];
  return objects.SelectMany(o=>{
   var urls=new List<string>();
   var type=Value(o,"type");
   if(type=="DieselGameBoxTall"||type=="OfferImageTall"||type=="Portrait")urls.Add(Value(o,"url"));
   foreach(var pair in o)if(pair.Key.Equals("portraitImage",StringComparison.OrdinalIgnoreCase)||pair.Key.Equals("keyImageTall",StringComparison.OrdinalIgnoreCase))urls.Add(pair.Value as string??"");
   return urls;
  }).Where(Allowed).Distinct().Take(5);
 }
 public static string Find(Game game){
  var fallback=Known(Artwork.AppId(game));
  if(fallback!=""){
   var id=Artwork.AppId(game);
   var direct="https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/"+id+"/library_600x900.jpg";
   var saved=Artwork.Download(new[]{direct,fallback+"?w=720"},true);
   if(saved=="")saved=Artwork.Download(new[]{fallback+"?w=720",fallback},false);
   if(saved!="")return saved;
  }
  var slug=Slug(game.Name);
  if(slug.Length<2||slug.Length>120)return "";
  try{
   var url="https://store-content.ak.epicgames.com/api/en-US/content/products/"+slug;
   var json=Encoding.UTF8.GetString(Artwork.Fetch(url,3*1024*1024));
   return Artwork.Download(Portraits(json,game.Name),true);
  }catch(WebException){}catch(ArgumentException){}catch(InvalidOperationException){}catch(IOException){}
  return "";
 }
}

