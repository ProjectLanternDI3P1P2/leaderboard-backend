# Brief — Audit du template backend .NET

## Demande
"En tant qu'orchestrateur tu vas analyser le template et me remonter les potentiels
problèmes de ce template et les axes d'amélioration."

## Nature
Audit **read-only**. Aucune modification de code source attendue. Livrable = rapport
de constats (problèmes) + axes d'amélioration, priorisés.

## Objet audité
Repo `leaderboard-backend`, branche `feature/claude-implementation`. C'est une copie du
template partagé `dotnet-backend-template` (ProjectLanternDI3P1P2) :
- .NET 10, Clean Architecture 6 projets (Domain / Application / Infrastructure /
  Presentation / Contracts / Test), solution `.slnx`.
- CQRS + MediatR 14, FluentValidation 12, EF Core 10 + Npgsql, Serilog, gRPC (Grpc.AspNetCore 2.76),
  RabbitMQ.Client 7, Scalar pour la doc API, Scrutor pour le DI scanning.
- CI GitHub Actions : ci / _build / _lint / _test / sonar / security(zizmor) / commitlint /
  release-please / back-merge / publish-contracts.
- Docker + compose, husky (commit-msg, pre-commit), dependabot, 36 ADR + 11 ADR-GLOB.

Enjeu particulier : c'est un **template**. Chaque défaut se réplique dans les 5 microservices
(ADR-0008, ADR-0012, ADR-0013 : pas de librairie partagée, le template est le SEUL vecteur
de standardisation). Le coût d'un défaut est donc multiplié par 5 et difficile à corriger a posteriori.

## Critères d'acceptation du rapport
1. Constats **vérifiés dans le code réel** (chemin fichier + ligne), pas des généralités.
2. Chaque problème : sévérité, impact concret, effort de correction.
3. Distinction nette : bug/faille réelle vs dette vs préférence de style.
4. Couverture : sécurité, correction, architecture/dérive ADR, tests, CI/CD & supply chain, DX, docs.
5. Preuve exécutable de l'état de santé : le template build-il et ses tests passent-ils ?

## Périmètre
Tout le repo SAUF `.claude/**` et `CLAUDE.md` (outillage local de l'utilisateur, non versionné).

## Hors périmètre
Implémentation de correctifs. Toute correction sera un workflow séparé, sur décision de l'utilisateur.
