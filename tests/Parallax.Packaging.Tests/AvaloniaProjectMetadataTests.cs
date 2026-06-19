using System.Xml.Linq;

using static Parallax.Packaging.Tests.PackagingTestSupport;

namespace Parallax.Packaging.Tests;

public sealed class AvaloniaProjectMetadataTests
{
    [Fact]
    public void AvaloniaAppPlatformReferencesAreIsolatedByRuntimeIdentifier()
    {
        XDocument project = XDocument.Load(Path.Combine(
            RepoRoot,
            "src/Parallax.App.Avalonia/Parallax.App.Avalonia.csproj"));
        XElement root = Assert.Single(project.Elements("Project"));
        var references = root
            .Descendants("ProjectReference")
            .Select(element => new ProjectReferenceMetadata(
                NormalizeProjectPath((string?)element.Attribute("Include")),
                (string?)element.Attribute("Condition") ?? string.Empty))
            .ToArray();
        var propertyGroups = root
            .Elements("PropertyGroup")
            .Select(element => new PropertyGroupMetadata(
                (string?)element.Attribute("Condition") ?? string.Empty,
                (string?)element.Element("DefineConstants") ?? string.Empty))
            .ToArray();

        Assert.Contains(
            references,
            item => item.Include == "../Parallax.Core/Parallax.Core.csproj" && item.Condition.Length == 0);

        AssertRidScopedReference(references, "../Parallax.Platform.Windows/Parallax.Platform.Windows.csproj", "win-");
        AssertRidScopedReference(references, "../Parallax.Platform.Mac/Parallax.Platform.Mac.csproj", "osx-");
        AssertRidScopedReference(references, "../Parallax.Platform.Linux/Parallax.Platform.Linux.csproj", "linux-");

        AssertDefineConstants(propertyGroups, "'$(RuntimeIdentifier)' == ''", string.Empty, "PARALLAX_MULTI_TARGET");
        AssertDefineConstants(propertyGroups, "RuntimeIdentifier", "win-", "PARALLAX_TARGET_WINDOWS");
        AssertDefineConstants(propertyGroups, "RuntimeIdentifier", "osx-", "PARALLAX_TARGET_MACOS");
        AssertDefineConstants(propertyGroups, "RuntimeIdentifier", "linux-", "PARALLAX_TARGET_LINUX");
    }

    private static void AssertRidScopedReference(
        IEnumerable<ProjectReferenceMetadata> references,
        string include,
        string ridPrefix)
    {
        var reference = Assert.Single(references, item => item.Include == include);
        Assert.Contains("'$(RuntimeIdentifier)' == ''", reference.Condition);
        Assert.Contains("RuntimeIdentifier", reference.Condition);
        Assert.Contains(ridPrefix, reference.Condition);
    }

    private static void AssertDefineConstants(
        IEnumerable<PropertyGroupMetadata> propertyGroups,
        string conditionToken,
        string ridPrefix,
        string defineConstant)
    {
        var propertyGroup = Assert.Single(
            propertyGroups,
            item => item.Condition.Contains(conditionToken, StringComparison.Ordinal)
                    && item.Condition.Contains(ridPrefix, StringComparison.Ordinal)
                    && item.DefineConstants.Contains(defineConstant, StringComparison.Ordinal));
        Assert.Contains("$(DefineConstants)", propertyGroup.DefineConstants);
    }

    private static string NormalizeProjectPath(string? path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private sealed record ProjectReferenceMetadata(string Include, string Condition);

    private sealed record PropertyGroupMetadata(string Condition, string DefineConstants);
}
