# US-COLLECTE-01 — suivi de livraison

## Résultat fonctionnel

Le dépôt Leaderboard définit le format central `leaderboard.collection.v1`, les fiches des cinq squads, les fixtures JSON descriptives, ainsi que la réception RabbitMQ et l'archivage brut MongoDB. Voir [catalogue](../US-COLLECTE-01-catalogue-evenements.md) et [index contrat](../collection/README.md). Le format est prêt à intégrer; l'adoption/validation externe reste à obtenir. Aucun XP ni classement n'est calculé par la collecte.

## Validation technique disponible

La révision R4 a passé une revue indépendante et les contrôles suivants : CSharpier sur 67 fichiers; `dotnet build Leaderboard.Test/Leaderboard.Test.csproj --configuration Release --maxcpucount:1 -p:UseSharedCompilation=false` (0 avertissement, 0 erreur); `dotnet test --project Leaderboard.Test/Leaderboard.Test.csproj --configuration Release --no-build --minimum-expected-tests 78` (88 réussis, 0 échec, 0 ignoré); `docker compose build api` réussi. Les tests couvrent notamment 16 routes, 17 réceptions, octets exacts, ACK après insert, reprise et quarantaine avec RabbitMQ 4.3.6 et MongoDB 8.0.34. CI distante non revendiquée.

## AC4 — charge réelle

Critères p95 explicitement confirmés par l'utilisateur : p95 publication-vers-insertion Mongo de la dernière minute <= première minute et pente <=0 ms/min sur 600 s à 50/s. Le seuil de retard de cadence <=200 ms est un seuil propre au banc, non attribué à l'utilisateur.

R3 a été exécuté 600 s : 30 000 publiés, stockés et confirmés, 8 190 000 octets comparés sans anomalie ni backlog/DLQ; p95 32.3356→27.9661 ms et pente -0.3506569697 ms/min. Le résultat complet est FAIL: retard maximal de publication 2592.7408 ms (>200). Rapport dans `scripts/collection/results/6fdb38fc-d57e-44d9-b857-7033395375bd.json`.

R4 a utilisé une émission cadencée et des confirms asynchrones bornés (256 en attente, timeout 30 s), seuils inchangés. À la demande de l'utilisateur, il a été arrêté vers la minute 3 : dernier instantané 9000 envoyés, 8999 confirmés, un en attente; arrêt des processus confirmé, aucun rapport final. Le critère complet demeure non validé. L'utilisateur a demandé de ne pas relancer de test long; ce choix est enregistré séparément, pas comme PASS.

## Historique des décisions conservées

- `combat-completed` décrit victoire et défaite; Dungeon acceptance requise dans les deux cas, Rewards confirmation seulement pour victoire. Aucun calcul ni attribution XP par collecte.
- Départ lobby est un fait métier, non une déconnexion; clan figé au moment de l'occurrence; mana nul explicite valide; transition d'étage répétée reçoit un nouvel ID.
- Item instancié: quantity 1. L'événement de gain d'or indique explicitement le portefeuille joueur ou héros.
- Extensions reportées: sélection de héros, équipement retiré, dépense d'or, coffre; ne pas les inclure dans les 16 routes.
- L'outbox transactionnelle, les confirms, le routage non livré et retries à identité/octet stables sont exigences pour les squads, pas preuve de leur implémentation.
- Le dépôt brut conserve les doublons et les corps exacts; projection seule peut dédupliquer; aucun TTL/purge n'est configuré.

## Limites restantes

- Obtenir l'adoption/validation des producteurs externes; aucune implémentation, package NuGet ou déploiement Kubernetes externe n'est revendiqué.
- AC4 600 s reste non validé, avec waiver utilisateur séparé; ne pas déclarer l'acceptation complète.
- Implémentation locale documentée : voir [notes OPS](../ops/README.md), [configuration](../deployment-configuration.md) et [banc de charge](../../scripts/collection/README.md).
- AC1–AC3 et leurs preuves détaillées : [bilan de validation](../collection/validation-us-collecte-01.md).

## Relais restant

- [ ] Obtenir adoption/validation des cinq squads productrices, notamment la sémantique métier et l'intégration du format central; l'attente n'empêche pas la disponibilité locale du contrat.
- [ ] Confirmer lors de l'intégration que chaque producteur persiste changement + outbox atomiquement, publie après commit avec confirms et gère les retours non routables, puis reprend les mêmes octets/MessageId.
- [ ] OPS confirme DNS `rabbitmq.rabbitmq.svc.cluster.local`, vhost, comptes, secrets et permissions de déclaration/consommation; fournit URI Mongo, TLS, permissions, capacité et sauvegardes.
- [ ] Tout nouvel essai de charge de dix minutes requiert une demande explicite future. Un run interrompu sans rapport final n'est pas une validation.
- [ ] Les extensions métier listées au catalogue demeurent hors contrat jusqu'à définition du besoin et du propriétaire.
