---
name: code-csharp-standards
description: House C# standards for every repository that adopts this skill. Use whenever writing, modifying or reviewing C# code in these repos: file layout, the type taxonomy (behaviour classes versus data types) and where each lives, immutability and dependency rules, method shape (one indentation level, guard clauses, two dots per chain), exception boundaries, async, naming, the 500-line ceiling, and the language features allowed. Pair with code-csharp-mstest for tests and code-roadbed-csharp for code that consumes the Roadbed libraries. The enforceable half of these rules lives in the house .editorconfig and the analyzers; this skill states the half a reviewer has to check.
owner: Matt
version: 1.0.0
visibility: public
---

# C# Standards

These are the house rules for C# in every repository that adopts this skill. Each is written so a reviewer can
answer yes or no to it on a diff, because the pipeline's standards reviewer uses this document as its rulebook.
Where a tool can enforce a rule, the tool enforces it and this document names the tool. Where no tool can, this
document is the rule.

**Grounding.** This skill consolidates four earlier artifacts and supersedes them as the place these rules live:

- Roadbed `docs/claude-project-instructions.md`, "C# Development Standards" (2026-01-20): the `this.` rule, the
  readonly-field versus init-only split by class purpose, constructor validation, cancellation-token placement,
  disposal, base-class logging. Carried, with the drift it suffered noted where the code moved on.
- Repository Starter Kit `github/instructions/coding-style-csharp.instructions.md` (2025-09-30): no `var`,
  file-scoped namespaces, sealed by default, one type per file, `nameof` in exceptions, no abbreviations. Carried;
  its "usings outside the namespace" line is corrected here because StyleCop SA1200 rejects it.
- Repository Starter Kit `github/instructions/object-calisthenics.instructions.md`: the nine original rules. Each
  rule's house disposition is in [references/object-calisthenics.md](references/object-calisthenics.md).
- A private product repo's "build landmines" note: the analyzer rules that fail a build during ordinary coding.
  Carried as section 13; the note itself is not public.

**This skill is Matt's.** It changes only in an attended session with him. An agent that finds it wrong says so and
leaves it.

---

## 1. Toolchain facts the rules depend on

- Every project targets `net10.0` with `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`,
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- Analyzers in every project: `StyleCop.Analyzers` **1.1.118**, `SonarAnalyzer.CSharp`,
  `Microsoft.CodeAnalysis.NetAnalyzers`. StyleCop is the binding style tool because its warnings are errors. The
  `.editorconfig` IDE rules are hints only, since `EnforceCodeStyleInBuild` is not set.
- The house `.editorconfig` and `.gitattributes` have one home copy each, owned by Matt alongside this skill. Each
  product repo carries a verbatim copy at `src/.editorconfig`. A copy that differs is drift; the fix lands in the home
  copy first and is re-copied, never edited in place downstream.
- **StyleCop 1.1.118 predates the `required` keyword** and fails the build (SA1206) on `public required`. The house
  order is therefore `required public` for as long as that version is pinned. The `.editorconfig` line carries this
  in its comment and flips only in the change that upgrades the package. Never change one without the other.

---

## 2. File layout

Every `.cs` file, in this order:

1. The file-scoped namespace as the first line: `namespace Acme.Catalog.Services;`
2. The `using` directives, after the namespace line, `System` first (StyleCop SA1200 and SA1208 enforce both).
3. One type.

```csharp
namespace Acme.Catalog.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Roadbed.Common;

/// <summary>
/// Promotes catalog prices that have passed their review date.
/// </summary>
internal sealed class CatalogPricePromoter : BaseClassWithLogging
{
    // ...
}
```

- **Block-scoped namespaces are not used.** The codebase is file-scoped (2,979 files to 5 on 2026-10-04).
- **`this.` on every instance member access**: fields, properties, methods, inside getters and setters. StyleCop
  SA1101 enforces it. Not on statics, locals, parameters, or `this()` and `base()` calls.
- **`var` is never used.** Declare the type. The `.editorconfig` says so on all three `var` settings and is correct.
  The 13,600 existing declarations are refactor by-product, not precedent.
