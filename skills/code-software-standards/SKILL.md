---
name: code-software-standards
description: Language-neutral software standards for every repository that adopts this skill, in any language. Use whenever writing, modifying or reviewing code, before the language skill for that code: the two kinds of type (data and behavior) and the shape test that tells them apart, the SOLID principles as review checks, behavior-class rules (no public state, one named method per rule-bearing transition, own state at most two members and never a primitive, immutability, validate on entry, dependencies injected), data-type rules (no behavior, wrap primitives), method shape (one level of nesting, guard clauses, no reaching through a collaborator, first-class collections, whole-word names), error boundaries, the 500-line ceiling with per-repository exceptions, ambient state through injected abstractions, and the rule of thumb for language features. A language skill such as code-csharp-standards carries the half that depends on the language and its tools; this skill carries the half that does not.
owner: Matt
version: 1.0.0
visibility: public
---

# Software Standards

These are the house rules for code in every repository that adopts this skill, whatever the language. Each is
written so a reviewer can answer yes or no to it on a diff. Where a language has a tool that enforces a rule, the
language skill names the tool; where none does, this document is the rule.

**Who it binds.** Everyone who writes code in a repository that adopts it, person or agent, in every language the
repository holds. A language skill refines these rules for its language; it does not relax them.

---

## 1. How this skill and a language skill divide the work

- This skill states what is true of good code in any language: how types are classified, what a behavior class
  may hold, how a method is shaped, where errors are caught, how big a file may be.
- A language skill states how that language's syntax and tools serve those rules: which keyword makes a field
  immutable, which analyzer enforces a rule, which language features are allowed, how the clock is injected.
- Where the two disagree, the language skill is wrong, because it was written to serve this one. Fix the language
  skill.
- A repository's own working agreement (`CLAUDE.md` or its equivalent) holds the lists that are the repository's:
  the files exempt from the size ceiling (section 8) and nothing else from this skill.

---

## 2. Two kinds of type, and the test is shape

Every type is one of two kinds, and **the test is shape, not folder or name**:

> A type with no method bodies beyond equality, hashing and a string rendering is **data**.
> A type with any other method body is a **behavior class**.

**Data types** are entities, transfer objects, request and response shapes, options and settings, row types. They
hold values and nothing else. Sections 5 and 6 apply to them.

**Behavior classes** are services, repositories, orchestrators, handlers, jobs, loaders: anything that does
something. Sections 3, 4 and 7 apply to them.

A type that is filed as data but has grown a method body has become a behavior class, and the rules for behavior
classes apply from that moment. Usually the method belongs on a service.

---

## 3. SOLID, as review checks

Each principle below is stated as the one question a reviewer asks, with the house reading. The principle in full,
the reasoning and worked examples are in [references/solid.md](references/solid.md).

### 3.1 Single responsibility: one reason to change

**Check:** can the class be described in one sentence without *and*? A class that changes when the database schema
changes *and* when the pricing rule changes has two responsibilities. A second detector: would two different people
ask for changes to two different parts of it?

**House reading:** sections 4.3 and 8 are this principle made measurable. A behavior class holding more than two
members of its own state, or a file past the size ceiling, is the usual sign of a second responsibility. Injected
dependencies are not counted: a class with eleven collaborators and one job has one responsibility.

### 3.2 Open for extension, closed for modification: add a case without editing every site

**Check:** when the next case arrives (a new vendor, format, status, rule), is it added in one place, by adding a
type, a row or a registration, or does it require editing a branch in several places?

**House reading:** one `switch` or `if` ladder over a value, in the one named method that owns that decision
(section 4.2), is allowed and is often the clearest form. The violation is the *same* ladder repeated across
callers, or a ladder that must be edited in step with another file. Then the cases become types behind one
abstraction, or rows in one table, and the ladder goes. A ladder that branches on the *kind* of thing it was
given, rather than on a value, is the purest form of the smell: it is a missing polymorphism.

