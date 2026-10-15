# Object Calisthenics: the nine rules and the house disposition of each

The nine rules are Jeff Bay's, from *The ThoughtWorks Anthology* (2008). They are reproduced here in their original
form so nobody has to go looking for them, followed by the house disposition of each one. The
dispositions were ruled by Matt on 2026-10-04, in an attended session, against measurements of the eight C# repos
(3,378 files; 983 orchestrator, service and repository classes; 287 entity files). This document carries the
reasoning; the rules themselves live in `SKILL.md`, which is what a reviewer applies.

**Precedent.** The Repository Starter Kit's `object-calisthenics.instructions.md` (Copilot, 2025) states the nine
rules for "business domain code" with DTOs, API models and configuration classes exempt. It says: *"No additional
rules must be added, and none of these rules should be replaced or removed."* That is honoured here in the sense
that matters: all nine are listed, none is renamed, and where the house narrows one the narrowing is stated as a
disposition under the original rule, not as a new rule.

**The two exemptions every disposition relies on.** Both come from the starter kit and were sharpened in session:

- **Data types are exempt**, and the test is shape, not folder or name: *a type with no method bodies beyond
  `ToString`, `Equals` and `GetHashCode` is data.* Entities, DTOs, POCOs, options types and row classes.
- **Injected dependencies are exempt** from the counting rules. The starter kit exempted the logger; the house
  exempts every constructor-injected collaborator, because in this architecture a behaviour class's fields are its
  dependencies, not its state.

---

## 1. One level of indentation per method

**Original.** A method has one level of indentation. A second level is extracted to a named method.

**Disposition: adopted as written.** Two ways to get there, both from the starter kit's examples: extract the inner
block to a private method, or filter first so the loop has nothing to test:

```csharp
// ❌ Two levels.
foreach (User user in users)
{
    if (user.IsActive)
    {
        this._mailer.Send(user.Email);
    }
}

// ✅ Extracted.
foreach (User user in users)
{
    this.SendIfActive(user);
}

// ✅ Filtered first.
IEnumerable<User> activeUsers = users.Where(user => user.IsActive);
foreach (User user in activeUsers)
{
    this._mailer.Send(user.Email);
}
```

## 2. Don't use the `else` keyword

**Original.** No `else`. Use early return, fail fast, guard clauses.

**Disposition: adopted as written**, with one tolerance: `else` may remain where both branches are a single
assignment to the same variable and a conditional expression would read worse. Fail-fast at the top of a method
is the house constructor-validation pattern applied to methods:

```csharp
public void ProcessOrder(Order order)
{
    ArgumentNullException.ThrowIfNull(order);
    if (!order.IsValid)
    {
        throw new InvalidOperationException("Invalid order");
    }

    // process
}
```

## 3. Wrap all primitives and strings

**Original.** No bare primitive crosses a method boundary or sits in a field; wrap it in a type that carries its
meaning and its validation (`Age` rather than `int`).

**Disposition: adopted in full** (Matt, 2026-10-04: *"our house rules should be to 'Wrap all primitives'"*). The rule has
two halves, and the session took them in order:

- **Configuration half, adopted.** A behaviour class holds no primitive fields. Configuration arrives as one options
  type. Measured: 34 primitive fields across the 983 behaviour classes, 21 of them on one orchestrator, and 91
  options and settings types already exist. This half is cheap and already house practice.
- **Domain half, adopted.** Typed identifiers and value objects on entities and method signatures (`PlaceId`,
  `CountyFips`, `PostalCode`, `Coordinate`): a `readonly record struct` over the primitive, validating in its
  constructor, so the compiler refuses a county id where a place id belongs and a GeoJSON pair cannot be read
  backwards. Database mapping happens once in the data layer's type handlers. This is the largest by-product on the
  list (3,540 bare entity properties when ruled) and lands one module at a time, identifiers first.

Matt's expectation, recorded: applying this rule together with rule 8 will create more small data types under
`Entities/` and `DTOs/`. Measured, that is true for one class today, and it is the right outcome where it happens.

## 4. First-class collections

**Original.** A class that contains a collection contains no other member. The collection gets its own type, and the
behaviour over it lives there.

**Disposition: adopted as written.** A behaviour class does not expose a raw `List<T>` of domain items; it holds a
type that wraps the collection and answers the questions callers ask of it (the starter kit's
`GroupUserCollection.GetActiveUsers()` example).

## 5. One dot per line

**Original.** One member access per line, so that a chain `a.B().C().D()` becomes a sequence of named steps.

**Disposition: adopted as two dots per chain, with two exemptions.** Measured with a chain instrument (the longest
run of member accesses on a line, argument lists stripped; a literal dot count over-reads sevenfold because a line
can hold several independent `this.x` arguments):

