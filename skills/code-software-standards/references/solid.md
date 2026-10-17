# SOLID: the five principles and the house reading of each

The five principles are Robert C. Martin's, first collected in "Design Principles and Design Patterns" (2000) and
given their acronym by Michael Feathers. Each is stated here close to its original form so nobody has to go looking
for it, followed by the house reading: the one question a reviewer asks, what passes, what fails, and how the
principle connects to the rules the house already measures. `SKILL.md` section 3 carries the checks; this document
carries the reasoning.

**Why these five, and why as checks.** Each principle names a way a design resists change. Stated as a principle it
is advice; stated as a question with a yes or no answer it is a review rule. The house adopts them as the latter.
Where a principle overlaps a rule the house already holds (the two-member state ceiling, the size ceiling, the one
named method per transition, constructor injection), the reading says so, so a reviewer is not asked the same
question twice under two names.

The examples are in a neutral pseudocode, not any language. A language skill shows the same shapes in its own
syntax.

---

## 1. Single responsibility principle

**Original.** A class should have one, and only one, reason to change.

**House reading.** Describe the class in one sentence. If the sentence needs *and*, the class has two reasons to
change and is two classes.

```text
// Fails: two reasons to change (the storage layout, and the pricing rule).
class PriceService:
    loadPricesFromTable(connection)
    applySeasonalDiscount(prices, season)

// Passes: each class changes for one reason.
class PriceRepository:      loadPrices(connection)
class SeasonalPricing:      apply(prices, season)
```

**Connection to existing rules.** Section 4.3 (own state at most two members, never a primitive) and section 8 (the
500-line ceiling) are this principle made countable. A class whose own state has a third member is usually
carrying a second job; a file past the ceiling almost always is. Injected dependencies are deliberately not
counted: in a dependency-injection architecture, a class with many collaborators and one job has one
responsibility, and a dependency ceiling would flag the house's own composition pattern as a violation.

**What this reading does not say.** It does not say a class must be tiny, and it does not say a class must have one
public method. A repository with eight queries over one table has one reason to change.

---

## 2. Open-closed principle

**Original.** Software entities should be open for extension but closed for modification: a new behavior is added
by adding code, not by changing code that works.

**House reading.** When the next case arrives, is it added in one place, by adding a type, a row or a
registration? Or does it require editing a branch in several places that must be kept in step?

```text
// Fails: the same ladder in three callers; a new vendor means three edits that must agree.
function fetch(vendor):         if vendor == A: ... else if vendor == B: ...
function describe(vendor):      if vendor == A: ... else if vendor == B: ...
function rateLimit(vendor):     if vendor == A: ... else if vendor == B: ...

// Passes: one abstraction, one implementation per vendor, one registration.
interface VendorAdapter:        fetch(), describe(), rateLimit()
class VendorA implements VendorAdapter
class VendorB implements VendorAdapter
registry.register(VendorA, VendorB)
```

**What is allowed, and this is the house's deliberate narrowing.** One `switch` or `if` ladder over a value, in the
one named method that owns that decision (section 4.2), is allowed and is often the clearest form: a switch
expression that maps a status to a label is not a design smell, it is a table. The violation is the *same* ladder
repeated across callers, or a ladder in one file that must be edited whenever another file changes. The test is
*how many places change when the next case arrives*, not *does a switch exist*.

**Connection to existing rules.** Section 4.2 (one named method per transition) is the single-ladder case. Section
3.5 (dependency inversion) is how the multi-ladder case is fixed: the cases become types behind one abstraction,
and the ladder becomes a registration.

---

## 3. Liskov substitution principle

**Original.** Subtypes must be substitutable for their base types: a program that works with the base type works
with any subtype without knowing the difference.

**House reading.** Can every caller of the base type be given the subtype without a surprise? Three surprises fail
the check: an inherited member that throws *not supported*; a precondition the subtype tightens (it rejects an
input the base accepted); a postcondition the subtype loosens (it returns null where the base promised a value,
or skips a side effect the base promised).

```text
// Fails: the subtype refuses what the base type promised to accept.
class Repository:               save(entity)
class ReadOnlyRepository extends Repository:
    save(entity):               throw NotSupported

// Passes: the roles are separate interfaces; a reader is not a kind of writer.
interface Reader:               get(id)
interface Writer:               save(entity)
class ReadOnlyRepository implements Reader
```

**Connection to existing rules.** The house closes types to inheritance by default and opens one only when it is
designed for it, saying so in its name or summary (the language skill names the keyword). A hierarchy that was
never designed cannot be substituted safely, so the cheapest way to honor this principle is to have few
hierarchies. Composition is the default: a class that needs part of another's behavior takes that other as an
injected dependency (section 3.5).

