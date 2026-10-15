# C# language features by version: what is allowed

**Baseline: C# 7.3.** Everything the language offered through 7.3 (May 2018) is the baseline and is allowed unless
`SKILL.md` restricts it by name. (There is no C# 7.4; the 7.x line ended at 7.3 and C# 8.0 followed in September
2019.) Every version after 7.3 is listed below, feature by feature, each with a one-line description, how much the
house code already uses it, and its disposition.

**Why this document exists.** The projects target `net10.0` and pin `<LangVersion>14</LangVersion>`, so the compiler
accepts exactly the versions on this page and refuses anything newer. Accepting is not adopting. A coding agent reads this page to learn which
features the house writes and which it does not, and a reviewer reads it to check a diff.

**Status: every row is ruled.** Matt walked the list one feature at a time on 2026-10-07 and 2026-10-15. Counting the
baseline-restrictions table: 30 Allowed, 8 Restricted, 45 Not allowed, 0 Pending. The pattern behind the rulings, stated
so the next feature can be judged the same way: **shorthand that hides a branch, an arithmetic step or a declaration is
declined** (using declarations, ranges, `??=`, null-conditional assignment, `field`); **features that make structure
visible are kept** (switch expressions with every arm broken after the arrow, property patterns, records, `with`); **library
and runtime internals are declined** (`ref` fields, inline arrays, function pointers, interceptors); **source-generator
hooks are Restricted** to a named generator; and **syntax the pinned StyleCop 1.1.118 has never seen waits** for the
analyzer upgrade (extension members, `field`).

**Dispositions.** `Allowed`, `Restricted`, `Not allowed`, or `Pending`.

- `Allowed`: use it where it fits; the row may carry a house convention.
- `Restricted`: allowed only where the plain form would be worse, and **every use carries a one-line comment
  immediately above it saying why**. A reviewer rejects a Restricted feature with no reason, and rejects the reason
  when the plain form would have read as well. An agent reaches for the plain form first. This is the same
  mechanism as the exception-boundary rule in `SKILL.md` section 7: permission plus a stated reason at the site.
- `Not allowed`: never in new code; existing uses are refactor by-product, and the row keeps the count.
- `Pending`: not yet ruled; a coding agent treats it as *not adopted* and does not introduce it in new code.

Where a row's disposition follows from a rule already in `SKILL.md`, the section is cited. Only Matt changes a
disposition, in an attended session.

**House usage** is the count of source lines (and files) across the eight C# repositories on 2026-10-07, 3,386
files, by pattern match. It is a cost estimate for a `Not allowed` ruling and a signal of house practice, not an
audit; a row marked *n/m* was not measurable by pattern.

---

## Baseline restrictions (features older than C# 8 that `SKILL.md` already restricts)

| Feature | Version | Disposition | Where ruled |
|---|---|---|---|
| `var` (implicitly typed locals) | 3.0 | Not allowed | SKILL.md section 2 |
| LINQ query syntax (`from x in ...`) | 3.0 | Not allowed; method syntax only | SKILL.md section 12 |
| `dynamic` | 4.0 | Not allowed outside a named interop boundary | SKILL.md section 12 |
| `unsafe` | 1.0 | Not allowed outside a named interop boundary | SKILL.md section 12 |
| `goto` | 1.0 | Not allowed | SKILL.md section 12 |
| `async void` | 5.0 | Not allowed | SKILL.md section 6.7 |
| Block-scoped namespaces | 1.0 | Not allowed; file-scoped only | SKILL.md section 2 |
| Local functions (a method declared inside a method) | 7.0 | Not allowed; a private method on the class, captured variables become parameters, so it is named and carries an XML summary (ruled 2026-10-07; 10 sites) | SKILL.md section 12 |

---