**The other direction fails too.** An abstraction arrives with the second real implementation, never ahead of it.
An interface with one implementation and no test double is speculation, and speculation is edited more often
than a plain class would have been.

### 3.3 Substitution: a subtype honors the base type's contract

**Check:** can every caller of the base type be given the subtype without knowing, and without a surprise? A
subtype that throws *not supported* from an inherited member, tightens a precondition, or loosens a postcondition
fails the check.

**Red flag:** a caller that tests the concrete type of something it received as an abstraction, or casts it,
has found a subtype that is not substitutable. The fix is in the hierarchy, never in the caller.

**House reading:** inheritance is for designed hierarchies, and a base type says so in its name or its summary; a
type not designed for inheritance is closed to it (the language skill names the keyword). Prefer composition: a
class that needs part of another's behavior takes it as a dependency.

### 3.4 Interface segregation: a consumer depends only on what it uses

**Check:** does any implementation leave a member empty or throwing? Does any consumer receive an interface and call
one member of ten?

**House reading:** interfaces are named for the role a consumer needs (*a thing that can fetch*, *a thing that can
notify*), not for the class that happens to implement them. One class may implement several roles. An interface
that mirrors a class's whole surface is a class in disguise.

### 3.5 Dependency inversion: behavior classes receive their collaborators

**Check:** does a behavior class construct a collaborator itself, reach a global, or name a concrete type where an
abstraction would do?

**House reading:** collaborators arrive through the constructor, as abstractions, and are validated on entry
(section 4.5). A behavior class constructs data types, collections, exceptions and nothing that does work.
Constructing collaborators is the job of the composition root and of factories that exist for that purpose.
There is no ceiling on the number of dependencies a class takes; the ceiling is on its own state.

**The same principle between modules.** A reference between modules points from the one that knows about
infrastructure (storage, transport, the host) toward the one that holds the rules, never the reverse, and never
in a cycle. A module of rules that references a module of storage has inverted nothing.

---

## 4. Behavior classes

### 4.1 No public state

A behavior class exposes no public getter or setter. Its dependencies are private and immutable; its own state is
private.

### 4.2 A state transition with a rule lives in exactly one named method

Callers never read an object's state, decide, and assign the object inline. The rule *only a shipped order can be
delivered* lives in one method named for the transition, where it is checked once and tested once.

```text
// Not allowed: the rule lives in this caller, and in every other caller.
if order.status == Shipped:
    order.status = Delivered
    order.deliveredOn = clock.now()

// Allowed: one named method owns the transition.
orderService.markDelivered(order, clock.now())
```

### 4.3 Own state: at most two members, never a primitive; dependencies do not count

A behavior class's own state, meaning anything that is not an injected dependency, is at most two members, and
none of them is a bare primitive (a string, a number, a flag, a duration). Configuration arrives as **one options
type**. There is no ceiling on injected dependencies; a logger is a dependency like any other.

### 4.4 No mutable instance state

Every field is assigned once, in the constructor, and never again. A class that must carry state across calls
holds it in a data type it owns, and even then prefers to return a new value.

### 4.5 Validate on entry

Every constructor parameter and every public method parameter is validated at the top, before any work: null,
empty, out of range. A failure is reported by the language's argument-exception type naming the parameter. The
happy path never runs on an argument that was not checked.

---

## 5. Data types

- A data type carries **no behavior**. If it needs a method with a body it has become a behavior
  class and section 4 applies; usually the method belongs on a service.
- A data type is **immutable once built**: every member is set at construction and never reassigned. The language
  skill names the keyword.
- Collections on a data type are **read-only to the holder**: an array or an immutable sequence, never a mutable
  list that a caller can grow.
- **Wrap all primitives**. A domain concept is never a bare primitive on a data type
  or in a method signature: an identifier is an `Id` type, a code a `Code` type, a position a `Coordinate`. The
  wrapper validates in its constructor and lets the compiler refuse one kind of identifier where another belongs.
  Mapping to and from storage happens once, at the storage boundary, never at read sites.