**What this reading does not say.** It does not forbid inheritance. A base job class that defines the run loop and
leaves one step to the subtype is a designed hierarchy and passes.

---

## 4. Interface segregation principle

**Original.** Clients should not be forced to depend on interfaces they do not use. Many small, role-specific
interfaces beat one large general one.

**House reading.** Two questions. Does any implementation leave a member empty or throwing? Does any consumer
receive an interface and use one member of many? Either is a sign the interface is shaped like a class rather
than like a role.

```text
// Fails: one interface mirrors one class; the notifier implements members it has no use for.
interface Channel:              send(message), receive(), archive(), purge()
class EmailNotifier implements Channel:
    receive():                  throw NotSupported
    archive():                  (empty)

// Passes: interfaces named for the role a consumer needs.
interface Sender:               send(message)
interface Receiver:             receive()
class EmailNotifier implements Sender
```

**Connection to existing rules.** An interface named for a role (*a thing that can fetch*, *a thing that can
notify*) is also what makes section 3.5 work: a consumer that depends on `Sender` can be given any sender, and a
test can supply a fake that implements one member. One class may implement several roles; that is the point.

---

## 5. Dependency inversion principle

**Original.** High-level modules should not depend on low-level modules; both should depend on abstractions.
Abstractions should not depend on details; details should depend on abstractions.

**House reading.** Does a behavior class construct a collaborator itself, reach a global or a static service, or
name a concrete type in its constructor where an abstraction would do? Any of the three fails.

```text
// Fails: the service builds its own storage and reads its own clock; neither can be replaced in a test.
class OrderService:
    constructor():
        this.repository = new SqlOrderRepository(connectionString)
    markDelivered(order):
        order.deliveredOn = SystemClock.now()

// Passes: collaborators arrive as abstractions and are validated on entry.
class OrderService:
    constructor(repository: OrderRepository, clock: Clock):
        requireNotNull(repository); requireNotNull(clock)
        this.repository = repository; this.clock = clock
```

**What a behavior class may construct.** Data types, collections, exceptions, and nothing that does work.
Constructing collaborators is the job of the composition root (where the application is assembled) and of
factories that exist for that purpose and are themselves injected.

**Connection to existing rules.** Section 4.5 (validate on entry) is the first line of every constructor that
follows this principle. Section 9 (ambient state as a dependency) is this principle applied to the clock, random
numbers, the environment, the file system and the network. Section 4.3 sets no ceiling on injected dependencies,
because this principle, followed, produces classes with many of them, and that is the design working.

---

## How the five fit together, in one paragraph

A class with one responsibility (1) is small enough that its collaborators can be named as roles (4) and injected
as abstractions (5). Because its collaborators are abstractions, the next case is a new implementation registered
once rather than a branch edited in many places (2), and because few hierarchies exist and the ones that do are
designed, every implementation can stand in for its role without surprise (3). Each principle makes the next one
cheap, and a violation of one usually shows up as a violation of another: the class that builds its own
collaborators (5) is the class with three reasons to change (1) and the switch ladder in four places (2).

---

## Quick reference

| Principle | One line | Red flag on a diff |
|---|---|---|
| Single responsibility | One reason to change | *and* in the one-sentence description; a third member of own state; a file past the ceiling |
| Open-closed | Add a case by adding, not editing | The same ladder in more than one place; a ladder over the kind of thing rather than a value |
| Substitution | A subtype keeps the contract | A type test or cast on something received as an abstraction; *not supported* in an override |
| Interface segregation | Depend only on what you use | An empty or throwing interface member; a consumer using one member of many |
| Dependency inversion | Receive collaborators as abstractions | `new` of something that does work inside a behavior class; a static service call; a module of rules referencing a module of storage |

## The same five, one level up

Each principle reads the same between modules as between classes. A module has one reason to change (1). A new
feature is a new module or a new registration, not an edit across several (2). Two modules behind the same contract
can stand in for each other (3). The surface one module exposes to another is the role the other needs, not the
whole of the first (4). References point from infrastructure toward rules and form no cycle (5). A reviewer who
sees a class pass all five and the modules around it fail them has found the larger problem.

## References

- Robert C. Martin, "Design Principles and Design Patterns" (2000), the paper that collected the five.
- Robert C. Martin, *Agile Software Development: Principles, Patterns, and Practices* (Prentice Hall, 2002),
  chapters 8 to 12, one per principle.
- Barbara Liskov and Jeannette Wing, "A Behavioral Notion of Subtyping", *ACM Transactions on Programming
  Languages and Systems* 16(6), 1994: the substitution principle in its original form.
- Jeff Bay, "Object Calisthenics", in *The ThoughtWorks Anthology* (Pragmatic Bookshelf, 2008): the nine rules
  behind `SKILL.md` sections 5 to 8, with their house dispositions in `code-csharp-standards`.
