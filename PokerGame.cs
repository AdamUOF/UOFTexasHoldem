#region References

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server.Items;
using Server.Mobiles;
using Server.Network;

#endregion

namespace Server.Engines.TexasHoldem
{
    public class PokerGame
    {
        #region Fields


        public Deck Deck { get; set; }

        public PokerGameState State { get; set; }


        public PokerExport Exporter { get; set; }
        public PokerDealer Dealer { get; set; }
        public PokerGameTimer PokerTimer { get; set; }

        public PokerPlayer DealerButton { get; private set; }
        public PokerPlayer SmallBlind { get; private set; }
        public PokerPlayer BigBlind { get; private set; }

        public int MinimumRaise { get; set; }
        public int MinimumBet { get; set; }

        public PokerPlayer CurrentTurn { get; set; }

        public List<Card> CommunityCards { get; set; }
        public List<PokerPlayer> Players { get; set; }
        public List<PokerPlayer> ActivePlayers { get; set; }
        public List<PlayerMobile> Viewers { get; set; }
        public List<PokerPot> PokerPots { get; set; }
        public List<PlayerAction> RoundActions { get; set; }

        public DateTime StartTime { get; set; }

        public Type TypeOfCurrency
        {
            get { return Dealer != null && Dealer.IsDonation ? typeof(DonationCoin) : typeof(Gold); }
        }

        public TimeSpan TurnLength { get { return TimeSpan.FromSeconds(60); } }

        public TimeSpan CooldownPeriod { get { return TimeSpan.FromSeconds(20); } }

        public TimeSpan HandCoolDown { get; set; }

        #endregion

        public PokerGame(PokerDealer dealer)
        {
            Dealer = dealer;

            State = PokerGameState.Inactive;
            Deck = new Deck();
            PokerTimer = new PokerGameTimer(this);
            Exporter = new PokerExport();

            CommunityCards = new List<Card>();
            Players = new List<PokerPlayer>();
            ActivePlayers = new List<PokerPlayer>();
            RoundActions = new List<PlayerAction>();
            PokerPots = new List<PokerPot>();
            Viewers = new List<PlayerMobile>();
        }

        #region Core Functionality

        /// <summary>
        ///     Process a tick of the poker game timer.  Refresh the gumps of all players, try to increment the round and check for
        ///     afk players
        /// </summary>
        public void ProcessTick()
        {
            if (IsIntermission() && HandCoolDown <= TimeSpan.FromSeconds(0))
            {
                ResetGame();
            }
            else if (State == PokerGameState.Showdown)
            {
                DoShowdown();
            }
            else if (State > PokerGameState.Inactive && State < PokerGameState.Showdown)
            {
                DoRoundAction();
                //if it is someones turn and they haven't done an action
                if (IsBettingRound && CurrentTurn != null)
                {
                    CurrentTurn.TurnEnd -= TimeSpan.FromSeconds(1);
                    if (CurrentTurn.TurnEnd == TimeSpan.FromSeconds(30) ||
                        CurrentTurn.TurnEnd == TimeSpan.FromSeconds(15) ||
                        CurrentTurn.TurnEnd == TimeSpan.FromSeconds(5))
                    {
                        CurrentTurn.SendMessage("Your turn will end in " + CurrentTurn.TurnEnd.Seconds +
                                                " seconds. If you do not make a move before your time runs out, you will fold your hand.");
                    }

                    //thye've used up their turn grace time, force them to fold from the hand
                    if (CurrentTurn.TurnEnd <= TimeSpan.FromSeconds(0))
                    {
                        DoAction(CurrentTurn, PlayerAction.Fold);
                    }
                }
            }
            else if (IsIntermission())
            {
                var seconds = HandCoolDown.Seconds;

                if (seconds == 20 || seconds == 10 || seconds == 5)
                {
                    PokerMessage(Dealer, "The next hand will begin in " + HandCoolDown.Seconds +
                                            " seconds.");
                }

                HandCoolDown -= TimeSpan.FromSeconds(1);

            }
        }

