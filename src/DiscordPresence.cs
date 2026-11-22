using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

sealed class DiscordPresence : IDisposable {
 internal const string ApplicationId="1573722235695276074";
 internal sealed class Activity {
  internal readonly string Title, Platform, CoverUrl;
  internal readonly DateTime Started;
  internal Activity(Game game,DateTime started){
   Title=Field(game.Name,"Jogo");Platform=Field(game.Platform,"Windows");CoverUrl=ImageUrl(game.DiscordCoverUrl);Started=started;
  }
 }

 static readonly int processId=GetProcessId();
 static readonly DateTime epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
 Task previous=Task.FromResult(0);
 CancellationTokenSource cancellation;
 Activity current;

 static int GetProcessId(){using(var process=Process.GetCurrentProcess())return process.Id;}
 internal static string ImageUrl(string value){
  Uri url;
  if(!Uri.TryCreate(value,UriKind.Absolute,out url)||url.Scheme!=Uri.UriSchemeHttps||url.UserInfo!=""||url.IsLoopback||
     !url.IsDefaultPort||url.HostNameType!=UriHostNameType.Dns||!url.Host.Contains(".")||url.AbsoluteUri.Length>2048)return "";
  return url.AbsoluteUri;
 }
 static string Field(string value,string fallback){
  var clean=new string((value??"").Where(c=>!Char.IsControl(c)).ToArray()).Trim();
  if(clean.Length==0)return fallback;
  var text=new StringBuilder();
  var remaining=122;
  var elements=StringInfo.GetTextElementEnumerator(clean);
  while(elements.MoveNext()){
   string element=elements.GetTextElement();
   int bytes=Encoding.UTF8.GetByteCount(element);
   if(bytes>remaining){if(text.Length>0)text.Append("…");break;}
   remaining-=bytes;text.Append(element);
  }
  return text.Length==0?fallback:text.ToString();
 }

 internal void Set(Activity activity){
  if(ReferenceEquals(activity,current))return;
  current=activity;
  if(cancellation!=null){
   var old=cancellation;old.Cancel();
   previous.ContinueWith(ignored=>old.Dispose(),TaskScheduler.Default);
  }
  if(activity==null){cancellation=null;return;}
  var next=new CancellationTokenSource();
  cancellation=next;
  previous=previous.ContinueWith(async ignored=>{
   if(!next.IsCancellationRequested)await Run(activity,next.Token).ConfigureAwait(false);
  },TaskScheduler.Default).Unwrap();
 }
 public void Dispose(){Set(null);}

