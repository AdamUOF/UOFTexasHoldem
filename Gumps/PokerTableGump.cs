#region References
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Server.Items;
using Server.Mobiles;
using VitaNex.SuperGumps;
using VitaNex.SuperGumps.UI;
#endregion

namespace Server.Engines.TexasHoldem
{
    public sealed class PokerTableGump : ListGump<PokerPlayer>
    {
        private const int CardX = 365;
        private const int CardY = 270;
        private const int Radius = 250;

        private readonly PokerGame _Game;
        private readonly PokerPlayer _Player;
        private bool IsIntermission { get; set; }
        private bool IsDonation { get; set; }

        public PokerTableGump(PlayerMobile user, PokerGame game)
            : base(user, null, 0, 0)
        {
            CanDispose = true;
            CanMove = true;
            Modal = false;
            ForceRecompile = true;

            CanSearch = false;

            EntriesPerPage = game.GetAllActivePlayers();

            _Game = game;
            if (_Game == null)
                return;

            _Game.IsPlayer(User, out _Player);

            Closable = _Game.IsViewer(User) || _Game.IsIntermission();

            Disposable = true;
            Dragable = true;
            Resizable = false;

            IsIntermission = _Game.IsIntermission();
            IsDonation = _Game.IsDonationGame();
        }

        protected override void CompileList(List<PokerPlayer> list)
        {
            list.Clear();

            list.TrimExcess();

            list.AddRange(_Game.ActivePlayers);

            base.CompileList(list);
        }

        protected override void CompileLayout(SuperGumpLayout layout)
        {
            layout.Add(
                "communitycards",
                () =>
                {
                    #region Community Cards
                    var cardcount = _Game.CommunityCards.Count;
                    if (cardcount > 0)
                    {
                        int nextX = CardX;

                        if (cardcount > 2)
                        {
                            //must use count - 2 because you want to account for first 2 cards
                            nextX = CardX - (15 * (cardcount - 2));
                        }

                        foreach (var card in _Game.CommunityCards)
                        {
                            AddBackground(nextX, CardY, 71, 95, 9350);
                            AddLabel(nextX + 10, CardY + 5, card.GetSuitColor(), card.GetRankLetter());
                            AddLabel(nextX + 6, CardY + 25, card.GetSuitColor(), card.GetSuitString());

                            nextX += 30;
                        }
                    }
                    #endregion


                    var pot = _Game.PokerPots.FirstOrDefault();
                    if (pot != null)
                    {
                        var sum = _Game.PokerPots.Sum(x => x.GetTotalCurrency());

                        if (sum > 0)
                        {
                            AddBackground(CardX - 35, CardY + 62, 167, 34, 5120);

                            AddHtml(CardX - 35, CardY + 71, 175, 22,
                                String.Format("<BIG><CENTER>{0:n0}</CENTER></BIG>",
                                    pot.GetTotalCurrency()).WrapUOHtmlColor(Color.Gold),
                                false,
                                false);
                        }
                    }
                });

            var range = GetListRange();

            if (range.Count > 0)
            {
                CompileEntryLayout(layout, range);
            }
        }

