using Mono.Cecil;

namespace SearchFix.Tests;

/// <summary>
/// Verifies that the RimWorld API surface SearchFix depends on still exists.
/// Run these after every RimWorld update. Failures mean the mod needs updating.
/// </summary>
[TestFixture]
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

    // --- helpers ---

    private TypeDefinition? GetType(string fullName) =>
        _module.Types.FirstOrDefault(t => t.FullName == fullName);
}
