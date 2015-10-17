using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Engines.TexasHoldem
{
    public class PokerPlayerObj
    {
        public int HandId { get; set; }
        public int CharacterSerial { get; set; }
        public int Won { get; set; }
        public int Bankroll { get; set; }

        public int Folded { get; set; }

        public string HoleCards { get; set; }

        public string Hand { get; set; }

        public PokerPlayerObj(int handid, PokerPlayer player)
        {
            HandId = handid;

            CharacterSerial = player.Owner.Serial.Value;

            Won = player.AmountWon;

            Bankroll = player.StartCurrency;

            StringBuilder sb = new StringBuilder();

            foreach (var card in player.HoleCards)
            {
                if (player.HoleCards.Last() != card)
                {
                    sb.Append(card.GetRankLetterExport() + card.GetSuitLetter() + " ");
                }
                else
                {
                    sb.Append(card.GetRankLetterExport() + card.GetSuitLetter());
                }
            }

            HoleCards = sb.ToString();

            Hand = player.GetHandString();
        }
    }
}