---

## 6. Method shape

### 6.1 One level of nesting per method

A method body has one level of *logic* nesting: one `if`, one loop, one `switch`. A second level is extracted to a
named private method, or removed by filtering first (loop over the filtered sequence instead of loop-then-test).
Blocks that frame a method rather than branch it, such as a resource scope, a `try`, or a lock, do not count.

### 6.2 Guard clauses, not `else`

Validate and return or throw early; the happy path runs at the bottom, unindented. `else` is allowed only where
both branches are a single assignment to the same variable and a conditional expression would read worse.

### 6.3 No reaching through a collaborator

A method asks its collaborators for what it needs; it does not walk through them to a third object's members. A
chain of member accesses is at most two steps; the language skill may exempt a specific idiom (a query-builder
chain, an await form) and names each exemption. Three shapes that fail, and the fix for each:

- reaching through a member (`this.factory.connection.kind`): take a local, or ask the collaborator for the value;
- a builder chain on one line: one call per statement, so each line can take a comment and reads without context;
- walking a framework object's model (`context.job.key.name`): assign the intermediate to a local.

### 6.4 First-class collections

A class that holds a collection holds nothing else but that collection and the behavior over it. A behavior class
does not expose a raw list of domain items; it holds a type that wraps the collection and answers the questions
callers ask of it.

### 6.5 Whole-word names

Names are whole words: `repository` not `repo`, `configuration` not `cfg`. An abbreviation is a sign of repetition
(extract the thing) or of a missing concept (name it). Acronyms that are words in the domain are fine and are
cased as words. A name says what the thing is, not that it is a thing: `data`, `info`, `item`, `manager`,
`helper`, `util` and `misc` are not names, alone or as a suffix. The same concept carries the same name
everywhere in a repository.

### 6.6 A method does what its name says, and nothing else

A method named for a query changes nothing; a method named for a command returns nothing a caller must inspect
except an outcome. A method that does two things has two names, and is two methods.

---

## 7. Errors

- **Catch broadly only at a named boundary**: the top of a scheduled job, a request handler, or a
  per-record loop where one bad record must not stop the batch. Every such catch carries a one-line reason naming
  the boundary, in the form the language skill prescribes. Anywhere else, catch the specific type or do not catch.
- **Rethrow preserving the original**: the language's bare-rethrow form, never a new throw of the caught object
  that resets its origin.
- **Fail fast.** An invalid argument, a broken invariant or a missing dependency is reported where it is detected,
  not carried forward to fail somewhere else.
- **An error is not control flow.** Expected outcomes (not found, nothing to do, already done) are return values,
  not exceptions.

---

## 8. Size: the 500-line ceiling

A production source file is at most **500 lines**, counting documentation and blank lines, excluding generated
files and migrations. A file over the ceiling is split by responsibility, usually along the private methods that
section 6.1 produced.

**Named exceptions live in that repository's working agreement, never in this skill.** This skill is public and
shared across repositories; the list is the repository's. Each entry names the file, its line count when listed,
and why it stays. Bay's 50-line class and 10-method limits are not adopted; the file ceiling is the house measure.

---

## 9. Ambient state arrives as a dependency

A behavior class reads the clock, random numbers, environment variables, the file system and the network through
an injected abstraction, never through a static call. A test then supplies a fake, and a behavior that depends on
*now* can be tested at any *now*. The language skill names the abstraction for the clock; the same shape applies to
the rest.

---

## 10. Comments, dead code and suppressed warnings

- A comment says **why**; the code says what. A comment that restates the next line is deleted.
- **No work-item tokens in comments** (`TODO`, `FIXME`, `XXX`, `HACK`): write *Pending:* or *Future work:* prose,
  or file the work where work is tracked.
- **No commented-out code.** Delete it; version control remembers.
- **No warning is suppressed without a reason on the same line**, and a suppression is a local decision about one
  site, never a project-wide switch flipped to make a build pass.

