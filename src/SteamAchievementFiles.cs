using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

static class SteamAchievementFiles {
 internal static AchievementProgress Progress(string folder,string account,string app){
  try{
   var user=At(Read(Path.Combine(folder,"UserGameStats_"+account+"_"+app+".bin")),"cache");
   var schema=At(At(Read(Path.Combine(folder,"UserGameStatsSchema_"+app+".bin")),app),"stats");
   if(user==null||schema==null)return null;
   var result=new AchievementProgress();
   foreach(var type in schema){
    var bits=At(type.Value as Dictionary<string,object>,"bits");
    if(bits==null)continue;
    var saved=At(user,type.Key);
    var flags=Get(saved,"data") is uint?(uint)Get(saved,"data"):0;
    foreach(var element in bits){
     int bit;
     if(!Int32.TryParse(element.Key,out bit)||bit<0||bit>=32)continue;
     var values=element.Value as Dictionary<string,object>;
     var display=At(values,"display");
     if(display==null)continue;
     bool unlocked=(flags&(1u<<bit))!=0;
     bool hidden=!unlocked&&Convert.ToString(Get(display,"hidden"),CultureInfo.InvariantCulture)=="1";
     string name=Localized(display,"name"),description=Localized(display,"desc");
     if(String.IsNullOrWhiteSpace(name))continue;
     var image=Get(display,unlocked?"icon":"icon_gray") as string??"";
     var icon=!hidden&&System.Text.RegularExpressions.Regex.IsMatch(image,@"^[0-9a-fA-F]{20,64}\.(?:png|jpg|jpeg)$")
      ?"https://shared.steamstatic.com/community_assets/images/apps/"+app+"/"+image:"";
     result.Entries.Add(new AchievementEntry{Id=app+":"+type.Key+":"+bit,Name=hidden?"Conquista secreta":name,
      Description=hidden?"Os detalhes aparecem depois de desbloquear.":description,Icon=icon,Hidden=hidden,Unlocked=unlocked});
    }
   }
   result.Total=result.Entries.Count;result.Unlocked=result.Entries.Count(x=>x.Unlocked);
   return result.Total>0?result:null;
  }catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
 }
 static string Localized(Dictionary<string,object> display,string key){
  var text=Get(display,key);
  var localized=text as Dictionary<string,object>;
  if(localized==null)return text as string??"";
  return Get(localized,"brazilian") as string??Get(localized,"portuguese") as string??Get(localized,"english") as string??localized.Values.OfType<string>().FirstOrDefault()??"";
 }
 internal static Dictionary<string,object> Read(string filename){
  var file=new FileInfo(filename);
  if(!file.Exists||file.Length>8*1024*1024)return null;
  using(var stream=File.Open(filename,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
  using(var reader=new BinaryReader(stream,Encoding.UTF8))return ReadNode(reader,0);
 }
 static Dictionary<string,object> ReadNode(BinaryReader reader,int depth){
  if(depth>40)throw new InvalidDataException("Steam KeyValues.");
  var node=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
  while(true){
   byte type=reader.ReadByte();
   if(type==8)return node;
   string key=ReadString(reader);
   object value;
   switch(type){
    case 0:value=ReadNode(reader,depth+1);break;
    case 1:value=ReadString(reader);break;
    case 2:value=reader.ReadUInt32();break;
    case 3:value=reader.ReadSingle();break;
    case 4:case 6:value=reader.ReadUInt32();break;
    case 7:value=reader.ReadUInt64();break;
    default:throw new InvalidDataException("Tipo Steam KeyValues.");
   }
   node[key]=value;
  }
 }
 static string ReadString(BinaryReader reader){
  var bytes=new List<byte>();
  while(bytes.Count<65536){
   byte value=reader.ReadByte();if(value==0)return Encoding.UTF8.GetString(bytes.ToArray());
   bytes.Add(value);
  }
  throw new InvalidDataException("Texto Steam KeyValues.");
 }
 internal static Dictionary<string,object> At(Dictionary<string,object> data,string key){return Get(data,key) as Dictionary<string,object>;}
 internal static object Get(Dictionary<string,object> data,string key){object value;return data!=null&&data.TryGetValue(key,out value)?value:null;}
}