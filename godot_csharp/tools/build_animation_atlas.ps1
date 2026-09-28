param([switch]$InspectOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common,System.Drawing.Primitives,System.Collections,System.Private.Windows.GdiPlus,System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
public static class AnimationAtlas {
  // ImageGen source conversion only. No generated limbs, poses or interpolated frames.
  // Every final pixel samples the explicitly selected source cell (nearest neighbour).
  public static string Build(string root, string actor, bool inspectOnly) {
    var atlas = new Bitmap(512,1536,PixelFormat.Format32bppArgb);
    string[] dirs={"front","back","left"};
    int[] counts={1,8,3,4,4,4,6,7};
    string[] actions={"idle","walk","talk","inspect","interact","sit","break","cast"};
    int[][] cells={new[]{0},new[]{8,9,10,11,12,13,14,15},new[]{1,2,3},new[]{4,5,6,7},new[]{16,17,18,19},new[]{20,21,22,23},new[]{24,25,26,27,28,29},new[]{32,33,34,35,36,37,38}};
    var report=new List<string>(); var resources=new List<string>(); var anims=new List<string>();
    int total=0;
    var sitImage=new Bitmap(Path.Combine(root,"godot_csharp/AssetsSource/Internal/Characters/AnimationV2/sit-left.png"));
    var sitBounds=new Rectangle[4];
    for(int f=0;f<4;f++) {
      int y0=actor=="detective"?0:sitImage.Height/2,y1=actor=="detective"?sitImage.Height/2:sitImage.Height;
      int x0=f*sitImage.Width/4,x1=(f+1)*sitImage.Width/4;
      int minX=x1,minY=y1,maxX=-1,maxY=-1;
      for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++) if(sitImage.GetPixel(x,y).A>=192) {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
      sitBounds[f]=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
    }
    for(int d=0;d<3;d++) {
      string src=Path.Combine(root,"godot_csharp/AssetsSource/Internal/Characters/AnimationV2/"+actor+"-"+dirs[d]+".png");
      using(var image=new Bitmap(src)) {
        // Generated source gutters are measured, never assumed to be exact tiles.
        int[] rowEdges=new int[6]; rowEdges[5]=image.Height;
        for(int r=1;r<5;r++) {
          int lo=(int)(image.Height*(r/5.0-0.06)), hi=(int)(image.Height*(r/5.0+0.06));
          int bestStart=-1,bestLength=0,runStart=-1;
          for(int y=lo;y<hi;y++) {
            bool clear=true;
            for(int x=0;x<image.Width;x++) if(image.GetPixel(x,y).A>=192) {clear=false;break;}
            if(clear && runStart<0) runStart=y;
            if((!clear || y==hi-1) && runStart>=0) {
              int len=y-runStart;
              if(len>bestLength){bestLength=len;bestStart=runStart;}
              runStart=-1;
            }
          }
          if(bestStart<0) throw new Exception("No clear source row gutter: "+src+" row "+r);
          rowEdges[r]=bestStart+bestLength/2;
        }
        report.Add("rowEdges="+String.Join(",",rowEdges));
        var bounds=new Rectangle[40];
        for(int i=0;i<40;i++) {
          int x0=(int)Math.Round((i%8)*image.Width/8.0), x1=(int)Math.Round((i%8+1)*image.Width/8.0);
          int y0=rowEdges[i/8], y1=rowEdges[i/8+1];
          int minX=x1,minY=y1,maxX=-1,maxY=-1;
          for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++) if(image.GetPixel(x,y).A>=192) {minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
          bounds[i]=maxX<0?Rectangle.Empty:Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
          report.Add(actor+" "+dirs[d]+" cell "+i+" bounds "+bounds[i]);
        }
        if(bounds[0].IsEmpty) throw new Exception("missing idle "+src);
        double scale=43.0/bounds[0].Height;
        for(int a=0;a<actions.Length;a++) {
          var entries=new List<string>();
          for(int f=0;f<counts[a];f++) {
            int cell=cells[a][f]; var b=bounds[cell];
            Bitmap frameImage=image; double frameScale=scale; int idleHeight=bounds[0].Height;
            if(a==5 && d==2) {frameImage=sitImage;b=sitBounds[f];idleHeight=sitBounds[0].Height;frameScale=43.0/idleHeight;}
            if(b.IsEmpty) throw new Exception("empty source "+src+" cell "+cell);
            // Register at the center of the two shoes, never at extended hands/coat bbox.
            int footMin=b.Right,footMax=-1;
            for(int y=b.Bottom-Math.Max(3,(int)(idleHeight*0.08));y<b.Bottom;y++) for(int x=b.Left;x<b.Right;x++) if(frameImage.GetPixel(x,y).A>=192) {footMin=Math.Min(footMin,x);footMax=Math.Max(footMax,x);}
            double footX=(footMin+footMax+1)/2.0;
            int w=(int)Math.Round(b.Width*frameScale),h=(int)Math.Round(b.Height*frameScale);
            int ox=32+(int)Math.Round((b.Left-footX)*frameScale),oy=55-h;
            if(ox<1 || ox+w>63 || oy<1) throw new Exception("frame exceeds canvas "+src+" "+cell+" bounds="+b+" scale="+scale+" target="+ox+","+oy+","+w+","+h);
            int row=d*8+a;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
              int sx=b.Left+Math.Min(b.Width-1,(int)((x+0.5)*b.Width/w));
              int sy=b.Top+Math.Min(b.Height-1,(int)((y+0.5)*b.Height/h));
              var c=frameImage.GetPixel(sx,sy);
              if(c.A>=192) atlas.SetPixel(f*64+ox+x,row*64+oy+y,Color.FromArgb(255,c.R,c.G,c.B));
            }
            string id="f_"+d+"_"+a+"_"+f;
            resources.Add("[sub_resource type=\"AtlasTexture\" id=\""+id+"\"]\natlas = ExtResource(\"1\")\nregion = Rect2("+(f*64)+", "+(row*64)+", 64, 64)\n");
            entries.Add("{\"duration\": 1.0, \"texture\": SubResource(\""+id+"\")}");
            total++;
          }
          bool loop=actions[a]=="idle"||actions[a]=="walk";
          anims.Add("{\"frames\": ["+String.Join(", ",entries)+"], \"loop\": "+(loop?"true":"false")+", \"name\": &\""+actions[a]+"_"+dirs[d]+"\", \"speed\": "+(actions[a]=="walk"?"10.0":"7.0")+"}");
        }
      }
    }
    string outdir=Path.Combine(root,"godot_csharp/ShareAsset/Internal/Characters/animation_v2");
    Directory.CreateDirectory(outdir);
    if(!inspectOnly) {
      atlas.Save(Path.Combine(outdir,actor+".png"),ImageFormat.Png);
      File.WriteAllText(Path.Combine(outdir,actor+".tres"),"[gd_resource type=\"SpriteFrames\" load_steps="+(total+2)+" format=3]\n\n[ext_resource type=\"Texture2D\" path=\"res://ShareAsset/Internal/Characters/animation_v2/"+actor+".png\" id=\"1\"]\n\n"+String.Join("\n",resources)+"\n[resource]\nanimations = ["+String.Join(",\n",anims)+"]\n");
    }
    File.WriteAllLines(Path.Combine(root,"artifacts/character-animation-v2/"+actor+"-bounds.txt"),report);
    sitImage.Dispose(); atlas.Dispose(); return actor+": "+total+" frames, 24 animations, 512x1536 atlas, binary alpha, 64x64 cells, anchor (32,55).";
  }
}
'@
$atlasRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
foreach ($atlasActor in @('detective','placeholder')) {
    [AnimationAtlas]::Build($atlasRoot, $atlasActor, $InspectOnly.IsPresent)
}