## C# 8.0 (September 2019, .NET Core 3.0)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| Nullable reference types | `string?` annotations and null-state analysis; `<Nullable>enable</Nullable>` in every project | 5,112 lines / 1,388 files | Allowed (required: every project enables it) |
| Switch expressions | `x switch { ... }` returning a value | 168 / 118 | Allowed; every arm breaks after `=>` (SKILL.md section 6.4, ruled 2026-10-07) |
| Property patterns | `obj is { Length: > 0 }` | 117 / 82 | Allowed (SKILL.md section 12) |
| Tuple and positional patterns | `(a, b) switch { (0, _) => ... }`, deconstruction in patterns | 2 / 1 | Restricted (ruled 2026-10-07): only where several co-varying inputs would otherwise be a nested `if` chain, with the reason stated; pattern variables are typed, `(int w, int h)`, never `var` |
| Using declarations | `using var x = ...;` without a block | 469 / 226 | Not allowed (ruled 2026-10-07): the block form with braces is the house way, the indentation shows the resource's scope; `using`, `try` and `lock` blocks do not count toward the one-level rule (SKILL.md 6.1) |
| Static local functions | `static int Helper(...)` inside a method, no captures | 2 / 2 | Not allowed (ruled 2026-10-07): local functions of any kind are private methods (baseline table); the compiler emits the same code, and a private method is named, documented and visible in the class's shape |
| Readonly instance members | `readonly` on a struct member that does not mutate | 15 / 14 | Not allowed (ruled 2026-10-07): unreachable under the immutability rule (SKILL.md 4.4) and `readonly record struct` as the house wrapper; a struct never has mutable state for a member to promise not to touch |
| Default interface members | Method bodies in an interface | n/m | Not allowed (ruled 2026-10-07): an interface declares, a base class or service implements; a body in an interface is behaviour with no place in the taxonomy (SKILL.md 3), and the versioning case it exists for does not arise when every implementer is in a house repo |
| Asynchronous streams | `IAsyncEnumerable<T>` and `await foreach` | 11 / 6 | Restricted (ruled 2026-10-07): only where the sequence is unbounded, too large to buffer, or produced by another task over time, with the reason stated at the producer; a method that could return a list returns a list. Producer takes `[EnumeratorCancellation] CancellationToken` last |
| Indices and ranges | `^1`, `a[1..^1]` | 144 / 89 | Not allowed (ruled 2026-10-07): `Substring`, `Length - n`, `list[list.Count - 1]`, `Path.GetFileName(uri.LocalPath)` for a last URL segment; the January standards' `uri.Segments[^1]` is superseded. 144 lines by-product |
| Null-coalescing assignment | `x ??= value` | 30 / 17 | Not allowed (ruled 2026-10-07): written out as `if (x == null) { x = value; }`; shorthand that hides a branch. 30 lines by-product |
| Disposable ref structs | `ref struct` with a `Dispose` pattern | n/m | Not allowed (ruled 2026-10-07): library-internals territory; the house writes no `ref struct`, and section 9's class-based disposal covers every resource it holds |
| Unmanaged constructed types | `where T : unmanaged` on constructed types | n/m | Not allowed (ruled 2026-10-07): only matters inside `unsafe`, which SKILL.md 12 forbids outside a named interop boundary |
| Stackalloc in nested expressions | `stackalloc` inside an expression, e.g. a call argument | 10 / 6 | Not allowed (ruled 2026-10-07): hides a stack allocation inside a call; the baseline form `Span<byte> buffer = stackalloc byte[n];` is unchanged. Nested uses among the 10 are by-product |
| Interpolated verbatim strings, either order | `$@"..."` or `@$"..."` | 17 / 5 | Not allowed (ruled 2026-10-07): the new spelling `@$` is not used; the baseline `$@` is the one way to write it |