- **Classes are `sealed` unless designed for inheritance.** A base class says so in its name or its summary.
- **Naming.** Private fields `_camelCase`; parameters and locals `camelCase`; types, members and constants
  `PascalCase`; interfaces start with `I`. (The starter kit's `ALL_CAPS` for constants is not used: StyleCop
  SA1310 forbids the underscores.)
- **Modifier order:** `required` first, then access, then `static`, then the rest (section 1).
- **`#region` blocks are encouraged** for grouping members (SA1124 is disabled for this reason).
- **No file header banner** (SA1633 disabled). XML documentation on every public and internal member
  (`GenerateDocumentationFile` makes a missing summary an error).
- **One type per file** (Matt, 2026-10-04; the starter kit already said it). A row class beside its repository moves
  to its own file under `DTOs/` (section 3). (When ruled, 80 files in behaviour folders declared more than one type;
  by-product.)

---

## 3. The type taxonomy, and where each type lives

Every type is one of two kinds, and **the test is shape, not folder or name**:

> A type with no method bodies beyond `ToString`, `Equals` and `GetHashCode` is **data**.
> A type with any other method body is a **behaviour class**.

**Data types** are entities, DTOs and POCOs: records, Dapper-bound entities, request and response shapes, options
and settings types, row classes. They live:

- under `Entities/` when `public`;
- under `DTOs/` when `internal` or `private`;
- **never under `Services/`, `Repositories/` or `Orchestrators/`.** A request or result type filed beside the
  service that uses it is misfiled. (On 2026-10-04 three request and result types and 30 options types sat in behaviour folders; moving them is
  by-product.)

**Behaviour classes** are orchestrators, services, repositories, jobs, loaders, composers, handlers: anything with a
method body. Sections 4 and 6 apply to them and not to data.

---

## 4. Behaviour classes

### 4.1 No public state

An orchestrator, service or repository exposes no public getter or setter. Its dependencies are `private readonly`
fields, or `private` and `protected` get-only properties over them in the Roadbed base-class pattern. On
2026-10-04 zero of the 983 behaviour classes violated this; the rule keeps it so.

### 4.2 A state transition with a rule lives in exactly one named method

Callers never read an entity's state, decide, and assign the entity inline.

```csharp
// ❌ The rule "only a shipped order can be delivered" lives in this caller, and in every other caller.
if (order.Status == OrderStatus.Shipped)
{
    order.Status = OrderStatus.Delivered;
    order.DeliveredOn = this._timeProvider.GetUtcNow();
}

// ✅ One named method owns the transition; the invariant is checked and tested in one place.
this._orderService.MarkDelivered(order, this._timeProvider.GetUtcNow());
```

Entities stay data in this architecture (0 of 287 entity files carry a method body), so the one place is a service
method named for the transition.

### 4.3 Own state: at most two members, never a primitive; injected dependencies do not count

A behaviour class's own state, meaning anything that is not an injected dependency, is at most two members, and
none of them is a primitive (`string`, `int`, `long`, `bool`, `TimeSpan`, `Uri`, and the like). Configuration
arrives as **one options type**, placed by section 3.

```csharp
// ❌ Twenty-one primitive fields of configuration on one orchestrator.
private readonly string _outputRoot;
private readonly int _batchSize;
private readonly bool _publishSitemap;
// ...

// ✅ One options type; the orchestrator holds one member for all of it.
private readonly SiteGenerationOptions _options;
```

**There is no ceiling on injected dependencies** (Matt, 2026-10-04). Constructor injection of as many collaborators
as the class needs is the house architecture. A logger is a dependency like any other and is not counted as state.

### 4.4 No mutable instance state

Every field is `readonly`; every property is get-only or `init`. (True everywhere on 2026-10-04 except three
fields.) A class that must carry state across calls holds it in a data type it owns, and even then prefers to
return a new value.

### 4.5 Constructor validation

Every constructor parameter is validated on entry with the throw helpers, and `nameof` is used wherever a
parameter name is quoted:

```csharp
public CatalogExportProjector(ICatalogDatabaseFactory factory, ILogger<CatalogExportProjector> logger)
{
    ArgumentNullException.ThrowIfNull(factory);
    ArgumentNullException.ThrowIfNull(logger);
    this._factory = factory;
    this.Logger = logger;
}
```

