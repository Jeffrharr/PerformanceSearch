using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

// ThingFilter.RecalculateDisplayRootCategory() determines which category node to use
// as the root of the filter tree UI — purely cosmetic, affects only which category is
// pre-expanded when you open a storage tab.
//
// The vanilla algorithm is O(categories × allowedDefs): for every category node in
// the database, it scans every allowed def to test containment. On a heavily modded
// game this can take several seconds per storage object on first open.
//
// The correct algorithm is a Lowest Common Ancestor (LCA) walk: start with the first
// allowed def's ancestor chain, then intersect with each subsequent def's ancestors.
// Short-circuit as soon as the common set collapses to root (which is the immediate
// result for the common "allow everything" case after checking 2-3 defs).
//
// Complexity: O(allowedDefs × tree depth). Tree depth is ~6-8 levels even in heavily
// modded games, making this effectively O(allowedDefs) — orders of magnitude cheaper.

namespace PerformanceSearch;

[HarmonyPatch(typeof(ThingFilter), "RecalculateDisplayRootCategory")]
static class ThingFilter_RecalculateDisplayRootCategory_Patch
{
    // Cached field accessors — located once, reused on every call.
    private static readonly FieldInfo AllowedDefsField =
        AccessTools.Field(typeof(ThingFilter), "allowedDefs");
    private static readonly FieldInfo DisplayRootCategoryIntField =
        AccessTools.Field(typeof(ThingFilter), "displayRootCategoryInt");

    static void Prefix(ThingFilter __instance, ref bool __runOriginal)
    {
        switch (PerformanceSearchSettings.FilterDisplayRootMode)
        {
            case DisplayRootMode.Vanilla:
                return; // let the original run unmodified

            case DisplayRootMode.AlwaysRoot:
                DisplayRootCategoryIntField.SetValue(__instance, ThingCategoryNodeDatabase.RootNode);
                __runOriginal = false;
                return;

            default: // DisplayRootMode.Lca
                var allowedDefs = (HashSet<ThingDef>)AllowedDefsField.GetValue(__instance);
                DisplayRootCategoryIntField.SetValue(__instance, FindDisplayRoot(__instance, allowedDefs));
                __runOriginal = false;
                return;
        }
    }

    private static TreeNode_ThingCategory FindDisplayRoot(
        ThingFilter filter, HashSet<ThingDef> allowedDefs)
    {
        if (allowedDefs.Count == 0)
            return ThingCategoryNodeDatabase.RootNode;

        var rootCatDef = ThingCategoryNodeDatabase.RootNode.catDef;

        // Build the initial candidate set from the first def's ancestor chain.
        HashSet<ThingCategoryDef> common = null;
        ThingCategoryDef deepestSingle = null;

        foreach (ThingDef def in allowedDefs)
        {
            if (common == null)
            {
                // First def: collect all ancestors including its own leaf categories.
                common = new HashSet<ThingCategoryDef>();
                if (def.thingCategories != null)
                {
                    foreach (ThingCategoryDef cat in def.thingCategories)
                    {
                        ThingCategoryDef c = cat;
                        while (c != null && common.Add(c))
                            c = c.parent;
                    }
                }

                // Fast path: single allowed def — deepest leaf category is the answer.
                if (allowedDefs.Count == 1)
                {
                    deepestSingle = DeepestOf(common, rootCatDef);
                    break;
                }
            }
            else
            {
                // Subsequent defs: intersect with this def's ancestors.
                if (def.thingCategories != null)
                {
                    var defAncestors = new HashSet<ThingCategoryDef>();
                    foreach (ThingCategoryDef cat in def.thingCategories)
                    {
                        ThingCategoryDef c = cat;
                        while (c != null && defAncestors.Add(c))
                            c = c.parent;
                    }
                    common.IntersectWith(defAncestors);
                }
                else
                {
                    // Def has no categories — only root can be common.
                    common.Clear();
                }

                // Short-circuit: once collapsed to root or empty, no tighter answer exists.
                if (common.Count == 0 || (common.Count == 1 && common.Contains(rootCatDef)))
                    return ThingCategoryNodeDatabase.RootNode;
            }
        }

        if (deepestSingle != null)
            return deepestSingle.treeNode ?? ThingCategoryNodeDatabase.RootNode;

        ThingCategoryDef lca = DeepestOf(common, rootCatDef);
        return lca?.treeNode ?? ThingCategoryNodeDatabase.RootNode;
    }

    // Returns the deepest category in the set by walking the parent chain to measure depth.
    // The set is at most O(tree depth) entries so this is cheap.
    private static ThingCategoryDef DeepestOf(
        HashSet<ThingCategoryDef> candidates, ThingCategoryDef rootCatDef)
    {
        ThingCategoryDef deepest = rootCatDef;
        int deepestDepth = 0;

        foreach (ThingCategoryDef cat in candidates)
        {
            int depth = 0;
            ThingCategoryDef c = cat;
            while (c.parent != null) { depth++; c = c.parent; }

            if (depth > deepestDepth)
            {
                deepestDepth = depth;
                deepest = cat;
            }
        }

        return deepest;
    }
}
