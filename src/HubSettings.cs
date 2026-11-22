using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

sealed class NexusTheme {
 public readonly string Key, Label;
 public readonly Color Background, Surface, Sidebar, Accent, Text, Muted;
 NexusTheme(string key,string label,int bg,int face,int side,int accent,int text,int muted){
  Key=key;Label=label;Background=C(bg);Surface=C(face);Sidebar=C(side);Accent=C(accent);Text=C(text);Muted=C(muted);
 }
 static Color C(int rgb){return Color.FromArgb(255,(rgb>>16)&255,(rgb>>8)&255,rgb&255);}
 internal static readonly NexusTheme[] All={
  new NexusTheme("NEXUS Dark","Escuro · roxo",0x0e1017,0x1a1e2a,0x131620,0x8b5cf6,0xf5f5f7,0x9299aa),
  new NexusTheme("NEXUS Neon","Neon · ciano",0x081318,0x0d1c25,0x10232c,0x24b7d6,0xe8fbff,0xacc9d4),
  new NexusTheme("NEXUS Minimal","Minimal · cinza",0x17181c,0x212227,0x1b1c20,0x8b95b1,0xf6f6f8,0xc4c5ce),
  new NexusTheme("Aurora","Aurora · verde e petróleo",0x0f1c1a,0x172a28,0x142421,0x62d7ad,0xeafff6,0xb3d7ca),
  new NexusTheme("Midnight","Meia-noite · azul",0x10182e,0x192341,0x111c35,0x7b9aff,0xf0f4ff,0xbdc9ed),
  new NexusTheme("Rose","Rosa · ameixa",0x251725,0x382336,0x2e1b2c,0xf697c0,0xfff0f8,0xe4c2d5),
  new NexusTheme("Sand","Areia · claro e quente",0xf8f1e7,0xf3e4d4,0xecdbc8,0xa34822,0x2c1d17,0x62463a),
  new NexusTheme("Light","Claro · azul",0xf3f6fc,0xe5edf9,0xe9eff9,0x255fb0,0x19243b,0x435773),
  new NexusTheme("Contrast","Alto contraste · amarelo",0x080808,0x111111,0x000000,0xffd82f,0xffffff,0xeeeeee)
 };
 internal static NexusTheme Find(string key){return All.FirstOrDefault(x=>x.Key==key)??All[0];}
}

