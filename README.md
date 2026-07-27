# Hypergeometric Draw Odds

An informational Slay the Spire 2 mod that adds native-style draw-chance
analysis to the combat card pile screens.

- Click (or alternate-click) cards to toggle selections.
- Hovering a card shows the chance of drawing any identical copy and the chance
  of drawing that exact physical card. Identical copies are grouped by card ID,
  exact upgrade level, enchantment ID, and enchantment amount.
- The Draw Pile and Discard Pile screens report the result in the game's native
  bottom information strip.
- The calculation is `1 - C(N-K, n) / C(N, n)`.
- `N` is population size, `K` is selected card instances, and `n` is the
  context-adjusted next hand draw capped by hand capacity and pile size.
- No-draw effects, hand-draw modifiers, retain/no-flush effects, and maximum
  hand size are included. Effects that create cards during the future
  `BeforeHandDraw` phase cannot be predicted before they resolve.

## What the next hand can actually reach

Odds are computed in two stages, in the order the game draws:

1. **Draw pile.** Consumed first.
2. **Reshuffle pool.** Reached only if the draw empties the draw pile. It is the
   discard pile *plus* every card in hand, because the end of the turn sends the
   hand to the discard before the next hand is dealt.

Cards a retain effect keeps in hand are excluded from both stages and always
read 0%. They are not reshuffled until they are played and leave the hand, so no
upcoming draw can reach them.

## Predicting the draw count

The draw count is what the *next* turn will deal, not what this one did. The
game's draw hooks answer for the turn they are asked in, and several effects roll
per-turn bookkeeping forward at the turn boundary, so asking them mid-turn
describes the hand already in hand. `NextHandDraw` advances that bookkeeping
across the call and restores it immediately.

| Effect | Why the mid-turn answer is wrong |
| --- | --- |
| Ring of the Snake, Bag of Preparation, Big Mushroom, Booming Conch | Only apply on turn 1, so they must be read at turn + 1 |
| Ring of the Drake | Applies within a turn window |
| Pocketwatch | Reads *last* turn's cards played; the roll happens in `BeforeSideTurnStart`, so this turn's count is what next turn will see. Playing past its threshold breaks it immediately |
| Pollinous Core | `BeforeHandDraw` ticks its turn counter just before the draw reads it |
| Draw Cards Next Turn | Gated on `AmountOnTurnStart`, which is still zero on the turn the power is applied |
| Pendulum | Draws in `AfterPlayerTurnStart` instead of modifying the hand draw, so the hook never reports it |
| Fiddle | Applies in the late `ModifyHandDrawLate` pass |

Clarity, Demesne, Machine Learning, Tools of the Trade, Tyranny, Mind Rot,
Pael's Blood, and Snecko Eye read their current amount and are not decayed until
after the draw, so they need no adjustment.

The shelf names whichever of these moved the count, so the number can be checked
rather than trusted.

`DrawPools` builds all of this from live combat state, and every screen resolves
its odds through it, so the piles, the draw count, and the reshuffle model
cannot diverge between screens.

## All Cards screen

During combat, use the **ALL** pile button beside Draw to open the mod's main
screen: a Card Library shelf beside one continuous grid of every card the next
hand could reach, in draw order. Like the Card Library, it draws over the run's
top bar and relic inventory rather than under them.

The grid runs the draw pile, then a card-sized **Reshuffle — Discard Pile +
Cards in Hand →** marker, then the reshuffle pool itself. The discard pile and
the hand share one section, sorted as a single run, because they are one
population: they return to the draw pile together. Anything retain is holding
back follows a final **Retained — Stays in Hand** marker, kept out of the
reshuffle it will not take part in. Card clicks toggle exact physical copies,
and hovering shows the same native Draw Chance tooltip the individual pile
screens use.

The shelf holds the whole query and its result:

- **Draw** — the **−**/**+** row sets how many cards the next hand draws, and
  the effects that moved it off the base of five are named underneath. Clicking
  the count restores the real next-turn draw, which is also restored every time
  the screen opens. While the count is set by hand the note says so and offers
  the real value.
- **Selection** — **ANY** calculates the chance of drawing at least the chosen
  number of selected cards; **ALL** the chance of drawing every one of them.
  The second **−**/**+** row sets the ANY target.
- **Draw Chance** — states the question in words, naming the picked cards
  ("Chance to draw Strike or Neutralize:"), then the required hits and the
  probability. A **Retained** row appears when a selection includes retained
  cards, which explains a lower-than-expected result.
- **Show Odds on Cards** — prints chances onto the cards themselves, using the
  same on-card readout as the Card Library's View Stats toggle. With nothing
  selected each card carries its own any-copy chance. Once cards are selected
  the question has changed, so only the selection is marked, and every badge
  carries the one joint chance the shelf reports, captioned with the query
  (`Any of 3`, `2 of 3`, `All 3`) so it is not mistaken for that card's own
  odds.
- The footer carries the mod name and version, with a **?** button whose hover
  tip explains the screen.

## Build

```powershell
dotnet build .\Hypergeo.csproj
dotnet test .\Hypergeo.Tests\Hypergeo.Tests.csproj
```

`Sts2PathDiscovery.props` finds the game through the Steam registry keys. If it
cannot, copy `Directory.Build.props.example` to `Directory.Build.props` and set
`Sts2Path`.

Building copies `Hypergeo.json`, `Hypergeo.dll`, and `Hypergeo.pdb` into
`<game>/mods/Hypergeo/`. Pass `-p:SkipModInstall=true` to build without
installing. The game must not be running, or the DLL will be locked.

## Publish to the Steam Workshop

```powershell
.\scripts\package-workshop.ps1
```

That stages `workshop/content/` and prints the `ModUploader.exe upload -w …`
command to run next. See `docs/sts2-modding.md` for the full pipeline and
`workshop/README.md` for the `workshop.json` field reference.

`workshop/mod_id.txt` appears after the first upload. **Commit it** — it is the
only link between this repository and the published Workshop item.

### Not done yet

- `workshop/image.png` is still Mega Crit's placeholder and needs real art
  before the mod goes public.
- The in-game mod-list image (`res://Hypergeo/mod_image.png`) requires
  `has_pck: true` and a Godot project that exports a `.pck`. This mod has
  neither, so the mod list shows no image. Not required for publishing.

## History

Formerly `draw-odds`, mod id `DrawOdds`, display name "Draw Odds". Renamed at
v0.5.0 so the identity is settled before the first Workshop publish.

v0.6.0 rebuilt the All Cards screen around the Card Library shelf and folded the
hand into the reshuffle pool. Before that, cards in hand were absent from the
screen and from the odds entirely.

v0.7.0 corrected the draw-count prediction. Every effect listed under
**Predicting the draw count** was previously read mid-turn, so Pocketwatch did
not break when its threshold was passed, Pendulum and Draw Cards Next Turn were
missed outright, and Pollinous Core was a turn behind.
