using UnityEngine;
using Verse;

namespace PerformanceSearch;

// Three mutually exclusive behaviors for RecalculateDisplayRootCategory.
public enum DisplayRootMode
{
    Lca,        // O(n) LCA walk-up — our optimization (default)
    AlwaysRoot, // Always return root, skip calculation entirely
    Vanilla,    // Run the original O(categories × allowedDefs) parallel scan
}

public class PerformanceSearchMod : Mod
{
    public PerformanceSearchMod(ModContentPack content) : base(content)
    {
        GetSettings<PerformanceSearchSettings>();
    }

    public override string SettingsCategory() => "Performance Search";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.Label("Search boxes");
        listing.CheckboxLabeled(
            "  Debounce search boxes (recommended)",
            ref PerformanceSearchSettings.SearchDebounceEnabled,
            "Keep all items visible while typing and apply the filter once you pause. " +
            "Eliminates input lag on large mod lists. Disable to restore vanilla behaviour.");

        listing.Gap();
        listing.Label("Storage tab — GlobalBills scan");
        listing.CheckboxLabeled(
            "  Cache GlobalBills() during storage tab draw (recommended)",
            ref PerformanceSearchSettings.StorageGlobalBillsCacheEnabled,
            "Skips the redundant second GlobalBills() scan each frame when the storage " +
            "filter hasn't changed. Disable to restore vanilla behaviour.");

        listing.Gap();
        listing.Label("Storage tab — filter tree root (choose one)");

        var lcaMode = PerformanceSearchSettings.FilterDisplayRootMode;

        if (listing.RadioButton(
            "  LCA algorithm (recommended)",
            lcaMode == DisplayRootMode.Lca,
            tooltip: "Calculates which sub-category to pre-expand using an O(n) " +
                     "walk-up instead of the vanilla O(n²) parallel scan."))
            PerformanceSearchSettings.FilterDisplayRootMode = DisplayRootMode.Lca;

        if (listing.RadioButton(
            "  Always use root category",
            lcaMode == DisplayRootMode.AlwaysRoot,
            tooltip: "Skip the calculation entirely — the filter tree always opens " +
                     "at the top level. Use if you still see a pause on storage open."))
            PerformanceSearchSettings.FilterDisplayRootMode = DisplayRootMode.AlwaysRoot;

        if (listing.RadioButton(
            "  Vanilla (original parallel scan)",
            lcaMode == DisplayRootMode.Vanilla,
            tooltip: "Runs the original O(categories × allowedDefs) calculation. " +
                     "Expect a multi-second pause on first open of each storage object."))
            PerformanceSearchSettings.FilterDisplayRootMode = DisplayRootMode.Vanilla;

        listing.End();
        GetSettings<PerformanceSearchSettings>().Write();
    }
}

public class PerformanceSearchSettings : ModSettings
{
    public static bool SearchDebounceEnabled = true;
    public static bool StorageGlobalBillsCacheEnabled = true;
    public static DisplayRootMode FilterDisplayRootMode = DisplayRootMode.Lca;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref SearchDebounceEnabled, "searchDebounceEnabled", true);
        Scribe_Values.Look(ref StorageGlobalBillsCacheEnabled, "storageGlobalBillsCacheEnabled", true);
        Scribe_Values.Look(ref FilterDisplayRootMode, "filterDisplayRootMode", DisplayRootMode.Lca);
    }
}
