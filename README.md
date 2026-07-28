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

During combat, press **W** (rebindable in Settings → Input) or use the **ALL**
pile button beside Draw to open the mod's main screen: a Card Library shelf beside one continuous grid of every card the next
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

### The shortcut

**W** opens the screen and closes it again, and **Settings → Input** carries a
**View All Cards** row for rebinding it to another key or to a controller button.

The shortcut joins the game's input system rather than working around it.
`NInputManager` holds the action → key and action → controller-button
dictionaries, watches raw input, and synthesises an action event when a binding
matches; Settings → Input builds one row per action from two lists of remappable
actions and edits those same dictionaries. `AllCardsHotkey` registers the action
with Godot — deliberately with no key event of its own, since a second input
source would fire the shortcut twice and ignore any rebinding — and adds it to
both lists so the row appears and accepts either kind of input.

`InputSettingsPatch` supplies the rest:

- The keyboard default goes into `DefaultKeyboardInputMap`, which is the base
  every saved mapping is layered onto and exactly what **Reset to Default**
  restores, so one patch covers a fresh profile, a returning one, and a reset.
  A different default is a one-line change to `AllCardsHotkey.DefaultKey`.
- The row's label is written directly, because every other row reads its title
  from the game's localisation tables and a mod cannot add to those.
- The first controller binding is made safe. Rebinding normally *swaps* buttons,
  handing the displaced input whatever button the rebound action used to have —
  but the game's defaults already spend every controller button, so this
  shortcut starts unbound and has nothing to hand over. Binding it frees the
  button instead, leaving whatever held it unbound and visible in the same list.

Combat already uses A for the draw pile, S for the discard pile, D for the deck,
X for the exhaust pile, M for the map, E to accept, Space to peek, and 1-0 to
select cards, so W is free and sits in the same cluster as the pile keys.

### Controllers, and Steam Input

A press reaches a game action in three hops:

```text
button → [Steam binding] → Steam action → Controller.* input → game action
```

Steam owns only the first hop. Its action set is fixed at the fifteen buttons
declared in `<game>/controller_config/game_actions_2868840.vdf`, which a mod
cannot add to, and while Steam Input is active the game disables its own
controller rebinding entirely — `ShouldAllowControllerRebinding` returns false.
That is why a binding set in game appears to be ignored.

So instead of chasing bindings, **Settings → Mod Settings → Hypergeometric Draw
Odds** offers **Override Draw Pile button**, off by default. It changes what the
Draw Pile button *does* rather than what it is bound to: every route into that
button — a keyboard key, a controller button, a Steam Input action, or a mouse
click — arrives at one method, so overriding there covers all of them at once
and needs no input map rewritten. Every binding is left alone and Settings keeps
showing them; only the destination changes. It is a fair trade only because the
All Cards screen shows the draw pile and then some, which is why it is opt-in.
The setting applies immediately and is remembered in
`user://hypergeo_settings.cfg`.

Without that setting the shortcut starts unbound on a controller and can be
bound to any button from Settings → Input, at the cost of whatever action holds
that button.

### What the screen remembers

The game builds a fresh screen every time the pile view opens, so `AllCardsSession`
holds what would otherwise be discarded on close:

- **Show Odds on Cards** is a display preference and lasts as long as the game does.
- The **selection**, the **number needed**, and any **hand-picked draw count**
  belong to one combat and are dropped when a different combat begins.

Selections hold card instances, so playing or discarding a selected card does not
deselect it — it is the same card in a different pile. A card that leaves the
reachable pools entirely is pruned on the next render. A hand-picked draw count is
kept only while the real next-turn draw is unchanged; once the situation moves,
the honest number wins.

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
