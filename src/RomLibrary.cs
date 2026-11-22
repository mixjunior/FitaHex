using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static class RomLibrary {
 sealed class Preset {
  public string Executable, Name, Platform, Formats, Arguments;
  public Preset(string exe,string name,string platform,string formats,string arguments="\"{rom_path}\""){
   Executable=exe;Name=name;Platform=platform;Formats=formats;Arguments=arguments;
  }
 }
 internal const string Positional="\"{rom_path}\"";
 const string Nintendo3dsFormats="3ds,cci,cxi,3dsx,zcci,zcxi,z3dsx,bcci,bcxi";
 static readonly Preset[] Presets={
  new Preset("azahar","Azahar","Nintendo 3DS",Nintendo3dsFormats),
  new Preset("citra","Citra","Nintendo 3DS",Nintendo3dsFormats),
  new Preset("lime3ds","Lime3DS","Nintendo 3DS",Nintendo3dsFormats),
  new Preset("shadps4","shadPS4","PlayStation 4","elf,eboot.bin"),
  new Preset("kytyps5","KytyPS5","PlayStation 5","elf,eboot.bin"),
  new Preset("sameboy","SameBoy","Game Boy / GBC","gb,gbc"),
  new Preset("visualboyadvance","VisualBoyAdvance","Game Boy / GBA","gba,gb,gbc,zip,7z"),
  new Preset("bsnes","bsnes","Super Nintendo","sfc,smc,fig"),
  new Preset("fceux","FCEUX","Nintendo NES","nes,unf,fds,zip"),
  new Preset("nestopia","Nestopia","Nintendo NES","nes,unf,fds,zip"),
  new Preset("dolphin","Dolphin","GameCube / Wii","iso,gcm,gcz,rvz,wbfs,wia,ciso,wad,dol,elf","-b -e \"{rom_path}\""),
  new Preset("pcsx2","PCSX2","PlayStation 2","iso,chd,cso,zso,gz,cue,elf,m3u","-batch \"{rom_path}\""),
  new Preset("duckstation","DuckStation","PlayStation","cue,chd,pbp,iso,img,ecm,m3u,ps-exe","-batch -- \"{rom_path}\""),
  new Preset("ppsspp","PPSSPP","PSP","iso,cso,chd,pbp,prx,elf"),
  new Preset("rpcs3","RPCS3","PlayStation 3","self,eboot.bin"),
  new Preset("mgba","mGBA","Game Boy / GBA","gba,gb,gbc,zip,7z"),
  new Preset("melon","melonDS","Nintendo DS","nds,dsi"),
  new Preset("desmume","DeSmuME","Nintendo DS","nds"),
  new Preset("project64","Project64","Nintendo 64","z64,n64,v64,zip"),
  new Preset("simple64","simple64","Nintendo 64","z64,n64,v64"),
  new Preset("mupen64","Mupen64Plus","Nintendo 64","z64,n64,v64"),
  new Preset("snes9x","Snes9x","Super Nintendo","smc,sfc,fig,zip"),
  new Preset("mesen","Mesen","Consoles retrô","nes,unf,fds,sfc,smc,gb,gbc,gba,pce,sgx,sms,gg"),
  new Preset("pcsx","PCSX","PlayStation","cue,chd,pbp,iso,img"),
  new Preset("xemu","xemu","Xbox","iso,xiso","-dvd_path \"{rom_path}\""),
  new Preset("xenia","Xenia","Xbox 360","iso,xex"),
  new Preset("cemu","Cemu","Wii U","wua,wux,wud,rpx","-g \"{rom_path}\""),
  new Preset("ryujinx","Ryujinx","Nintendo Switch","xci,nsp,nro,nca"),
  new Preset("yuzu","yuzu","Nintendo Switch","xci,nsp,nro","-g \"{rom_path}\""),
  new Preset("citron","Citron","Nintendo Switch","xci,nsp,nro","-g \"{rom_path}\"")
 };
 internal const string CommonFormats="nes,unf,unif,fds,sfc,smc,fig,st,bs,gba,gbc,gb,nds,dsi,z64,n64,v64,iso,chd,cso,zso,cue,pbp,img,mds,ccd,ecm,gdi,cdi,rvz,gcm,wbfs,gcz,wia,ciso,xci,nsp,nro,wua,wux,wud,rpx,gen,md,smd,32x,sms,gg,sg,pce,sgx,a26,a52,a78,lnx,j64,jag,ngp,ngc,ws,wsc,vec,vb,col,int,rom,d64,t64,prg,adf,adz,ipf,tap,tzx,dsk,xex,self,eboot.bin,zip,7z,"+Nintendo3dsFormats;
 internal static Emulator Detect(string executable){
  var name=Path.GetFileNameWithoutExtension(executable);
  var preset=Presets.FirstOrDefault(x=>name.StartsWith(x.Executable,StringComparison.OrdinalIgnoreCase));
  return new Emulator{
   Target=executable,Working=Path.GetDirectoryName(executable),
   Name=preset==null?name:preset.Name,Platform=preset==null?"Emulador":preset.Platform,
   Args=preset==null?Positional:preset.Arguments,Extensions=preset==null?CommonFormats:preset.Formats
  };
 }
 internal static Emulator ImportProfile(Emulator saved){
  var fallback=Detect(saved.Target);
  bool retro=RetroArch(saved.Target);
  var formats=String.IsNullOrWhiteSpace(saved.Extensions)?fallback.Extensions:
   retro?saved.Extensions:String.Join(",",(saved.Extensions+","+fallback.Extensions).Split(new[]{','},StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase));
  return new Emulator{
   Target=fallback.Target,Name=fallback.Platform!="Emulador"?fallback.Name:(String.IsNullOrWhiteSpace(saved.Name)?fallback.Name:saved.Name),
   Working=String.IsNullOrWhiteSpace(saved.Working)?fallback.Working:saved.Working,
   Platform=fallback.Platform!="Emulador"?fallback.Platform:(String.IsNullOrWhiteSpace(saved.Platform)?fallback.Platform:saved.Platform),
   Args=String.IsNullOrWhiteSpace(saved.Args)?fallback.Args:saved.Args,Extensions=formats,Folder=saved.Folder
  };
 }
 internal static bool RetroArch(string executable){return Path.GetFileNameWithoutExtension(executable).StartsWith("retroarch",StringComparison.OrdinalIgnoreCase);}
 internal static string CoreFormats(string corePath){
  var root=Path.GetDirectoryName(Path.GetDirectoryName(corePath));
  if(String.IsNullOrEmpty(root))return "";
  var info=Path.Combine(root,"info",Path.GetFileNameWithoutExtension(corePath)+".info");
  if(!File.Exists(info))return "";
  var value=Regex.Match(File.ReadAllText(info),@"(?m)^\s*supported_extensions\s*=\s*""([^""]+)""");
  return value.Success?value.Groups[1].Value.Replace('|',','):"";
 }
 static HashSet<string> Extensions(string names){
  return new HashSet<string>((names??"").Split(new[]{',',';','|',' '},StringSplitOptions.RemoveEmptyEntries)
   .Select(x=>x.Trim().TrimStart('*','.')).Where(x=>Regex.IsMatch(x,@"^[a-zA-Z0-9-]+$")).Select(x=>"."+x),StringComparer.OrdinalIgnoreCase);
 }
 internal static string[] Find(string folder,string formats){
  if(!Directory.Exists(folder))throw new IOException("A pasta de ROMs não foi encontrada.");
  if(String.IsNullOrWhiteSpace(formats))formats=CommonFormats;
  var extensions=Extensions(formats);
  if(extensions.Count==0)extensions=Extensions(CommonFormats);
  bool bootFile=(formats??CommonFormats).Split(',').Any(name=>String.Equals(name.Trim(),"eboot.bin",StringComparison.OrdinalIgnoreCase));
  var next=new Stack<string>();next.Push(folder);
  var found=new List<string>();
  while(next.Count>0){
   var path=next.Pop();
   try{
    found.AddRange(Directory.GetFiles(path).Where(file=>extensions.Contains(Path.GetExtension(file))||bootFile&&Path.GetFileName(file).Equals("eboot.bin",StringComparison.OrdinalIgnoreCase)));
    foreach(var branch in Directory.GetDirectories(path)){
     try{if((File.GetAttributes(branch)&FileAttributes.ReparsePoint)==0)next.Push(branch);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
   }catch(UnauthorizedAccessException){if(path==folder)throw;}catch(IOException){if(path==folder)throw;}
  }
  return found.OrderBy(file=>file,StringComparer.CurrentCultureIgnoreCase).ToArray();
 }
 internal static string Key(string emulator,string rom){return "rom:"+Path.GetFullPath(emulator)+"|"+Path.GetFullPath(rom);}
 static string GameName(string rom){
  if(!Path.GetFileName(rom).Equals("eboot.bin",StringComparison.OrdinalIgnoreCase))return Path.GetFileNameWithoutExtension(rom);
  var folder=Path.GetDirectoryName(rom);
  if(Path.GetFileName(folder).Equals("USRDIR",StringComparison.OrdinalIgnoreCase))folder=Path.GetDirectoryName(folder);
  if(Path.GetFileName(folder).Equals("PS3_GAME",StringComparison.OrdinalIgnoreCase))folder=Path.GetDirectoryName(folder);
  return Path.GetFileName(folder);
 }
 internal static Game NewGame(Emulator emulator,string rom){
  return new Game{
   StoreId=Key(emulator.Target,rom),Rom=rom,Name=GameName(rom),
   Platform=emulator.Platform,Source="Emulador",Target=emulator.Target,Folder=emulator.Working,Args=emulator.Args
  };
 }
 internal static string Arguments(Game game){
  if(!String.IsNullOrWhiteSpace(game.PortData))return WithFile(game.Args,"{game_path}",game.PortData);
  if(String.IsNullOrWhiteSpace(game.Rom))return game.Args.Replace("{rom_path}","");
  return WithFile(game.Args,"{rom_path}",game.Rom);
 }
 static string WithFile(string template,string token,string file){
  if(!template.Contains(token))return (template+" \""+file+"\"").Trim();
  return template.Replace("\""+token+"\"",token).Replace(token,"\""+file+"\"");
 }
 internal static string ResolvePath(string value,string oldRom){
  var path=value.Trim().Trim('"');
  if(Directory.Exists(path)&&oldRom!="")path=Path.Combine(path,Path.GetFileName(oldRom));
  if(!File.Exists(path))throw new IOException(oldRom==""?"Selecione um arquivo existente.":"ROM não encontrada. Se você mudou o nome do arquivo, selecione a ROM diretamente.");
  return Path.GetFullPath(path);
 }
}

