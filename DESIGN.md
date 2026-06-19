# PerformanceSearch — Design

## Problem

RimWorld's `QuickSearchWidget` fires a full synchronous filter on every keystroke with
no debouncing. All search paths (architect menu, ingredient filters, bill config) funnel
through `QuickSearchFilter.Matches(string)`, which iterates every node in a category
tree and does a culture-aware string comparison per node. With many mods installed this
causes visible input latency.

## Approach: Debounce at the filter level

Rather than patching each individual search consumer (architect menu, ThingFilterUI,
etc.), we patch `QuickSearchFilter.Matches(string)` directly. This is the single
chokepoint all search paths share, so one patch covers everything — including any
mod-added search widgets that use the same vanilla class.

During a 150ms debounce window after the last keystroke, `Matches()` returns `true`
for everything. This keeps all items visible while the user types (no flicker), then
the real filter applies once they pause.

## Why not a trie or other data structure?

The bottleneck is call frequency, not algorithmic complexity. `QuickSearchFilter`
already maintains a per-instance LRU cache (5000 entries) that makes repeated
`Matches()` calls within the same frame cheap. The only cost is the first call after
each keystroke clears the cache. Debouncing eliminates that cost entirely, making
structural improvements unnecessary.

## Harmony patch strategy

Two patches:

1. **Postfix on `QuickSearchFilter.Text` setter** — records the real-time timestamp
   whenever search text changes. Uses `Time.realtimeSinceStartup` rather than game
   ticks so it works correctly during pause and on loading screens.

2. **Prefix on `QuickSearchFilter.Matches(string)`** — within the debounce window,
   short-circuits with `return true`, skipping the original entirely. Outside the
   window, defers to the original (and its LRU cache).

The `Matches(ThingDef)` and `Matches(SpecialThingFilterDef)` overloads both delegate
to `Matches(string)` internally, so they are covered by the single string patch.

## Conflict risk

Low. We patch two methods that no other performance mod is known to patch.
`QuickSearchFilter` is a leaf utility class with no subclasses. If another mod also
patches `Matches(string)` as a prefix, Harmony will run both prefixes in load order —
ours returning `false` during the debounce window will prevent the other prefix from
seeing calls during that window, which is benign (their filter result is irrelevant
while we're returning true for everything anyway).

The one risk: a mod that replaces `QuickSearchFilter` entirely with a subclass would
bypass our patch. This is unlikely but worth monitoring.

## Debounce duration

150ms was chosen as the sweet spot where fast typing never triggers mid-word, but
the filter feels instant when the user pauses. This value is a named constant
(`DebounceSeconds`) and can be adjusted.

## State management

Per-filter debounce state is stored in a `ConditionalWeakTable` keyed on the
`QuickSearchFilter` instance. This allows filter instances to be garbage collected
normally without any manual cleanup — the table holds weak references to keys.

## API compatibility tests

A companion NUnit project uses `Mono.Cecil` to verify that the types and methods we
patch still exist in `Assembly-CSharp.dll` after each RimWorld update. Run `./test.sh`
before loading the game after any update.
