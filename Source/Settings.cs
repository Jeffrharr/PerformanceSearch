using UnityEngine;
using Verse;

namespace PerformanceSearch;

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
        listing.CheckboxLabeled(
            "Debounce search boxes",
            ref PerformanceSearchSettings.SearchDebounceEnabled,
            "Keep all items visible while typing in a search box and apply the filter once " +
            "you pause. Eliminates input lag on large mod lists. Disable if search feels " +
            "unresponsive or conflicts with another mod.");
        listing.CheckboxLabeled(
            "Always use root category for storage filters",
            ref PerformanceSearchSettings.AlwaysUseRootCategory,
            "Skip calculating which sub-category to pre-expand when opening a storage tab. " +
            "The filter tree will always open at the top level. " +
            "Enable this if you still experience a pause when first opening storage objects.");
        listing.End();
        GetSettings<PerformanceSearchSettings>().Write();
    }
}

public class PerformanceSearchSettings : ModSettings
{
    public static bool SearchDebounceEnabled = true;

    // When true, RecalculateDisplayRootCategory always returns root immediately —
    // the UI tree opens at the top level instead of pre-focused on the relevant category.
    // Useful as a fallback if the LCA algorithm is still too slow for a particular setup.
    public static bool AlwaysUseRootCategory = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref SearchDebounceEnabled, "searchDebounceEnabled", true);
        Scribe_Values.Look(ref AlwaysUseRootCategory, "alwaysUseRootCategory", false);
    }
}