`ArgumentException.ThrowIfNullOrWhiteSpace` for strings; `ObjectDisposedException.ThrowIf(this._disposed, this)`
for disposal checks (CA1513).

### 4.6 Primary constructors

**Primary constructors on records only** (Matt, 2026-10-04). A behaviour class uses an explicit constructor with
validation: a primary constructor cannot validate its parameters on entry without a workaround and does not produce
the `private readonly` field the `this._field` pattern needs. The `.editorconfig` says
`csharp_style_prefer_primary_constructors = false` for this reason. (When ruled, 30 existed across three repos, 25 on
classes; by-product.)

### 4.7 Data access goes through Roadbed, never through Dapper directly

A repository reaches the database through Roadbed's data abstractions: the `Roadbed.Crud` base repositories and the
`Roadbed.Data` executor and request types. A product repo never has `using Dapper;`. The library underneath is
Roadbed's concern, so it can change without any product repo noticing (Matt, 2026-10-04). Should the data layer ever
move to raw ADO.NET, row-to-entity mapping lives in mapper types under `Mappers/`, in the shape of Roadbed's CSV entity
mapper, never in a repository method body. (When ruled, 86 files in two repos called Dapper directly; by-product.)

---

## 5. Data types

- A DTO or entity is a `record`, or a `sealed class` where a mapper needs one, with `required` on non-nullable
  members and `init` setters (or `set` only where the mapper, Dapper for instance, requires it).
- Collections on DTOs are **arrays**, not `IList<T>`: they signal immutability, match JSON array semantics and
  perform better. (Roadbed standards, carried.)
- A data type carries **no behaviour**. If it needs a method with a body it has become a behaviour class and
  section 4 applies; usually the method belongs on a service.
- **JSON serialisation: `System.Text.Json` is the house library**, with `[JsonPropertyName]` on DTO members (Matt,
  2026-10-04: *"We are purposely moving to System.Text.Json"*). The January standards' *"Always use Newtonsoft.Json"*
  is superseded. New code never references Newtonsoft; the 21 files that still do, all in one repo, are migration debt.
- **Wrap all primitives** (Matt, 2026-10-04: *"our house rules should be to 'Wrap all primitives'"*). A domain concept
  is never a bare `long`, `string` or `double` on an entity or in a method signature: a place id is a `PlaceId`, a
  county code a `CountyFips`, a position a `Coordinate`. The wrapper is a `readonly record struct` over the primitive,
  validates in its constructor, and gives the compiler the power to refuse a county id passed where a place id was
  expected. Mapping to and from the database happens once, in the data layer's type handlers, never at read sites.
  (When ruled, 3,540 entity properties were bare primitives; this is the largest by-product on the list and lands one
  module at a time, identifiers first.)

---

## 6. Method shape

The nine Object Calisthenics rules behind this section, with each one's house disposition, are in
[references/object-calisthenics.md](references/object-calisthenics.md).

### 6.1 One level of indentation per method

A method body has one level of nesting: one `if`, one loop, one `using`. A second level is extracted to a named
private method, or removed by filtering first (`foreach` over `rows.Where(...)` instead of `foreach` then `if`).

### 6.2 Guard clauses, not `else`

Validate and return or throw early; the happy path runs at the bottom, unindented. `else` is allowed only where
both branches are a single assignment to the same variable and a conditional expression would read worse.

```csharp
// ✅
if (rows.Count == 0)
{
    return 0L;
}

return await this.InsertAsync(rows, cancellationToken).ConfigureAwait(false);
```

### 6.3 Two dots per chain

A member-access chain has at most two dots. **Exempt: LINQ chains, and `await x.Method().ConfigureAwait(false)`.**
Nothing else is exempt (Matt, 2026-10-04).

