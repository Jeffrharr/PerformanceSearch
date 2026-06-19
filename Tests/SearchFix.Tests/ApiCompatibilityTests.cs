using Mono.Cecil;

namespace SearchFix.Tests;

/// <summary>
/// Verifies that the RimWorld API surface SearchFix depends on still exists.
/// Run these after every RimWorld update. Failures mean the mod needs updating.
/// </summary>
[TestFixture]
[Category("RequiresGameDll")]
public class ApiCompatibilityTests
{
    private const string FallbackDllPath =
        "/home/deck/.local/share/Steam/steamapps/common/RimWorld/RimWorldLinux_Data/Managed/Assembly-CSharp.dll";

    private static string DllPath =>
        Environment.GetEnvironmentVariable("RIMWORLD_ASSEMBLY") ?? FallbackDllPath;

    private ModuleDefinition _module = null!;

    [OneTimeSetUp]
    public void LoadAssembly()
    {
        if (!File.Exists(DllPath))
            Assert.Ignore($"Assembly-CSharp.dll not found at {DllPath} — set RIMWORLD_ASSEMBLY to run these tests.");
        _module = ModuleDefinition.ReadModule(DllPath);
    }

    [OneTimeTearDown]
    public void Dispose() => _module?.Dispose();

    // --- QuickSearchFilter ---

    [Test]
    public void QuickSearchFilter_TypeExists()
    {
        Assert.That(GetType("RimWorld.QuickSearchFilter"), Is.Not.Null,
            "RimWorld.QuickSearchFilter no longer exists");
    }

    [Test]
    public void QuickSearchFilter_Text_SetterExists()
    {
        var type = GetType("RimWorld.QuickSearchFilter");
        Assert.That(type, Is.Not.Null);
        var setter = type!.Properties
            .SingleOrDefault(p => p.Name == "Text")?.SetMethod;
        Assert.That(setter, Is.Not.Null,
            "QuickSearchFilter.Text setter no longer exists");
    }

    [Test]
    public void QuickSearchFilter_Matches_StringOverloadExists()
    {
        var type = GetType("RimWorld.QuickSearchFilter");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m =>
            m.Name == "Matches" &&
            m.Parameters.Count == 1 &&
            m.Parameters[0].ParameterType.FullName == "System.String");
        Assert.That(method, Is.Not.Null,
            "QuickSearchFilter.Matches(string) no longer exists");
    }

    [Test]
    public void QuickSearchFilter_Matches_ReturnsBoolean()
    {
        var type = GetType("RimWorld.QuickSearchFilter");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m =>
            m.Name == "Matches" &&
            m.Parameters.Count == 1 &&
            m.Parameters[0].ParameterType.FullName == "System.String");
        Assert.That(method?.ReturnType.FullName, Is.EqualTo("System.Boolean"),
            "QuickSearchFilter.Matches(string) no longer returns bool");
    }

    [Test]
    public void QuickSearchFilter_Text_GetterExists()
    {
        var type = GetType("RimWorld.QuickSearchFilter");
        Assert.That(type, Is.Not.Null);
        var getter = type!.Properties
            .SingleOrDefault(p => p.Name == "Text")?.GetMethod;
        Assert.That(getter, Is.Not.Null,
            "QuickSearchFilter.Text getter no longer exists — debounce state tracking will break");
    }

    // --- QuickSearchWidget (uses QuickSearchFilter) ---

    [Test]
    public void QuickSearchWidget_TypeExists()
    {
        Assert.That(GetType("RimWorld.QuickSearchWidget"), Is.Not.Null,
            "RimWorld.QuickSearchWidget no longer exists");
    }

    [Test]
    public void QuickSearchWidget_HasFilterField()
    {
        var type = GetType("RimWorld.QuickSearchWidget");
        Assert.That(type, Is.Not.Null);
        var field = type!.Fields.SingleOrDefault(f =>
            f.Name == "filter" &&
            f.FieldType.FullName == "RimWorld.QuickSearchFilter");
        Assert.That(field, Is.Not.Null,
            "QuickSearchWidget.filter (QuickSearchFilter) field no longer exists");
    }

    // --- ITab_Storage / BillUtility (StorageTabFix) ---

    [Test]
    public void ITab_Storage_TypeExists()
    {
        Assert.That(GetType("RimWorld.ITab_Storage"), Is.Not.Null,
            "RimWorld.ITab_Storage no longer exists");
    }

    [Test]
    public void ITab_Storage_FillTab_Exists()
    {
        var type = GetType("RimWorld.ITab_Storage");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m => m.Name == "FillTab");
        Assert.That(method, Is.Not.Null,
            "ITab_Storage.FillTab no longer exists — storage tab transpiler will fail");
    }

    [Test]
    public void BillUtility_GlobalBills_Exists()
    {
        var type = GetType("RimWorld.BillUtility");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m => m.Name == "GlobalBills");
        Assert.That(method, Is.Not.Null,
            "BillUtility.GlobalBills no longer exists — storage tab transpiler will fail");
    }

    [Test]
    public void ThingFilterUI_DoThingFilterConfigWindow_Exists()
    {
        var type = GetType("Verse.ThingFilterUI");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m => m.Name == "DoThingFilterConfigWindow");
        Assert.That(method, Is.Not.Null,
            "ThingFilterUI.DoThingFilterConfigWindow no longer exists — storage tab transpiler will fail");
    }

    [Test]
    public void ThingFilter_SetAllow_ThingDef_Exists()
    {
        var type = GetType("Verse.ThingFilter");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m =>
            m.Name == "SetAllow" &&
            m.Parameters.Count == 2 &&
            m.Parameters[0].ParameterType.FullName == "Verse.ThingDef");
        Assert.That(method, Is.Not.Null,
            "ThingFilter.SetAllow(ThingDef, bool) no longer exists");
    }

    [Test]
    public void ThingFilter_SetAllow_SpecialThingFilterDef_Exists()
    {
        var type = GetType("Verse.ThingFilter");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m =>
            m.Name == "SetAllow" &&
            m.Parameters.Count == 2 &&
            m.Parameters[0].ParameterType.FullName == "Verse.SpecialThingFilterDef");
        Assert.That(method, Is.Not.Null,
            "ThingFilter.SetAllow(SpecialThingFilterDef, bool) no longer exists");
    }

    [Test]
    public void ThingFilter_SetDisallowAll_Exists()
    {
        var type = GetType("Verse.ThingFilter");
        Assert.That(type, Is.Not.Null);
        var method = type!.Methods.SingleOrDefault(m => m.Name == "SetDisallowAll");
        Assert.That(method, Is.Not.Null,
            "ThingFilter.SetDisallowAll no longer exists");
    }

    // --- helpers ---

    private TypeDefinition? GetType(string fullName) =>
        _module.Types.FirstOrDefault(t => t.FullName == fullName);
}
