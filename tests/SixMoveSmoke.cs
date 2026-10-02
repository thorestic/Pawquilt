using System;
using System.Collections.Generic;
using System.Drawing;
namespace Qa3moz {
    // Focused integration smoke checks only; never opens an overlay or touches settings.
    internal static class SixMoveSmoke {
        static int checks;
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        static void Advance(Interaction model,double seconds,double idle){for(int i=0;i<(int)Math.Ceiling(seconds/.025);i++)model.Tick(.025,idle,180);}
        [STAThread] public static int Main(string[] args){try{
            using(var art=new PixelArt(args[0])) {
                Check(art.Ready && art.HasMoves,"Complete base and six-move art required");
                int[] totals={340,620,580,830,2230,1580};int renders=0;
                for(int n=0;n<PixelArt.MoveNames.Length;n++) {
                    string name=PixelArt.MoveNames[n];
                    Check(Math.Abs(art.Duration(name)*1000-totals[n])<.001,"Authoritative timing "+name);
                    Check(art.Frame(name,999,false)==PixelArt.MoveCounts[n]-1,"One-shot final frame "+name);
                    for(int frame=0;frame<PixelArt.MoveCounts[n];frame++)foreach(int size in new[]{128,256}) {
                        using(var image=SpriteArt.Render(art.Strip(name),new Rectangle(frame*128,0,128,128),size,size,true,false)) {
                            bool visible=false,hole=false;for(int y=0;y<size;y++)for(int x=0;x<size;x++){int alpha=image.GetPixel(x,y).A;visible|=alpha>=16;hole|=alpha==0;}
                            Check(visible && hole,"Rendered silhouette "+name+" "+frame);renders++;
                        }
                    }
                }
                var displays=new List<Display>{new Display("left",-1920,0,0,1080,1),new Display("right",0,0,1920,1200,1.25)};
                var motion=new Motion(displays,731){UseSixMoves=true,Variation=true};
                var rest=new Interaction{UseSixMoves=true,StretchDuration=art.Duration("sleepy_stretch")};
                rest.RestNow();Check(rest.State==Reaction.Stretch,"Manual rest begins combined strip");
                Advance(rest,2.0,0);Check(rest.State==Reaction.Stretch,"Full stretch duration honored");
                Advance(rest,.4,0);Check(rest.State==Reaction.Sleep && rest.ManualRest,"Combined strip reaches latched sleep");
                Advance(rest,3,0);Check(rest.State==Reaction.Sleep,"Unrelated input cannot wake manual sleep");
                rest.Cancel(motion,true);Check(rest.State==Reaction.Sleep && rest.ManualRest,"Context cancellation preserves rest");
                rest.Friendly(true);rest.RequestPetting();Check(rest.State==Reaction.Sleep,"Autonomous reactions cannot interrupt rest");
                rest.WakeNow();Advance(rest,1,0);Check(!rest.ManualRest && rest.State==Reaction.None,"Explicit wake completes");
                var automatic=new Interaction{UseSixMoves=true,StretchDuration=art.Duration("sleepy_stretch")};
                Advance(automatic,2.5,181);Check(automatic.State==Reaction.Sleep && !automatic.ManualRest,"Automatic idle sleep");
                automatic.Tick(.025,0,180);Check(automatic.State==Reaction.Wake,"Input resumes automatic sleep");
                var pet=new Interaction{UseSixMoves=true,HappyDuration=art.Duration("happy_jump")};
                pet.Down(new Vec(0,0),.53,.3);pet.Move(new Vec(0,0),.53,.3,1,10);pet.Move(new Vec(12,0),.53,.3,1,10);pet.Up(motion);
                Check(pet.State==Reaction.Pet,"Head stroke starts petting");Advance(pet,.9,0);Check(pet.State==Reaction.HappyJump,"Petting transitions to happy jump");
                Vec anchor=motion.Position;Advance(pet,1.1,0);Check(pet.State==Reaction.None && (motion.Position-anchor).Length==0,"Embedded jump has no additional world arc");
                pet.Friendly();Check(pet.State==Reaction.Scratch,"Idle scratch connected");Advance(pet,1,0);Check(pet.State==Reaction.None,"Scratch completion");
                pet.Friendly(true);Check(pet.State==Reaction.Somersault,"Idle somersault connected");Advance(pet,1,0);Check(pet.State==Reaction.None,"Somersault and landing complete");
                pet.RequestPetting();Check(pet.State==Reaction.Request,"Affection request connected");Advance(pet,1.7,0);Check(pet.State==Reaction.None,"Request finishes");
                motion.Paused=true;motion.Tick(.05,0,null);Check((motion.Position-anchor).Length==0,"Pause remains latched");motion.Paused=false;
                bool run=false;for(int i=0;i<6000;i++){motion.Tick(.05,0,null);run|=motion.Running;Check(motion.FitsWorkAreas,"Seeded motion remains in work areas");}
                Check(run,"Occasional light run selected");
                var held=new Interaction{UseSixMoves=true};held.Down(new Vec(0,0),.7,.8);held.Move(new Vec(20,0),.7,.8,1,10);Check(held.Dragging,"Body drag retained");held.Cancel(motion,true);Check(!held.Dragging && !held.Armed,"Capture cancellation retained");
                Console.WriteLine("PASS: "+checks+" focused checks; "+renders+" new-frame renders; base art, manual/automatic sleep, pet/jump/landing, request, scratch, flip, pause, drag cancellation and seeded run/bounds.");
            }return 0;
        }catch(Exception error){Console.Error.WriteLine("SIX-MOVE SMOKE FAILED: "+error.Message);return 2;}}
    }
}
