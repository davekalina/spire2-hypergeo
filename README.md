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

## All Cards screen

During combat, press **W** (rebindable in Settings → Input) or use the **%** pile
button beside Draw to open the mod's main screen: a Card Library shelf beside one
continuous grid of every card the next hand could reach, in draw order. Like the
Card Library, it draws over the run's top bar and relic inventory rather than
under them.

The grid runs the draw pile, then a card-sized **Reshuffle — Discard Pile +
Cards in Hand →** marker, then the reshuffle pool itself. The discard pile and
the hand share one section, sorted as a single run, because they are one
population: they return to the draw pile together. Anything retain is holding
back follows a final **Retained — Stays in Hand** marker, kept out of the
reshuffle it will not take part in. Card clicks toggle exact physical copies,
and hovering shows the same native Draw Chance tooltip the individual pile
screens use.

The shelf holds the whole query and its result:

- **Search** — the Card Library's own search bar, at the top of the shelf. It
  matches a card's name or the text of its description, and a rarity name stands
  for every card of that rarity. It only decides what the grid draws: selections,
  populations and odds all come from the full pools, so searching narrows the
  view without moving a number. A selected card that scrolls out of the filter
  stays selected and stays in the calculation.

- **Draw** — the **−**/**+** row sets how many cards the next hand draws, and
  the effects that moved it off the base of five are named underneath. Clicking
  the count restores the real next-turn draw. While the count is set by hand the
  note says so and offers the real value.
- **Selection** — how many of the selected cards the hand needs. One asks for
  any of them; the full count asks for every one, so no separate ANY/ALL mode is
  needed.
- **Draw Chance** — states the question in words, naming the picked cards
  ("Chance to draw Strike or Neutralize:"), then the required hits and the
  probability. A **Retained** row appears when a selection includes retained
  cards, which explains a lower-than-expected result.
- **Combine Same Card Odds** — on by default, so an on-card chance covers every
  copy of that card: "will I draw a Strike" is the usual question. Off answers
  for that physical copy alone, which differs between copies once they sit in
  different piles. The hover tip reports both either way.
- **Show Odds on Cards** — prints chances onto the cards themselves, using the
  same on-card readout as the Card Library's View Stats toggle. With nothing
  selected each card carries its own any-copy chance. Once cards are selected
  the question has changed, so only the selection is marked, and every badge
  carries the one joint chance the shelf reports, captioned with the query
  (`This card`, `Any of 3`, `2 of 3`, `All 3`) so it is not mistaken for that
  card's own odds. The band spans the card's own art rect and sizes itself to
  the text.
- **Rawdog Mode** — replaces the combat query with a plain hypergeometric
  calculator: deck size, draw size, hits in deck, hits wanted, each on a stepper.
  It opens on the run's deck size, because a calculator is reached for to ask
  about a deck being built rather than about this turn. It reports the chance of
  **exactly**, **at least** and **at most** that many hits, plus the number of
  hits the draw is **expected** to contain. The card grid is left alone
  underneath, still searchable.
- The footer carries the mod name and version, with a **?** button whose hover
  tip explains the screen. That tip is `HelpText` in
  `HypergeoCode/AllCardsPileScreenView.cs`.

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
