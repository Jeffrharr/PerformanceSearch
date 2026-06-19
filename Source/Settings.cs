using UnityEngine;
using Verse;

namespace PerformanceSearch;

public class PerformanceSearchMod : Mod
{
    public PerformanceSearchMod(ModContentPack content) : base(content)
    {
        GetSettings<ThingFilterFixSettings>();
    }

    public override string SettingsCategory() => "Performance Search";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled(
            "Always use root category for storage filters",
            ref ThingFilterFixSettings.AlwaysUseRootCategory,
            "Skip calculating which sub-category to pre-expand when opening a storage tab. " +
            "The filter tree will always open at the top level. " +
            "Enable this if you still experience a pause when first opening storage objects.");
        listing.End();
        GetSettings<ThingFilterFixSettings>().Write();
    }
}

public class ThingFilterFixSettings : ModSettings
{
    // When true, RecalculateDisplayRootCategory always returns root immediately —
    // the UI tree opens at the top level instead of pre-focused on the relevant category.
    // Useful as a fallback if the LCA algorithm is still too slow for a particular setup.
    public static bool AlwaysUseRootCategory = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref AlwaysUseRootCategory, "alwaysUseRootCategory", false);
    }
}
