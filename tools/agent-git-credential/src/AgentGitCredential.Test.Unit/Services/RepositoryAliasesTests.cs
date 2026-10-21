namespace AgentGitCredential.Test.Unit.Services;

using System;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="RepositoryAliases"/>.</summary>
[TestClass]
public sealed class RepositoryAliasesTests
{
    private static readonly RepositoryName Plans = new RepositoryName("mattonweb", "agent-technology-build-plans");
    private static readonly RepositoryName PlansAsCdo = new RepositoryName("mattonweb", "agent-technology-build-plans#cdo");

    /// <summary>An unset variable is the empty map, under which a repository resolves to itself.</summary>
    [TestMethod]
    public void Parse_Null_ResolvesToSelf()
    {
        RepositoryAliases aliases = RepositoryAliases.Parse(null);

        Assert.AreEqual(Plans, aliases.Resolve(Plans));
    }

    /// <summary>A blank variable is the empty map too.</summary>
    [TestMethod]
    public void Parse_Blank_ResolvesToSelf()
    {
        RepositoryAliases aliases = RepositoryAliases.Parse("   ");

        Assert.AreEqual(Plans, aliases.Resolve(Plans));
    }

    /// <summary>One alias resolves its repository to the suffixed title.</summary>
    [TestMethod]
    public void Parse_OneAlias_ResolvesToTarget()
    {
        RepositoryAliases aliases = RepositoryAliases.Parse("mattonweb/agent-technology-build-plans=mattonweb/agent-technology-build-plans#cdo");

        Assert.AreEqual(PlansAsCdo, aliases.Resolve(Plans));
    }

    /// <summary>A repository that is not in the map still resolves to itself.</summary>
    [TestMethod]
    public void Parse_OneAlias_LeavesOtherRepositoriesAlone()
    {
        RepositoryAliases aliases = RepositoryAliases.Parse("mattonweb/agent-technology-build-plans=mattonweb/agent-technology-build-plans#cdo");
        RepositoryName city = new RepositoryName("mattonweb", "City");

        Assert.AreEqual(city, aliases.Resolve(city));
    }

    /// <summary>Several aliases are separated by semicolons, with spaces tolerated around each.</summary>
    [TestMethod]
    public void Parse_SeveralAliases_ResolvesEach()
    {
        RepositoryAliases aliases = RepositoryAliases.Parse(" mattonweb/A=mattonweb/A#x ; mattonweb/B=mattonweb/B#y; ");

        Assert.AreEqual("A#x", aliases.Resolve(new RepositoryName("mattonweb", "A")).Name);
        Assert.AreEqual("B#y", aliases.Resolve(new RepositoryName("mattonweb", "B")).Name);
    }

    /// <summary>A pair without an equals sign is refused.</summary>
    [TestMethod]
    public void Parse_NoEquals_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => RepositoryAliases.Parse("mattonweb/A"));
    }

    /// <summary>A side that is not owner/name is refused.</summary>
    [TestMethod]
    public void Parse_TargetNotASlug_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => RepositoryAliases.Parse("mattonweb/A=cdo"));
    }

    /// <summary>The same repository aliased twice is refused rather than silently last-wins.</summary>
    [TestMethod]
    public void Parse_RepositoryAliasedTwice_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => RepositoryAliases.Parse("mattonweb/A=mattonweb/A#x;mattonweb/A=mattonweb/A#y"));
    }
}
