namespace AgentGitCredential.Test.Unit.Services;

using System.IO;
using AgentGitCredential.DTOs;
using AgentGitCredential.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>Tests for <see cref="GitCredentialRequestParser"/>.</summary>
[TestClass]
public sealed class GitCredentialRequestParserTests
{
    /// <summary>The three fields git sends are read; the blank line ends the request.</summary>
    [TestMethod]
    public void Parse_ProtocolHostPath_ReadsAllThree()
    {
        using (StringReader input = new StringReader("protocol=https\nhost=github.com\npath=mattonweb/City.git\n\nignored=yes\n"))
        {
            GitCredentialRequest request = GitCredentialRequestParser.Parse(input);

            Assert.AreEqual("https", request.Protocol);
            Assert.AreEqual("github.com", request.Host);
            Assert.AreEqual("mattonweb/City.git", request.Path);
        }
    }

    /// <summary>Without useHttpPath git sends no path, and the parser leaves it null.</summary>
    [TestMethod]
    public void Parse_NoPath_PathIsNull()
    {
        using (StringReader input = new StringReader("protocol=https\nhost=github.com\n\n"))
        {
            GitCredentialRequest request = GitCredentialRequestParser.Parse(input);

            Assert.IsNull(request.Path);
        }
    }

    /// <summary>A value may itself contain an equals sign.</summary>
    [TestMethod]
    public void Parse_EqualsInValue_KeepsWholeValue()
    {
        using (StringReader input = new StringReader("host=github.com\npath=a/b=c\n\n"))
        {
            GitCredentialRequest request = GitCredentialRequestParser.Parse(input);

            Assert.AreEqual("a/b=c", request.Path);
        }
    }
}