partial class Hub {
 readonly Label recentGame=new Label();
 void SetupRecent(){
  recentGame.Dock=DockStyle.Top;recentGame.Height=34;recentGame.AutoEllipsis=true;
  recentGame.TextAlign=ContentAlignment.MiddleLeft;recentGame.Padding=new Padding(8,0,8,0);
  libraryPage.Controls.Add(recentGame);
 }
 void UpdateRecent(){
  var last=data.Games.Where(g=>g.Last.HasValue).OrderByDescending(g=>g.Last.Value).FirstOrDefault();
  recentGame.Text=last==null?"Último jogo jogado · nenhum por enquanto":"Último jogo jogado  ·  "+last.Name+"  ·  "+last.Last.Value.ToString("g");
 }
 internal static bool CanRemove(Game game){return !game.Archived&&(game.ManualInstall||String.IsNullOrEmpty(game.StoreId)||game.Rom!="");}
 void RemoveGame(Game game){
  if(!CanRemove(game)||running.Contains(game.Id))return;
  if(MessageBox.Show(this,"Mover "+game.Name+" para o histórico como desinstalado? Os arquivos e o progresso serão preservados.",ProductIdentity.Name,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
  game.Archived=true;game.Missing=true;game.WasInstalled=true;
  Save();RefreshList();
 }
 void ApplyTheme(){
  var theme=NexusTheme.Find(data.Theme);accent=theme.Accent;surface=theme.Surface;
  ForeColor=theme.Text;BackColor=theme.Background;rail.BackColor=theme.Sidebar;hero.BackColor=theme.Background;
  list.BackColor=BackColor;list.ForeColor=ForeColor;((Gallery)list).ApplyWindowsTheme();empty.BackColor=surface;chrome.BackColor=rail.BackColor;status.BackColor=rail.BackColor;
  updateLink.LinkColor=theme.Text;updateLink.ActiveLinkColor=theme.Accent;updateLink.VisitedLinkColor=theme.Text;
  status.ForeColor=details.ForeColor=controller.ForeColor=emptyText.ForeColor=recentGame.ForeColor=theme.Muted;
  subtitle.ForeColor=theme.Muted;
  search.BackColor=surface;search.ForeColor=ForeColor;
  foreach(var button in toolbar.Controls.OfType<HubButton>().Concat(detailButtons.Controls.OfType<HubButton>()).Concat(chrome.Controls.OfType<FlowLayoutPanel>().SelectMany(p=>p.Controls.OfType<HubButton>()))){
   button.Accent=accent;button.BackColor=surface;button.ForeColor=ForeColor;
  }
  ApplyDesignTheme();ClearCards();hero.Invalidate(true);list.Invalidate();
 }
 void Settings(){
  using(var form=new Form{Text="Configurações · "+ProductIdentity.Name,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(670,445),MinimumSize=new Size(590,420),BackColor=BackColor,ForeColor=ForeColor,Font=Font,Padding=new Padding(18),MaximizeBox=false,MinimizeBox=false}){
   var heading=new Label{Text="Configurações",Dock=DockStyle.Top,Height=54,Font=Skin.HeadingFont(24)};
   var panes=new SoftPanel{Dock=DockStyle.Fill};
   var tabs=new SoftFlow{Dock=DockStyle.Left,Width=152,FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=rail.BackColor,Padding=new Padding(6)};
   var pages=new[]{SettingsPage("Aparência"),SettingsPage("Biblioteca"),SettingsPage("Integrações"),SettingsPage("Controle"),SettingsPage("Backup"),SettingsPage("Windows")};
   var buttons=new HubButton[pages.Length];
   for(int index=0;index<pages.Length;index++){
    int selectedPage=index;
    buttons[index]=Action(tabs,(string)pages[index].Tag,()=>{
     for(int n=0;n<pages.Length;n++){pages[n].Visible=n==selectedPage;buttons[n].Active=n==selectedPage;}
     pages[selectedPage].BringToFront();
    });
    buttons[index].AutoSize=false;buttons[index].Width=132;buttons[index].Height=36;buttons[index].Sidebar=true;
    panes.Controls.Add(pages[index]);pages[index].Visible=index==0;
   }
   buttons[0].Active=true;
   var titles=new[]{"Janela","Janela sem borda","Tela cheia"};
   var modes=new ComboBox{Width=290,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Modo da janela"};
   modes.Items.AddRange(titles);modes.SelectedIndex=Math.Max(0,Math.Min(2,displayMode));
   var themes=new ComboBox{Width=290,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="Tema"};
   themes.Items.AddRange(NexusTheme.All.Select(t=>t.Label).Cast<object>().ToArray());
   themes.SelectedIndex=Array.IndexOf(NexusTheme.All,NexusTheme.Find(data.Theme));
   var preview=new Label{Width=290,Height=46,Padding=new Padding(12,0,0,0),TextAlign=ContentAlignment.MiddleLeft,Text="Prévia do tema"};
   Action swatch=()=>{var picked=NexusTheme.All[themes.SelectedIndex];preview.BackColor=picked.Surface;preview.ForeColor=picked.Text;};
   themes.SelectedIndexChanged+=(s,e)=>swatch();swatch();
   pages[0].Controls.Add(new Label{Text="Modo da janela",AutoSize=true});pages[0].Controls.Add(modes);
   pages[0].Controls.Add(new Label{Text="Tema",AutoSize=true,Margin=new Padding(3,12,3,3)});pages[0].Controls.Add(themes);pages[0].Controls.Add(preview);
   var minimize=new CheckBox{AutoSize=true,Text="Minimizar o "+ProductIdentity.Name+" ao abrir um jogo",Checked=data.Minimize};
   var detect=new CheckBox{AutoSize=true,Text="Atualizar jogos dos launchers ao iniciar",Checked=data.AutoScan};
   pages[1].Controls.Add(minimize);pages[1].Controls.Add(detect);
   Action(pages[1],"Ver mini tour",()=>ShowTour(false,form));
   pages[2].Controls.Add(new Label{Text="Consulte as páginas de conta dos launchers em Social.",MaximumSize=new Size(365,0),AutoSize=true});
   Action(pages[2],"Abrir Social",()=>{form.DialogResult=DialogResult.Cancel;Navigate("Social");});
   var discordEnabled=new CheckBox{AutoSize=true,Text="Mostrar ROMs e jogos avulsos no Discord",Checked=data.DiscordEnabled,Margin=new Padding(3,15,3,5)};
   pages[2].Controls.Add(discordEnabled);
   pages[2].Controls.Add(new Label{Text="Abra o Discord para desktop, permita o compartilhamento de atividade no Discord e inicie o jogo pelo FitaHex.",AutoSize=true,MaximumSize=new Size(350,0)});
   pages[3].Controls.Add(new Label{Text="Direcional · navegar\nA · selecionar ou jogar\nB · voltar\nLB / RB · trocar de seção\nY · buscar\nX · alternar favorito",AutoSize=true});
   Action(pages[4],"Abrir pasta de dados",()=>System.Diagnostics.Process.Start("explorer.exe","\""+Storage.Root+"\""));
   pages[4].Controls.Add(new Label{Text=Storage.Root,AutoSize=true,MaximumSize=new Size(370,0),
    ForeColor=NexusTheme.Find(data.Theme).Muted,Margin=new Padding(3,5,3,10)});
   Action(pages[4],"Exportar backup…",ExportLibrary);
   Action(pages[4],"Restaurar backup…",()=>{if(RestoreLibrary())form.DialogResult=DialogResult.Cancel;});
   var windows=new WindowsShortcuts.Options(pages[5]);
   Action(pages[5],"Aplicativos de inicialização do Windows",()=>OpenLink("ms-settings:startupapps"));
   var footer=new SoftFlow{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};
   var save=Action(footer,"Salvar",()=>form.DialogResult=DialogResult.OK);save.Primary=true;
   var cancel=Action(footer,"Cancelar",()=>form.DialogResult=DialogResult.Cancel);
   form.AcceptButton=save;form.CancelButton=cancel;
   form.Controls.Add(panes);form.Controls.Add(tabs);form.Controls.Add(footer);form.Controls.Add(heading);
   if(form.ShowDialog(this)!=DialogResult.OK)return;
   if(!windows.Apply(this))return;
   data.Theme=NexusTheme.All[themes.SelectedIndex].Key;data.Minimize=minimize.Checked;data.AutoScan=detect.Checked;
   data.WindowMode=modes.SelectedIndex;data.Fullscreen=modes.SelectedIndex==2;
   data.DiscordEnabled=discordEnabled.Checked;UpdateDiscordPresence();
   Save();ApplyTheme();BuildNav();SetMode(modes.SelectedIndex,false);RefreshList();
  }
 }
 SoftFlow SettingsPage(string title){
  var page=new SoftFlow{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(19,7,8,0),Tag=title};
  page.Controls.Add(new Label{Text=title,AutoSize=true,Font=Skin.HeadingFont(17),Margin=new Padding(3,2,3,13)});
  return page;
 }
 void ExportLibrary(){
  using(var file=new SaveFileDialog{Filter="Backup "+ProductIdentity.Name+"|*.zip",FileName=ProductIdentity.Name+"-backup.zip"})
   if(file.ShowDialog(this)==DialogResult.OK){Save();Storage.Export(file.FileName);MessageBox.Show(this,"Backup exportado.","Backup",MessageBoxButtons.OK,MessageBoxIcon.Information);}
 }
 bool RestoreLibrary(){
  if(running.Count>0||scanning||xboxScanning||loadingArt||checkingInstallations)throw new IOException("Aguarde os jogos e as atualizações da biblioteca antes de restaurar o backup.");
  using(var file=new OpenFileDialog{Filter="Backup "+ProductIdentity.Name+"|*.zip"}){
   if(file.ShowDialog(this)!=DialogResult.OK||MessageBox.Show(this,"Substituir a biblioteca atual pelo backup?","Restaurar",MessageBoxButtons.YesNo)!=DialogResult.Yes)return false;
   var restored=Storage.Import(file.FileName);restored.RestoreArchivedGames();restored.MigrateCategories();Storage.Write(restored);
   data=restored;UpdateDiscordPresence();collectionFilter="";sourceFilter="";chosenStates.Clear();UpdateFilterLabel();
   Navigate("Jogos");selected=null;ApplyRail();ApplyGridSize();ApplyTheme();BuildNav();SetMode(data.WindowMode??(data.Fullscreen?2:0),false);RefreshList();
   return true;
  }
 }
}