        /// <summary>
        ///     Check if the user is already at the table
        /// </summary>
        public bool IsBlind(PokerPlayer player)
        {
            return player == BigBlind || player == DealerButton || player == SmallBlind;
        }

        /// <summary>
        ///     Check if the user is already at the table
        /// </summary>
        public bool IsViewer(PlayerMobile pm)
        {
            return Viewers.Contains(pm);
        }

        /// <summary>
        ///     Check if the user is already at the table
        /// </summary>
        public bool IsPlayer(PlayerMobile pm)
        {
            PokerPlayer player;
            return IsPlayer(pm, out player);
        }

        /// <summary>
        ///     Check if the user is part of the current hand
        /// </summary>
        public bool IsActivePlayer(PlayerMobile pm)
        {
            PokerPlayer player;
            return IsPlayer(pm, out player);
        }

        public bool IsActivePlayer(PlayerMobile pm, out PokerPlayer player)
        {
            player = ActivePlayers.FirstOrDefault(x => x.Owner == pm);
            return player != null;
        }

        public bool IsPlayer(PlayerMobile pm, out PokerPlayer player)
        {
            player = Players.FirstOrDefault(x => x.Owner == pm);
            return player != null;
        }

        public bool IsDonationGame()
        {
            return TypeOfCurrency == typeof(DonationCoin);
        }

        /// <summary>
        ///     Send a message to all poker players of the last persons move. For increase visibility, also display it over their
        ///     head.
        /// </summary>
        public void PokerMessage(Mobile from, string message)
        {
            foreach (var player in Players.ToArray())
            {
                from.PrivateOverheadMessage(MessageType.Regular, 0x9A, true, message, player.Owner.NetState);
                if (player.Owner != null)
                {
                    player.Owner.SendMessage(0x9A, "[{0}]: {1}", from.Name, message);
                }
            }
        }

        /// <summary>
        ///     Get a count of all active players in a game
        /// </summary>
        public int GetAllActivePlayers()
        {
            return ActivePlayers.Count;
        }

        /// <summary>
        ///     Get a count of all players in a game that can still act in rounds
        /// </summary>
        public int GetActiveElliblePlayersCount()
        {
            return ActivePlayers.Count(x => x.Currency > 0 && !x.HasFolded);
        }

        public bool IsIntermission()
        {
            return State == PokerGameState.Intermission;
        }

        /// <summary>
        ///     Refresh all gumps for playermobiles
        /// </summary>
        public void RefreshGumps()
        {
            foreach (var player in Players.ToArray())
            {
                new PokerTableGump(player.Owner, this).Send();
            }

            foreach (var viewer in Viewers.ToArray())
            {
                if (viewer.HasGump(typeof(PokerTableGump)))
                    new PokerTableGump(viewer, this).Send();
                else
                    Viewers.Remove(viewer);

            }
        }

        /// <summary>
        ///     Closes all poker gumps for playermobiles
        /// </summary>
        public void CloseGumps()
        {
            foreach (var player in Players.ToArray())
            {
                player.CloseAllGumps();
            }

            foreach (var viewer in Viewers.ToArray())
            {
                if (viewer.HasGump(typeof(PokerTableGump)))
                    viewer.CloseGump(typeof(PokerTableGump));
                else
                    Viewers.Remove(viewer);
            }
        }

        /// <summary>
        ///     Creates a list of active players and grabs the next active player that can act. If play doesn't exist, grab the
        ///     first player in the list
        ///     <returns>next active player</returns>
        /// </summary>
        public PokerPlayer GetNextActivePlayer(PokerPlayer current)
        {
            var index = ActivePlayers.IndexOf(current);

            PokerPlayer activeplayer;

            do
            {
                activeplayer = ActivePlayers[index == -1 ? 0 : (index + 1 >= ActivePlayers.Count ? 0 : (index + 1))];
                index = ActivePlayers.IndexOf(activeplayer);
            }
            while (activeplayer.Currency == 0 || activeplayer.HasFolded);

            return activeplayer == current ? null : activeplayer;
        }

