using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class Hub {
 string Choose(string title,string[] options){
  using(var dialog=new Form{Text=UiLanguage.Text(title),StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(435,Math.Min(450,34+44*options.Length)),BackColor=BackColor,ForeColor=ForeColor,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   var buttons=new SoftFlow{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(14)};
   string result=null;
   foreach(var option in options){
    var choice=option;
    var button=Button(choice);button.AutoSize=false;button.Width=369;button.Height=36;
    button.Click+=(s,e)=>{result=choice;dialog.DialogResult=DialogResult.OK;};buttons.Controls.Add(button);
   }
   dialog.Controls.Add(buttons);
   return dialog.ShowDialog(this)==DialogResult.OK?result:null;
  }
 }
 void AddCustom(){
  const string emulator="Emulador — importar uma pasta de ROMs";
  const string standalone="Jogo avulso — escolher executável ou atalho";
  var choice=Choose("Adicionar à biblioteca",new[]{emulator,standalone});
  if(choice==emulator)ChooseEmulator();
  else if(choice==standalone)EditCustom(null);
 }
 void ChooseEmulator(){
  var saved=data.Emulators.ToArray();
  var options=saved.Select((item,index)=>(index+1)+". "+item.Name+"  ·  "+item.Platform).ToArray();
  const string newProfile="Escolher executável do emulador…";
  var choice=Choose("Emulador para as ROMs",new[]{newProfile}.Concat(options).ToArray());
  if(choice==null)return;
  ImportRoms(choice==newProfile?null:saved[Array.IndexOf(options,choice)]);
 }
 async void ImportRoms(Emulator profile){
  try{
   var emulator=profile;
   if(emulator==null||!File.Exists(emulator.Target)){
    using(var picker=new OpenFileDialog{Title=UiLanguage.Text("Escolha o executável do emulador"),Filter="Emuladores (.exe)|*.exe",CheckFileExists=true}){
     if(picker.ShowDialog(this)!=DialogResult.OK)return;
     emulator=RomLibrary.Detect(picker.FileName);
    }
    if(RomLibrary.RetroArch(emulator.Target)){
     var directory=Path.Combine(emulator.Working,"cores");
     var installed=Directory.Exists(directory)?Directory.GetFiles(directory,"*_libretro.dll"):new string[0];
     string corePath=installed.Length==1?installed[0]:null;
     if(corePath==null){
      using(var core=new OpenFileDialog{Title=UiLanguage.Text("Escolha o núcleo do RetroArch para estes jogos"),Filter="Núcleos do RetroArch|*.dll",
       InitialDirectory=Directory.Exists(directory)?directory:emulator.Working,CheckFileExists=true}){
       if(core.ShowDialog(this)!=DialogResult.OK)return;
       corePath=core.FileName;
      }
     }
     emulator.Name="RetroArch · "+Path.GetFileNameWithoutExtension(corePath).Replace("_libretro","");
     emulator.Args="-L \""+corePath+"\" "+RomLibrary.Positional;
     emulator.Extensions=RomLibrary.CoreFormats(corePath);
    }
   }else emulator=RomLibrary.ImportProfile(emulator);
   var folder=FolderPicker.Show(this,UiLanguage.Text("Escolha a pasta principal das ROMs"),emulator.Folder);
   if(folder==null)return;
   var format=emulator.Extensions;
   message.Text=UiLanguage.Text("Procurando ROMs compatíveis…");
   var files=await Task.Run(()=>RomLibrary.Find(folder,format));
   if(IsDisposed)return;
   if(files.Length==0){
    var help=emulator.Platform=="Nintendo 3DS"?" Se o jogo estiver em um pacote CIA, instale-o primeiro pelo próprio emulador.":"";
    var choice=MessageBox.Show(this,UiLanguage.Text("Não encontrei ROMs nesta pasta nem nas subpastas.")+help+
     "\n\n"+UiLanguage.Text("Quer escolher os arquivos diretamente?"),"Importar ROMs",MessageBoxButtons.YesNo,MessageBoxIcon.Information);
    if(choice!=DialogResult.Yes)return;
    using(var picker=new OpenFileDialog{Title=UiLanguage.Text("Escolha os arquivos dos jogos"),Filter="Todos os arquivos|*.*",
     Multiselect=true,CheckFileExists=true,InitialDirectory=folder}){
     if(picker.ShowDialog(this)!=DialogResult.OK)return;
     files=picker.FileNames;
    }
   }
   var keys=new System.Collections.Generic.HashSet<string>(data.Games.Where(g=>!InUninstalledHistory(g)&&g.Rom!=""&&Path.IsPathRooted(g.Target)&&Path.IsPathRooted(g.Rom)).Select(g=>RomLibrary.Key(g.Target,g.Rom)),StringComparer.OrdinalIgnoreCase);
   var games=files.Where(file=>keys.Add(RomLibrary.Key(emulator.Target,file))).Select(file=>RomLibrary.NewGame(emulator,file)).ToArray();
   var current=profile!=null&&String.Equals(profile.Target,emulator.Target,StringComparison.OrdinalIgnoreCase)?profile:
    data.Emulators.FirstOrDefault(x=>x.Target.Equals(emulator.Target,StringComparison.OrdinalIgnoreCase)&&x.Args==emulator.Args);
      if(current==null){emulator.Folder=folder;data.Emulators.Add(emulator);}
   else{
    current.Folder=folder;current.Name=emulator.Name;current.Platform=emulator.Platform;
    current.Working=emulator.Working;current.Extensions=emulator.Extensions;current.Args=emulator.Args;
   }
   for(int index=0;index<games.Length;index++){
    var discovered=games[index];
    var savedGame=MatchUninstalled(data,discovered);
    if(savedGame==null){RecordInstallation(discovered);data.Games.Add(discovered);continue;}
    savedGame.Target=discovered.Target;savedGame.Rom=discovered.Rom;savedGame.StoreId=discovered.StoreId;
    savedGame.Platform=discovered.Platform;savedGame.Folder=discovered.Folder;savedGame.Args=discovered.Args;
    RecordInstallation(savedGame);savedGame.RefreshCover=true;games[index]=savedGame;
   }
   Save();
   sourceFilter=EmulatorFilter+emulator.Target;collectionFilter="";chosenStates.Clear();search.Clear();UpdateFilterLabel();
   Navigate("Jogos");
   if(games.Length!=0)selected=games[0];
   RefreshList();
   LoadArt(false);
   Notify(games.Length==0?UiLanguage.Text("Essas ROMs já estão na biblioteca."):games.Length+UiLanguage.Text(" ROMs importadas de ")+emulator.Name+".");
  }catch(Exception error){if(!IsDisposed)Safe(()=>{throw new IOException(error.Message,error);});}
 }
 bool IsCustom(Game game){return game.Rom!=""||game.StoreId==""&&MatchesLauncher(game,WithoutLauncher);}
 void EditCustom(Game original,string type="Manual",bool restore=false){
  var game=original??new Game{Source=type};
  using(var form=new CustomGameDialog(game,original==null,BackColor,surface,accent)){
   if(form.ShowDialog(this)!=DialogResult.OK)return;
   var path=form.ValidPath;
   if(original==null){
    var incoming=new Game{Name=form.GameName,Source=game.Source,Target=path,Platform=form.GamePlatform==""?game.Platform:form.GamePlatform,PortData=form.PortContent??""};
    var archivedGame=MatchUninstalled(data,incoming);
    if(archivedGame!=null){game=archivedGame;restore=true;}
   }
   var previous=game.Rom!=""?game.Rom:game.Target;
   var pathChanged=!String.Equals(previous,path,StringComparison.OrdinalIgnoreCase);
   var portChanged=game.Source=="Port"&&(!String.Equals(game.PortData,form.PortContent,StringComparison.OrdinalIgnoreCase)||game.Args!=form.PortArguments);
   if((original==null||pathChanged||portChanged)&&data.Games.Any(x=>x!=game&&(game.Rom!=""?
       String.Equals(x.Target,game.Target,StringComparison.OrdinalIgnoreCase)&&String.Equals(x.Rom,path,StringComparison.OrdinalIgnoreCase):
       x.Rom==""&&String.Equals(x.Target,path,StringComparison.OrdinalIgnoreCase)&&x.Source==game.Source&&(game.Source=="Port"?String.Equals(x.PortData,form.PortContent,StringComparison.OrdinalIgnoreCase)&&x.Args==form.PortArguments:x.Args==game.Args))))
    throw new IOException(UiLanguage.Text("Esse arquivo já está cadastrado."));
   bool newCover=form.Cover!=""&&!String.Equals(form.Cover,game.Cover,StringComparison.OrdinalIgnoreCase);
   var cover=original==null&&restore&&!newCover?game.Cover:Storage.Asset(form.Cover);
   var badge=SourceIcons.Save(form.Badge);
   if(game.Rom!=""){
    game.Rom=path;game.StoreId=RomLibrary.Key(game.Target,path);
   }else {
    game.Target=path;
    if(pathChanged)game.Folder=Path.GetDirectoryName(path);
   }
   if(game.Source=="Port"){game.PortData=form.PortContent;game.Args=form.PortArguments;}
   if(game.Rom==""&&(game.Icon==""||pathChanged)){
    using(var icon=SourceIcons.FromFile(path))if(icon!=null)game.Icon=InstalledArt.Save(icon);
   }
   if(original!=null||!restore){
    game.DiscordCoverUrl=form.DiscordCoverUrl;game.Badge=badge;
   }
   game.Name=form.GameName;game.Platform=form.GamePlatform==""?game.Platform:form.GamePlatform;game.Cover=cover;
   if(original==null||restore||pathChanged&&!game.Archived){
    RecordInstallation(game);if(restore)game.RefreshCover=!newCover;
   }
   if(newCover)game.RefreshCover=false;
   if(original==null&&!restore)data.Games.Add(game);
   selected=game;Save();
   if(original==null){
    sourceFilter="";collectionFilter="";chosenStates.Clear();search.Clear();UpdateFilterLabel();
    Navigate("Jogos");selected=game;
   }
   RefreshList();LoadArt(false);
  }
 }
 HubButton playButton;
 internal static bool RequiresPlayButton(Game game){return game.Rom!=""||game.Source=="Port";}
 void SetupCustomCardActions(){
  list.MouseClick+=(s,e)=>{
   var item=list.HitTest(e.Location).Item;
   var game=item==null?null:item.Tag as Game;
   if(game==null)return;
   if(e.Button==MouseButtons.Right){item.Selected=true;item.Focused=true;GameMenu(game,e.Location);return;}
   if(e.Button!=MouseButtons.Left||list.View!=View.LargeIcon||item!=hoveredCard||cardHover<.15f)return;
   var art=CardArt(item.Bounds);
   if(CardMore(art).Contains(e.Location))GameMenu(game,e.Location);
   else if(CardFavorite(art).Contains(e.Location))Safe(()=>FavoriteGame(game));
   else if(CardPlay(art).Contains(e.Location))Safe(()=>GameAction(game));
  };
 }
}
sealed class CustomGameDialog : Form {
 readonly TextBox name, path, platform, discordCoverUrl, portData, portArgs;
 readonly PictureBox preview;
 readonly Label imageHint;
 readonly string oldRom;
 readonly bool launcherGame;
 string cover,badge;
 internal string GameName{get{return name.Text.Trim();}}
 internal string DiscordCoverUrl{get{return DiscordPresence.ImageUrl(discordCoverUrl.Text.Trim());}}
 internal string GamePlatform{get{return platform.Text.Trim();}}
 internal string ValidPath{get;private set;}
 internal string Cover{get{return cover;}}
 internal string Badge{get{return badge;}}
 internal string PortContent{get;private set;}
 internal string PortArguments{get{return portArgs==null?"":portArgs.Text.Trim();}}
 internal CustomGameDialog(Game game,bool adding,Color bg,Color surface,Color accent,bool launcherGame=false){
  this.launcherGame=launcherGame;oldRom=game.Rom;cover=game.Cover;badge=game.Badge;
  Text=adding?UiLanguage.Text("Adicionar jogo"):UiLanguage.Text("Editar ")+game.Name;
  StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(770,485);MinimumSize=new Size(630,450);Font=Skin.UiFont(10);BackColor=bg;ForeColor=bg.GetBrightness()<.5f?Color.White:Color.FromArgb(27,33,45);Padding=new Padding(22);
  var art=new SoftPanel{Dock=DockStyle.Left,Width=224,Padding=new Padding(0,0,19,0),AllowDrop=true};
  preview=new PictureBox{Dock=DockStyle.Fill,BackColor=surface,SizeMode=PictureBoxSizeMode.Zoom,Cursor=Cursors.Hand,AccessibleName=UiLanguage.Text("Escolher capa"),AllowDrop=true};
  preview.Click+=(s,e)=>PickCover();
  imageHint=new Label{Dock=DockStyle.Top,Height=35,TextAlign=ContentAlignment.MiddleLeft,Text=UiLanguage.Text("Capa"),Cursor=Cursors.Hand};imageHint.Click+=(s,e)=>PickCover();
  var artActions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45,WrapContents=false};
  AddButton(artActions,"Escolher…",PickCover);
  AddButton(artActions,"Remover",()=>{cover="";ShowCover();});
  art.Controls.Add(preview);art.Controls.Add(artActions);art.Controls.Add(imageHint);
  foreach(Control target in new Control[]{art,preview}){
   target.DragEnter+=(s,e)=>e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;
   target.DragDrop+=(s,e)=>{var paths=(string[])e.Data.GetData(DataFormats.FileDrop);if(paths!=null&&paths.Length>0)SetCover(paths[0]);};
  }
  var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,AutoScroll=true,Padding=new Padding(0,4,0,0)};
  fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
  fields.Controls.Add(new Label{Text=UiLanguage.Text(game.Source=="Programa"?"Nome do programa":"Nome do jogo"),AutoSize=true});
  name=Input(game.Name,surface);fields.Controls.Add(name,0,1);
  if(!launcherGame){
  fields.Controls.Add(new Label{Text=UiLanguage.Text(oldRom==""?"Caminho do arquivo ou atalho":"Caminho da ROM"),AutoSize=true,Margin=new Padding(3,12,3,3)},0,2);
  path=Input(oldRom==""?game.Target:oldRom,surface);fields.Controls.Add(path,0,3);
  var browse=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Dock=DockStyle.Fill};
  AddButton(browse,"Escolher arquivo…",PickPath);
  if(oldRom!="")AddButton(browse,"Mudar pasta…",PickFolder);
  fields.Controls.Add(browse,0,4);
  if(oldRom!="")fields.Controls.Add(new Label{Text=UiLanguage.Text("A mudança de pasta vale apenas para este jogo."),AutoSize=true,ForeColor=Color.LightGray},0,5);
  else{
   var extras=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Dock=DockStyle.Fill,Margin=new Padding(0,12,0,0)};
   AddButton(extras,"Ícone de origem…",PickBadge);
   var reset=AddButton(extras,"Usar ícone automático",()=>badge="");
   reset.AccessibleName=UiLanguage.Text("Usar ícone de origem automático");
   fields.Controls.Add(extras,0,5);
  }
  if(game.Source=="Port"){
   ClientSize=new Size(770,625);
   fields.Controls.Add(new Label{Text=UiLanguage.Text("Arquivo de dados do jogo (opcional)"),AutoSize=true},0,6);
   portData=Input(game.PortData,surface);fields.Controls.Add(portData,0,7);
   var dataButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false,Dock=DockStyle.Fill};
   AddButton(dataButtons,"Escolher dados…",()=>{using(var file=new OpenFileDialog{Title=UiLanguage.Text("Escolher arquivo de dados do port"),CheckFileExists=true})if(file.ShowDialog(this)==DialogResult.OK)portData.Text=file.FileName;});
   AddButton(dataButtons,"Usar jogo embutido",()=>portData.Clear());
   fields.Controls.Add(dataButtons,0,8);
   fields.Controls.Add(new Label{Text=UiLanguage.Text("Argumentos opcionais · {game_path} indica o arquivo de dados"),AutoSize=true},0,9);
   portArgs=Input(game.Args,surface);fields.Controls.Add(portArgs,0,10);
  }
  int platformRow=game.Source=="Port"?11:6;
  fields.Controls.Add(new Label{Text=UiLanguage.Text("Plataforma"),AutoSize=true,Margin=new Padding(3,11,3,3)},0,platformRow);
  platform=Input(game.Platform,surface);fields.Controls.Add(platform,0,platformRow+1);
  fields.Controls.Add(new Label{Text=UiLanguage.Text("Link público da imagem no Discord (opcional)"),AutoSize=true,Margin=new Padding(3,11,3,3)},0,platformRow+2);
  discordCoverUrl=Input(game.DiscordCoverUrl??"",surface);
  discordCoverUrl.AccessibleName=UiLanguage.Text("Link público da imagem no Discord");
  fields.Controls.Add(discordCoverUrl,0,platformRow+3);
  }
  var actions=new SoftFlow{Dock=DockStyle.Bottom,Height=47,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
  var save=AddButton(actions,adding?"Adicionar jogo":"Salvar",ValidateAndClose);((HubButton)save).Accent=accent;((HubButton)save).Primary=true;AcceptButton=save;
  var cancel=AddButton(actions,"Cancelar",()=>DialogResult=DialogResult.Cancel);cancel.DialogResult=DialogResult.Cancel;CancelButton=cancel;
  var heading=new Label{Text=adding?UiLanguage.Text("Adicionar jogo"):game.Name,Font=Skin.HeadingFont(21),Dock=DockStyle.Top,Height=50,AutoEllipsis=true};
  Controls.Add(fields);Controls.Add(art);Controls.Add(actions);Controls.Add(heading);
  ShowCover();
 }
 static TextBox Input(string value,Color color){return new TextBox{Text=value,Dock=DockStyle.Top,BackColor=color,ForeColor=color.GetBrightness()<.5f?Color.White:Color.FromArgb(27,33,45),BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(3,3,3,7)};}
 static Button AddButton(FlowLayoutPanel panel,string label,Action action){
  var button=Hub.Button(label);button.Click+=(s,e)=>action();panel.Controls.Add(button);return button;
 }
 void ShowCover(){
  Image next=null;
  if(cover!=""){
   if(Path.IsPathRooted(cover)){try{next=CoverImages.Open(cover);}catch(ArgumentException){}catch(IOException){}catch(OutOfMemoryException){}}
   else next=Storage.Picture(cover);
  }
  var old=preview.Image;preview.Image=next;if(old!=null)old.Dispose();
  imageHint.Text=UiLanguage.Text(next==null?"Capa · clique ou arraste":"Capa");
 }
 void SetCover(string file){
  var old=cover;cover=file;ShowCover();
  if(preview.Image==null){cover=old;ShowCover();MessageBox.Show(this,UiLanguage.Text("Não foi possível abrir a imagem. WebP, AVIF e HEIF podem exigir as extensões de imagem do Windows."),UiLanguage.Text("Capa"));}
 }
 void PickCover(){
  using(var dialog=new OpenFileDialog{Title=UiLanguage.Text("Escolher capa"),Filter=CoverImages.Filter,CheckFileExists=true})
   if(dialog.ShowDialog(this)==DialogResult.OK)SetCover(dialog.FileName);
 }
 void PickBadge(){
  using(var dialog=new OpenFileDialog{Title=UiLanguage.Text("Escolher ícone de origem"),Filter="Imagem, atalho ou executável|*.png;*.jpg;*.ico;*.exe;*.lnk;*.url",CheckFileExists=true})
   if(dialog.ShowDialog(this)==DialogResult.OK)badge=dialog.FileName;
 }
 void PickPath(){
  var current="";
  try{if(!String.IsNullOrWhiteSpace(path.Text))current=Path.GetDirectoryName(Path.GetFullPath(path.Text));}catch(ArgumentException){}catch(NotSupportedException){}
  using(var dialog=new OpenFileDialog{Title=UiLanguage.Text(oldRom==""?"Escolher arquivo ou atalho":"Escolher ROM"),Filter="Todos os arquivos|*.*",CheckFileExists=true,InitialDirectory=current??""}){
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   path.Text=dialog.FileName;if(GameName=="")name.Text=Path.GetFileNameWithoutExtension(dialog.FileName);
  }
 }
 void PickFolder(){
  var folder=FolderPicker.Show(this,UiLanguage.Text("Escolha a pasta que contém esta ROM"),Path.GetDirectoryName(oldRom));
  if(folder!=null)path.Text=Path.Combine(folder,Path.GetFileName(oldRom));
 }
 void ValidateAndClose(){
  try{
   if(GameName=="")throw new IOException(UiLanguage.Text("Informe um nome."));
   if(launcherGame){DialogResult=DialogResult.OK;return;}
   if(discordCoverUrl.Text.Trim()!=""&&DiscordCoverUrl==""){discordCoverUrl.Focus();throw new IOException(UiLanguage.Text("Cole um link público de imagem que comece com https://."));}
   ValidPath=RomLibrary.ResolvePath(path.Text,oldRom);
   PortContent=portData==null||String.IsNullOrWhiteSpace(portData.Text)?"":RomLibrary.ResolvePath(portData.Text,"");
   DialogResult=DialogResult.OK;
  }catch(Exception error){MessageBox.Show(this,error.Message,"Verifique os campos",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 }
 protected override void Dispose(bool disposing){if(disposing&&preview!=null&&preview.Image!=null){var image=preview.Image;preview.Image=null;image.Dispose();}base.Dispose(disposing);}
}




