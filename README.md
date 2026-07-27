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

Cards a retain effect keeps in hand never leave it, so they are excluded from
both stages and always read 0%.

`DrawPools` builds these populations from live combat state, and every screen
resolves its odds through it, so the piles, the natural draw count, and the
reshuffle model cannot diverge between screens.

## All Cards screen

During combat, use the **ALL** pile button beside Draw to open the mod's main
screen: a Card Library shelf beside one continuous grid of every card the next
hand could reach, in draw order.

The grid runs Draw Pile, then Discard Pile, then Cards in Hand, with a
card-sized **DISCARD PILE →** and **CARDS IN HAND →** marker at each boundary.
Card clicks toggle exact physical copies, and hovering shows the same native
Draw Chance tooltip the individual pile screens use.

The shelf holds the whole query and its result:

- **DRAW** — the **−**/**+** row sets how many cards the next hand draws.
  Clicking the count restores the natural next-turn draw after modifiers,
  retain, and hand-capacity constraints. That natural value is also restored
  every time the screen opens.
- **SELECTION** — **ANY** calculates the chance of drawing at least the chosen
  number of selected cards; **ALL** the chance of drawing every one of them.
  The second **−**/**+** row sets the ANY target.
- **DRAW CHANCE** — selected count, required hits, and the resulting
  probability. A **Held in hand** row appears when a selection includes retained
  cards, which explains a lower-than-expected result.
- **Show Odds on Cards** — prints each card's any-copy draw chance onto the
  cards themselves, using the same on-card readout as the Card Library's View
  Stats toggle.

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

v0.6.0 rebuilt the All Cards screen around the Card Library shelf, added the
Cards in Hand section, and folded the hand into the reshuffle pool. Before that,
cards in hand were absent from the screen and from the odds entirely.
