namespace AgentGitCredential.Test.Unit.Services;

using System;
using AgentGitCredential.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="CommandArguments"/>.</summary>
[TestClass]
public sealed class CommandArgumentsTests
{
    /// <summary>Pairs after the verb are read by name, case-insensitively.</summary>
    [TestMethod]
    public void Parse_PairsAfterVerb_RequireFindsThem()
    {
        CommandArguments arguments = CommandArguments.Parse(["set", "--repository", "a/b", "--App-Id", "7"]);

        Assert.AreEqual("a/b", arguments.Require("repository"));
        Assert.AreEqual("7", arguments.Require("app-id"));
    }

    /// <summary>A missing option is named in the error.</summary>
    [TestMethod]
    public void Require_Missing_ThrowsNamingOption()
    {
        CommandArguments arguments = CommandArguments.Parse(["set"]);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => arguments.Require("repository"));

        Assert.IsTrue(exception.Message.Contains("--repository", StringComparison.Ordinal));
    }

    /// <summary>A bare value where an option was expected is refused.</summary>
    [TestMethod]
    public void Parse_ValueWithoutOption_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CommandArguments.Parse(["set", "repository", "a/b"]));
    }
}