```csharp
// ❌ Reaching through a collaborator.
string type = this._factory.Connection.ConnectionStringType;
DateTime now = this._timeProvider.GetUtcNow().UtcDateTime;

// ❌ StringBuilder chain. One call per statement: scannable without context, and each line can take a comment.
builder.Append("SELECT ").Append(columns).AppendLine(" FROM place");
// ✅
builder.Append("SELECT ");
builder.Append(columns);
builder.AppendLine(" FROM place");

// ❌ Three dots.
string status = entity.Status.ToString().ToLowerInvariant();
// ✅
string status = entity.Status.ToString();
status = status.ToLowerInvariant();

// ❌ A fully qualified name reads as a chain. Add the using directive instead.
byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
// ✅ with `using System.Text;`
byte[] bytes = Encoding.UTF8.GetBytes(value);

// ❌ Walking a framework object's data model.
string name = context.JobDetail.Key.Name;
// ✅ Take a local; the chain below it is two dots.
IJobDetail jobDetail = context.JobDetail;
string name = jobDetail.Key.Name;

// ✅ Exempt: LINQ, and ConfigureAwait.
List<string> names = rows.Where(r => r.IsActive).Select(r => r.Name).ToList();
Place place = await this._repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
```

Measured 2026-10-04: 232 lines in 134 files to bring under the rule, none of them LINQ or `ConfigureAwait`.

### 6.4 First-class collections

A class that holds a collection holds nothing else but that collection and the behaviour over it. A behaviour
class does not expose a raw `List<T>` of domain items; it holds a type that wraps the collection and answers the
questions callers ask of it.

### 6.5 No abbreviations

Names are whole words: `repository` not `repo`, `configuration` not `cfg`, `cancellationToken` not `ct`.
Acronyms that are words in the domain (`Id`, `Url`, `Csv`, `Fips`, `Ats`, `Mcp`) are fine and are cased as words:
`Id`, not `ID`.

### 6.6 Async

- `CancellationToken cancellationToken = default` is **always the last parameter**, after required and optional
  parameters (Roadbed standards, carried; matches the base class library).
- Every `await` carries `.ConfigureAwait(false)` (CA2007).
- **Never `.Result` or `.Wait()`** on a task in production code (8 files on 2026-10-04, jobs and scheduling;
  by-product).
- **Never `async void`** (0 today).
- A method that awaits nothing returns the task directly rather than being marked `async`.
- `HttpRequestMessage` can be sent once; create a new instance per retry attempt (Roadbed standards, carried).

---

## 7. Exceptions

**Catch `Exception` only at a boundary**: a scheduled job's `ExecuteAsync`, an endpoint handler, or a loader's
per-record loop where one bad record must not stop the batch. Every such catch carries the CA1031 pragma and a
one-line reason naming the boundary:

```csharp
#pragma warning disable CA1031 // Boundary: one malformed row must not abort the nightly load; logged and counted.
catch (Exception ex)
{
    this.LogError(ex, "Row {RowNumber} skipped", rowNumber);
    skipped++;
}
#pragma warning restore CA1031
```

Anywhere else, catch the specific type or do not catch. Rethrow with `throw;`, never `throw ex;`. (157 existing
CA1031 pragmas carry no reason; adding one is by-product.)

---

## 8. Size: the 500-line ceiling

A production `.cs` file is at most **500 lines**, counting documentation and blank lines, excluding generated files,
designer files and migrations. A file over the ceiling is split by responsibility, usually along the private methods
section 6.1 produced.

**Named exceptions live in that repository's `CLAUDE.md`, never in this skill.** This skill is public and shared
across repos; the list is repo-specific. Each entry names the file, its line count when listed, and why it stays. (On 2026-10-04, 92 files
exceeded 500 and 16 exceeded 1,000; the largest was 2,476.)

---

## 9. Disposal

A class that owns a managed resource (a `SemaphoreSlim`, for instance) is `sealed`, keeps a `_disposed` field,
implements `Dispose()` idempotently, calls `ObjectDisposedException.ThrowIf(this._disposed, this)` at the top of
every public member, and never adds a finalizer without an unmanaged resource. Consumers use `using`. (Roadbed
standards, carried.)

---

## 10. Logging

A class that inherits `BaseClassWithLogging` (every `BaseSchedulingJob<T>` does) logs through the base methods,
`this.LogDebug(...)`, `this.LogInformation(...)`, `this.LogError(ex, ...)`, which check the level before formatting.
`this.Logger.LogDebug(...)` on such a class formats the message even when debug is off. A class with an injected
`ILogger<T>` and no base class calls the logger directly. (Roadbed standards, carried.)

