using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

static class OwnedGames {
 internal static List<Game> Scan(List<string> errors){
  var games=new List<Game>();
  try{
   foreach(var root in Stores.SteamRoots()){
    var cache=Path.Combine(root,"appcache","librarycache");
    var metadata=Path.Combine(root,"appcache","appinfo.vdf");
    if(!Directory.Exists(cache)||!File.Exists(metadata))continue;
    var ids=new HashSet<uint>();
    foreach(var directory in Directory.EnumerateDirectories(cache)){
     uint id;
     if(UInt32.TryParse(Path.GetFileName(directory),out id)&&id>0)ids.Add(id);
    }
    if(ids.Count==0)continue;
    using(var file=File.Open(metadata,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
    using(var reader=new BinaryReader(file,Encoding.UTF8))games.AddRange(ReadSteam(reader,ids));
    break;
   }
  }catch(Exception){errors.Add("Steam");}
  try{
   var database=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"GOG.com\Galaxy\storage\galaxy-2.0.db");
   if(File.Exists(database))games.AddRange(ReadGalaxy(database));
  }catch(Exception){errors.Add("GOG Galaxy");}
  try{games.AddRange(RiotGames.Available());}catch(Exception){errors.Add("Riot Games");}
  return games;
 }

 internal static int Add(Library library,IEnumerable<Game> games){
  var keys=new HashSet<string>(library.Games.Select(g=>g.StoreId),StringComparer.OrdinalIgnoreCase);
  int count=0;
  foreach(var game in games){
   if(!keys.Add(game.StoreId))continue;
   library.Games.Add(game);
   count++;
  }
  return count;
 }

 internal static List<Game> ReadSteam(BinaryReader reader,HashSet<uint> ids){
  var result=new List<Game>();
  var stream=reader.BaseStream;
  uint format=reader.ReadUInt32();
  reader.ReadUInt32();
  long end=stream.Length;
  string[] keys=null;
  if(format==0x07564429){
   end=reader.ReadInt64();
   if(end<16||end>stream.Length-4)throw new InvalidDataException("Tabela Steam inválida.");
   stream.Position=end;
   int count=reader.ReadInt32();
   if(count<0||count>1000000||count>stream.Length-stream.Position)throw new InvalidDataException("Tabela Steam inválida.");
   keys=new string[count];
   for(int index=0;index<count;index++)keys[index]=ReadString(reader,stream.Length,false);
   stream.Position=16;
  }else if(format!=0x07564428)throw new InvalidDataException("Formato Steam desconhecido.");

  while(stream.Position<=end-4){
   uint id=reader.ReadUInt32();
   if(id==0)break;
   uint size=reader.ReadUInt32();
   long next=stream.Position+size;
   if(size<60||next>end)throw new InvalidDataException("Registro Steam inválido.");
   if(ids.Contains(id)){
    stream.Position+=60;
    string name="",type="";
    try{ReadSteamObject(reader,next,keys,0,"",ref name,ref type);}
    catch(InvalidDataException){name="";}
    if(!String.IsNullOrWhiteSpace(name)&&String.Equals(type,"game",StringComparison.OrdinalIgnoreCase))
     result.Add(new Game{Name=name,Source="Steam",StoreId="steam:"+id,Target="steam://run/"+id,Missing=true});
   }
   stream.Position=next;
  }
  return result;
 }
 static void ReadSteamObject(BinaryReader reader,long end,string[] keys,int depth,string path,ref string name,ref string type){
  if(depth>48)throw new InvalidDataException();
  while(reader.BaseStream.Position<end){
   byte kind=reader.ReadByte();
   if(kind==8)return;
   string key;
   if(keys==null)key=ReadString(reader,end,false);
   else{
    if(end-reader.BaseStream.Position<4)throw new InvalidDataException();
    uint index=reader.ReadUInt32();
    if(index>=keys.Length)throw new InvalidDataException();
    key=keys[index];
   }
   if(kind==0){
    ReadSteamObject(reader,end,keys,depth+1,path+"/"+key,ref name,ref type);
    if(path=="/appinfo"&&key=="common")return;
   }else if(kind==1||kind==5){
    var value=ReadString(reader,end,kind==5);
    if(path=="/appinfo/common"){
     if(key=="name")name=value;
     if(key=="type")type=value;
    }
   }else if(kind==2||kind==3||kind==4||kind==6||kind==7){
    reader.BaseStream.Position+=kind==7?8:4;
    if(reader.BaseStream.Position>end)throw new InvalidDataException();
   }else throw new InvalidDataException();
  }
 }
 static string ReadString(BinaryReader reader,long end,bool wide){
  var bytes=new List<byte>();
  int width=wide?2:1;
  while(end-reader.BaseStream.Position>=width&&bytes.Count<65536){
   byte first=reader.ReadByte(),second=wide?reader.ReadByte():(byte)0;
   if(first==0&&second==0)return (wide?Encoding.Unicode:Encoding.UTF8).GetString(bytes.ToArray());
   bytes.Add(first);
   if(wide)bytes.Add(second);
  }
  throw new InvalidDataException();
 }

 const string GalaxyQuery=@"SELECT DISTINCT p.releaseKey,p.value
 FROM ProductPurchaseDates d
 JOIN GamePieces p ON p.releaseKey=d.gameReleaseKey
 JOIN GamePieceTypes t ON t.id=p.gamePieceTypeId
 WHERE t.type='title' AND (p.releaseKey GLOB 'gog_*' OR p.releaseKey GLOB 'origin_*' OR p.releaseKey GLOB 'battlenet_*')
 AND p.releaseKey NOT IN (SELECT p2.releaseKey FROM GamePieces p2 JOIN GamePieceTypes t2 ON t2.id=p2.gamePieceTypeId
  WHERE t2.type='gameType' AND lower(trim(p2.value,char(34))) IN ('dlc','addon','add-on'))
 AND p.releaseKey NOT IN (
  SELECT 'gog_' || CAST(j.value AS TEXT) FROM GamePieces dlc
  JOIN GamePieceTypes dt ON dt.id=dlc.gamePieceTypeId AND dt.type='dlcs'
  JOIN json_each(CASE WHEN json_valid(dlc.value) THEN dlc.value ELSE '{}' END,'$.dlcs') j
 )
 AND p.releaseKey NOT IN (
  SELECT CAST(j.value AS TEXT) FROM GamePieces dlc
  JOIN GamePieceTypes dt ON dt.id=dlc.gamePieceTypeId AND dt.type='dlcs'
  JOIN json_each(CASE WHEN json_valid(dlc.value) THEN dlc.value ELSE '{}' END,'$.dlcs') j
 )";
 internal static List<Game> ReadGalaxy(string path){
  IntPtr database=IntPtr.Zero,statement=IntPtr.Zero;
  var games=new List<Game>();
  try{
   if(sqlite3_open_v2(Utf8(path),out database,1,IntPtr.Zero)!=0)throw new IOException("Biblioteca Galaxy indisponível.");
   sqlite3_busy_timeout(database,1500);
   if(sqlite3_prepare_v2(database,Utf8(GalaxyQuery),-1,out statement,IntPtr.Zero)!=0)throw new IOException("Formato da biblioteca Galaxy desconhecido.");
   var json=new JavaScriptSerializer();
   int status;
   while((status=sqlite3_step(statement))==100){
    string release=Column(statement,0);

    string name;
    try{
     var value=json.Deserialize<Dictionary<string,string>>(Column(statement,1));
     if(value==null||!value.TryGetValue("title",out name)||String.IsNullOrWhiteSpace(name))continue;
    }catch(ArgumentException){continue;}catch(InvalidOperationException){continue;}
    var game=GalaxyOwnedGame(release,name);
    if(game!=null)games.Add(game);
   }
   if(status!=101)throw new IOException("Leitura da biblioteca Galaxy interrompida.");
   return games;
  }finally{
   if(statement!=IntPtr.Zero)sqlite3_finalize(statement);
   if(database!=IntPtr.Zero)sqlite3_close(database);
  }
 }
 internal static Game GalaxyOwnedGame(string release,string name){
  var match=Regex.Match(release??"", @"^(gog|origin|battlenet)_([a-zA-Z0-9:_-]+)$");
  if(!match.Success||String.IsNullOrWhiteSpace(name))return null;
  var platform=match.Groups[1].Value;var id=match.Groups[2].Value;
  if(platform=="gog"&&!Regex.IsMatch(id,@"^\d+$"))return null;
  return new Game{Name=name,Source=platform=="gog"?"GOG":platform=="origin"?"EA app":"Battle.net",
   StoreId=platform+":"+id,Missing=true};
 }
 internal static void ReadGalaxyTimes(string path,Playtime times){
  const string query=@"SELECT gt.releaseKey, MAX(gt.minutesInGame),
   (SELECT p.value FROM GamePieces p JOIN GamePieceTypes t ON t.id=p.gamePieceTypeId
    WHERE p.releaseKey=gt.releaseKey AND t.type='title' LIMIT 1)
   FROM GameTimes gt WHERE gt.minutesInGame>0 GROUP BY gt.releaseKey";
  IntPtr database=IntPtr.Zero,statement=IntPtr.Zero;
  try{
   if(sqlite3_open_v2(Utf8(path),out database,1,IntPtr.Zero)!=0)return;
   sqlite3_busy_timeout(database,1500);
   if(sqlite3_prepare_v2(database,Utf8(query),-1,out statement,IntPtr.Zero)!=0)return;
   var json=new JavaScriptSerializer();
   while(sqlite3_step(statement)==100){
    uint minutes;
    if(!UInt32.TryParse(Column(statement,1),out minutes)||minutes==0)continue;
    string title="",value=Column(statement,2);
    if(value!="")try{
     var metadata=json.Deserialize<Dictionary<string,string>>(value);
     if(metadata!=null)metadata.TryGetValue("title",out title);
    }catch(ArgumentException){}catch(InvalidOperationException){}
    times.AddGalaxy(Column(statement,0),title,minutes*60L);
   }
  }finally{
   if(statement!=IntPtr.Zero)sqlite3_finalize(statement);
   if(database!=IntPtr.Zero)sqlite3_close(database);
  }
 }
 static byte[] Utf8(string text){return Encoding.UTF8.GetBytes(text+"\0");}
 static string Column(IntPtr statement,int index){
  var pointer=sqlite3_column_text(statement,index);
  int length=sqlite3_column_bytes(statement,index);
  if(pointer==IntPtr.Zero||length<=0||length>1024*1024)return "";
  var bytes=new byte[length];Marshal.Copy(pointer,bytes,0,length);
  return Encoding.UTF8.GetString(bytes);
 }
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] file,out IntPtr db,int flags,IntPtr vfs);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_busy_timeout(IntPtr db,int milliseconds);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int size,out IntPtr statement,IntPtr tail);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_step(IntPtr statement);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_column_text(IntPtr statement,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_column_bytes(IntPtr statement,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_finalize(IntPtr statement);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
}
