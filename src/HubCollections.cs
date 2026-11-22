using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

partial class Hub {
 readonly SoftPanel collectionShowcase=new SoftPanel();
 readonly SoftFlow collectionTiles=new SoftFlow();
 void SetupCollectionShowcase(){
  collectionShowcase.Dock=DockStyle.Top;collectionShowcase.Height=178;collectionShowcase.Visible=false;
  var actions=new SoftFlow{Dock=DockStyle.Top,Height=47};Action(actions,"＋ Nova coleção",()=>Collection(null));
  collectionTiles.Dock=DockStyle.Fill;collectionTiles.AutoScroll=true;collectionTiles.WrapContents=false;
  collectionShowcase.Controls.Add(collectionTiles);collectionShowcase.Controls.Add(actions);
  libraryPage.Controls.Add(collectionShowcase);
 }
 void UpdateCollectionShowcase(Shelf[] shelves){
  bool overview=section=="Coleções";
  collectionShowcase.Visible=overview;hero.Visible=!overview;
  if(!overview)return;
  collectionTiles.SuspendLayout();
  try{
   Clear(collectionTiles);
   foreach(var shelf in shelves){
    var item=shelf;var titles=data.Games.Where(g=>BelongsTo(g,item)).ToArray();
    var tile=new SoftPanel{Width=172,Height=103,BackColor=surface,Margin=new Padding(0,1,9,0)};
    var hit=Action(collectionTiles,item.Name+"  ·  "+titles.Length,()=>Navigate("shelf:"+item.Name));
    hit.AccessibleName="Abrir coleção "+item.Name+" com "+titles.Length+" jogos";
    collectionTiles.Controls.Remove(hit);hit.Dock=DockStyle.Bottom;hit.AutoSize=false;hit.Height=32;
    tile.Controls.Add(hit);
    tile.Paint+=(s,e)=>{
     var previews=titles.Where(g=>g.Cover!="").Take(4).ToArray();
     if(previews.Length==0){Skin.Symbol(e.Graphics,"folder",new Rectangle(71,18,30,30),accent);return;}
     for(int index=0;index<previews.Length;index++){
      var area=new Rectangle(7+index*40,6,36,62);
      var thumb=Thumbnail(previews[index].Cover);
      if(thumb>=cardImages.Count)continue;
      using(var shape=Skin.Round(area,5)){
       var state=e.Graphics.Save();e.Graphics.SetClip(shape);
       Skin.Crop(e.Graphics,cardImages[thumb],area);e.Graphics.Restore(state);
      }
     }
    };
    tile.Cursor=Cursors.Hand;tile.Click+=(s,e)=>Navigate("shelf:"+item.Name);
    collectionTiles.Controls.Add(tile);
   }
  }finally{collectionTiles.ResumeLayout();}
 }
 bool InCollections{get{return section=="Coleções"||section.StartsWith("shelf:",StringComparison.Ordinal);}}
 void Collection(Shelf existing){
  using(var f=new Editor(existing==null?"Nova coleção":"Renomear coleção")){
   f.MinimumSize=new Size(380,180);f.Size=new Size(460,190);
   f.Add("Nome",existing==null?"":existing.Name);
   if(f.ShowDialog(this)!=DialogResult.OK)return;
   string name=f.V("Nome");
   if(name==""||data.Shelves.Any(s=>s!=existing&&s.Name.Equals(name,StringComparison.CurrentCultureIgnoreCase))){
    MessageBox.Show(this,"Escolha um nome preenchido e diferente das outras coleções.","Coleções");return;
   }
   if(existing==null)data.Shelves.Add(new Shelf{Name=name});
   else{
    if(chosenCollections.Remove(existing.Name))chosenCollections.Add(name);
    existing.Name=name;
   }
   Save();Navigate("shelf:"+name);BuildNav();RefreshList();
  }
 }
 void DeleteCollection(Shelf shelf){
  if(MessageBox.Show(this,"Excluir a coleção "+shelf.Name+"?","Coleções",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  data.Shelves.Remove(shelf);RemoveCollectionFilter(shelf.Name);
  Save();Navigate("Coleções");BuildNav();RefreshList();
 }
 void ManageCollection(Shelf shelf){
  using(var dialog=new Form{Text="Jogos de "+shelf.Name,StartPosition=FormStartPosition.CenterParent,ClientSize=new Size(615,510),MinimumSize=new Size(490,420),BackColor=BackColor,ForeColor=ForeColor,Font=Font,Padding=new Padding(14),MinimizeBox=false}){
   var checkedIds=new HashSet<string>(shelf.Ids);
   var items=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,BackColor=surface,ForeColor=ForeColor,BorderStyle=BorderStyle.None,IntegralHeight=false};
   var query=new TextBox{Dock=DockStyle.Top,AccessibleName="Buscar jogos para a coleção",BackColor=surface,ForeColor=ForeColor};
   var cue=new Label{Text="Busque e marque os jogos desta coleção.",Dock=DockStyle.Top,Height=31};
   var saveRow=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft};
   var save=Action(saveRow,"Salvar",()=>dialog.DialogResult=DialogResult.OK);save.Active=true;
   var cancel=Action(saveRow,"Cancelar",()=>dialog.DialogResult=DialogResult.Cancel);
   dialog.AcceptButton=save;dialog.CancelButton=cancel;
   var choices=data.Games.OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToArray();
   bool updating=false;
   Action refresh=()=>{
    updating=true;items.BeginUpdate();
    try{
     items.Items.Clear();
     foreach(var game in choices.Where(g=>Match(g,query.Text)))items.Items.Add(new CollectionChoice(game),checkedIds.Contains(game.Id));
    }finally{items.EndUpdate();updating=false;}
   };
   items.ItemCheck+=(s,e)=>{
    if(updating)return;
    var game=((CollectionChoice)items.Items[e.Index]).Game;
    if(e.NewValue==CheckState.Checked)checkedIds.Add(game.Id);else checkedIds.Remove(game.Id);
   };
   query.TextChanged+=(s,e)=>refresh();
   dialog.Controls.Add(items);dialog.Controls.Add(saveRow);dialog.Controls.Add(query);dialog.Controls.Add(cue);
   refresh();
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;
   shelf.Ids=data.Games.Where(g=>checkedIds.Contains(g.Id)).Select(g=>g.Id).ToList();
   Save();RefreshList();
  }
 }
 sealed class CollectionChoice {
  internal readonly Game Game;
  internal CollectionChoice(Game game){Game=game;}
  public override string ToString(){return Game.Name+"   ·   "+Origin(Game);}
 }
 internal void TestCollections(){
  var game=data.Games.First();
  var collection=new Shelf{Name="Coleção de teste",Ids=new List<string>{game.Id}};
  var blank=new Shelf{Name="Coleção vazia"};
  var second=new Shelf{Name="Outra coleção",Ids=new List<string>{game.Id}};
  data.Shelves.Add(collection);data.Shelves.Add(blank);data.Shelves.Add(second);
  try{
   if(navigation.ContainsKey("shelf:"+collection.Name))throw new InvalidOperationException("Submenu de coleções fechado.");
   Navigate("Coleções");
   if(!navigation.ContainsKey("shelf:"+collection.Name)||shown!=1||list.Items[0].Tag!=game)throw new InvalidOperationException("Visão das coleções.");
   if(list.Groups.Count!=3||list.Groups[0].Header!=collection.Name||list.Groups[2].Header!=second.Name||list.Items.Cast<ListViewItem>().Count(item=>item.Tag==game)!=2)throw new InvalidOperationException("Tiras e jogos compartilhados.");
   Navigate("shelf:"+blank.Name);
   if(shown!=0||!navigation["shelf:"+blank.Name].Active)throw new InvalidOperationException("Coleção vazia.");
   Navigate("shelf:"+collection.Name);
   if(shown!=1||list.Items[0].Tag!=game)throw new InvalidOperationException("Jogos da coleção.");
  }finally{data.Shelves.Remove(collection);data.Shelves.Remove(blank);data.Shelves.Remove(second);Navigate("Jogos");}
 }
}