        /// <summary>
        ///     Gets the amount a player needs to bet in order to meet the current minimum bet to stay in the game
        ///     <returns>amount needed to meet call</returns>
        /// </summary>
        public int GetCallAmount(PokerPlayer player)
        {
            return MinimumBet - player.TotalBetInRound;
        }

        /// <summary>
        ///     Raises can only be made if a bet has already been posted
        ///     <returns>amount needed to meet call</returns>
        /// </summary>
        public bool CanRaise()
        {
            return RoundActions.Exists(x => x == PlayerAction.Bet);
        }

        public bool HasRaised()
        {
            return RoundActions.Exists(x => x == PlayerAction.Raise);
        }

        /// <summary>
        ///     In poker, every second round is a betting round. This is useful in making code to determine state changes more
        ///     compact.
        /// </summary>
        public bool IsBettingRound { get { return State > PokerGameState.Inactive && State < PokerGameState.Intermission && (int)State % 2 == 0; } }

        public bool CanEndBettingRound()
        {
            return !ActivePlayers.Exists(x => x.Currency > 0 && !x.HasFolded && (!x.HasActed || GetCallAmount(x) > 0));
        }

        #endregion

        #region Initialize Game

        /// <summary>
        ///     Determines the blinds for a new hand of poker.  The dealer button and blinds always move to the left.
        /// </summary>
        public void DetermineBlinds()
        {
            if (!Players.Contains(DealerButton))
                DealerButton = null;

            if (!Players.Contains(SmallBlind))
                SmallBlind = null;

            if (!Players.Contains(BigBlind))
                BigBlind = null;

            if (GetActiveElliblePlayersCount() >= 2)
            {
                if (DealerButton == null)
                {
                    if (SmallBlind != null)
                    {
                        DealerButton = SmallBlind;
                        SmallBlind = GetNextActivePlayer(DealerButton);
                    }
                    else
                    {
                        DealerButton = GetNextActivePlayer(null);
                        SmallBlind = GetNextActivePlayer(DealerButton);
                    }
                }
                else
                {
                    DealerButton = GetNextActivePlayer(DealerButton);
                    SmallBlind = GetNextActivePlayer(DealerButton);
                }

                if (GetActiveElliblePlayersCount() >= 3)
                    BigBlind = GetNextActivePlayer(SmallBlind);
            }
        }

        /// <summary>
        ///     Attempt to disburse any pending credit to players
        /// </summary>
        public void ProcessCredit()
        {
            foreach (var player in Players.ToArray())
            {
                player.ProcessCredit(Dealer.MinBuyIn, Dealer.MaxBuyIn, TypeOfCurrency);
            }
        }

        #endregion

        #region Start/Stop Game/Reset Game
        public void BeginGame()
        {
            StartTime = DateTime.Now;
            DetermineBlinds();

            PokerPots.Add(new PokerPot());

            State = PokerGameState.DealHoleCards;         
        }

        /// <summary>
        ///     End the poker game and clear players of any relevant variables to that specific game
        /// </summary>
        public void EndGame()
        {
            State = PokerGameState.Inactive;

            CurrentTurn = null;

            //process credit first so that players that have queued in with a rebuy won't be kicked for having low currency
            ProcessCredit();

            ProcessLeaves();

            StartIntermission();

            RefreshGumps();
        }

        public void ResetGame()
        {
            CloseGumps();

            State = PokerGameState.Inactive;

            RoundActions = new List<PlayerAction>();

            CommunityCards.Clear();

            //create and shuffle a new deck
            Deck = new Deck();

            PokerPots.Clear();

            ActivePlayers.Clear();

            foreach (var player in Players.ToArray())
            {
                player.ClearGame();
            }

            ActivePlayers.AddRange(Players.Where(x => x.Currency > 0));

            if (GetActiveElliblePlayersCount() > 1)
            {
                BeginGame();
            }
            else
            {
                StopIntermission();
                ActivePlayers.Clear();
            }
        }

