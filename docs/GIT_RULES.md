# Git rules

How a change travels from a developer's machine to a release. The reasons behind
each rule are in the ADRs linked below; this page is the checklist.

## Branches

| Branch | Role |
| --- | --- |
| `dev` | Default branch. Every pull request targets it. |
| `main` | Released code. Only receives the promotion pull request from `dev` and the release pull request from release-please. |
| `feature/…`, `fix/…`, `chore/…`, `docs/…` | Working branches, created from `dev`. The prefix is a convention, not enforced. |

Never push directly to `dev` or `main`: both are protected.

```text
feature/xxx --merge commit--> dev --merge commit--> main --> tag + CHANGELOG
                               ^                      |
                               +----- back-merge -----+
```

## Merging

- **Merge commit only**, on `dev` and on `main`. No squash, no rebase merge
  ([ADR-0002](./adr/0002-merge-strategy-depends-on-the-target-branch.md)). Every
  commit keeps its author and its own line in the history.
- Enforced by a ruleset on both branches
  ([ADR-0007](./adr/0007-merge-methods-enforced-by-rulesets.md)).
- To update a branch with the latest `dev`, prefer `git pull --rebase origin dev`.
  A merge from `dev` is accepted, but it adds noise to the history.
- After a release, `main` is merged back into `dev` automatically
  ([ADR-0003](./adr/0003-automatic-back-merge-from-main-to-dev.md)).

## Commit messages

[Conventional Commits](https://www.conventionalcommits.org), because every commit
reaches `main` and release-please builds the changelog and the version from them.

```text
<type>(<optional scope>)<optional !>: <subject>
```

| Type | Changelog | Version |
| --- | --- | --- |
| `feat` | Added | minor |
| `fix` | Fixed | patch |
| `perf` | Performance | patch |
| `refactor` | Changed | patch |
| `revert` | Reverted | patch |
| `docs`, `test`, `chore`, `ci`, `build`, `style` | hidden | none |

A `!` after the type, or a `BREAKING CHANGE:` footer, is a breaking change: major
bump. Hidden types alone never trigger a release.

- Header at most 100 characters. Scope free. Subject case free.
- Messages written by git itself (`Merge …`, `Revert "…"`, `fixup! …`) are not
  checked.

Examples: `feat(player): expose level over gRPC`, `fix: reject empty names`,
`build(deps): bump Serilog`.

## Local hooks

Run once per clone: `dotnet tool restore` then `dotnet husky install`.

| Hook | Does |
| --- | --- |
| `pre-commit` | `dotnet csharpier format` on staged `.cs` files, then re-stages them. |
| `commit-msg` | Rejects a message that is not a Conventional Commit. |

Hooks can be skipped with `--no-verify`. CI runs the same checks and cannot be.

## Pull request checks

| Check | Required |
| --- | --- |
| `Lint / CSharpier` | yes |
| `Test / dotnet test` | yes |
| `Build / dotnet build` | yes |
| `Commitlint` | yes |
| `Trivy Security Scan` | yes (reports, never fails) |
| `GitHub Actions audit` (zizmor) | yes |
| `SonarQube Cloud scan` | **no**, skipped on Dependabot pull requests |

`dev` requires branches to be up to date before merging.

## Releases

1. Open a pull request from `dev` to `main`, merge it with a merge commit.
2. release-please opens or updates `chore(main): release X.Y.Z` on `main`.
3. Merging it writes `CHANGELOG.md`, bumps the version and tags `vX.Y.Z`.
4. The back-merge workflow merges `main` into `dev`.
