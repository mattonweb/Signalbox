namespace AgentGitCredential.Services;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AgentGitCredential.Entities;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;

/// <summary>
/// The KeePass database of app registrations: one entry per repository, titled <c>github.com/owner/name</c>,
/// with the app identifier, the installation identifier and the private key as entry strings. The file is
/// opened for each operation and closed again, so two processes never hold it at once for long.
/// </summary>
public sealed class RegistrationStore
{
    private const string AppIdField = "AppId";
    private const string InstallationIdField = "InstallationId";
    private const string PrivateKeyField = "PrivateKeyPem";
    private const string TitlePrefix = "github.com/";

    private readonly StoreLocation _location;
    private readonly IDatabaseKeySource _keySource;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistrationStore"/> class.
    /// </summary>
    /// <param name="location">Where the database lives.</param>
    /// <param name="keySource">What opens it.</param>
    public RegistrationStore(StoreLocation location, IDatabaseKeySource keySource)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(keySource);
        this._location = location;
        this._keySource = keySource;
    }

    /// <summary>
    /// Creates an empty database at the configured path. Refuses to overwrite one that exists.
    /// </summary>
    public void Create()
    {
        if (File.Exists(this._location.DatabasePath))
        {
            throw new InvalidOperationException("A database already exists at " + this._location.DatabasePath + ".");
        }

        string? directory = Path.GetDirectoryName(this._location.DatabasePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        PwDatabase database = new PwDatabase();
        database.New(this.ConnectionInfo(), this._keySource.CreateKey());
        database.Save(null);
        database.Close();
    }

    /// <summary>
    /// Finds the registration for a repository.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <returns>The registration, or <see langword="null"/> when none is stored.</returns>
    public AppRegistration? Find(RepositoryName repository)
    {
        PwDatabase database = this.Open();
        try
        {
            PwEntry? entry = FindEntry(database, repository);
            return entry is null ? null : ReadRegistration(entry, repository);
        }
        finally
        {
            database.Close();
        }
    }

    /// <summary>
    /// Adds or replaces the registration for a repository.
    /// </summary>
    /// <param name="registration">The registration.</param>
    public void Save(AppRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        PwDatabase database = this.Open();
        try
        {
            PwEntry entry = FindEntry(database, registration.Repository) ?? AddEntry(database, registration.Repository);
            WriteRegistration(entry, registration);
            database.Save(null);
        }
        finally
        {
            database.Close();
        }
    }

    /// <summary>
    /// Lists the repositories that have a registration.
    /// </summary>
    /// <returns>The repository names, in database order.</returns>
    public IReadOnlyList<RepositoryName> List()
    {
        PwDatabase database = this.Open();
        try
        {
            List<RepositoryName> names = new List<RepositoryName>();
            foreach (PwEntry entry in database.RootGroup.Entries)
            {
                AddName(names, entry);
            }

            return names;
        }
        finally
        {
            database.Close();
        }
    }

    private static string TitleFor(RepositoryName repository)
    {
        return TitlePrefix + repository.ToString();
    }

    private static PwEntry? FindEntry(PwDatabase database, RepositoryName repository)
    {
        string title = TitleFor(repository);
        foreach (PwEntry entry in database.RootGroup.Entries)
        {
            if (string.Equals(entry.Strings.ReadSafe(PwDefs.TitleField), title, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }

    private static PwEntry AddEntry(PwDatabase database, RepositoryName repository)
    {
        PwEntry entry = new PwEntry(true, true);
        entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, TitleFor(repository)));
        database.RootGroup.AddEntry(entry, true);
        return entry;
    }

    private static void WriteRegistration(PwEntry entry, AppRegistration registration)
    {
        entry.Strings.Set(AppIdField, new ProtectedString(false, registration.AppId.ToString()));
        entry.Strings.Set(InstallationIdField, new ProtectedString(false, registration.InstallationId.ToString()));
        entry.Strings.Set(PrivateKeyField, new ProtectedString(true, registration.PrivateKey.Value));
        entry.Touch(true);
    }

    private static AppRegistration ReadRegistration(PwEntry entry, RepositoryName repository)
    {
        long appId = long.Parse(entry.Strings.ReadSafe(AppIdField), CultureInfo.InvariantCulture);
        long installationId = long.Parse(entry.Strings.ReadSafe(InstallationIdField), CultureInfo.InvariantCulture);
        return new AppRegistration
        {
            Repository = repository,
            AppId = new AppId(appId),
            InstallationId = new InstallationId(installationId),
            PrivateKey = new PrivateKeyPem(entry.Strings.ReadSafe(PrivateKeyField)),
        };
    }

    private static void AddName(List<RepositoryName> names, PwEntry entry)
    {
        string title = entry.Strings.ReadSafe(PwDefs.TitleField);
        if (!title.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        names.Add(RepositoryNameParser.ParseSlug(title.Substring(TitlePrefix.Length)));
    }

    private IOConnectionInfo ConnectionInfo()
    {
        return IOConnectionInfo.FromPath(this._location.DatabasePath);
    }

    private PwDatabase Open()
    {
        if (!File.Exists(this._location.DatabasePath))
        {
            throw new InvalidOperationException("No registration database at " + this._location.DatabasePath + "; run 'init' first.");
        }

        PwDatabase database = new PwDatabase();
        CompositeKey key = this._keySource.CreateKey();
        database.Open(this.ConnectionInfo(), key, null);
        return database;
    }
}
