#region References
using System;
using System.Collections.Generic;
#endregion

namespace Server.Engines.TexasHoldem
{
	public class Deck
	{
		private Stack<Card> _Deck;
		private List<Card> _UsedCards;

		public Deck()
		{
			InitDeck();
		}

		private void InitDeck()
		{
			_Deck = new Stack<Card>(52);
			_UsedCards = new List<Card>();

			foreach (Suit s in Enum.GetValues(typeof(Suit)))
			{
				foreach (Rank r in Enum.GetValues(typeof(Rank)))
				{
					_Deck.Push(new Card(s, r));
				}
			}

			Shuffle(5);
		}

		public Card Pop()
		{
			_UsedCards.Add(_Deck.Peek());
			return _Deck.Pop();
		}

		public Card Peek()
		{
			return _Deck.Peek();
		}

		public void Shuffle(int count)
		{
			var deck = new List<Card>(_Deck.ToArray());

			for (int i = 0; i < count; ++i)
			{
				for (int j = 0; j < deck.Count; ++j)
				{
					int index = Utility.Random(deck.Count);
					Card temp = deck[index];

					deck[index] = deck[j];
					deck[j] = temp;
				}
			}

			_Deck = new Stack<Card>(deck);
		}
	}
}