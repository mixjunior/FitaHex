using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class FolderPicker {
 const uint PickFolders=0x20, ForceFileSystem=0x40, PathMustExist=0x800, NoChangeDirectory=0x8;
 const uint FileSystemPath=0x80058000;
 const int Cancelled=unchecked((int)0x800704C7);

 internal static string Show(IWin32Window owner,string title,string initialDirectory){
  var dialog=Create(title,initialDirectory);
  try{
   int result=dialog.Show(owner.Handle);
   if(result==Cancelled)return null;
   Marshal.ThrowExceptionForHR(result);
   IShellItem item;
   dialog.GetResult(out item);
   return ReadPath(item);
  }finally{Marshal.ReleaseComObject(dialog);}
 }

 static IFileDialog Create(string title,string initialDirectory){
  var dialog=(IFileDialog)new FileOpenDialog();
  try{
   uint options;
   dialog.GetOptions(out options);
   dialog.SetOptions(options|PickFolders|ForceFileSystem|PathMustExist|NoChangeDirectory);
   dialog.SetTitle(title);
   dialog.SetOkButtonLabel("Selecionar pasta");
   if(Directory.Exists(initialDirectory)){
    IShellItem item;
    var type=typeof(IShellItem).GUID;
    SHCreateItemFromParsingName(initialDirectory,IntPtr.Zero,ref type,out item);
    try{dialog.SetFolder(item);}finally{Marshal.ReleaseComObject(item);}
   }
   return dialog;
  }catch{
   Marshal.ReleaseComObject(dialog);
   throw;
  }
 }

 static string ReadPath(IShellItem item){
  IntPtr path=IntPtr.Zero;
  try{item.GetDisplayName(FileSystemPath,out path);return Marshal.PtrToStringUni(path);}
  finally{Marshal.FreeCoTaskMem(path);Marshal.ReleaseComObject(item);}
 }

 internal static void Verify(string initialDirectory){
  var dialog=Create("Escolher pasta de ROMs",initialDirectory);
  try{
   uint options;
   dialog.GetOptions(out options);
   IShellItem item;
   dialog.GetFolder(out item);
   if((options&PickFolders)==0||!String.Equals(ReadPath(item),initialDirectory,StringComparison.OrdinalIgnoreCase))
    throw new IOException("Seletor de pastas do Explorador.");
  }finally{Marshal.ReleaseComObject(dialog);}
 }

 [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
 class FileOpenDialog {}

 [ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IFileDialog {
  // The COM interface uses this vtable order, including methods the picker does not call.
  [PreserveSig] int Show(IntPtr owner);
  void SetFileTypes(uint count,IntPtr filters);
  void SetFileTypeIndex(uint index);
  void GetFileTypeIndex(out uint index);
  void Advise(IntPtr events,out uint cookie);
  void Unadvise(uint cookie);
  void SetOptions(uint options);
  void GetOptions(out uint options);
  void SetDefaultFolder(IShellItem folder);
  void SetFolder(IShellItem folder);
  void GetFolder(out IShellItem folder);
  void GetCurrentSelection(out IShellItem item);
  void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
  void GetFileName(out IntPtr name);
  void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
  void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
  void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
  void GetResult(out IShellItem item);
 }

 [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 interface IShellItem {
  void BindToHandler(IntPtr context,ref Guid handler,ref Guid type,out IntPtr result);
  void GetParent(out IShellItem parent);
  void GetDisplayName(uint format,out IntPtr name);
 }

 [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]
 static extern void SHCreateItemFromParsingName(string path,IntPtr context,ref Guid type,out IShellItem item);
}