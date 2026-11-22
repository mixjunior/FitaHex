using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

sealed class UpdateReport {
 internal sealed class Row {
  internal readonly string Name;
  internal int Installed, Available, Covers, Times;
  internal readonly HashSet<string> TimeSources=new HashSet<string>();
  internal readonly List<string> Warnings=new List<string>();
  internal Row(string name){Name=name;}
  internal string LibrarySource {
   get {
    switch(Name){
     case "Steam":return "Instalações e cache Steam";
     case "Epic Games":return "Manifestos locais";
     case "Xbox / Microsoft Store":return "Jogos registrados no Windows";
     case "Riot Games":return "Riot Client e catálogo de jogos disponíveis";
     case "GOG":return "Instalações e cache GOG Galaxy";
     case "EA app":case "Battle.net":return "Biblioteca em cache do GOG Galaxy";
     default:return "Sem leitura direta de biblioteca";
    }
   }
  }
 }
 readonly Dictionary<string,Row> bySource;
 internal readonly Row[] Rows;
 internal bool ArtFailed;
 internal int TotalGames {get{return Rows.Sum(row=>row.Installed+row.Available);}}
 internal int TotalCovers {get{return Rows.Sum(row=>row.Covers);}}
 internal UpdateReport(IEnumerable<string> launchers){
  Rows=launchers.Select(name=>new Row(name)).ToArray();
  bySource=Rows.ToDictionary(row=>row.Name,StringComparer.OrdinalIgnoreCase);
 }
 internal void ReadGames(IEnumerable<Game> installed,IEnumerable<Game> available,IEnumerable<Game> currentInstalled){
  var keys=new HashSet<string>(currentInstalled.Select(game=>game.StoreId),StringComparer.OrdinalIgnoreCase);
  foreach(var row in Rows){
   row.Installed=installed.Where(game=>game.Source==row.Name).Select(game=>game.StoreId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
   row.Available=available.Where(game=>game.Source==row.Name&&!keys.Contains(game.StoreId)).Select(game=>game.StoreId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
  }
 }
 internal void Cover(string source){Row row;if(bySource.TryGetValue(source,out row))row.Covers++;}
 internal void Time(string source,string origin){Row row;if(bySource.TryGetValue(source,out row)){row.Times++;row.TimeSources.Add(origin);}}
 internal void ReadErrors(string installedErrors,IEnumerable<string> cacheErrors,string xboxError){
  foreach(var line in installedErrors.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){
   int colon=line.IndexOf(':');
   if(colon<0)continue;
   var provider=line.Substring(0,colon);
   var source=provider=="Epic"?"Epic Games":provider=="Gog"?"GOG":provider=="Scan"?RiotGames.Source:provider;
   Row row;if(bySource.TryGetValue(source,out row))row.Warnings.Add("Falha ao ler instalações");
  }
  foreach(var error in cacheErrors){
   var sources=error=="GOG Galaxy"?new[]{"GOG","EA app","Battle.net"}:new[]{error};
   foreach(var source in sources){
    Row row;if(bySource.TryGetValue(source,out row))row.Warnings.Add("Falha ao ler cache ou catálogo");
   }
  }
  Row xbox;
  if(xboxError!=""&&bySource.TryGetValue(XboxGames.Source,out xbox))xbox.Warnings.Add("Falha ao consultar o Windows");
 }
 internal static void Verify(){
  var report=new UpdateReport(new[]{"Steam","GOG","EA app"});
  var installed=new Game{StoreId="steam:1",Source="Steam"};
  var missing=new Game{StoreId="steam:2",Source="Steam"};
  report.ReadGames(new[]{installed,installed},new[]{installed,missing},new[]{installed});
  report.Cover("Steam");report.Time("Steam","Steam");report.ReadErrors("",new[]{"GOG Galaxy"},"");
  if(report.TotalGames!=2||report.TotalCovers!=1||report.Rows[0].Available!=1||report.Rows[1].Warnings.Count!=1||report.Rows[2].Warnings.Count!=1)
   throw new InvalidOperationException("Resumo da atualização.");
 }
}

partial class Hub {
 void ShowUpdateReport(){
  var report=latestUpdate;
  if(report==null)return;
  using(var dialog=new Form{Text="Resumo da atualização",ClientSize=new Size(905,421),MinimumSize=new Size(725,390),
   StartPosition=FormStartPosition.CenterParent,BackColor=BackColor,ForeColor=ForeColor,Font=Font,Padding=new Padding(19),
   MaximizeBox=false,MinimizeBox=false}){
   var heading=new Label{Text="Resumo da atualização",Font=Skin.HeadingFont(21),Dock=DockStyle.Top,Height=44};
   var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,
    RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,
    BackgroundColor=BackColor,BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,
    ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,EnableHeadersVisualStyles=false,GridColor=Skin.Mix(BackColor,ForeColor,.12f),
    AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,RowTemplate={Height=30}};
   var rows=new DataGridViewCellStyle{BackColor=BackColor,ForeColor=ForeColor,SelectionBackColor=Skin.Mix(surface,accent,.2f),
    SelectionForeColor=ForeColor,Padding=new Padding(6,3,6,3)};
   grid.DefaultCellStyle=rows;
   grid.AlternatingRowsDefaultCellStyle=new DataGridViewCellStyle(rows){BackColor=Skin.Mix(BackColor,surface,.45f)};
   grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle(rows){BackColor=surface};
   foreach(var title in new[]{"Launcher","Instalados","Disponíveis","Capas","Tempos","Fonte e avisos"})
    grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=title,Width=title=="Launcher"?157:86,SortMode=DataGridViewColumnSortMode.NotSortable});
   grid.Columns[5].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
   grid.Columns[5].DefaultCellStyle.WrapMode=DataGridViewTriState.True;
   foreach(var row in report.Rows){
    var notes=new List<string>{row.LibrarySource};
    if(row.TimeSources.Count>0)notes.Add("Tempos: "+String.Join(", ",row.TimeSources.OrderBy(name=>name)));
    notes.AddRange(row.Warnings);
    grid.Rows.Add(row.Name,row.Installed,row.Available,row.Covers,row.Times,String.Join(" · ",notes));
   }
   var footer=new SoftFlow{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft};
   var close=Action(footer,"Fechar",()=>dialog.Close());close.Primary=true;dialog.AcceptButton=close;dialog.CancelButton=close;
   var caption=new Label{Dock=DockStyle.Bottom,Height=37,TextAlign=ContentAlignment.MiddleLeft,
    Text=report.ArtFailed?"A busca de capas foi interrompida. O resumo mostra o que foi atualizado até a falha.":
    "Disponíveis são jogos desinstalados encontrados no cache ou catálogo. Capas e tempos mostram as mudanças desta atualização."};
   dialog.Controls.Add(grid);dialog.Controls.Add(caption);dialog.Controls.Add(footer);dialog.Controls.Add(heading);
   dialog.Shown+=(sender,args)=>grid.ClearSelection();
   dialog.ShowDialog(this);
  }
 }

 void ShowTour(bool remember=true,IWin32Window owner=null){
  string[] titles={"Sua biblioteca","Jogos avulsos e ROMs","Seu progresso"};
  string[] descriptions={
   "Use Atualizar jogos para buscar seus jogos nos launchers. Abra Ver resumo para conferir o que veio de cada plataforma. Use os filtros para encontrar um jogo.",
   "Em Adicionar, escolha um executável ou um emulador e a pasta das ROMs. Depois, selecione o jogo e clique em Jogar.",
   "Use Favoritos e Coleções para organizar a biblioteca. Marque jogos como zerados e consulte o Histórico para ver o progresso dos desinstalados."
  };
  using(var dialog=new Form{Text="Mini tour do FitaHex",ClientSize=new Size(484,241),StartPosition=FormStartPosition.CenterParent,
   FormBorderStyle=FormBorderStyle.FixedDialog,BackColor=BackColor,ForeColor=ForeColor,Font=Font,Padding=new Padding(19),
   MaximizeBox=false,MinimizeBox=false}){
   var step=new Label{Dock=DockStyle.Top,Height=26,ForeColor=NexusTheme.Find(data.Theme).Muted};
   var heading=new Label{Dock=DockStyle.Top,Height=47,Font=Skin.HeadingFont(21)};
   var description=new Label{Dock=DockStyle.Fill,Padding=new Padding(0,5,4,0)};
   int page=0;
   var footer=new SoftFlow{Dock=DockStyle.Bottom,Height=44,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};
   HubButton next=null,back=null;
   Action show=()=>{step.Text=(page+1)+" de "+titles.Length;heading.Text=titles[page];description.Text=descriptions[page];
    back.Enabled=page>0;next.Text=page+1==titles.Length?"Concluir":"Próximo";};
   next=Action(footer,"Próximo",()=>{if(page==titles.Length-1)dialog.Close();else{page++;show();}});next.Primary=true;
   back=Action(footer,"Anterior",()=>{if(page>0){page--;show();}});
   var skip=Action(footer,"Pular",()=>dialog.Close());
   dialog.AcceptButton=next;dialog.CancelButton=skip;
   dialog.Controls.Add(description);dialog.Controls.Add(footer);dialog.Controls.Add(heading);dialog.Controls.Add(step);
   show();dialog.ShowDialog(owner??this);
  }
  if(remember){data.TourShown=true;Safe(Save);}
 }
}
