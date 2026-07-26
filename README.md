# Hypergeometric Draw Odds

An informational Slay the Spire 2 mod that adds native-style draw-chance
analysis to the combat Draw Pile and Discard Pile screens.
Discard cards show a nonzero chance only when the next hand will exhaust the
current draw pile and continue after a reshuffle.

- Click (or alternate-click) cards to toggle selections.
- Hovering a card shows the chance of drawing any identical copy and the chance
  of drawing that exact physical card. Identical copies are grouped by card ID,
  exact upgrade level, enchantment ID, and enchantment amount.
- The pile screen's native bottom information strip updates in real time with
  the probability of drawing at least one selected card.
- The calculation is `1 - C(N-K, n) / C(N, n)`.
- `N` is draw-pile size, `K` is selected card instances, and `n` is the
  context-adjusted next hand draw capped by hand capacity and pile size.
- No-draw effects, hand-draw modifiers, retain/no-flush effects, and maximum
  hand size are included. Effects that create cards during the future
  `BeforeHandDraw` phase cannot be predicted before they resolve.

## All Cards screen

During combat, use the **ALL** pile button beside Draw to open a combined
analysis screen. One continuous grid lists sorted Draw cards first, followed by
sorted Discard cards. Probabilities preserve the game's real draw order: Draw
is consumed first, and Discard becomes available only if the requested draw
crosses a reshuffle.

- Card clicks always toggle exact physical copies.
- Hovering a card shows the same native Draw Chance tooltip used by the
  individual Draw and Discard screens.
- A card-sized **DISCARD PILE →** spacer marks the exact boundary between the
  two piles.
- **ANY** calculates the chance of drawing at least the chosen number of
  selected cards.
- **ALL** calculates the chance of drawing every selected card.
- The two **−**/**+** rows change cards drawn and the ANY target respectively.
- Clicking the central draw-count button restores the natural next-turn draw
  after modifiers, retain, and hand-capacity constraints. This natural value is
  also restored every time the screen opens.
- The combined result uses the pile screen's native bottom information text.

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
