using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using System.Xml;

static class InstalledArt {
 internal static bool Supported(Game game){return XboxGames.IsTarget(game.Target)||game.Source==XboxGames.Source||game.Source==RiotGames.Source;}
 internal static Artwork.Result Find(Game game){
  var result=new Artwork.Result{Id=game.Id,Cover="",Banner="",Icon=""};
  if(game.Source==RiotGames.Source){
   if(game.Icon!="")return result;
   var executable=RiotGames.IconPath(game);
   if(executable!="")try{using(var icon=Icon.ExtractAssociatedIcon(executable))if(icon!=null)using(var bitmap=icon.ToBitmap())result.Icon=Save(bitmap);}catch(ArgumentException){}catch(IOException){}
   return result;
  }
  var root=game.Install;
  if(!String.IsNullOrWhiteSpace(root)&&Directory.Exists(root)){
   var files=new[]{Path.Combine(root,"AppxManifest.xml"),Path.Combine(root,"MicrosoftGame.config"),Path.Combine(root,"Content","MicrosoftGame.config")};
   foreach(var file in files){
    if(!File.Exists(file))continue;
    try{ReadArt(file,game,result);}catch(IOException){}catch(UnauthorizedAccessException){}catch(XmlException){}
   }
  }
  if(game.Icon==""&&result.Icon==""&&XboxGames.IsTarget(game.Target))result.Icon=ShellIcon(game.Target);
  return result;
 }
 internal static void ReadArt(string manifest,Game game,Artwork.Result art){
  var root=Path.GetDirectoryName(manifest);
  var xmlSettings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024};
  XDocument doc;
  using(var reader=XmlReader.Create(manifest,xmlSettings))doc=XDocument.Load(reader);
  var elements=doc.Descendants().Where(e=>e.Name.LocalName=="VisualElements"||e.Name.LocalName=="DefaultTile"||e.Name.LocalName=="ShellVisuals").ToArray();
  if(XboxGames.IsTarget(game.Target)){
   var appId=game.Target.Substring(game.Target.LastIndexOf('!')+1);
   elements=elements.Where(e=>{
    var owner=e.Ancestors().FirstOrDefault(a=>a.Name.LocalName=="Application");
    return owner==null||String.Equals((string)owner.Attribute("Id"),appId,StringComparison.OrdinalIgnoreCase);
   }).ToArray();
  }
  var attributes=elements.SelectMany(e=>e.Attributes()).ToArray();
  if(game.Cover==""&&art.Cover=="")art.Cover=FindImage(root,attributes,new[]{"Cover","PortraitLogo","Square480x480Logo","Square310x310Logo","Square150x150Logo","Logo"},image=>image.Height>image.Width*1.13&&image.Height>=120);
  if(game.Banner==""&&art.Banner=="")art.Banner=FindImage(root,attributes,new[]{"Wide310x150Logo","SplashScreenImage"},image=>image.Width>image.Height*1.33&&image.Width>=240);
  if(game.Icon==""&&art.Icon=="")art.Icon=FindImage(root,attributes,new[]{"Square480x480Logo","Square310x310Logo","Square150x150Logo","Square44x44Logo","Logo","SmallLogo"},image=>image.Width>=30&&image.Height>=30);
 }
 static string FindImage(string root,XAttribute[] attributes,string[] names,Func<Image,bool> fits){
  foreach(var alias in names)foreach(var attribute in attributes.Where(a=>a.Name.LocalName==alias)){
   foreach(var candidate in Files(root,attribute.Value)){
    try{
     using(var stream=File.OpenRead(candidate))using(var image=Image.FromStream(stream,true,true)){
      if(image.Width<=4096&&image.Height<=4096&&fits(image))return Save(image);
     }
    }catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(ExternalException){}
   }
  }
  return "";
 }
 static IEnumerable<string> Files(string root,string relative){
  if(String.IsNullOrWhiteSpace(relative)||relative.Contains(":")||Path.IsPathRooted(relative))yield break;
  var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  var target=Path.GetFullPath(Path.Combine(root,relative));
  if(!target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))yield break;
  var dir=Path.GetDirectoryName(target);var stem=Path.GetFileNameWithoutExtension(target);var extension=Path.GetExtension(target);
  if((!extension.Equals(".png",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".jpg",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".jpeg",StringComparison.OrdinalIgnoreCase))||!Directory.Exists(dir))yield break;
  string[] variants;
  try{variants=Directory.GetFiles(dir,stem+".*"+extension);}catch(IOException){variants=new string[0];}catch(UnauthorizedAccessException){variants=new string[0];}
  foreach(var file in variants.Where(f=>Path.GetFileName(f).StartsWith(stem+".",StringComparison.OrdinalIgnoreCase)).OrderByDescending(VariantScore))yield return file;
  if(File.Exists(target))yield return target;
 }
 static int VariantScore(string file){
  var match=System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file),@"(?:scale|targetsize)-(\d+)");
  int n;
  return match.Success&&Int32.TryParse(match.Groups[1].Value,out n)?n:0;
 }
 internal static string Save(Image image){
  var relative=Path.Combine("images",Guid.NewGuid().ToString("N")+".png");
  var output=Path.Combine(Storage.Root,relative);Directory.CreateDirectory(Path.GetDirectoryName(output));image.Save(output,ImageFormat.Png);return relative;
 }
 [StructLayout(LayoutKind.Sequential)]
 struct NativeSize{public int Width,Height;}
 [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface ImageFactory{
  [PreserveSig] int GetImage(NativeSize size,uint flags,out IntPtr bitmap);
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]
 static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out ImageFactory item);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
 internal static string ShellIcon(string target){using(var image=ShellImage(target))return image==null?"":Save(image);}
 internal static Bitmap ShellImage(string target){
  ImageFactory factory=null;IntPtr bitmap=IntPtr.Zero;
  try{
   var id=typeof(ImageFactory).GUID;
   var canonical=@"::{4234d49b-0245-4df3-b780-3893943456e1}\"+target.Substring(target.LastIndexOf('\\')+1);
   if(SHCreateItemFromParsingName(canonical,IntPtr.Zero,ref id,out factory)<0&&SHCreateItemFromParsingName(target,IntPtr.Zero,ref id,out factory)<0)return null;
   if(factory==null||factory.GetImage(new NativeSize{Width=256,Height=256},5,out bitmap)<0||bitmap==IntPtr.Zero)return null;
   return Image.FromHbitmap(bitmap);
  }catch(ExternalException){return null;}catch(ArgumentException){return null;}
  finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(factory!=null)Marshal.ReleaseComObject(factory);}
 }
}


