using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32;

static class SourceIcons {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 struct FileInfo {
  public IntPtr Icon;
  public int Index;
  public uint Attributes;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string Name;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string Type;
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
 static extern IntPtr SHGetFileInfo(string file,uint attrs,out FileInfo info,uint length,uint flags);
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
 internal static Bitmap FromFile(string file){
  if(!File.Exists(file))return null;
  var ext=Path.GetExtension(file).ToLowerInvariant();
  try{
   if(new[]{".png",".jpg",".jpeg",".bmp",".gif",".ico"}.Contains(ext)){
    using(var image=Image.FromFile(file))return new Bitmap(image);
   }
   FileInfo info;
   if(SHGetFileInfo(file,0,out info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x100)==IntPtr.Zero||info.Icon==IntPtr.Zero)return null;
   try{return Icon.FromHandle(info.Icon).ToBitmap();}finally{DestroyIcon(info.Icon);}
  }catch(ArgumentException){return null;}catch(IOException){return null;}catch(ExternalException){return null;}catch(OutOfMemoryException){return null;}
 }
 internal static string Save(string file){
  if(file=="")return "";
  if(!Path.IsPathRooted(file))return Storage.Asset(file);
  using(var bitmap=FromFile(file)){
   if(bitmap==null)throw new IOException("Não foi possível ler o ícone de origem. Selecione uma imagem, executável ou atalho.");
   return InstalledArt.Save(bitmap);
  }
 }
 internal static string CommandFile(string value){
  var match=Regex.Match(Environment.ExpandEnvironmentVariables(value??""),@"^\s*(?:""([^""]+\.exe)""|(.+?\.exe)(?=\s|$))",RegexOptions.IgnoreCase);
  return match.Success?(match.Groups[1].Success?match.Groups[1].Value:match.Groups[2].Value):"";
 }
 internal static string Launcher(string origin,Game game){
  if(origin==RiotGames.Source&&File.Exists(game.Target))return game.Target;
  if(origin=="Steam"){
   var file=Stores.SteamRoots().Select(root=>Path.Combine(root,"steam.exe")).FirstOrDefault(File.Exists);
   if(file!=null)return file;
  }
  var protocol=origin=="Steam"?"steam":origin=="Epic Games"?"com.epicgames.launcher":origin=="GOG"?"goggalaxy":origin=="EA app"?"origin2":origin=="Ubisoft Connect"?"uplay":origin=="Battle.net"?"battlenet":"";
  if(protocol!="")using(var handler=Registry.ClassesRoot.OpenSubKey(protocol+@"\shell\open\command")){
   var file=CommandFile(handler==null?"":Convert.ToString(handler.GetValue("")));
   if(File.Exists(file))return file;
  }
  return "";
 }
}