        public void StopIntermission()
        {
            PokerMessage(Dealer, "There were not enough players to start a new hand.");

            if (PokerTimer != null && PokerTimer.Running)
                PokerTimer.Stop();

            State = PokerGameState.Inactive;
        }

        public void StartIntermission()
        {
            State = PokerGameState.Intermission;

            if (PokerTimer == null || !PokerTimer.Running)
            {
                PokerTimer = new PokerGameTimer(this);
                PokerTimer.Start();
            }
            HandCoolDown = CooldownPeriod;
        }

        #endregion

        #region Round Functionality

        public void MakeBet(PokerPlayer player, int bet)
        {
            player.MakeBet(bet);

            var pot = PokerPots.FirstOrDefault();

            if (pot != null)
                pot.AddtoPot(bet, player);
        }

        public void CreateSidePots()
        {
            PokerPot newPot;
            do
            {
                var pot = PokerPots.Last();
                newPot = pot.TrySplitPot();
                if (newPot != null)
                    PokerPots.Add(newPot);
            }
            while (newPot != null);
        }

        public void DoRoundAction() //Happens once State is changed (once per state)
        {
            if (IsBettingRound && CanEndBettingRound())
            {
                //if all players have acted and have either gone all-in, folded or have equal amounts in community currency

                State++;
            }
            else if (!IsBettingRound)
            {
                //if this is one of the 3 card draw stages
                if (State == PokerGameState.Flop || State == PokerGameState.Turn || State == PokerGameState.River)
                {
                    var round = State.ToString().ToLower();
                    var numberOfCards = State == PokerGameState.Flop ? 3 : 1;

                    PopCards(numberOfCards, round);
                }

                //prepare next betting round
                if (State == PokerGameState.DealHoleCards)
                {
                    SetUpHole();
                }
                else if (GetActiveElliblePlayersCount() > 1)
                {
                    SetUpBettingRound();
                }

                RankHands();

                State++;
            }

            RefreshGumps();
        }

        public void PopCards(int amount, string message)
        {
            if (amount > 0) //Pop the appropriate number of cards from the top of the deck
            {
                var sb = new StringBuilder();

                sb.Append("The " + message + " shows: ");

                for (int i = 0; i < amount; ++i)
                {
                    Card popped = Deck.Pop();

                    if (i == 2 || amount == 1)
                    {
                        sb.Append(popped.Name + ".");
                    }
                    else
                    {
                        sb.Append(popped.Name + ", ");
                    }

                    CommunityCards.Add(popped);
                }

                PokerMessage(Dealer, sb.ToString());
            }
        }
        /// <summary>
        ///     Deal initial cards to players
        /// </summary>
        public void SetUpHole()
        {
            for (var i = 0; i < 2; ++i)
            //Simulate passing one card out at a time, going around the circle of players 2 times
            {
                foreach (var player in ActivePlayers.ToArray())
                {
                    Card card = Deck.Pop();
                    player.AddCard(card);
                }
            }

            //if >3 players, smallblind posts smallblind else dealerbutton does
            DoAction(BigBlind != null ? SmallBlind : DealerButton, PlayerAction.Bet, Dealer.SmallBlind, true, false);

            //if >3 players, bigblind posts bigblind else smallblind does
            DoAction(BigBlind ?? SmallBlind, PlayerAction.Bet, Dealer.BigBlind, true, false);

            CurrentTurn = BigBlind ?? SmallBlind;

            AssignNextTurn();
        }

        /// <summary>
        ///     Start a betting round of the game. Creates valid players and sets the first player's turn
        /// </summary>
        public void SetUpBettingRound()
        {
            //reset these variables because they need to be 0 for a new round
            MinimumBet = 0;
            MinimumRaise = 0;

            foreach (var player in ActivePlayers)
            {
                player.ClearRound();
            }

            CurrentTurn = DealerButton;

            RoundActions = new List<PlayerAction>();

            //start betting again
            AssignNextTurn();
        }

