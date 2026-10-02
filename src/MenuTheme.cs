using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Qa3moz {
    internal sealed class PetColors : ProfessionalColorTable {
        internal static readonly Color Background=Color.FromArgb(29,35,39), Cream=Color.FromArgb(242,237,216),
            Emerald=Color.FromArgb(89,213,153), Selected=Color.FromArgb(43,70,59), Border=Color.FromArgb(66,85,75);
        public override Color ToolStripDropDownBackground { get{return Background;} }
        public override Color ImageMarginGradientBegin { get{return Background;} }
        public override Color ImageMarginGradientMiddle { get{return Background;} }
        public override Color ImageMarginGradientEnd { get{return Background;} }
        public override Color MenuBorder { get{return Border;} }
        public override Color MenuItemBorder { get{return Selected;} }
        public override Color MenuItemSelected { get{return Selected;} }
        public override Color MenuItemSelectedGradientBegin { get{return Selected;} }
        public override Color MenuItemSelectedGradientEnd { get{return Selected;} }
        public override Color MenuItemPressedGradientBegin { get{return Selected;} }
        public override Color MenuItemPressedGradientMiddle { get{return Selected;} }
        public override Color MenuItemPressedGradientEnd { get{return Selected;} }
        public override Color CheckBackground { get{return Selected;} }
        public override Color CheckSelectedBackground { get{return Selected;} }
        public override Color CheckPressedBackground { get{return Selected;} }
        public override Color SeparatorDark { get{return Border;} }
        public override Color SeparatorLight { get{return Background;} }
    }
    internal sealed class PetMenuRenderer : ToolStripProfessionalRenderer {
        public PetMenuRenderer():base(new PetColors()){RoundedEdges=true;}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
            e.TextColor=e.Item.Enabled?PetColors.Cream:Color.FromArgb(142,158,148);
            if(e.Item is ToolStripLabel)e.TextColor=PetColors.Emerald;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e){e.ArrowColor=PetColors.Emerald;base.OnRenderArrow(e);}
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
            Rectangle r=e.ImageRectangle;
            using(var pen=new Pen(PetColors.Emerald,2)) {
                e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                e.Graphics.DrawLines(pen,new[]{new Point(r.Left+2,r.Top+r.Height/2),new Point(r.Left+r.Width/2-1,r.Bottom-4),new Point(r.Right-2,r.Top+3)});
            }
        }
    }
    internal sealed class PetMenu : ContextMenuStrip {
        readonly Font themeFont=new Font("Segoe UI",10,FontStyle.Regular);
        public PetMenu() {
            Renderer=new PetMenuRenderer();BackColor=PetColors.Background;ForeColor=PetColors.Cream;
            Font=themeFont;Padding=new Padding(6);ShowImageMargin=false;ShowCheckMargin=true;
            ItemAdded+=delegate(object sender,ToolStripItemEventArgs e){Style(e.Item);};
        }
        void Style(ToolStripItem item) {
            item.Padding=new Padding(7,4,7,4);item.Margin=new Padding(0,1,0,1);
            var parent=item as ToolStripMenuItem;
            if(parent!=null) {
                parent.DropDown.Renderer=Renderer;parent.DropDown.BackColor=PetColors.Background;
                parent.DropDown.ForeColor=PetColors.Cream;parent.DropDown.Font=themeFont;
                parent.DropDown.ItemAdded+=delegate(object sender,ToolStripItemEventArgs e){Style(e.Item);};
            }
        }
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing)themeFont.Dispose();}
    }
}
