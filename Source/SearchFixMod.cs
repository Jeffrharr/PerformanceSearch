using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

// RimWorld's QuickSearchFilter.Matches(string) is the single chokepoint all search paths
// funnel through — architect menu, ingredient filters, bill config, and any other
// QuickSearchWidget in the game. It is called synchronously for every node in a full
// tree walk on every frame the search text differs from the previous frame, with no
// debouncing. With many mods installed this means thousands of string comparisons per
// keystroke, causing visible input latency.
//
// QuickSearchFilter already maintains an LRU cache (5000 entries) that makes repeated
// Matches() calls within the same frame cheap, but the cache is cleared every time
// Text is set — i.e., on every keystroke — so the first frame after each keypress
// always pays the full cost.
//
// Fix: for 150ms after the last Text change, Matches() returns true for everything.
// This keeps all items visible while the user is actively typing (no flicker, no lag),
// then the real filter snaps in once they pause. A single patch on Matches(string)
// covers all three overloads because Matches(ThingDef) and Matches(SpecialThingFilterDef)
// both delegate to Matches(string) internally.
//
// A trie or other algorithmic improvement is unnecessary — the bottleneck is call
// frequency, not search complexity.

namespace PerformanceSearch;

[StaticConstructorOnStartup]
public static class SearchFixMod
{
    // 150ms is long enough that fast typing never triggers the filter mid-word,
    // short enough that the filter feels instant when the user pauses.
    private const float DebounceSeconds = 0.15f;

    // ConditionalWeakTable is used instead of a Dictionary so that QuickSearchFilter
    // instances can be garbage collected normally — we never hold a strong reference
    // to them. This matters because search widgets are created per-window and may be
    // discarded frequently.
    private static readonly ConditionalWeakTable<QuickSearchFilter, DebounceState> States = new();

    // [StaticConstructorOnStartup] ensures this runs after all mods are loaded and
    // RimWorld's own static initialization is complete. PatchAll() scans this assembly
    // for nested classes annotated with [HarmonyPatch] and applies them.
    static SearchFixMod()
    {
        new Harmony("performancesearch").PatchAll();
    }

    // Holds the timestamp of the most recent Text assignment for a given filter.
    // Initialized to float.MinValue so the debounce window is never active before
    // the user has typed anything.
    private class DebounceState
    {
        public float LastChangeTime = float.MinValue;
    }

    // Postfix on QuickSearchFilter.Text setter.
    // Runs after the original setter (which updates inputText, searchText, and clears
    // the LRU cache). Records the current real time so the Matches patch knows how
    // recently the search text changed.
    // We use Time.realtimeSinceStartup rather than game ticks because the loading
    // screen and pause state affect tick progression but not real elapsed time.
    [HarmonyPatch(typeof(QuickSearchFilter), "Text", MethodType.Setter)]
    static class QuickSearchFilter_Text_Setter_Patch
    {
        static void Postfix(QuickSearchFilter __instance)
        {
            States.GetOrCreateValue(__instance).LastChangeTime = Time.realtimeSinceStartup;
        }
    }

    // Prefix on QuickSearchFilter.Matches(string) — the only overload that does real
    // work. The other two overloads, Matches(ThingDef) and Matches(SpecialThingFilterDef),
    // both call this one internally so patching here covers all search paths.
    //
    // If the filter instance has no debounce state (TryGetValue fails), the user hasn't
    // typed yet and we let the original run normally.
    //
    // Within the debounce window: set __result = true and return false to skip the
    // original entirely. Returning true from every Matches() call means the tree walk
    // completes instantly and all items remain visible while the user types.
    //
    // Outside the debounce window: return true to run the original Matches() normally,
    // which uses the LRU cache for subsequent calls within the same frame.
    [HarmonyPatch(typeof(QuickSearchFilter), nameof(QuickSearchFilter.Matches), typeof(string))]
    static class QuickSearchFilter_Matches_Patch
    {
        static bool Prefix(QuickSearchFilter __instance, ref bool __result)
        {
            if (!PerformanceSearchSettings.SearchDebounceEnabled)
                return true;

            if (!States.TryGetValue(__instance, out var state))
                return true;

            if (Time.realtimeSinceStartup - state.LastChangeTime < DebounceSeconds)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }
}
