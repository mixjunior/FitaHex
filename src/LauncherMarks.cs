using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

static class LauncherMarks {
 // Brand paths from Simple Icons: its Steam and Riot Games assets.
 const string Steam="M11.979 0C5.678 0 .511 4.86.022 11.037l6.432 2.658c.545-.371 1.203-.59 1.912-.59.063 0 .125.004.188.006l2.861-4.142V8.91c0-2.495 2.028-4.524 4.524-4.524 2.494 0 4.524 2.031 4.524 4.527s-2.03 4.525-4.524 4.525h-.105l-4.076 2.911c0 .052.004.105.004.159 0 1.875-1.515 3.396-3.39 3.396-1.635 0-3.016-1.173-3.331-2.727L.436 15.27C1.862 20.307 6.486 24 11.979 24c6.627 0 11.999-5.373 11.999-12S18.605 0 11.979 0zM7.54 18.21l-1.473-.61c.262.543.714.999 1.314 1.25 1.297.539 2.793-.076 3.332-1.375.263-.63.264-1.319.005-1.949s-.75-1.121-1.377-1.383c-.624-.26-1.29-.249-1.878-.03l1.523.63c.956.4 1.409 1.5 1.009 2.455-.397.957-1.497 1.41-2.454 1.012H7.54zm11.415-9.303c0-1.662-1.353-3.015-3.015-3.015-1.665 0-3.015 1.353-3.015 3.015 0 1.665 1.35 3.015 3.015 3.015 1.663 0 3.015-1.35 3.015-3.015zm-5.273-.005c0-1.252 1.013-2.266 2.265-2.266 1.249 0 2.266 1.014 2.266 2.266 0 1.251-1.017 2.265-2.266 2.265-1.253 0-2.265-1.014-2.265-2.265z";
 const string Riot="M12.534 21.77l-1.09-2.81 10.52.54-.451 4.5zM15.06 0L.307 6.969 2.59 17.471H5.6l-.52-7.512.461-.144 1.81 7.656h3.126l-.116-9.15.462-.144 1.582 9.294h3.31l.78-11.053.462-.144.82 11.197h4.376l1.54-15.37Z";
 static PointF Point(string[] tokens,ref int i,PointF basis){
  return new PointF(basis.X+Number(tokens[i++]),basis.Y+Number(tokens[i++]));
 }
 static float Number(string token){return Single.Parse(token,CultureInfo.InvariantCulture);}
 static GraphicsPath Parse(string svg){
  var tokens=Regex.Matches(svg,@"[MLHVCSZmlhvcsz]|-?(?:\d+(?:\.\d+)?|\.\d+)").Cast<Match>().Select(m=>m.Value).ToArray();
  var path=new GraphicsPath(FillMode.Winding);
  var current=PointF.Empty;var start=current;var control=current;char operation='M',last=' ';
  int i=0;
  while(i<tokens.Length){
   if(Char.IsLetter(tokens[i][0]))operation=tokens[i++][0];
   var basis=Char.IsLower(operation)?current:PointF.Empty;
   char kind=Char.ToUpperInvariant(operation);
   switch(kind){
    case 'M':current=Point(tokens,ref i,basis);start=current;path.StartFigure();operation=operation=='m'?'l':'L';break;
    case 'L':var next=Point(tokens,ref i,basis);path.AddLine(current,next);current=next;break;
    case 'H':var h=new PointF(basis.X+Number(tokens[i++]),current.Y);path.AddLine(current,h);current=h;break;
    case 'V':var v=new PointF(current.X,basis.Y+Number(tokens[i++]));path.AddLine(current,v);current=v;break;
    case 'C':case 'S':
     var first=kind=='C'?Point(tokens,ref i,basis):last=='C'||last=='S'?new PointF(2*current.X-control.X,2*current.Y-control.Y):current;
     control=Point(tokens,ref i,basis);var end=Point(tokens,ref i,basis);path.AddBezier(current,first,control,end);current=end;break;
    case 'Z':path.CloseFigure();current=start;break;
   }
   last=kind;
  }
  return path;
 }
 internal static Bitmap Embedded(string source){
  var key=source=="GOG"?"gogdotcom":source==XboxGames.Source?"xbox":source=="Epic Games"?"epicgames":source=="EA app"?"ea":
   source=="Ubisoft Connect"?"ubisoft":source=="Battle.net"?"battledotnet":"";
  if(key=="")return null;
  using(var stream=typeof(LauncherMarks).Assembly.GetManifestResourceStream("Nexus.Marks."+key+".png")){
   if(stream==null)return null;
   using(var original=Image.FromStream(stream))return new Bitmap(original);
  }
 }
 internal static Bitmap Create(string source){
  var isSteam=source=="Steam";
  var bitmap=new Bitmap(48,48);
  using(var g=Graphics.FromImage(bitmap))using(var path=Parse(isSteam?Steam:Riot)){
   g.SmoothingMode=SmoothingMode.AntiAlias;
   g.ScaleTransform(2,2);
   using(var fill=new SolidBrush(isSteam?Color.White:Color.FromArgb(255,72,90)))g.FillPath(fill,path);
  }
  return bitmap;
 }
}
