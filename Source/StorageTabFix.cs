using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
// ThingFilter is mutated during that call. A Transpiler replaces the second
// GlobalBills() scan with a branch: if dirty, run the scan and diff as normal; if
// not dirty, reuse the first result so the diff is always empty. The player-visible
// bill-invalidation warning is unaffected — it still fires on any frame where the
// filter actually changed.

static class StorageTabFix
{
    // Set by ThingFilter mutation postfixes; reset by the FillTab prefix each frame.
    // Only touched on the main game thread so no volatile/Interlocked needed.
    internal static bool FilterDirtyThisFrame;

    [HarmonyPatch(typeof(ITab_Storage), "FillTab")]
    static class FillTab_Patch
    {
        // Reset before each frame so a clean draw never triggers the second scan.
        static void Prefix() => FilterDirtyThisFrame = false;

        static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = instructions.ToList();

            // Match by method name rather than exact MethodInfo so the search is
            // robust to signature changes across RimWorld updates.
            int firstGBIdx = codes.FindIndex(c =>
                c.opcode == OpCodes.Call &&
                (c.operand as MethodInfo)?.Name == nameof(BillUtility.GlobalBills));
            int filterIdx = codes.FindIndex(c =>
                c.opcode == OpCodes.Call &&
                (c.operand as MethodInfo)?.Name == "DoThingFilterConfigWindow");
            int secondGBIdx = filterIdx >= 0
                ? codes.FindIndex(filterIdx + 1, c =>
                    c.opcode == OpCodes.Call &&
                    (c.operand as MethodInfo)?.Name == nameof(BillUtility.GlobalBills))
                : -1;

            if (firstGBIdx < 0 || filterIdx < 0 || secondGBIdx < 0)
            {
                Log.Warning("[PerformanceSearch] ITab_Storage.FillTab: expected IL pattern not found — GlobalBills optimisation skipped.");
                return codes;
            }

            // 'first' local: the last stloc between the first GlobalBills call and
            // DoThingFilterConfigWindow.
            int firstStlocIdx = -1;
            for (int i = filterIdx - 1; i > firstGBIdx; i--)
            {
                if (codes[i].IsStloc()) { firstStlocIdx = i; break; }
            }

            // 'second' local: the first stloc after the second GlobalBills call.
            int secondStlocIdx = codes.FindIndex(secondGBIdx + 1, c => c.IsStloc());

            if (firstStlocIdx < 0 || secondStlocIdx < 0)
            {
                Log.Warning("[PerformanceSearch] ITab_Storage.FillTab: stloc pattern not found — GlobalBills optimisation skipped.");
                return codes;
            }

            // Insert before the second GlobalBills call:
            //
            //   if (FilterDirtyThisFrame) goto labelDoScan  <- dirty: run real scan
            //   ldloc first                                  <- not dirty: push first…
            //   goto labelStloc                              <- …and share the stloc
            // [labelDoScan:]
            //   [original second GlobalBills + Where + ToArray chain]
            // [labelStloc:]
            //   stloc second                                 <- stores whichever is on stack
            var labelDoScan = il.DefineLabel();
            var labelStloc  = il.DefineLabel();

            codes[secondGBIdx].labels.Add(labelDoScan);
            codes[secondStlocIdx].labels.Add(labelStloc);

            codes.InsertRange(secondGBIdx, new[]
            {
                new CodeInstruction(OpCodes.Ldsfld,
                    AccessTools.Field(typeof(StorageTabFix), nameof(FilterDirtyThisFrame))),
                new CodeInstruction(OpCodes.Brtrue_S, labelDoScan),
                MakeLdloc(codes[firstStlocIdx]),
                new CodeInstruction(OpCodes.Br_S, labelStloc),
            });

            return codes;
        }

        // Returns a ldloc instruction that loads the same local as the given stloc.
        static CodeInstruction MakeLdloc(CodeInstruction stloc)
        {
            if (stloc.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (stloc.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (stloc.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (stloc.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            if (stloc.opcode == OpCodes.Stloc_S) return new CodeInstruction(OpCodes.Ldloc_S, stloc.operand);
            return new CodeInstruction(OpCodes.Ldloc, stloc.operand);
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
            // SetAllow overloads — one per allow/deny type used in the filter UI
            foreach (var argTypes in new[]
            {
                new[] { typeof(ThingDef),           typeof(bool) },
                new[] { typeof(SpecialThingFilterDef), typeof(bool) },
                new[] { typeof(ThingCategoryDef),   typeof(bool) },
                new[] { typeof(StuffCategoryDef),   typeof(bool) },
            })
            {
                var m = AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.SetAllow), argTypes);
                if (m != null) yield return m;
            }

            // Bulk operations (Reset to defaults, copy from parent filter, etc.)
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
