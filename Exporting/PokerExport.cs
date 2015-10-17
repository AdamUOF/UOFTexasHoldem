#region References

using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Server.Mobiles;
using VitaNex;
using VitaNex.Data.SQL;
using VitaNex.Modules.UOFLegends;

#endregion

namespace Server.Engines.TexasHoldem
{
    public class PokerExport
    {
        private List<PokerAction> Actions { get; set; }

        public PokerExport()
        {
            Actions = new List<PokerAction>();
        }

        public void AddAction(PlayerMobile actor, PlayerAction action, PokerGameState state, int amountbet)
        {
            Actions.Add(new PokerAction(actor, action, state, amountbet));
        }

        public void ProcessHand(PokerGame game)
        {
            var task = new Task(async () =>
            {
                object handidobject = await UOFLegends.InsertWithReturn("pokerhands", new List<PokerHand>{new PokerHand(game)}, "id");

                if (handidobject != null)
                {
                    var handid = (int) handidobject;

                    foreach (var action in Actions)
                    {
                        action.HandId = handid;
                    }

                    await UOFLegends.InsertWithReturn("pokeractions", Actions, "id");

                    List<PokerPlayerObj> playerobjects = game.ActivePlayers.Select(player => new PokerPlayerObj(handid, player)).ToList();

                    await UOFLegends.InsertWithReturn("pokerplayers", playerobjects, "id");

                    Actions = new List<PokerAction>();

                    foreach (var player in game.Players.ToArray())
                    {
                        new PokerWebsiteGump(player.Owner, handid).Send();
                    }

                    foreach (var viewer in game.Viewers.ToArray())
                    {
                        new PokerWebsiteGump(viewer, handid).Send();
                    }

                    //send gump here uoforever.com/legends/pokerhands/handid
                }
            });

            task.Start();
        }
    }
}