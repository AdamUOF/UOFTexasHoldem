using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Mobiles;

namespace Server.Engines.TexasHoldem
{
    public class PokerAction
    {
        public int HandId { get; set; }
        public int PlayerSerial { get; set; }
        public int State { get; set; }
        public int Type { get; set; }
        public int Amount { get; set; }

        public PokerAction(PlayerMobile actor, PlayerAction action, PokerGameState state, int amountbet)
        {
            PlayerSerial = actor.Serial;
            Type = (int) action;
            State = (int) state;
            Amount = amountbet;
        }
    }
}
