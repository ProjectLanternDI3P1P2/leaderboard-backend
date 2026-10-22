# Enforce formatting with CSharpier

Backend repositories use CSharpier and verify formatting automatically in CI and
before commits.

## Considered Options

Formatting rules could be documented and left to individual developers and IDEs.

With roughly thirty developers, different IDE defaults and incomplete manual
adherence would produce unnecessary formatting differences and review noise.

A machine-enforced formatter makes the repository configuration the single source
of truth instead of relying on every contributor to reproduce the same local
settings. CSharpier deliberately has few configuration options, preventing style
rules from becoming subjective policy.

## Consequences

Formatting differences are detected automatically before or during CI.

Developers may use different IDEs while producing the same repository style.

Formatting rules should remain minimal and should not be expanded into subjective
style policies without a shared decision.
