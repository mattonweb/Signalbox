---
name: dotnet-solution-architecture
description: House solution architecture for .NET repositories that adopt this skill. Use whenever creating a project, adding a project reference, deciding which project a class belongs in, choosing what a project makes public, or reviewing any of those. Covers the one-solution-per-repository rule, the four project kinds (Application, Library, Adapter, Orchestrator) and their dependency rules, project naming, the public surface of a class library (Configuration, Contracts, dependency-injection registration, nothing else), the folder layout inside a project, the optional test projects, and the adoption state each repository declares. Pair with code-software-standards and code-csharp-standards for the code inside the projects.
owner: Matt
version: 1.0.0
visibility: public
---

# .NET solution architecture

A multi-application solution built from three kinds of class library (Libraries, Adapters and Orchestrators) and one
kind of host (Applications), each with explicit boundaries and dependency rules, influenced by Hexagonal Architecture
(Ports and Adapters). Every capability has exactly one owning project. Consumers use that capability only through its
public contracts and never depend on its implementation.

1. Each capability has one owning project. A capability is one thing the solution can do that is named from outside
   the project: a data source, an external system, a workflow, a cross-cutting service.
2. Adapter contracts are shared across applications.
3. Only Configuration, Contracts and dependency-injection registration are public.
4. Repositories are internal to their Adapter.
5. Adapters never depend on other Adapters.
6. Orchestrators own workflows and entry points (jobs, scheduled tasks).
7. Libraries contain only cross-cutting concerns.
8. Applications supply configuration, call each project's registration, and contain no application logic.

> Libraries support. Adapters integrate. Orchestrators coordinate. Applications compose.
>
> Repositories are never public contracts. A Repository exists to support an Adapter's implementation and may only
> be consumed from within that Adapter. Cross-Adapter workflows are always coordinated by an Orchestrator.

**Every kind except Application is optional.** A solution has Adapters, Orchestrators and Libraries only when it has
the thing each one holds. A small application with one data source is one Application and one Adapter. An
Orchestrator exists only when there is a workflow that spans capabilities or an entry point such as a scheduled job;
a Library exists only when a cross-cutting concern is shared by more than one project. Never create an empty project
to complete the picture.

## 1. Source control

Each git repository holds exactly one Visual Studio solution (`.slnx`).

> A solution is a **development-time organizational boundary**, not a runtime or deployment boundary. A
> multi-application solution, several Applications sharing Adapters and Libraries, is supported and encouraged.

This skill governs the projects within that solution. Libraries from outside the solution (NuGet packages, shared
assemblies) play the Library role from outside; a solution-local Library wraps only what they do not cover.

## 2. Project naming

**Pattern**

```
{{solution}}.{{kind}}.{{name}}
```

- `{{solution}}` is the common prefix shared by every project in the solution.
- `{{kind}}` is one of `Application`, `Library`, `Adapter`, `Orchestrator` or `Test`.
- `{{name}}` identifies the host kind, the domain, the capability, the external system or the responsibility.

| Kind | Examples |
|---|---|
| Application | `Bar.Application.Web`, `Bar.Application.Api`, `Bar.Application.Service`, `Bar.Application.Console` |
| Library | `Bar.Library.Data.MySql`, `Bar.Library.Email`, `Bar.Library.Observability` |
| Adapter | `Bar.Adapter.Orders`, `Bar.Adapter.Splunk`, `Bar.Adapter.Dynatrace` |
| Orchestrator | `Bar.Orchestrator.MoveData`, `Bar.Orchestrator.LogAnalysis`, `Bar.Orchestrator.SummaryReport` |
| Test | `Bar.Test.Unit`, `Bar.Test.Integration` |

## 3. Project kinds

### Application

The host and the composition root. It:

- Loads configuration and resolves secrets (for example from a KeePass database).
- Populates each project's options object and calls its `ServiceCollectionExtensions` registration.
- Configures schedulers (for example Quartz.NET) and registers Orchestrator jobs.
- Holds the presentation layer: Razor Pages and their page models, controllers, views, static assets, console
  command parsing. Presentation is how a user reaches the application; it is not application logic.

The Application contains no application logic. Workflows and entry points live in Orchestrators. Capabilities live in
Adapters and Libraries. A page model or controller action calls a contract and renders the result; when it starts
doing more than that, the logic belongs in an Adapter or an Orchestrator.

### Library

Provides reusable, application-wide capabilities and cross-cutting concerns: email and SMTP utilities, common
database primitives, logging abstractions, common configuration, shared exceptions. A Library knows nothing about
the solution's domain.

### Adapter

Wraps a single capability, such as an external system or a data source, behind a stable contract. Owns
communication, translation, validation, and integration-specific error handling. Contains no workflow, scheduling or
entry-point logic.

> An Adapter may depend on Libraries but never on Orchestrators or other Adapters.

### Orchestrator

Coordinates application workflows involving one or more capabilities. Owns application workflows and their entry
points, such as Quartz.NET jobs and scheduled tasks. May invoke one or more Adapters.

> An Orchestrator interacts with an Adapter exclusively through the Adapter's public contracts. Repositories remain
> internal to the Adapter and cannot be reached by Orchestrators or other Adapters. This lets the Adapter own data
> access, validation, mapping, logging and integration-specific error handling before exposing data through its
> contract.

### Test (optional)

At most one `{{solution}}.Test.Unit` and at most one `{{solution}}.Test.Integration` per solution, each present only
when the solution has tests of that kind. Neither is required. A test project may reference any project in the
solution. How tests are written is the `code-csharp-mstest` skill's; where they live is this one's.

## 4. Dependency rules

Dependencies flow toward reusable capabilities and integration boundaries, never sideways between peers.