        public void RankHands()
        {
            foreach (var player in ActivePlayers.Where(x => !x.HasFolded))
            {
                player.RankHand(CommunityCards);
            }
        }

        public void DoAction(PokerPlayer player, PlayerAction action, int bet = 0, bool isforced = false,
            bool verbose = true)
        {
            if (!isforced && CurrentTurn != player)
                return;

            var pm = player.Owner;
            string message = string.Empty;

            Exporter.AddAction(pm, action, State, action == PlayerAction.AllIn ? player.Currency : bet);

            switch (action)
            {
                case PlayerAction.Bet:
                    message = String.Format("I {0} {1}.", "bet", bet);

                    MakeBet(player, bet);

                    MinimumBet = bet;
                    //raise after a bet is always 2*bet according to poker rules
                    MinimumRaise = bet * 2;

                    break;
                case PlayerAction.Raise:
                    message = String.Format("I {0} {1}.",
                        RoundActions.Exists(x => x == PlayerAction.Raise) ? "reraise" : "raise", bet);

                    MakeBet(player, GetCallAmount(player) + bet);

                    MinimumBet += bet;
                    MinimumRaise = bet;

                    break;

                case PlayerAction.Call:
                    message = "I call.";

                    //match what is on the table from the last player. This takes into account how much you already have on the table in that round
                    bet = GetCallAmount(player);

                    MakeBet(player, bet);

                    break;

                case PlayerAction.Check:

                    message = "Check.";

                    break;

                case PlayerAction.Fold:
                    message = "I fold.";

                    player.HasFolded = true;

                    if (ActivePlayers.Count(x => !x.HasFolded) == 1)
                    {
                        DoShowdown();
                        return;
                    }

                    break;

                case PlayerAction.AllIn:
                    if (player.Currency > 0)
                    {
                        message = MinimumBet > player.Currency ? "I call: all-in." : "All in.";

                        int difference = player.Currency + player.TotalBetInRound;

                        if (difference > MinimumBet)
                        {
                            MinimumBet = difference;
                        }

                        MakeBet(player, player.Currency);
                    }

                    break;
            }

            RefreshGumps();

            if (verbose)
            {
                PokerMessage(pm, message);
            }

            if (!isforced)
            {
                player.HasActed = true;
            }

            RoundActions.Add(action);

            if (!isforced && !CanEndBettingRound())
            {
                AssignNextTurn();
            }
        }

        public void AssignNextTurn()
        {
            CurrentTurn = GetNextActivePlayer(CurrentTurn);

            RefreshGumps();

            if (CurrentTurn != null)
            {
                CurrentTurn.TurnEnd = TurnLength;

                if (CurrentTurn.RequestLeave)
                {
                    DoAction(CurrentTurn, PlayerAction.Fold);
                    return;
                }

                if (GetActiveElliblePlayersCount() == 1 && CanEndBettingRound())
                {
                    DoAction(CurrentTurn, PlayerAction.Check);
                    return;
                }

                new PokerBetGump(CurrentTurn.Owner, this, CurrentTurn).Send();
            }
        }



        /// <summary>
        ///     Determines the winner, ends the game and then tries to start a new one
        /// </summary>
        public void DoShowdown()
        {           
            State = PokerGameState.DetermineWinners;
            RefreshGumps();
            CreateSidePots();
            DetermineWinners();
            Exporter.ProcessHand(this);
            EndGame();
        }

        public void DetermineWinners()
        {
            foreach (var player in ActivePlayers)
            {
                player.RankHand(CommunityCards);
            }

            foreach (var pot in PokerPots)
            {
                pot.AwardPot(ActivePlayers);
            }

            foreach (var player in ActivePlayers)
            {
                player.DistributeCredit(this);
            }
        }
        #endregion

        #region Add/Remove Players