## C# 9.0 (November 2020, .NET 5)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| Records (reference) | `record` with value equality and `with` | 363 / 274 | Allowed (SKILL.md sections 5, 12) |
| Init-only setters | `{ get; init; }` | 3,244 / 409 | Allowed (SKILL.md section 5) |
| Top-level statements | A `Program.cs` with no class and no `Main` | 0 of 16 entry points | Not allowed (ruled 2026-10-07): `Program.cs` holds a documented `Program` class with a documented `Main`, like every other file; the `.editorconfig` no longer prefers top-level statements |
| Relational and logical patterns | `is not null`, `is > 0 and < 10`, `or` | 1,315 / 678 | Allowed (SKILL.md section 12) |
| Target-typed `new` | `Foo f = new();` | 4 / 3 | Allowed when the type is on the left (SKILL.md section 12) |
| Static anonymous functions | `static x => ...` lambdas with no captures | 55 / 18 | Allowed (SKILL.md section 12) |
| Target-typed conditional | `cond ? a : b` where branches convert to the target type | n/m | Allowed (ruled 2026-10-07): compiler leniency on an expression the house already writes; it only removes a cast |
| Covariant return types | An override returns a more derived type | n/m | Allowed (ruled 2026-10-07): removes a cast that was always safe; reachable only in the Roadbed base-class hierarchies since classes are sealed by default |
| Extension `GetEnumerator` in `foreach` | An extension method makes a type enumerable | n/m | Not allowed (ruled 2026-10-07): a type that should be enumerable implements `IEnumerable<T>` itself or is wrapped in a collection type (SKILL.md 6.5); enumeration bolted on from outside hides where behaviour lives |
| Lambda discard parameters | `(_, _) => ...` | 1 / 1 | Allowed (ruled 2026-10-07): says "unused" explicitly |
| Attributes on local functions | `[Attr] void Local() { }` | n/m | Not allowed (ruled 2026-10-07): unreachable, local functions are not allowed |
| Native-sized integers | `nint`, `nuint` | 0 | Not allowed (ruled 2026-10-07): interop territory, behind the `unsafe` boundary rule |
| Function pointers | `delegate* unmanaged<...>` | 0 | Not allowed (ruled 2026-10-07): requires `unsafe` |
| Module initializers | `[ModuleInitializer]` | 0 | Not allowed (ruled 2026-10-07): startup logic with no visible caller; installers are the house startup path |
| `SkipLocalsInit` | Suppress the `localsinit` flag | 0 | Not allowed (ruled 2026-10-07): requires `unsafe` |
| Partial method enhancements | Partial methods with return values and accessibility | n/m | Restricted (ruled 2026-10-07): only where a source generator requires the signature, with the generator named in the reason |

## C# 10 (November 2021, .NET 6)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| File-scoped namespace declaration | `namespace X;` as the first line | 3,371 / 3,371 | Allowed and required (SKILL.md section 2) |
| Record structs | `record struct`, `readonly record struct` | 6 / 6 | Allowed (SKILL.md section 5 names it for wrapped primitives) |
| Global using directives | `global using X;` once per project | 0 | Not allowed (ruled 2026-10-07): every file states its own usings, `using System;` included. `<ImplicitUsings>enable</ImplicitUsings>` is the same feature by another route and is to be removed from every project file in a future pass (99 projects; up to 903 files then gain the explicit lines the compiler asks for) |
| Extended property patterns | `is { A.B: value }` | 0 | Not allowed (ruled 2026-10-15): the dotted path is the reach-through shape of the two-dots rule inside a pattern; nested braces `{ A: { B: value } }` are the house form and keep each level visible |
| `with` expressions on structs and anonymous types | Non-destructive mutation beyond records | 6 production / 3 test (all on records; an earlier count of 54 matched the word in log strings) | Allowed on records and record structs (ruled 2026-10-15): the immutability rule's own tool, and a copy that carries new members automatically. Not allowed on anonymous types. In test code the MSTest skill's ban stands: every value written out |
| Interpolated string handlers | Custom `$""` processing, mostly for logging and performance | 0 | Not allowed to author (ruled 2026-10-15); libraries that use them, such as the logging abstractions, are simply used |
| Lambda natural type and explicit return type | `var f = () => 1;`, `int () => 1` | n/m | Natural type Not allowed (needs `var`; a lambda is assigned to a declared `Func<>` or `Action<>`); explicit return type Allowed where the compiler asks for one (ruled 2026-10-15) |
| Attributes on lambdas | `[Attr] () => ...` | n/m | Not allowed (ruled 2026-10-15): the house uses endpoint classes, not inline lambdas |
| Constant interpolated strings | `const string s = $"{A}{B}";` | 0 | Allowed (ruled 2026-10-15): replaces concatenation in constant declarations |
| `sealed` on record `ToString` override | Stops derived records regenerating it | 0 | Not allowed (ruled 2026-10-15): unreachable, records are sealed |
| Mixed declaration and assignment in deconstruction | `(x, int y) = tuple;` | n/m | Not allowed (ruled 2026-10-15): hides which names are new; declare first, then deconstruct |
| `CallerArgumentExpression` | Captures an argument's source text | 1 / 1 | Restricted (ruled 2026-10-15): written only when authoring a house guard helper the base class library lacks, with the reason stated. `ArgumentNullException.ThrowIfNull` and the other library helpers that carry it stay required (SKILL.md 4.5) |
| `AsyncMethodBuilder` on methods | Custom async builders | n/m | Not allowed (ruled 2026-10-15): runtime-internals territory |