---

## 11. Time

Behaviour classes read the clock through an injected `TimeProvider` (Matt, 2026-10-04). `DateTime.Now` and
`DateTimeOffset.Now` are never used. `DateTime.UtcNow` and `DateTimeOffset.UtcNow` are allowed only in data types
and static helpers that cannot take a dependency. Tests supply a `FakeTimeProvider`. (When ruled, 38 files already
injected `TimeProvider`, 7 sites used `.Now`, and 315 read `UtcNow` directly, mostly in behaviour classes; by-product.)

---

## 12. Language features

Allowed and preferred: records, `init`, `required`, pattern matching (`is null`, `is not null`, property
patterns), switch expressions, collection expressions, target-typed `new()` when the type is on the left,
`nameof`, string interpolation, tuples for private return values, local functions, `static` lambdas,
expression-bodied members where the body is one expression.

Not used:

- `var` (section 2).
- `dynamic` and `unsafe`, except inside a named interop boundary with a comment saying why (8 files today).
- `goto` (0 today).
- LINQ query syntax (`from x in ...`); method syntax only (Matt, 2026-10-04; 6 sites when ruled).
- Primary constructors on classes (section 4.6).
- Newtonsoft.Json in new code (section 5).
- `DateTime.Now` and `DateTimeOffset.Now` (section 11).
- `async void` (section 6.6).
- `#pragma warning disable` without a reason on the same line.
- `TODO`, `FIXME`, `XXX`, `HACK` in comments: Sonar S1135 fails the build. Write "Pending:" or "Future work:" prose.
- Commented-out code. Delete it; git remembers.

---

## 13. Analyzer landmines

Rules that fail the build during ordinary coding, with the fix (carried from a private landmines note):

| Rule | What trips it | Fix |
|---|---|---|
| SA1206 | `public required` under StyleCop 1.1.118 | `required public` until the package is upgraded (section 1) |
| SA1642 | Constructor summary not the standard sentence | `Initializes a new instance of the <see cref="X"/> class.` (American spelling), then more |
| CS1574 | `<see cref>` to a type not directly referenced | `<c>Namespace.Type</c>` instead |
| SA1107 | Two statements on one line | One per line |
| SA1117 | Call arguments split some-on-this-line, some-on-next | All on one line, or one per line |
| SA1501 | Single-line block `if (x) { return; }` | Braces on their own lines |
| SA1513 | Closing brace followed directly by a statement | Blank line after `}` |
| S2325 | Instance member reads no instance state | `virtual` when it is an override target; `static` otherwise |
| S1135 | `TODO`, `FIXME`, `XXX`, `HACK` in a comment | Prose without the token |
| CA1031 | `catch (Exception)` | Boundary pragma with reason (section 7), or catch the specific type |
| CA2007 | `await` without `ConfigureAwait` | `.ConfigureAwait(false)` |
| CA1513 | `if (disposed) throw new ObjectDisposedException(...)` | `ObjectDisposedException.ThrowIf(this._disposed, this)` |

Rules deliberately disabled in the `.editorconfig`, with reasons: SA1010 and SA1011 (StyleCop 1.1.118 false
positives on collection expressions and nullable arrays), SA1124 (regions encouraged), SA1309 (`_camelCase`
fields), SA1633 (no header), SA1201 (member order, pending StyleCop support for .NET 10), IDE0063, IDE0090,
IDE0130, S1066, S1075, S3267. Disabling another rule is a decision made in the home `.editorconfig` by its owner, never a reflex
to a build error in one repo.

---

## 14. Things to NEVER do