 static async Task Run(Activity activity,CancellationToken cancellation){
  var serializer=new JavaScriptSerializer();
  while(!cancellation.IsCancellationRequested){
   try{
    using(var pipe=Connect(cancellation)){
     if(pipe!=null){
      await WithTimeout(pipe,Write(pipe,0,serializer.Serialize(new {v=1,client_id=ApplicationId})),1800,cancellation).ConfigureAwait(false);
      var ready=await WithTimeout(pipe,Read(pipe),1800,cancellation).ConfigureAwait(false);
      if(ready.Code==1){
       var response=serializer.Deserialize<Dictionary<string,object>>(ready.Body);
       if(Value(response,"evt")=="READY"&&!cancellation.IsCancellationRequested)
        await Publish(pipe,serializer,activity,cancellation).ConfigureAwait(false);
      }
     }
    }
   }catch(IOException){}catch(TimeoutException){}catch(InvalidOperationException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(OperationCanceledException){}
   try{await Task.Delay(4500,cancellation).ConfigureAwait(false);}catch(OperationCanceledException){return;}
  }
 }

 static NamedPipeClientStream Connect(CancellationToken cancellation){
  for(int index=0;index<10&&!cancellation.IsCancellationRequested;index++){
   var pipe=new NamedPipeClientStream(".","discord-ipc-"+index,PipeDirection.InOut,PipeOptions.Asynchronous,System.Security.Principal.TokenImpersonationLevel.Anonymous);
   try{pipe.Connect(70);return pipe;}
   catch(TimeoutException){pipe.Dispose();}
   catch(UnauthorizedAccessException){pipe.Dispose();}
   catch{pipe.Dispose();throw;}
  }
  return null;
 }
 static async Task Publish(Stream pipe,JavaScriptSerializer serializer,Activity activity,CancellationToken cancellation){
  await WithTimeout(pipe,Write(pipe,1,Payload(serializer,activity)),1800,cancellation).ConfigureAwait(false);
  int attempt=0;
  Task refresh=Task.Delay(4500,cancellation);
  var stopped=new TaskCompletionSource<bool>();
  using(cancellation.Register(()=>stopped.TrySetResult(true))){
   while(true){
    var receive=Read(pipe);
    if(refresh!=null&&await Task.WhenAny(receive,stopped.Task,refresh).ConfigureAwait(false)==refresh){
     refresh=null;
     if(!cancellation.IsCancellationRequested)
      await WithTimeout(pipe,Write(pipe,1,Payload(serializer,activity,attempt)),1800,cancellation).ConfigureAwait(false);
    }
    if(await Task.WhenAny(receive,stopped.Task).ConfigureAwait(false)==stopped.Task){
     try{await WithTimeout(pipe,Write(pipe,1,Payload(serializer,null)),350,CancellationToken.None).ConfigureAwait(false);}
     finally{Observe(receive);}
     return;
    }
    var packet=await receive.ConfigureAwait(false);
    if(packet.Code==2)return;
    if(packet.Code==3)await Write(pipe,4,packet.Body).ConfigureAwait(false);
    if(packet.Code!=1)continue;
    var response=serializer.Deserialize<Dictionary<string,object>>(packet.Body);
    if(Value(response,"evt")!="ERROR"||Value(response,"cmd")!="SET_ACTIVITY")continue;
    if(attempt==2)throw new IOException("O Discord recusou a atividade.");
    attempt=attempt==0&&activity.CoverUrl!=""?1:2;
    await WithTimeout(pipe,Write(pipe,1,Payload(serializer,activity,attempt)),1800,cancellation).ConfigureAwait(false);
   }
  }
 }
 static void Observe(Task task){
  task.ContinueWith(failed=>{GC.KeepAlive(failed.Exception);},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
 }
 static async Task<T> WithTimeout<T>(Stream pipe,Task<T> task,int milliseconds,CancellationToken token){
  if(await Task.WhenAny(task,Task.Delay(milliseconds,token)).ConfigureAwait(false)!=task){
   Observe(task);pipe.Dispose();throw new TimeoutException();
  }
  return await task.ConfigureAwait(false);
 }
 internal static string Payload(JavaScriptSerializer serializer,Activity activity,int attempt=0){
  object value=null;
  if(activity!=null){
   var fields=new Dictionary<string,object>{
    {"type",0},{"details",activity.Title},{"state",activity.Platform},{"status_display_type",2},
    {"timestamps",new {start=(long)(activity.Started.ToUniversalTime()-epoch).TotalSeconds}}
   };
   if(attempt<2)fields["name"]=activity.Title;
   if(attempt==0&&activity.CoverUrl!="")fields["assets"]=new {large_image=activity.CoverUrl,large_text=activity.Title};
   value=fields;
  }
  return serializer.Serialize(new {cmd="SET_ACTIVITY",args=new {pid=processId,activity=value},nonce=Guid.NewGuid().ToString("N")});
 }
 static string Value(Dictionary<string,object> response,string key){
  object value;return response!=null&&response.TryGetValue(key,out value)?value as string:"";
 }
 internal sealed class Packet {
  internal readonly int Code;internal readonly string Body;
  internal Packet(int code,string body){Code=code;Body=body;}
 }
 internal static async Task<bool> Write(Stream pipe,int code,string payload){
  byte[] text=Encoding.UTF8.GetBytes(payload),frame=new byte[text.Length+8];
  Buffer.BlockCopy(BitConverter.GetBytes(code),0,frame,0,4);
  Buffer.BlockCopy(BitConverter.GetBytes(text.Length),0,frame,4,4);
  Buffer.BlockCopy(text,0,frame,8,text.Length);
  await pipe.WriteAsync(frame,0,frame.Length).ConfigureAwait(false);
  return true;
 }
 internal static async Task<Packet> Read(Stream pipe){
  byte[] header=new byte[8];
  await ReadAll(pipe,header).ConfigureAwait(false);
  int length=BitConverter.ToInt32(header,4);
  if(length<0||length>1024*1024)throw new InvalidDataException("Quadro IPC inválido.");
  var bytes=new byte[length];await ReadAll(pipe,bytes).ConfigureAwait(false);
  return new Packet(BitConverter.ToInt32(header,0),Encoding.UTF8.GetString(bytes));
 }
 static async Task ReadAll(Stream pipe,byte[] bytes){
  for(int offset=0;offset<bytes.Length;){
   int count=await pipe.ReadAsync(bytes,offset,bytes.Length-offset).ConfigureAwait(false);
   if(count==0)throw new EndOfStreamException();
   offset+=count;
  }
 }
}

partial class Hub {
 readonly DiscordPresence discordPresence=new DiscordPresence();
 readonly Dictionary<string,DiscordPresence.Activity> discordSessions=new Dictionary<string,DiscordPresence.Activity>();

 void StartDiscordSession(Game game,DateTime start){
  if(game.Rom==""&&!IsCustom(game))return;
  discordSessions[game.Id]=new DiscordPresence.Activity(game,start);
  UpdateDiscordPresence();
 }
 void EndDiscordSession(Game game){
  if(discordSessions.Remove(game.Id))UpdateDiscordPresence();
 }
 void UpdateDiscordPresence(){
  discordPresence.Set(data.DiscordEnabled?
   discordSessions.Values.OrderByDescending(activity=>activity.Started).FirstOrDefault():null);
 }

 internal static void TestDiscordPresence(){
  const string customName="Majora's Mask 3D (Meu jogo)";
  var rom=new DiscordPresence.Activity(new Game{Name=customName,Rom="zelda.3ds",Target="azahar.exe"},DateTime.Now);
  if(rom.Title!=customName)throw new InvalidDataException("Título salvo da ROM no Discord.");
  var json=new JavaScriptSerializer();
  var activity=new DiscordPresence.Activity(new Game{Name="Pokémon Emerald",Platform="Game Boy Advance",DiscordCoverUrl="https://example.com/cover.png"},new DateTime(2026,11,21,10,0,0,DateTimeKind.Utc));
  using(var buffer=new MemoryStream()){
   DiscordPresence.Write(buffer,1,DiscordPresence.Payload(json,activity)).GetAwaiter().GetResult();
   buffer.Position=0;
   var packet=DiscordPresence.Read(buffer).GetAwaiter().GetResult();
   var args=(Dictionary<string,object>)json.Deserialize<Dictionary<string,object>>(packet.Body)["args"];
   var values=(Dictionary<string,object>)args["activity"];
   var assets=(Dictionary<string,object>)values["assets"];
   if((string)assets["large_image"]!="https://example.com/cover.png")throw new InvalidDataException("Capa Discord.");
   if(packet.Code!=1||(string)values["name"]!="Pokémon Emerald"||(string)values["details"]!="Pokémon Emerald"||(int)values["status_display_type"]!=2)
    throw new InvalidDataException("Atividade Discord.");
  }
  var alternate=json.Deserialize<Dictionary<string,object>>(DiscordPresence.Payload(json,activity,2));
  var alternateFields=(Dictionary<string,object>)((Dictionary<string,object>)alternate["args"])["activity"];
  if(alternateFields.ContainsKey("assets")||(int)alternateFields["status_display_type"]!=2||(string)alternateFields["details"]!="Pokémon Emerald")
   throw new InvalidDataException("Compatibilidade Discord.");
  var cleared=(Dictionary<string,object>)json.Deserialize<Dictionary<string,object>>(DiscordPresence.Payload(json,null))["args"];
  if(cleared["activity"]!=null||DiscordPresence.ApplicationId!="1573722235695276074"||DiscordPresence.ImageUrl("file:///C:/rom.png")!="")throw new InvalidDataException("Configuração Discord.");
 }
}