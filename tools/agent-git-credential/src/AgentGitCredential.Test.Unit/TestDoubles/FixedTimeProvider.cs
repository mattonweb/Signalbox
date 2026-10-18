namespace AgentGitCredential.Test.Unit.TestDoubles;

using System;

/// <summary>
/// A clock that always answers with the instant it was given.
/// </summary>
public sealed class FixedTimeProvider
    : TimeProvider
{
    private readonly DateTimeOffset _now;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedTimeProvider"/> class.
    /// </summary>
    /// <param name="now">The instant to report.</param>
    public FixedTimeProvider(DateTimeOffset now)
    {
        this._now = now;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        return this._now;
    }
}
