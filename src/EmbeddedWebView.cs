using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

static class EmbeddedWebView {
 const string SdkVersion="1.0.2365.46";
 const string Core="Microsoft.Web.WebView2.Core", Forms="Microsoft.Web.WebView2.WinForms";
 static bool ready;
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
 static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 internal static void Register(){AppDomain.CurrentDomain.AssemblyResolve+=Resolve;}
 static Assembly Resolve(object sender,ResolveEventArgs args){
  var name=new AssemblyName(args.Name).Name;
  if(name!=Core&&name!=Forms)return null;
  Prepare();
  return Assembly.LoadFrom(Filename(name+".dll"));
 }
 static string Filename(string name){
  return Path.Combine(Storage.Root,"WebRuntime",SdkVersion,name);
 }
 static void InstallFile(string name){
  var target=Filename(name);
  var executable=Assembly.GetExecutingAssembly();
  using(var resource=executable.GetManifestResourceStream("Nexus.WebView."+name)){
   if(resource==null)throw new IOException("Componente WebView2 ausente.");
   using(var hash=SHA256.Create()){
    var expected=Convert.ToBase64String(hash.ComputeHash(resource));
    if(File.Exists(target))using(var file=File.OpenRead(target)){
     if(expected==Convert.ToBase64String(hash.ComputeHash(file)))return;
    }
   }
   resource.Position=0;
   Directory.CreateDirectory(Path.GetDirectoryName(target));
   var temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    using(var file=File.Create(temp))resource.CopyTo(file);
    if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);
   }finally{if(File.Exists(temp))File.Delete(temp);}
  }
 }
 internal static void Prepare(){
  if(ready)return;
  foreach(var name in new[]{Core+".dll",Forms+".dll","WebView2Loader.dll"})InstallFile(name);
  if(LoadLibraryEx(Filename("WebView2Loader.dll"),IntPtr.Zero,0x00000900)==IntPtr.Zero)
   throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  ready=true;
 }
}
