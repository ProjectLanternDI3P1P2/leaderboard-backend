# Audit du template backend .NET — constats consolidés

Révision : `feature/claude-implementation` @ `9a79ef5`. Arbre propre.
Sources : reviewer(DIAGNOSE) C1-C7, artisan-light(archi) R1-R13, artisan-light(ci/cd) F1-F12, testeur(preuve).
Sévérités arbitrées par l'orchestrateur, avec vérification directe des constats P0/P1.

## Santé exécutable (testeur, TEST_PASS)
build 0 warning / 0 erreur · 21/21 tests verts (2,5 s) · `dotnet format` conforme ·
0 package vulnérable ou déprécié (transitives incluses) · SDK 10.0.401 satisfait global.json.
=> Le template n'est pas cassé. Les constats ci-dessous sont de la conception et du process, pas des régressions.

## P0 — à traiter avant de cloner le template pour un 6e service

| # | Constat | Ancrage | Effort |
|---|---|---|---|
| P0-1 | Aucune migration EF committée : le template ne démarre pas end-to-end sans qu'un dev lance `migrations add`. Contredit ADR-0036 et le README qui suppose la migration initiale existante. | `find -iname "*migration*"` = vide hors docs ; README.md:88-93,118-134 | petit |
| P0-2 | Dual-write : publication RabbitMQ AVANT le commit. `AddPlayerAsync` ne fait qu'un `AddAsync` tracké ; `SaveChangesAsync` n'a lieu qu'après `next()`. Commit en échec => événement `player.created.v1` déjà diffusé (mandatory:true) pour un joueur jamais persisté. Aucun outbox. | CreatePlayerCommandHandler.cs:23-27 + CommandTransactionBehavior.cs:18-22 | moyen |
| P0-3 | `sonar.yaml` tourne sur `pull_request` depuis un fork (repo public obligatoire en SonarCloud free) avec `SONAR_TOKEN` exposé pendant `dotnet build`. Seul garde-fou = `github.actor != 'dependabot[bot]'`. Un `<Exec>` MSBuild dans un .csproj de fork exfiltre le token sans toucher au YAML — angle mort de zizmor qui audite le YAML, pas le build. | sonar.yaml:10-15, :33, :59-67 | petit |

P0-2 est le plus structurant : c'est le handler de référence CQRS+messaging, il sera copié tel quel.

## P1 — dette qui se réplique x5

| # | Constat | Ancrage | Effort |
|---|---|---|---|
| P1-1 | `RabbitMqMessagePublisher` est singleton mais ouvre connexion TCP + canal + `ExchangeDeclare` à CHAQUE message. Épuise les connexions du broker sous rafale. Ni publisher-confirms ni DLX malgré ADR-GLOB-002 (SHALL). | RabbitMqMessagePublisher.cs:18-30 | moyen |
| P1-2 | `/health/live` et `/health/ready` strictement identiques, `AddHealthChecks()` nu sans aucun check. Readiness répond Healthy avec Postgres down => le pod garde du trafic. Contredit ADR-0030. | BuilderExtension.cs:15 ; ApplicationExtension.cs:19-20 | petit |
| P1-3 | Checklist de renommage `Combat.*` incomplète : elle omet le Dockerfile (8 occurrences) et sonar.yaml:69,72. Ce repo en est la preuve — il s'appelle leaderboard-backend et tout est encore `Combat.*`. | README.md:235-236 ; Dockerfile:4-10,15,30 | moyen |
| P1-4 | Ni `Directory.Packages.props` ni `Directory.Build.props`. EF Core 10.0.11 répété 9x, Grpc.* 3x. Aucun `TreatWarningsAsErrors`/`EnableNETAnalyzers` ; `_lint` ne vérifie que le whitespace. Chaque service dérive sans que la CI le voie. | absence prouvée par grep ; csproj multiples | faible-moyen |

## P2 — promesses d'ADR non tenues

