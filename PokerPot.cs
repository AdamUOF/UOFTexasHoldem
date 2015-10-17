#region References

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace Server.Engines.TexasHoldem
{
    public class PokerPot
    {
        public Dictionary<PokerPlayer, int> ContributionToPot;

        public PokerPot()
        {
            ContributionToPot = new Dictionary<PokerPlayer, int>();
        }

        public PokerPot(Dictionary<PokerPlayer, int> contributers)
        {
            ContributionToPot = new Dictionary<PokerPlayer, int>(contributers);
        }

        public int GetTotalCurrency()
        {
            return ContributionToPot.Sum(x => x.Value);
        }

        public void AddtoPot(int amount, PokerPlayer player)
        {
            if (ContributionToPot.ContainsKey(player))
            {
                ContributionToPot[player] += amount;
            }
            else
            {
                ContributionToPot.Add(player, amount);
            }
        }

        public PokerPot TrySplitPot()
        {
            var minamount = ContributionToPot.Min(x => x.Value);

            var toreturn = new Dictionary<PokerPlayer, int>();

            foreach (var kvp in ContributionToPot.ToArray())
            {
                if (kvp.Value > minamount && !kvp.Key.HasFolded)
                {
                    toreturn.Add(kvp.Key, kvp.Value-minamount);
                    ContributionToPot[kvp.Key] = minamount;
                }
            }

            if (toreturn.Count > 0)
                return new PokerPot(toreturn);

            return null;
        }

        public void AwardPot(List<PokerPlayer> rankedPlayerList)
        {
            var elligibleplayers = ElligiblePlayers(rankedPlayerList);
            var highesthand = elligibleplayers.Max(x => x.HandRank);

            var winners = elligibleplayers.Where(x => x.HandRank == highesthand).ToList();

            var totalpot = 0;
            var share = 0;

            foreach (var kvp in ContributionToPot.Where(x => !winners.Contains(x.Key)).ToList())
            {
                totalpot += ContributionToPot[kvp.Key];
                kvp.Key.AwardCredit(-ContributionToPot[kvp.Key]);
            }

            share = totalpot / winners.Count;

            foreach (var winner in winners)
            {
                winner.AwardCredit(share);
                winner.ReturnCredit(ContributionToPot[winner]);
            }
        }

        public List<PokerPlayer> ElligiblePlayers(List<PokerPlayer> allPlayers)
        {
            return allPlayers.Where(x => ContributionToPot.ContainsKey(x)).ToList();
        }
    }
}