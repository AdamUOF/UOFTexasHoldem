using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Server.Engines.TexasHoldem
{
    public class PokerHand
    {
        public int FinalPot { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public string CommunityCards { get; set; }

        public PokerHand(PokerGame game)
        {
            StartTime = game.StartTime;
            EndTime = DateTime.Now;

            FinalPot = game.PokerPots.Sum(x => x.GetTotalCurrency());

            StringBuilder sb = new StringBuilder();

            foreach (var card in game.CommunityCards)
            {
                if (game.CommunityCards.Last() != card)
                {
                    sb.Append(card.GetRankLetterExport() + card.GetSuitLetter() + " ");
                }
                else
                {
                    sb.Append(card.GetRankLetterExport() + card.GetSuitLetter());
                }
            }

            CommunityCards = sb.ToString();
        }
    }
}