---

## 11. Language features: the rule of thumb

Every language offers shorthand. The house test for each feature, which the language skill applies row by row:
**shorthand that hides a branch, a calculation or a declaration is out; syntax that makes structure visible is
in.** A feature the language skill has not ruled on is not used in new code until its row says it is allowed.

---

## 12. Things to NEVER do

1. **NEVER** put a method body on a data type; a type with behavior is a behavior class, filed and reviewed as one.
2. **NEVER** expose public state on a behavior class.
3. **NEVER** branch on an object's state and assign it inline in a caller; name the transition and own it once.
4. **NEVER** hold a bare primitive as a behavior class's own state; configuration is one options type.
5. **NEVER** reassign a field after construction.
6. **NEVER** run the happy path on an argument that was not validated on entry.
7. **NEVER** construct a collaborator inside a behavior class; it arrives through the constructor.
8. **NEVER** inherit from a type that was not designed for it; compose instead.
9. **NEVER** leave an interface member empty or throwing *not supported* in an implementation.
10. **NEVER** test or cast the concrete type of something received as an abstraction; fix the hierarchy.
11. **NEVER** nest more than one level of logic inside a method body.
12. **NEVER** reach through a collaborator to a third object's members.
13. **NEVER** abbreviate an identifier, or name a thing `data`, `info`, `manager`, `helper` or `util`.
14. **NEVER** catch broadly outside a named boundary, and never without a stated reason.
15. **NEVER** rethrow in the form that resets the error's origin.
16. **NEVER** use an exception for an expected outcome.
17. **NEVER** exceed 500 lines in a production file without an entry in that repository's working agreement.
18. **NEVER** read the clock, randomness or the environment through a static call in a behavior class.
19. **NEVER** write `TODO`, `FIXME`, `XXX` or `HACK` in a comment, or leave commented-out code.
20. **NEVER** suppress a warning without a reason on the same line.
21. **NEVER** give a domain concept a bare primitive type on a data type or in a signature; wrap it.
22. **NEVER** edit this skill outside an attended session with its owner.

---

## 13. Review checklist

Answer every line yes before a change is ready. Each is observable on the diff.

- [ ] Every new type is classified, data or behavior, by the shape test
- [ ] Each behavior class can be described in one sentence without *and*
- [ ] The next case of any branch can be added in one place; no abstraction ahead of its second implementation
- [ ] No subtype weakens its base type's contract; no inheritance from a type not designed for it; no caller
      tests or casts the concrete type of an abstraction it received
- [ ] No implementation leaves an interface member empty or throwing; interfaces are role-shaped
- [ ] Collaborators arrive through the constructor as abstractions; nothing that does work is constructed inside;
      module references point toward the rules and form no cycle
- [ ] Behavior class exposes no public getter or setter
- [ ] State transitions with a rule live in one named method; no ask-then-set in callers
- [ ] Own state is at most two non-primitive members; configuration is one options type
- [ ] Every field is assigned once; data types are immutable once built
- [ ] Every constructor and public method validates its parameters on entry
- [ ] No method body nests more than one level of logic
- [ ] Guard clauses; no `else` except the single-assignment case
- [ ] No chain reaches through a collaborator; builder calls are one per statement
- [ ] Collections are wrapped in first-class types where behavior lives with them
- [ ] No abbreviations in identifiers; no vague nouns; one name per concept across the repository
- [ ] Each method does what its name says and nothing else
- [ ] Broad catches only at a named boundary with a reason; rethrows preserve the origin; no exception for an
      expected outcome
- [ ] Production file is 500 lines or fewer, or listed in the repository's working agreement with a reason
- [ ] Clock, randomness and environment reach behavior classes as injected dependencies
- [ ] No domain concept as a bare primitive on a data type or in a signature
- [ ] No work-item tokens, no commented-out code, no unexplained suppression
- [ ] No language feature the language skill has not ruled allowed