        /// <summary>
        ///     Add a player to the poker game
        /// </summary>
        public void AddPlayer(PlayerMobile player, int buyin)
        {
            if (!CanJoinTable(player, buyin))
            {
                return;
            }

            var seat = Dealer.Seats.FirstOrDefault(x => !Dealer.SeatTaken(x));

            if (seat != Point3D.Zero)
            {
                var pokerplayer = new PokerPlayer(player, buyin, seat, this);
                Players.Add(pokerplayer);
            }

            if (Players.Count > 1 && !IsIntermission() && HandCoolDown <= TimeSpan.FromSeconds(0))
            {
                StartIntermission();
            }
        }

        public bool CanJoinTable(PlayerMobile player, int buyin)
        {
            if (!ShardInfo.IsTestCenter && player.AccessLevel == AccessLevel.Player && Players.Any(p => p.Owner.NetState != null && player.NetState != null &&
                                       p.Owner.NetState.Address.Equals(player.NetState.Address)))
            {
                return false;
            }

            if (player.PokerJoinTimer > DateTime.UtcNow)
            {
                TimeSpan nextuse = player.PokerJoinTimer - DateTime.UtcNow;
                player.SendMessage("You cannot join another poker game for " + nextuse.Seconds + " seconds.");
                return false;
            }

            if (player.Aggressed.Any(info => (DateTime.UtcNow - info.LastCombatTime) < TimeSpan.FromSeconds(60)))
            {
                player.SendMessage("You cannot join poker while you are in combat!");
                return false;
            }

            if (player.Aggressors.Any(info => (DateTime.UtcNow - info.LastCombatTime) < TimeSpan.FromSeconds(60)))
            {
                player.SendMessage("You cannot join poker while you are in combat!");
                return false;
            }

            if (player.Party != null)
            {
                player.SendMessage("You cannot join a poker game while in a party.");
                return false;
            }

            if (!Dealer.InRange(player.Location, 8))
            {
                player.PrivateOverheadMessage(MessageType.Regular, 0x22, true, "I am too far away to do that",
                    player.NetState);
                return false;
            }

            if (IsPlayer(player))
            {
                player.SendMessage(0x22, "You are already seated at this table");
                return false;
            }

            if (Players.Count >= Dealer.MaxPlayers)
            {
                player.SendMessage(0x22, "Sorry, that table is full");
                return false;
            }

            if (!Dealer.Seats.Exists(x => !Dealer.SeatTaken(x)))
            {
                player.SendMessage(0x22, "Sorry, that table is full");
                return false;
            }

            if (!Banker.WithdrawPackAndBank(player, TypeOfCurrency, buyin))
            {
                player.SendMessage(0x22, "Your bank box lacks the funds to join this poker table.");
                return false;
            }

            return true;
        }

        /// <summary>
        ///     Remove player from the game
        /// </summary>
        public void RemovePlayer(PokerPlayer player)
        {
            if (player != null)
            {
                //null these here so we know that one of the key players for the next game has left
                if (DealerButton == player)
                {
                    DealerButton = null;
                }
                if (SmallBlind == player)
                {
                    SmallBlind = null;
                }
                if (BigBlind == player)
                {
                    BigBlind = null;
                }

                //Move to view list so that they can continue viewing the game
                if (ActivePlayers.Contains(player))
                    Viewers.Add(player.Owner);

                Players.Remove(player);

                player.LeaveGame(this);
            }
        }

        /// <summary>
        ///     Removes players from the game that meet leave criteria
        /// </summary>
        public void ProcessLeaves()
        {
            foreach (
                var player in
                    Players.ToArray()
                        .Where(player => !player.IsOnline() || player.Currency < Dealer.BigBlind || player.RequestLeave)
                )
            {
                RemovePlayer(player);
            }
        }

        #endregion
    }

    public class PokerGameTimer : Timer
    {
        private readonly PokerGame _Game;

        public PokerGameTimer(PokerGame game)
            : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
        {
            _Game = game;
        }

        protected override void OnTick()
        {
            _Game.ProcessTick();
        }
    }
}