| # | Constat | ADR | Ancrage |
|---|---|---|---|
| P2-1 | Aucune corrélation propagée, aucun OpenTelemetry. `CorrelationId` = son propre `MessageId`. Impossible de relier un POST à l'événement produit. | GLOB-009 (SHALL) | PlayerCreatedMessageFactory.cs:22-24 |
| P2-2 | Résilience gRPC partielle : deadline oui, retry et circuit breaker absents, aucun Polly. | GLOB-006 (SHALL) | GrpcServiceRegistration.cs:24-27 |
| P2-3 | Aucun scaffold consommateur RabbitMQ (zéro `BasicConsume`/`BackgroundService`), donc aucune référence pour la déduplication exigée par ADR-0025. | GLOB-002, 0025 | grep exhaustif vide |
| P2-4 | Application construit et sérialise le Protobuf (couplage au format fil), alors que le README promet l'inverse côté gRPC. | 0011 | Combat.Application.csproj:9,17 ; PlayerCreatedMessageFactory.cs |
| P2-5 | Supply chain en trompe-l'œil : dependabot sans écosystème `docker`, images en tags flottants alors que zizmor revendique le pinning SHA, Trivy sans restore ni lock file et `exit-code: 0`. | — | dependabot.yml ; Dockerfile:1,20 ; security.yml:17-32 |

## P3 — corrections bon marché
- Masquage PII des logs inerte : `[IgnoreLogging]` posé nulle part, aucun log `{@x}`, la policy n'est jamais sollicitée. Scaffolding trompeur. (IgnoreLoggingDestructuringPolicy.cs:9)
- Pas de règle croisée `Health <= MaxHealth` : `Health=999999, MaxHealth=1` persistable et publiable. (CreatePlayerValidator.cs:20-24)
- Règle FluentValidation morte sur `Id` : `Guid.TryParse(guid.ToString())` ne peut jamais échouer. (CreatePlayerValidator.cs:11)
- `IClock` documenté « jamais DateTimeOffset.UtcNow » mais la factory statique l'appelle en dur. (PlayerCreatedMessageFactory.cs:22,24)
- Dossier `Domain/Exceptions` déclaré et vide ; `KeyNotFoundException` BCL réutilisé faute de hiérarchie domaine.
- Code d'exemple mort : `DamageCalculator`, `CombatStatus`, zéro référence.
- `back-merge.yaml` : `gh pr merge --auto` sans détection de conflit => dérive dev/main silencieuse. (back-merge.yaml:38-51)
- `.dockerignore` sans `.git/` alors que Dockerfile:14 fait `COPY . .` (atténué : le multi-stage ne l'embarque pas dans l'image finale).
- Secrets en clair dans compose.yaml (assumé/loopback, mais contredit ADR-GLOB-008 et sera copié).
- `Combat.Contracts/README.md:11` annonce 0.1.1 contre 2.0.0 réel dans le manifest.

## Conflit inter-agents arbitré
reviewer jugeait `.dockerignore` SAIN, recon-cicd le signalait (F10). Vérification orchestrateur :
`.dockerignore` n'a aucune entrée `.git` et Dockerfile:14 fait `COPY . .` => recon-cicd a raison.
Atténuation : multi-stage, le runtime ne prend que `/app/publish`. Sévérité BASSE retenue.

## Point NON vérifié (hypothèse à confirmer)
Le SDK Web globe `appsettings*.json` en Content. Un `appsettings.Local.json` de poste dev
(ignoré par git, non exclu par `.dockerignore`) pourrait atterrir dans `/app/publish` et donc
dans l'image finale. Non prouvé par cet audit — à confirmer par un build réel avant d'agir.

## Ce qui est solide
Domain pur sans dépendance EF/ASP.NET. CQRS par feature cohérent. ProblemDetails/422 conforme.
Mapping gRPC par convention réellement fonctionnel. Options typées toutes validées au démarrage.
Absence d'auth locale conforme à ADR-GLOB-004 (décision assumée, pas un trou). gRPC loopback vérifié.
Middleware ne fuit ni stack trace ni type d'exception en prod. Aucun SQL brut, aucune captive dependency,
`CancellationToken` propagé de bout en bout. Actions toutes pinnées par SHA, `persist-credentials: false`,
permissions minimales, aucun `continue-on-error`, aucun filtre `paths:` sur un check requis.
Dockerfile multi-stage non-root. 47 ADR denses et à jour. Lacune des tests d'intégration
honnêtement documentée et assumée, pas dissimulée.
