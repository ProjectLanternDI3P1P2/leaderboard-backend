# Version HTTP APIs in the URL

HTTP API major versions are part of the route. Client-facing APIs owned by a
service use `/api/<service>/v1/...` when the gateway routes by service prefix.
Player uses `/api/player/v1/...` and Leaderboard uses
`/api/leaderboard/v1/...`. Existing `/api/v1/<resource>` routes in other
services may remain until their gateway routes and clients are migrated.

## Considered Options

Header-based versioning and content negotiation were considered possible
alternatives.

URL versioning makes the selected contract immediately visible in requests,
logs, documentation and manual tests. A service prefix gives the gateway a
stable route even when several services expose resources with the same name.

Headers would hide the version from the most visible part of the request without
providing a useful benefit for this project.

## Consequences

Backward-compatible additions remain in the current major API version.

Breaking contract changes require a new major route such as
`/api/leaderboard/v2`.

Multiple versions may temporarily coexist when compatibility requires it.

Gateway aliases may preserve older external paths while forwarding to the
service-owned route. The alias is a routing concern, not a second business API.
