# Backend Service Template

The reference shape of a backend microservice for this project: a .NET Clean
Architecture solution, the CI pipeline that guards it, and the branching flow
that releases it. Every service repository starts from this one.

There is no shared backend library, so this template is the only mechanism by
which the five services share standards. What is written here is what they have
in common.

## Language

### Branching and release

**Feature branch**:
A short-lived branch carrying one unit of work. It merges into `dev`, never into
`main`.
_Avoid_: topic branch, working branch

**dev**:
The integration branch, and the repository's default branch. Every feature lands
here first, and it is the branch static analysis treats as the reference.
_Avoid_: develop, integration, trunk

**main**:
The branch carrying released versions: tags, `CHANGELOG.md`, and the version in
the project file. It only ever receives `dev`.
_Avoid_: master, release branch, production branch

**Promotion**:
Merging `dev` into `main`. It moves already-integrated work into the released
line; it neither builds nor deploys anything by itself.
_Avoid_: release, deploy, ship

**Release**:
The tag and changelog entry that release-please produces on `main` once a
promotion lands. A release is a marker, not an act of publishing.
_Avoid_: version bump, publish

**Back-merge**:
Merging `main` back into `dev` after a release, so `dev` regains the changelog
and version written by release-please. Without it, every promotion conflicts.
_Avoid_: sync, reverse merge, downstream merge

**Deployment**:
Running a released version somewhere. This template does not perform one — it
stops at the release.
_Avoid_: release, rollout

**New code**:
The subset of the codebase static analysis judges on a pull request: what
changed relative to the reference branch. Quality thresholds apply to it, not to
the repository as a whole.
_Avoid_: diff, delta, changed lines

**Required check**:
A CI job whose success gates a merge. A check that can be skipped by a path
filter must never be required — it would block the pull request forever.
_Avoid_: gate, status

### Service structure

**Microservice**:
One deployable service, living in one repository, owning its own database. There
are five.
_Avoid_: service, app, module

**Presentation**:
The project holding the HTTP API — controllers, DTOs, middleware. Named for the
Clean Architecture layer, not for any user interface; there is none.
_Avoid_: API, Web, Host

**Command**:
A request to change state, named for the use case it performs.
_Avoid_: action, operation, mutation

**Query**:
A request to read state, which changes nothing.
_Avoid_: getter, fetch, lookup

**Handler**:
The application-layer class executing exactly one command or query. It
orchestrates; it holds no HTTP concern and no business invariant of its own.
_Avoid_: service, manager, processor

**Repository**:
The abstraction through which the domain reaches persistence, declared in the
domain and implemented in infrastructure.
_Avoid_: DAO, store, gateway

**Message envelope**:
The fixed metadata wrapping every asynchronous message — identity, causation,
type, version, origin, time — around an opaque payload.
_Avoid_: header, wrapper, metadata block

**Contract version**:
The major version of a message or HTTP API, carried in its name or route.
Additive changes keep it; removals and meaning changes require a new one.
_Avoid_: schema version, revision

### Warehouse

**Warehouse**:
The player statistics and league table, derived from the raw event store. It is
a projection: it can always be recalculated, never edited by hand.
_Avoid_: data warehouse, read model, cache

**Generation**:
One complete version of the warehouse. Reads use the active generation; a rebuild
writes a new one and switches to it once complete.
_Avoid_: snapshot, version, copy

**Rebuild**:
The recalculation of a whole generation from the raw event store, resumable from
its last checkpoint.
_Avoid_: replay, refresh, reprocessing