## C# 11 (November 2022, .NET 7)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| Required members | `required` on a property or field; compiler enforces initialisation | 1,437 / 354 | Allowed (SKILL.md section 5). Written `required public` while StyleCop is 1.1.118 (section 1) |
| Raw string literals | `"""..."""`, no escaping, multi-line | 132 / 24 | Allowed (ruled 2026-10-15): for text that belongs in the code, templates and test fixtures; SQL stays in `.txt` resource files as today |
| Generic math: static abstract and static virtual interface members | `static abstract T operator +(T, T)` | 0 | Not allowed (ruled 2026-10-15): numeric-library territory; consuming `INumber<T>` from the base library is using the library |
| Generic attributes | `[Attr<T>]` | 0 | Allowed (ruled 2026-10-15): type-safe where `typeof(T)` took any type |
| UTF-8 string literals | `"text"u8` | 2 / 1 | Restricted (ruled 2026-10-15): byte-level I/O only, with the reason stated |
| Newlines in interpolation holes | Multi-line expressions inside `{}` | n/m | Not allowed (ruled 2026-10-15): compute the value on its own line, then interpolate the name |
| List patterns | `is [first, .., last]` | 0 | Restricted (ruled 2026-10-15): same terms as tuple and positional patterns; typed pattern variables, reason stated |
| File-local types | `file class X` | 0 | Not allowed (ruled 2026-10-15): a source-generator device that conflicts with one type per file |
| Auto-default structs | Struct constructors need not set every field | n/m | Allowed (ruled 2026-10-15): compiler leniency, no syntax |
| Pattern match `Span<char>` on a constant string | `span is "text"` | n/m | Allowed (ruled 2026-10-15): compiler leniency, no syntax |
| Extended `nameof` scope | `nameof(parameter)` in parameter attributes | n/m | Allowed (ruled 2026-10-15): what `[CallerArgumentExpression(nameof(argument))]` relies on |
| `ref` fields and `scoped ref` | `ref` fields in `ref struct`; lifetime annotations | 4 / 2 | Not allowed (ruled 2026-10-15): `Span<T>`-internals territory; 4 sites by-product |
| Improved method group conversion | Compiler caches delegates for method groups | automatic | Allowed (no syntax; compiler behaviour) |

## C# 12 (November 2023, .NET 8)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| Primary constructors on classes and structs | `class Service(IDep dep) { }` | 26 / 25 | Not allowed (SKILL.md section 4.6: records only) |
| Positional records | `record Point(int X, int Y);` | 8 / 5 | Allowed (SKILL.md section 4.6) |
| Collection expressions | `int[] a = [1, 2, 3];`, spread `[..x, ..y]` | 170 / 103 | Allowed (SKILL.md section 12) |
| Inline arrays | `[InlineArray(n)]` struct as a fixed buffer | 0 | Not allowed (ruled 2026-10-15): library-internals territory |
| Optional parameters in lambdas | `(int x = 1) => ...` | 1 / 1 | Not allowed (ruled 2026-10-15): a lambda with defaults is a method; make it a private method |
| `ref readonly` parameters | Pass by reference, read-only, caller may pass a value | 0 | Not allowed (ruled 2026-10-15): the house passes records and small values; revisit if a struct ever grows large enough to need it |
| Alias any type | `using Point = (int X, int Y);` | 0 | Not allowed (ruled 2026-10-15): define the type; a tuple with a nickname is what wrap-all-primitives forbids |
| `[Experimental]` attribute | Marks an API as experimental; use is a diagnostic | 0 | Not allowed to author (ruled 2026-10-15): no house API has an experimental tier |
| Interceptors (preview) | Source-generator replacement of call sites | 0 | Not allowed (ruled 2026-10-15): invisible at the call site by design, and still preview |

