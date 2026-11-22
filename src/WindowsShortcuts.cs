using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Linq;
using System.Windows.Forms;

static class WindowsShortcuts {
 const string Description="Atalho gerenciado pelo FitaHex";
 static string ShortcutPath(Environment.SpecialFolder folder){
  var path=Environment.GetFolderPath(folder,Environment.SpecialFolderOption.DoNotVerify);
  return path==""?"":Path.Combine(path,ProductIdentity.Name+".lnk");
 }

 static string ManagedTarget(string path){
  if(!File.Exists(path))return "";
  dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
  try{
   dynamic shortcut=shell.CreateShortcut(path);
   try{return (string)shortcut.Description==Description?(string)shortcut.TargetPath:"";}
   finally{Marshal.FinalReleaseComObject(shortcut);}
  }finally{Marshal.FinalReleaseComObject(shell);}
 }

 static bool Owns(string path){return ManagedTarget(path)!="";}

 static string StartupCopy {get{return Path.Combine(Storage.Root,"Startup",ProductIdentity.Name+".exe");}}
 static string RefreshStartupCopy(string executable,string copy){
  if(String.Equals(executable,copy,StringComparison.OrdinalIgnoreCase))return copy;
  if(File.Exists(copy)){
   using(var hash=SHA256.Create())
   using(var source=File.OpenRead(executable))
   using(var installed=File.OpenRead(copy)){
    if(hash.ComputeHash(source).SequenceEqual(hash.ComputeHash(installed)))return copy;
   }
  }
  Directory.CreateDirectory(Path.GetDirectoryName(copy));
  var pending=copy+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   File.Copy(executable,pending);
   if(File.Exists(copy))File.Replace(pending,copy,null);else File.Move(pending,copy);
  }finally{if(File.Exists(pending))File.Delete(pending);}
  return copy;
 }
 internal static void RepairStartup(){
  var path=ShortcutPath(Environment.SpecialFolder.Startup);
  if(Owns(path))RegisterStartup(path,Application.ExecutablePath,StartupCopy);
 }
 static void RegisterStartup(string path,string executable,string copy){
  if(File.Exists(path)&&!Owns(path))throw new IOException(UiLanguage.Text("Já existe um atalho com esse nome na pasta Inicializar."));
  var target=RefreshStartupCopy(executable,copy);
  if(!String.Equals(ManagedTarget(path),target,StringComparison.OrdinalIgnoreCase))Set(path,true,target);
 }
 static void Set(string path,bool enabled,string target=null){
  if(path=="")throw new IOException(UiLanguage.Text("A pasta do Windows não está disponível."));
  if(!enabled){
   if(Owns(path))File.Delete(path);
   return;
  }
  if(File.Exists(path)&&!Owns(path))throw new IOException(UiLanguage.Text("Já existe um atalho com esse nome em ")+Path.GetDirectoryName(path)+".");
  target=target??Application.ExecutablePath;
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  var pending=Path.Combine(Path.GetDirectoryName(path),Guid.NewGuid().ToString("N")+".lnk");
  dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
  try{
   dynamic shortcut=shell.CreateShortcut(pending);
   try{
    shortcut.Description=Description;
    shortcut.TargetPath=target;
    shortcut.WorkingDirectory=Path.GetDirectoryName(target);
    shortcut.IconLocation=target+",0";
    shortcut.Save();
   }finally{Marshal.FinalReleaseComObject(shortcut);}
   if(File.Exists(path))File.Replace(pending,path,null);
   else File.Move(pending,path);
  }finally{
   Marshal.FinalReleaseComObject(shell);
   if(File.Exists(pending))File.Delete(pending);
  }
 }

 internal sealed class Options {
  readonly CheckBox startup, desktop;
  readonly string startupPath=ShortcutPath(Environment.SpecialFolder.Startup);
  readonly string desktopPath=ShortcutPath(Environment.SpecialFolder.DesktopDirectory);
  internal Options(Control panel){
   startup=Add(panel,"Iniciar o FitaHex ao entrar no Windows",startupPath);
   desktop=Add(panel,"Criar atalho na Área de Trabalho",desktopPath);
  }
  static CheckBox Add(Control panel,string label,string path){
   var box=new CheckBox{Text=UiLanguage.Text(label),AutoSize=true,Enabled=path!="",Margin=new Padding(3,7,3,7)};
   try{box.Checked=Owns(path);}
   catch(COMException){box.Enabled=false;}
   panel.Controls.Add(box);return box;
  }
  internal bool Apply(IWin32Window owner){
   try{
    if(startup.Enabled){
     if(startup.Checked)RegisterStartup(startupPath,Application.ExecutablePath,StartupCopy);
     else Set(startupPath,false);
    }
    if(desktop.Enabled)Set(desktopPath,desktop.Checked);
    return true;
   }catch(Exception error){
    if(!(error is IOException||error is UnauthorizedAccessException||error is COMException))throw;
    MessageBox.Show(owner,error.Message,"Opções do Windows",MessageBoxButtons.OK,MessageBoxIcon.Warning);
    return false;
   }
  }
 }

 internal static void Verify(string fixture){
  var path=Path.Combine(fixture,"FitaHex.lnk");
  Set(path,true);
  if(!Owns(path))throw new IOException(UiLanguage.Text("Criação de atalho."));
  var copy=Path.Combine(fixture,"installed",ProductIdentity.Name+".exe");
  RegisterStartup(path,Application.ExecutablePath,copy);
  if(!File.Exists(copy)||!String.Equals(ManagedTarget(path),copy,StringComparison.OrdinalIgnoreCase))
   throw new IOException(UiLanguage.Text("Alvo estável de inicialização."));
  RegisterStartup(path,copy,copy);
  Set(path,false);
  if(File.Exists(path))throw new IOException(UiLanguage.Text("Remoção de atalho."));
 }
 internal static void FirstRun(Library library){
  if(library.WindowsSetupShown)return;
  using(var form=new Form{Text=UiLanguage.Text("Configuração inicial · ")+ProductIdentity.Name,ClientSize=new Size(460,190),
      StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,
      Font=Skin.UiFont(10),MaximizeBox=false,MinimizeBox=false,Padding=new Padding(18)}){
   var controls=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
   controls.Controls.Add(new Label{Text=UiLanguage.Text("Como você quer abrir o FitaHex?"),AutoSize=true,Font=Skin.HeadingFont(16),Margin=new Padding(3,3,3,9)});
   var options=new Options(controls);
   var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=39,FlowDirection=FlowDirection.RightToLeft};
   var confirm=new Button{Text=UiLanguage.Text("Continuar"),AutoSize=true};
   var skip=new Button{Text=UiLanguage.Text("Pular"),AutoSize=true,DialogResult=DialogResult.Cancel};
   confirm.Click+=(sender,args)=>{if(options.Apply(form))form.DialogResult=DialogResult.OK;};
   footer.Controls.Add(confirm);footer.Controls.Add(skip);
   form.Controls.Add(controls);form.Controls.Add(footer);
   form.AcceptButton=confirm;form.CancelButton=skip;
   form.ShowDialog();
  }
  library.WindowsSetupShown=true;
  Storage.Write(library);
 }
}
