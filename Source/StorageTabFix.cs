using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PerformanceSearch;

// ITab_Storage.FillTab() is called every GUI frame while the storage inspector tab
// is open. It calls BillUtility.GlobalBills() twice per frame — once before rendering
// the ThingFilter UI to snapshot which production bills can output to this storage,
// and once after to detect bills that lost eligibility due to a filter change. On the
// frame a filter checkbox is clicked, the diff fires a player-visible warning message.
//
// The double call is intentional as a change-detection mechanism, but it runs
// unconditionally on every frame. On the ~99.9% of frames where the player is just
// looking, both scans are wasted. GlobalBills() walks every map, every bill giver,
// and every bill — on a large save this is the dominant cost in FillTab().
//
// Fix: a dirty flag is reset at the start of each FillTab() call and set whenever
// ThingFilter is mutated during that call. A Postfix on GlobalBills() materializes
// the generator into a cached List<Bill> on the first call; the second call returns
// the cache directly when the filter is clean, making it free. The bill-invalidation
// warning is unaffected — when the filter IS dirty the cache is refreshed and the
// diff runs normally.

static class StorageTabFix
{
    // True while FillTab() is executing on this frame.
    private static bool s_inFillTab;

    // Cached result from the first GlobalBills() call this frame.
    // Null when outside FillTab or after a dirty-filter refresh.
    private static List<Bill> s_cachedBills;

    // Set by ThingFilter mutation postfixes; reset by the FillTab prefix each frame.
    // Only touched on the main thread so no volatile/Interlocked needed.
    internal static bool FilterDirtyThisFrame;

    [HarmonyPatch(typeof(ITab_Storage), "FillTab")]
    static class FillTab_Patch
    {
        static void Prefix()
        {
            s_inFillTab = true;
            s_cachedBills = null;
            FilterDirtyThisFrame = false;
        }

        static void Postfix()
        {
            s_inFillTab = false;
            s_cachedBills = null;
        }
    }

    // Postfix on GlobalBills(): materializes the generator into a List<Bill> on the
    // first call, caches it, and returns the cache on the second call when the filter
    // hasn't changed. Outside FillTab the method runs entirely unaffected.
    [HarmonyPatch(typeof(BillUtility), nameof(BillUtility.GlobalBills))]
    static class GlobalBills_Cache_Patch
    {
        static void Postfix(ref IEnumerable<Bill> __result)
        {
            if (!s_inFillTab) return;

            if (s_cachedBills != null && !FilterDirtyThisFrame)
            {
                // Second call, filter unchanged — serve the cache.
                __result = s_cachedBills;
                return;
            }

            // First call (or filter changed) — materialize and cache.
            // FillTab was going to enumerate the generator via .ToArray() anyway;
            // materializing it here just moves that cost to a List instead.
            s_cachedBills = __result.ToList();
            __result = s_cachedBills;
        }
    }

    // Marks the filter dirty when any ThingFilter mutation fires during a FillTab()
    // call. Covers all paths that DoThingFilterConfigWindow() takes when the player
    // clicks a checkbox, a category button, or a bulk-change control.
    [HarmonyPatch]
    static class ThingFilter_Mutation_Patch
    {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var argTypes in new[]
            {
                new[] { typeof(ThingDef),               typeof(bool) },
                new[] { typeof(SpecialThingFilterDef),  typeof(bool) },
                new[] { typeof(ThingCategoryDef),       typeof(bool) },
                new[] { typeof(StuffCategoryDef),       typeof(bool) },
            })
            {
                var m = AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.SetAllow), argTypes);
                if (m != null) yield return m;
            }

            foreach (var name in new[]
            {
                nameof(ThingFilter.SetAllowAll),
                nameof(ThingFilter.SetDisallowAll),
                nameof(ThingFilter.CopyAllowancesFrom),
            })
            {
                var m = AccessTools.Method(typeof(ThingFilter), name);
                if (m != null) yield return m;
            }
        }

        static void Postfix() => FilterDirtyThisFrame = true;
    }
}