## C# 13 (November 2024, .NET 9)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| `params` collections | `params ReadOnlySpan<T>`, `params IEnumerable<T>`, not only arrays | 0 | Allowed (ruled 2026-10-15): a signature detail on the author's side; callers see no difference |
| `System.Threading.Lock` | A dedicated lock type; `lock (lockObject)` uses `EnterScope` | 1 / 1 | Allowed (ruled 2026-10-15): new lock fields are `Lock`, never `object`; the 37 existing `lock (object)` sites are by-product |
| `\e` escape sequence | Character literal for ESC (U+001B) | 0 | Allowed (ruled 2026-10-15): names the character instead of `\u001b` |
| Implicit index access in object initialisers | `= { [^1] = x }` | n/m | Not allowed (ruled 2026-10-15): unreachable, from-end indices are not allowed |
| `ref` locals and `unsafe` in iterators and async methods | Previously disallowed | n/m | Not allowed (ruled 2026-10-15): behind the `unsafe` and `ref` rulings |
| `ref struct` types implement interfaces | `ref struct S : IFoo` | 0 | Not allowed (ruled 2026-10-15): `Span<T>`-internals territory |
| `allows ref struct` generic anti-constraint | Type parameters that accept `ref struct` | 0 | Not allowed (ruled 2026-10-15): `Span<T>`-internals territory |
| Partial properties and indexers | `partial int X { get; set; }` in partial types | 0 | Restricted (ruled 2026-10-15): only where a named source generator requires it, same terms as partial-method enhancements |
| `OverloadResolutionPriority` | Library authors rank overloads | 0 | Not allowed to author (ruled 2026-10-15): for public library surfaces with overload ambiguities the house does not have |
| Method group overload optimisations | Compiler behaviour | automatic | Allowed (no syntax) |

## C# 14 (November 2025, .NET 10)

| Feature | What it is | House use | Disposition |
|---|---|---|---|
| Extension members | `extension(Type t) { ... }` blocks: extension properties, static members, operators | 0 | Not allowed until the StyleCop upgrade, then revisit (ruled 2026-10-15): syntax the pinned analyzer has never seen. Classic extension methods (C# 3) are unchanged |
| Null-conditional assignment | `a?.B = value;` | 0 | Not allowed (ruled 2026-10-15): the same hidden branch as `??=`; write the `if` |
| `nameof` on unbound generics | `nameof(List<>)` | 0 | Allowed (ruled 2026-10-15): removes a workaround |
| Implicit `Span<T>` / `ReadOnlySpan<T>` conversions | More conversions apply without casts | automatic | Allowed (ruled 2026-10-15): compiler leniency, no syntax |
| Modifiers on simple lambda parameters | `(ref x) => ...` without a type | 0 | Not allowed (ruled 2026-10-15): write the type, `(ref int x) => ...` |
| `field`-backed properties | The `field` keyword names the compiler-generated backing field in an accessor | 0 | Not allowed (ruled 2026-10-15): declare the `_camelCase` field; the pinned StyleCop does not know the keyword |
| `partial` events and constructors | More members can split across partial declarations | 0 | Restricted (ruled 2026-10-15): same terms as the other partial members, a named source generator requires it |
| User-defined compound assignment operators | `operator +=` and friends | 0 | Not allowed (ruled 2026-10-15): exists to mutate in place, which the immutable house wrappers forbid |

---

## How a row changes

A disposition changes only in an attended session with Matt, who owns this skill. When a `Pending` row becomes
`Allowed`, it may gain a one-line house convention (where it is used, where it is not). When it becomes
`Not allowed`, the house-use count on that row is the size of the by-product refactor, and the row keeps the count so
the cost stays visible. A future C# version is handled on the house's schedule, not the SDK's: `<LangVersion>` stays
pinned at 14, so its features cannot compile until this document gains that version's rows, Matt rules them, and the pin
moves in the same change (Matt, 2026-10-15). Nothing arrives by upgrade.