1. **NEVER** use `var`.
2. **NEVER** omit `this.` on an instance member.
3. **NEVER** write `public required`; it is `required public` under StyleCop 1.1.118.
4. **NEVER** put a public setter, or any public getter, on an orchestrator, service or repository.
5. **NEVER** branch on an entity's state and assign the entity inline in a caller; name the transition.
6. **NEVER** hold a primitive as a behaviour class's own state; use one options type.
7. **NEVER** declare a non-readonly instance field on a behaviour class.
8. **NEVER** chain more than two dots outside LINQ and `ConfigureAwait`; StringBuilder goes one call per statement.
9. **NEVER** nest more than one level inside a method body.
10. **NEVER** catch `Exception` outside a job, endpoint or loader boundary, and never without the pragma and a reason.
11. **NEVER** block on a task with `.Result` or `.Wait()`.
12. **NEVER** place a `CancellationToken` anywhere but last.
13. **NEVER** file a data type under `Services/`, `Repositories/` or `Orchestrators/`.
14. **NEVER** put a method body on an entity, DTO or POCO.
15. **NEVER** abbreviate an identifier.
16. **NEVER** exceed 500 lines in a production file without an entry in that repo's `CLAUDE.md`.
17. **NEVER** write `TODO`, `FIXME`, `XXX` or `HACK` in a comment.
18. **NEVER** disable an analyzer rule in a product repo's `.editorconfig`; the change is made in the home copy or not at all.
19. **NEVER** read the clock with `DateTime.Now` or `DateTimeOffset.Now`; inject `TimeProvider`.
20. **NEVER** write LINQ query syntax; method syntax only.
21. **NEVER** put a primary constructor on a class; records only.
22. **NEVER** reference Newtonsoft.Json in new code; `System.Text.Json`.
23. **NEVER** give a domain concept a bare primitive type on an entity or in a signature; wrap it.
24. **NEVER** `using Dapper;` in a product repo; reach the database through Roadbed's data abstractions.
25. **NEVER** edit this skill outside an attended session with Matt, who owns it.

---

## 15. Review checklist

Answer every line yes before a change is ready. Each is observable on the diff.

- [ ] File starts with a file-scoped namespace; usings follow it, `System` first; one type in the file
- [ ] No `var`
- [ ] `this.` on every instance member access
- [ ] `required` precedes the access modifier
- [ ] New classes are `sealed` unless designed for inheritance
- [ ] Every new type is classified, data or behaviour, by the shape test, and filed accordingly
- [ ] No data type under `Services/`, `Repositories/` or `Orchestrators/`
- [ ] Behaviour class exposes no public getter or setter
- [ ] Behaviour class's own state is at most two non-primitive members; configuration is one options type
- [ ] Every instance field is `readonly`; properties are get-only or `init`
- [ ] Every constructor parameter is validated with a throw helper; `nameof` wherever a parameter is named
- [ ] No method body nests more than one level
- [ ] Guard clauses; no `else` except the single-assignment case
- [ ] No chain over two dots outside LINQ and `ConfigureAwait`; StringBuilder calls are one per statement
- [ ] Fully qualified type names replaced by `using` directives
- [ ] No abbreviations in identifiers
- [ ] `CancellationToken` is last; every `await` has `ConfigureAwait(false)`; no `.Result`, `.Wait()`, `async void`
- [ ] `catch (Exception)` only at a named boundary, with the CA1031 pragma and a one-line reason
- [ ] Rethrows are `throw;`
- [ ] Production file is 500 lines or fewer, or listed in the repo's `CLAUDE.md` with a reason
- [ ] Disposable class is `sealed`, idempotent, and guards its public members
- [ ] Base-class logging methods used where the base class provides them
- [ ] Clock read through an injected `TimeProvider`; no `.Now`
- [ ] No primary constructor on a class
- [ ] JSON through `System.Text.Json`; LINQ in method syntax
- [ ] No bare primitive for a domain concept on an entity or in a signature; identifiers, codes and coordinates are wrapped
- [ ] No `using Dapper;`; the database is reached through Roadbed's data abstractions
- [ ] No `TODO`, `FIXME`, `XXX`, `HACK`; no commented-out code; no new suppression without a reason

---

## Decisions record

Every item in this skill was ruled by Matt in an attended session on 2026-10-04, including the six that were drafted as
open questions: one type per file (2), primary constructors on records only (4.6), `System.Text.Json` (5), wrap all
primitives (5), injected `TimeProvider` (11), LINQ method syntax only (12). Nothing in this skill is marked as pending.
