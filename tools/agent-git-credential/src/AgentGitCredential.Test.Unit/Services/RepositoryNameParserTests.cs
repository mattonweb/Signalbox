namespace AgentGitCredential.Test.Unit.Services;

using System;
using AgentGitCredential.Entities;
using AgentGitCredential.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="RepositoryNameParser"/>.</summary>
[TestClass]
public sealed class RepositoryNameParserTests
{
    /// <summary>A plain slug parses into owner and name.</summary>
    [TestMethod]
    public void ParseSlug_OwnerAndName_ReturnsBoth()
    {
        RepositoryName name = RepositoryNameParser.ParseSlug("mattonweb/Roadbed");

        Assert.AreEqual("mattonweb", name.Owner);
        Assert.AreEqual("Roadbed", name.Name);
    }

    /// <summary>A slug with a .git suffix loses the suffix.</summary>
    [TestMethod]
    public void ParseSlug_GitSuffix_StripsSuffix()
    {
        RepositoryName name = RepositoryNameParser.ParseSlug("mattonweb/Roadbed.Net.Mcp.git");

        Assert.AreEqual("Roadbed.Net.Mcp", name.Name);
    }

    /// <summary>A slug without a slash is refused.</summary>
    [TestMethod]
    public void ParseSlug_NoSlash_Throws()
    {
        Assert.ThrowsExactly<FormatException>(() => RepositoryNameParser.ParseSlug("Roadbed"));
    }

    /// <summary>The credential path git sends is owner/name.git.</summary>
    [TestMethod]
    public void TryParseCredentialPath_GitPath_ReturnsName()
    {
        RepositoryName? name = RepositoryNameParser.TryParseCredentialPath("mattonweb/City.git");

        Assert.IsNotNull(name);
        Assert.AreEqual("City", name.Value.Name);
    }

    /// <summary>An absent path yields no name rather than an error.</summary>
    [TestMethod]
    public void TryParseCredentialPath_Null_ReturnsNull()
    {
        Assert.IsNull(RepositoryNameParser.TryParseCredentialPath(null));
    }

    /// <summary>A path that is not owner/name yields no name rather than an error.</summary>
    [TestMethod]
    public void TryParseCredentialPath_SingleSegment_ReturnsNull()
    {
        Assert.IsNull(RepositoryNameParser.TryParseCredentialPath("gist"));
    }

    /// <summary>An https remote address parses.</summary>
    [TestMethod]
    public void ParseRemoteUrl_Https_ReturnsName()
    {
        RepositoryName name = RepositoryNameParser.ParseRemoteUrl("https://github.com/mattonweb/Signalbox.git");

        Assert.AreEqual("mattonweb/Signalbox", name.ToString());
    }

    /// <summary>An ssh remote address parses.</summary>
    [TestMethod]
    public void ParseRemoteUrl_Ssh_ReturnsName()
    {
        RepositoryName name = RepositoryNameParser.ParseRemoteUrl("git@github.com:mattonweb/Signalbox.git");

        Assert.AreEqual("mattonweb/Signalbox", name.ToString());
    }
}