        protected override void CompileEntryLayout(
            SuperGumpLayout layout, int length, int index, int pIndex, int yOffset, PokerPlayer entry)
        {
            const int centerY = CardY + Radius;

            layout.Add(
                "entry" + index,
                () =>
                {
                    //if User is a viewer, we want to base his perspective off the first acitve player for simplicity
                    int playerIndex = List.IndexOf(_Player ?? List.FirstOrDefault());
                    int entryIndex = List.IndexOf(entry);

                    var indexDifference = entryIndex - playerIndex;

                    //nameplates are circular around the community cards
                    double xdist = Radius * Math.Sin(indexDifference * 2.0 * Math.PI / List.Count);
                    double ydist = Radius * Math.Cos(indexDifference * 2.0 * Math.PI / List.Count);

                    int x = CardX + (int) xdist;
                    int y = CardY + (int) ydist;

                    #region Hole Cards
                    if (entry.HoleCards.Count > 0 && indexDifference == 0 && (_Player != null || IsIntermission))
                    {
                        int lastY = centerY - 85;

                        var nextX = x;
                        foreach (var card in entry.HoleCards)
                        {
                            AddBackground(nextX, lastY, 71, 95, 9350);
                            AddLabel(nextX + 10, lastY + 5, card.GetSuitColor(), card.GetRankLetter());
                            AddLabel(nextX + 6, lastY + 25, card.GetSuitColor(), card.GetSuitString());

                            nextX += 30;
                        }

                        AddBackground(x-80, y-110, 261, 22, 9200);
                        AddHtml(x - 80, y-107, 255, 22,
                            String.Format("<BIG><CENTER>{0}</CENTER></BIG>", entry.GetHandString()), false, false);
                    }
                    #endregion

                    #region Nameplates
                    AddBackground(x-60, y, 221, 86, 9270);

                    if (!IsIntermission) //Display if a player is a dealer or blind
                    {
                        if (_Game.IsBlind(entry))
                        {
                            string title = GetTitleString(entry);

                            AddHtml(x - 45, y + 34, 101, 22,
                                String.Format("{0}", title).WrapUOHtmlColor(Color.Red), false, false);
                        }

                        //Display if a player has folded their hand or gone all-in
                        if (entry.Currency == 0 || entry.HasFolded)
                        {
                            AddHtml(x + 105, y + 34, 101, 22,
                                String.Format("<BIG>{0}</BIG>", entry.Currency == 0 ? "All-In" : "Folded")
                                    .WrapUOHtmlColor(Color.Red), false, false);
                        }
                    }
                    else
                    {
                        var lost = entry.AmountWon < 0;
                        if (entry.AmountWon != 0)
                        AddHtml(x - 45, y + 34, 200, 22,
                            String.Format("{0}: {1:n0} coins", lost ? "Lost" : "Won", entry.AmountWon).WrapUOHtmlColor(lost ? Color.Red : Color.LawnGreen), false, false);
                    }

                    var color = _Game.CurrentTurn != null && _Game.CurrentTurn == entry ? Color.GreenYellow : Color.White;
                    AddHtml(x-50, y + 15, 200, 22, String.Format("<BIG><CENTER>{0}</CENTER></BIG>", entry.Owner.RawName)
                        .WrapUOHtmlColor(color), false, false);

                    color = IsDonation ? Color.White : Color.Gold;
                    AddHtml(x-50, y + 55, 200, 22,
                        string.Format("<CENTER>({0:n0})</CENTER>", entry.Currency)
                        .WrapUOHtmlColor(color), false, false);

                    if (!IsIntermission && entry.TotalBetInRound > 0)
                    {
                        AddBackground(x - 35, indexDifference == 0 ? y - 33 : y + 85, 167, 34, 5120);
                        AddHtml(x - 35, indexDifference == 0 ? y - 24 : y + 93, 175, 22,
                            String.Format("<CENTER>{0:n0}</CENTER>",
                            entry.TotalBetInRound).WrapUOHtmlColor(color), false, false);
                    }

                    //display other players hole cards
                    if (indexDifference != 0 && (IsIntermission && !entry.HasFolded || User.AccessLevel >= AccessLevel.GameMaster))
                    {
                        int lastY = y + 85;

                        var nextX = x > CardX ? x + 95 : x - 60;
                        foreach (var card in entry.HoleCards)
                        {
                            AddBackground(nextX, lastY, 35, 50, 9350);
                            AddLabel(nextX + 12, lastY + 5, card.GetSuitColor(), card.GetRankLetter());
                            AddLabel(nextX + 8, lastY + 25, card.GetSuitColor(), card.GetSuitString());
                            nextX += 30;
                        }
                    }
                    #endregion
                });
        }

        private string GetTitleString(PokerPlayer entry)
        {
            if (entry == _Game.DealerButton)
                return "Dealer";
            if (entry == _Game.SmallBlind)
                return "Small Blind";
            return "Big Blind";
        }
    }
}