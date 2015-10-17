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
    public class PokerBetGump : DialogGump
    {
        public PokerBetGump(PlayerMobile user, PokerGame game, PokerPlayer player, Gump parent = null)
            : base(user, parent, 526, 449)
        {
            Closable = false;
            Disposable = false;
            Dragable = true;
            Resizable = false;
            _Player = player;
            _Game = game;
        }

        private readonly PokerGame _Game;
        private readonly PokerPlayer _Player;
        private int Bet { get; set; }

        private PlayerAction Action { get; set; }

        protected override void CompileLayout(SuperGumpLayout layout)
        {
            layout.Add(
                "Main",
                () =>
                {
                    bool canBet = _Player.Currency > _Game.GetCallAmount(_Player);
                    AddBackground(0, 0, 200, canBet ? 157 : 97, 9270);

                    int yoffset = 20;

                    if (canBet)
                    {
                        //call/check
                        AddRadio(14, yoffset, 9727, 9730, true, (i, selected) =>
                        {
                            if (selected)
                            {
                                Action = _Game.GetCallAmount(_Player) > 0 ? PlayerAction.Call : PlayerAction.Check;
                            }
                        });
                        AddHtml(50, yoffset + 4, 60, 45,
                            string.Format("{0}", _Game.GetCallAmount(_Player) > 0 ? "Call" : "Check")
                                .WrapUOHtmlColor(Color.White), false, false);

                        if (_Game.GetCallAmount(_Player) > 0)
                        {
                            AddHtml(105, yoffset + 4, 200, 22,
                                String.Format("{0}", string.Format("{0:n0}", _Game.GetCallAmount(_Player)))
                                    .WrapUOHtmlColor(KnownColor.LawnGreen), false, false);
                        }

                        yoffset += 30;

                        AddRadio(14, yoffset, 9727, 9730, false, (i, selected) =>
                        {
                            if (selected)
                            {
                                Action = _Game.CanRaise() ? PlayerAction.Raise : PlayerAction.Bet;
                            }
                        });
                        AddHtml(50, yoffset + 4, 60, 45,
                            string.Format("{0}", !_Game.CanRaise() ? "Bet" : (_Game.HasRaised() ? "Reraise" : "Raise"))
                                .WrapUOHtmlColor(Color.LawnGreen), false, false);

                        AddTextEntry(105, yoffset + 4, 200, 22, 455, _Game.MinimumRaise > 0 ? _Game.MinimumRaise.ToString() : _Game.Dealer.BigBlind.ToString(), (t, s) =>
                        {
                            int temp;

                            Int32.TryParse(s, out temp);

                            Bet = temp;
                        });

                        yoffset += 30;
                    }

                    AddRadio(14, yoffset, 9727, 9730, !canBet, (i, selected) =>
                    {
                        if (selected)
                        {
                            Action = PlayerAction.AllIn;
                        }
                    });
                    AddHtml(50, yoffset + 4, 60, 45, "All-In".WrapUOHtmlColor(Color.White), false, false);

                    yoffset += 30;
                    AddRadio(14, yoffset, 9727, 9730, false, (i, selected) =>
                    {
                        if (selected)
                        {
                            Action = PlayerAction.Fold;
                        }
                    });
                    AddHtml(50, yoffset + 4, 60, 45, "Fold".WrapUOHtmlColor(Color.White), false, false);


                    AddButton(104, yoffset + 3, 247, 248, b => { ProcessSelection(); });
                });
        }

        private void ProcessSelection()
        {
            switch (Action)
            {
                case PlayerAction.Check:
                {
                    DoCheck();
                    break;
                }
                case PlayerAction.Call:
                {
                    DoCall();
                    break;
                }
                case PlayerAction.AllIn:
                {
                    DoAllIn();
                    break;
                }
                case PlayerAction.Fold:
                {
                    DoFold();
                    break;
                }
                case PlayerAction.Bet:
                {
                    DoBet();
                    break;
                }
                case PlayerAction.Raise:
                {
                    DoRaise();
                    break;
                }
            }
        }

        public void DoCheck()
        {
            _Game.DoAction(_Player, PlayerAction.Check);
        }

        public void DoCall()
        {
            _Game.DoAction(_Player, PlayerAction.Call);
        }

        public void DoAllIn()
        {
            _Game.DoAction(_Player, PlayerAction.AllIn);
        }

        public void DoFold()
        {
            _Game.DoAction(_Player, PlayerAction.Fold);
        }

        public void DoBet()
        {
            if (Bet < _Game.Dealer.BigBlind)
            {
                User.SendMessage(0x22, "Your must bet at least {0:#,0} {1}.", _Game.BigBlind,
                    _Game.Dealer.IsDonation ? "donation coins." : "gold.");

                Refresh();
            }
            else if (Bet > _Player.Currency)
            {
                User.SendMessage(0x22, "You cannot bet more gold than you currently have!");

                Refresh();
            }
            else if (Bet == _Player.Currency)
            {
                _Game.DoAction(_Player, PlayerAction.AllIn);
            }
            else
            {
                _Game.DoAction(_Player, PlayerAction.Bet, Bet);
            }
        }

        public void DoRaise()
        {
            if (Bet < _Game.MinimumRaise)
            {
                User.SendMessage(0x22,
                    "The minimum you can raise is the amount of the last bet made.");

                Refresh();
            }
            else if (Bet + _Game.MinimumBet > _Player.Currency)
            {
                User.SendMessage(0x22,
                    string.Format("You do not have enough {0} to raise by that much.",
                        _Game.Dealer.IsDonation ? "donation coins" : "gold"));

                Refresh();
            }
            else if (Bet + _Game.GetCallAmount(_Player) == _Player.Currency)
            {
                _Game.DoAction(_Player, PlayerAction.AllIn);
            }
            else
            {
                _Game.DoAction(_Player, PlayerAction.Raise, Bet);
            }
        }
    }
}