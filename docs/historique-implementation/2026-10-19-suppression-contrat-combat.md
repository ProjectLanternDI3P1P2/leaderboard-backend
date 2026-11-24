# Suppression du contrat Combat et validation locale — 2026-10-19

## Changement

Suppression de `Combat.Contracts` dans Application, Infrastructure et Presentation. Retrait des exemples qui l'utilisaient : client/serveur gRPC Player, intercepteurs et enregistrement gRPC, fabrique `combat.player.created`, enveloppe et publisher RabbitMQ Combat, abstractions inutilisées et test gRPC. Le test du handler de création est adapté à la seule persistance. Les exemples de calcul/statut de combat sont retirés.

L'API REST Player et sa persistance PostgreSQL restent disponibles pour vérifier le template. Le contrat local Leaderboard et le catalogue métier ne sont pas remplacés.

Restauration NuGet limitée à nuget.org ; retrait du secret de build, de son fichier Compose optionnel, de la configuration GitHub Packages dans les jobs de restauration CI et du port gRPC 8081. README et documentation de déploiement alignés. Aucun jeton requis pour construire le projet actuel.

## Validation exécutée

- [x] `dotnet build Leaderboard.Presentation.slnx --configuration Release` : succès, **0 avertissement, 0 erreur**.
- [x] `dotnet test --solution Leaderboard.Presentation.slnx --configuration Release --no-build` : **22 réussis, 0 échec, 0 ignoré**, incluant l'intégration REST sur PostgreSQL réel.
- [x] `dotnet csharpier check .` : **52 fichiers vérifiés** ; `git diff --check` sans erreur.
- [x] `docker compose build api` : image construite sans secret ni package Combat.
- [x] `docker compose up -d --no-build --wait` : API démarrée ; PostgreSQL et RabbitMQ healthy.
- [x] `powershell -NoProfile -File scripts/Test-ContainerCommunication.ps1` : liveness HTTP 200, création HTTP 201 et relecture REST, exactement une ligne SQL correspondante, publication/routage/lecture RabbitMQ avec payload inchangé.

Le script utilise l'API de gestion RabbitMQ indépendamment de l'application ; aucun consommateur ou publisher applicatif n'est implémenté après ce nettoyage. Il ne valide ni les futurs contrats ni l'outbox. La file temporaire est supprimée ; le joueur de test `9dd76108-d80f-431f-b9be-71fddb808778` reste dans la base locale.

## État laissé

API : http://localhost:8080 ; Scalar : http://localhost:8080/scalar.
PostgreSQL : `127.0.0.1:5433`, base/utilisateur `leaderboard`, volume conservé.
RabbitMQ : `127.0.0.1:5672`, gestion http://localhost:15672, identifiants locaux `leaderboard` / `leaderboard`.

MongoDB de `US-DATA-01` ne fait pas partie du Compose actuel et n'a pas été testé. Aucun volume supprimé, aucun commit créé.
