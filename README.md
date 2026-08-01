# Hypergeometric Draw Odds

An informational Slay the Spire 2 mod that answers the question you were going to
count out by hand: *what are the odds I draw that next turn?*

Select cards on any combat pile screen and it reports the chance the next hand
contains them — `1 - C(N-K, n) / C(N, n)`, the hypergeometric distribution,
worked out over the piles as they actually stand.

It changes nothing about the game. No card moves, no number is altered; the mod
only reads and reports, and its manifest declares `affects_gameplay: false`.

The draw count it works from accounts for the relics, powers and status effects
that change how much you draw next turn, along with retain effects and maximum
hand size. Cards created during the next turn's draw cannot be predicted before
they exist, so those are the one thing the odds cannot see coming.

## What the next hand can actually reach

Odds are computed in two stages, in the order the game draws:

1. **Draw pile.** Consumed first.
2. **Reshuffle pool.** Reached only if the draw empties the draw pile. It is the
   discard pile *plus* every card in hand, because the end of the turn sends the
   hand to the discard before the next hand is dealt.

Cards a retain effect keeps in hand are excluded from both stages and always
read 0%. They are not reshuffled until they are played and leave the hand, so no
upcoming draw can reach them.

## All Cards screen

During combat, press **W** (rebindable in Settings → Input) or use the **%** pile
button beside Draw to open the mod's main screen: a Card Library shelf beside one
continuous grid of every card the next hand could reach, in draw order. Like the
Card Library, it draws over the run's top bar and relic inventory rather than
under them.

The grid runs the draw pile, then a **Reshuffle** marker, then the reshuffle
pool itself. The discard pile and the hand share one section, sorted as a single
run, because they are one population: they return to the draw pile together.
Anything a retain effect is holding back follows a final **Retained** marker,
kept out of the reshuffle it will not take part in. Each marker names what
follows it, and says more on hover.

Click cards to select them. Hovering one shows the same native Draw Chance
tooltip the individual pile screens use.

The shelf beside the grid holds the whole query and its result:

- **Search** narrows what the grid draws. Selections and odds always come from
  the full piles, so searching changes the view without moving a number.
- **Draw** sets how many cards the next hand draws, and names the relics and
  powers that moved it off the base of five.
- **Hits** sets how many of the selected cards the hand needs. One asks for any
  of them; the full count asks for all of them; zero asks for none.
- **Draw Chance** states the question in words and answers it.
- **Simulate Draw** deals a hand at random from those same piles, in cards
  rather than percentages.

Options along the bottom put the odds on the cards themselves, count copies of a
card together or separately, decide whether the hand joins the reshuffle, and
swap the combat query for a plain calculator. Each explains itself on hover, and
Settings → Mod Settings carries a default for each one.

Building the mod and publishing it to the Workshop are covered in
[DEVELOPING.md](DEVELOPING.md).

## History

Formerly `draw-odds`, mod id `DrawOdds`, display name "Draw Odds". Renamed at
v0.5.0 so the identity is settled before the first Workshop publish.

v0.6.0 rebuilt the All Cards screen around the Card Library shelf and folded the
hand into the reshuffle pool. Before that, cards in hand were absent from the
screen and from the odds entirely.

v0.7.0 corrected the draw-count prediction. The effects that change how much the
next hand draws were previously read mid-turn, so Pocketwatch did not break when
its threshold was passed, Pendulum and Draw Cards Next Turn were missed
outright, and Pollinous Core was a turn behind.

## Licence

MIT — see [LICENSE](LICENSE).

That covers this mod's own source. It does not cover Slay the Spire 2, which is the
property of Mega Crit. The mod compiles against the game's assemblies and loads the
game's own scenes and textures at runtime from the player's installed copy; none of
that is redistributed here.
