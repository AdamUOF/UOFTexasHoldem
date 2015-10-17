#region References

using System;
using System.Drawing;
using Server.Gumps;
using Server.Mobiles;
using VitaNex.SuperGumps;
using VitaNex.SuperGumps.UI;

#endregion

namespace Server.Engines.TexasHoldem
{
    public class PokerWebsiteGump : DialogGump
    {
        public PokerWebsiteGump(PlayerMobile user, int handid, Gump parent = null)
            : base(user, parent, 200, 270)
        {
            Closable = true;
            Disposable = false;
            Dragable = true;
            Resizable = false;
            HandId = handid;
        }

        private int HandId { get; set; }

        protected override void CompileLayout(SuperGumpLayout layout)
        {
            layout.Add(
                "Main",
                () =>
                {
                    AddBackground(0, 0, 418, 56, 5120);
                    AddBackground(37, 28, 350, 22, 9350);

                    AddHtml(9, 6, 408, 22, string.Format("<BIG><CENTER>{0}</CENTER></BIG>", "View This Hand on Ultima Online: Forever Legends").WrapUOHtmlColor(Color.Gold), false, false);
                    AddHtml(44, 29, 336, 22, string.Format("<CENTER><BIG><A HREF=\"{0}{1}\">{0}{1}</A></BIG></CENTER>", "legends.uoforever.com/pokerhands/".WrapUOHtmlColor(Color.Gold), HandId), false, false);
                });
        }
    }
}