| What the two-dot ceiling catches | Lines | Resolution |
|---|---|---|
| String and StringBuilder chains | 151 | One call per statement (Matt's explicit ruling; scannable without context, one comment per line) |
| Reaching through a member (`this.X.Y.Z`) | 37 | Take a local, or ask the collaborator for what you need |
| Fully qualified type names | 28 | Add the `using` directive; a by-product Matt wanted |
| Framework data chains (`context.JobDetail.Key.Name`) | 16 | Assign `context.JobDetail` to a local; the rest is two dots |

**Exempt: LINQ chains, and `await x.Method().ConfigureAwait(false)`** (the analyzer requires the latter on the
same expression). Nothing else. At two dots, `this.Logger.LogInformation(...)` passes without a special case, so the
data-type and injected-dependency exemptions are not needed for this rule.

Why two and not one: at one dot every `this.member.Method()` call, which StyleCop SA1101 requires to be written
that way, would violate. Two is the smallest ceiling compatible with the `this.` rule.

## 6. Don't abbreviate

**Original.** Names are whole words. An abbreviation is either a sign of repetition (the name is too long because
the thing is used too often, so extract it) or of a missing concept.

**Disposition: adopted as written.** Domain acronyms that are words in this business (`Id`, `Url`, `Csv`, `Fips`,
`Ats`, `Mcp`) are fine, cased as words.

## 7. Keep all entities small

**Original.** Bay's limits: 50 lines per class, 10 files per package. The starter kit's: 10 methods per class, 50
lines per class, 10 classes per namespace.

**Disposition: adopted with a house ceiling of 500 lines per production file; the method and namespace limits are
not adopted.** Fifty lines is unreachable for a Dapper repository with documented SQL, and the house already carries
`GenerateDocumentationFile`, which puts a summary on every member. Measured on 2026-10-04: median production file 91
lines, 92 files over 500, 16 over 1,000, the largest 2,476. **Named exceptions live in each repository's own
`CLAUDE.md`, never in this skill**, which is public and shared across repos.

## 8. No classes with more than two instance variables

**Original.** Two instance variables. A third is a sign the class has two responsibilities. The starter kit adds:
do not count the logger.

**Disposition: adopted for a class's own state, with injected dependencies exempt and no ceiling on them.**
Measured: 1,095 instance members across the 983 behaviour classes, of which 943 are injected interfaces and
service classes, 38 options objects, 34 primitives; 862 classes already hold two or fewer members. Counting
dependencies would have produced 121 violations that were all false alarms, including the recurring five-dependency
house shape (logger, database factory, activity service, source service, mapper). Matt declined a dependency ceiling
on 2026-10-04: *"I don't think we need a ceiling on dependency injection."*

What the rule therefore says: own state at most two members, never a primitive (rule 3's configuration half), and
no mutable instance state (every field `readonly`, every property get-only or `init`). The immutability clause is
the rule's real payload in a dependency-injection architecture: with fields readonly and dependencies exempt, the
remaining state is the class's responsibility made visible.

## 9. No getters, setters or properties

**Original.** "Tell, don't ask." An object does not expose its state for others to make decisions on; the behaviour
that uses the data lives with the data. The starter kit adds: use private constructors and static factory methods
for domain classes; DTOs are exempt.

**Disposition: adopted as two rules, for behaviour classes only.**

1. Orchestrators, services and repositories hold no public state; dependencies are private readonly.
2. A state transition with a rule lives in exactly one named service method; callers never branch on an entity's
   state and then assign it inline.

Why two rules and not the original: in this architecture entities are data by design (0 of 287 entity files carry a
method body) and behaviour lives in services, so the original rule's home, the rich domain object, does not exist
here. The invariant the rule protects, that a transition is checked in one place, is kept by putting that place in
a named service method. Measured: 0 public getters or setters on behaviour classes, and about two ask-then-set
sites, so both rules are nearly free today. The starter kit's private-constructor-and-factory guidance applies to
domain classes with behaviour and is therefore not a house rule.

**Placement, which the original rule never needed:** because the rule depends on telling data from behaviour, data
types live under `Entities/` (public) or `DTOs/` (private), never under `Services/`, `Repositories/` or
`Orchestrators/`. The 80 files that hold row classes beside a repository, and the request and result types filed
under `Services/`, move; that is by-product, and one type per file is the open decision that would finish it.

---

## References

- Jeff Bay, "Object Calisthenics", in *The ThoughtWorks Anthology* (Pragmatic Bookshelf, 2008).
  https://www.cs.helsinki.fi/u/luontola/tdd-2009/ext/ObjectCalisthenics.pdf
- Repository Starter Kit, `github/instructions/object-calisthenics.instructions.md` (Copilot, 2025): the nine rules
  with the domain-versus-DTO exemption and the logger exemption this document generalises.
- Robert C. Martin, *Clean Code* (Prentice Hall, 2008).