**Allowed**

```
Application
   ├──> Orchestrator
   ├──> Adapter
   └──> Library

Orchestrator
   ├──> Adapter
   └──> Library

Adapter
   └──> Library

Library
   └──> Library

Test
   └──> any project in the solution
```

**Not allowed**

```
Adapter      ─X─> Adapter
Adapter      ─X─> Orchestrator
Library      ─X─> Adapter
Library      ─X─> Orchestrator
Orchestrator ─X─> Orchestrator
anything     ─X─> Application
anything     ─X─> Test
```

Two Libraries may not reference each other in a cycle.

## 5. The public surface of a class library

The public surface of a Library, Adapter or Orchestrator consists only of the configuration required to register it,
the contracts required to consume it, and the composition hook that makes it available through dependency
injection. Everything else is `internal`.

> Configuration exposes how to configure it. Contracts expose how to use it. Dependency injection exposes how to
> register it. Everything else stays internal.

An Application knows exactly three things about an Adapter:

- `Configuration/{{Name}}Options`
- `Contracts/*`
- `ServiceCollectionExtensions`

**Registration from the composition root**

```csharp
var options = new OrdersOptions
{
    ConnectionString = secrets.Get("Orders:ConnectionString"),
    ApiKey = secrets.Get("Orders:ApiKey"),
};

services.AddOrdersAdapter(options);
```

The host constructs the options object and passes it in. The project does not read configuration itself.

**Tests and `internal`.** A test project reaches a project's internal types through `InternalsVisibleTo` naming the
test project, declared in the project under test. Prefer testing through the contracts; use the internal route for
the pieces the contracts cannot reach, and never widen a type to `public` to make it testable.

## 6. Folder layout inside a class library

```
{{solution}}.{{kind}}.{{name}}/
│
├── Assets/                          internal: embedded resources the project needs at runtime
│   ├── Tables/
│   │   └── foo/
│   │       ├── install
│   │       ├── create
│   │       ├── read
│   │       ├── list
│   │       ├── update
│   │       └── delete
│   └── StoredProcedures/
│       ├── foo-select-by-category
│       └── foo-select-by-tag
│
├── Configuration/                   public surface
│   └── {{Name}}Options              the options object the host populates
│
├── Contracts/                       public surface
│   ├── FooCollectionResponse        data type
│   ├── FooDeleteResponse            data type
│   ├── FooSingleResponse            data type
│   ├── FooRequest                   data type
│   └── IFooService                  the interface registered in dependency injection
│
├── Entities/                        internal
│   └── Foo
│
├── Services/                        internal
│   └── FooService
│
├── Repositories/                    internal
│   └── FooRepository
│
└── ServiceCollectionExtensions      public dependency-injection registration
```

- `Assets/` exists only in a project that embeds resources. The files under `Assets/Tables/<table>/` are the
  per-operation SQL for one table and the file names are the operation names; **their extension and their content
  are governed by the repository's SQL skill**, not by this one. A project with no database has no `Assets/Tables/`.
- `Configuration/` holds the options the consuming Application must supply to register the project. It exposes what
  the project needs without exposing how the project works.
- `Contracts/` holds the interfaces registered in dependency injection and the data types that cross the boundary
  in either direction. Under `code-software-standards` these are data types: no behavior, primitives wrapped.
- `Entities/` holds the project's own model of the thing it wraps, never seen outside the project. A contract type
  and an entity may look alike; they stay separate so the contract can hold still while the entity changes.
- A folder that would be empty is not created.

## 7. Adoption state: every adopting repository declares one

A repository that holds this skill states, in its `CLAUDE.md`, which of three states it is in. The state tells an
agent what to do with code that does not yet conform, which the skill alone cannot say.

| State | Meaning | What an agent does |
|---|---|---|
| `target` | The standard is the goal. Nothing existing has been moved. | New projects follow sections 2 to 6. Existing projects are left exactly as they are; no renames, no moves, no reference changes, however wrong they look against this skill. |
| `migrating` | Some projects have been moved; the `CLAUDE.md` names which. | New projects conform. Named projects conform. Unnamed projects are left as they are until a change explicitly moves them, and the move adds them to the list. |
| `conforming` | Every project conforms. | A change that would break a rule in sections 4 or 5 is declined and the reason given. |

A repository with no state line is treated as `target`. **A migration is always its own change**, never folded into
a feature: a rename or a move of a project is a commit that does nothing else.

**Names used before this standard.** Older solutions use `Module` for what this skill calls an Adapter, and `Jobs`
or `Loader` for what it calls an Orchestrator. `Library` is unchanged. A `Module` that holds a workflow or a job
splits into an Adapter and an Orchestrator when it migrates; a `Module` that only wraps a capability becomes an
Adapter by rename.

## 8. Review checks

Ask these of any change that creates a project, adds a project reference or adds a public type:

- Which single project owns this capability, and is this change in it?
- Does the new reference point in an allowed direction (section 4)? A sideways reference between two Adapters or
  two Orchestrators is always wrong; the fix is an Orchestrator above them or a Library below them, never the
  reference.
- Is the new public type in `Configuration/` or `Contracts/`, or is it `ServiceCollectionExtensions`? Anything else
  public is a defect, including a Repository, an Entity and a Service.
- Does the Application do anything a page model or a controller should not: a loop over a repository, a decision
  about data, a call to two Adapters in sequence? That is an Orchestrator waiting to be written.
- Does the repository's adoption state permit touching the project this change touches?

The dependency and public-surface rules are mechanically checkable: a test that reads each project's
`ProjectReference` items and its public types, and fails on a disallowed direction or a public type outside the
three allowed places. A repository that adds such a test lists it beside its adoption state so a reviewer knows the
check runs.
