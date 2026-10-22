# US-COLLECTE-01 — bilan de validation

Le contrat central est défini dans ce dépôt pour intégration par les squads productrices. Adoption, implémentations externes, publication de package NuGet et déploiement Kubernetes ne sont pas revendiqués. Les JSON sous [`docs/fixtures/us-collecte-01`](../fixtures/us-collecte-01/manifest.json) décrivent les exemples; le protocole transport reste Protobuf binaire. Les détails sont dans [contrat commun](contrat-commun.md) et les cinq fiches producteur.

## Catalogue des événements et contrat

Les 16 routes sont documentées (Dungeon 3, Combat 5, Player 3, Rewards 3, Progression 2), avec champs, déclencheurs et fixtures. Contrat `leaderboard.collection.v1`, namespace C# `Leaderboard.Contracts.Collection.V1`, distinct de l'ancien contrat `leaderboard.events.v1`. Les squad validations externes restent à obtenir.

## AC1 — archiver exactement le payload reçu

Le corps AMQP est copié puis archivé octet pour octet, sans parsing/résérialisation dans le chemin d'archivage. Les tests `CopiesOriginalBody...`, `BrokerToMongo...` et `EveryCatalogueRoute...` couvrent la copie exacte, le trajet broker vers Mongo et les routes du catalogue. Les fixtures sont des exemples descriptifs; le producteur doit publier le binaire protobuf.

## AC2 — transaction, date de réception et squad

Le reçu conserve `TransactionId`, `ReceivedAt` généré côté collecteur et `ProducerSquad` comme métadonnées séparées du corps. `ArchiveEventTests` et les tests d'intégration couvrent ces champs et le statut de validation associé. `ReceivedAt` représente la réception, pas `occurred_at`.

## AC3 — arrêt avant insert et reprise

Le message n'est ACK qu'après l'insertion Mongo acquittée. Les scénarios `StopBeforeWrite...` et `ChildProcessCrashBeforeInsert...` vérifient l'arrêt avant écriture puis la redélivrance/reprise sans ACK anticipé. Les doublons de livraison sont conservés en reçus bruts distincts.

## Contrôles techniques

Implémentation locale RabbitMQ vers MongoDB présente : octets originaux append-only, statut/erreurs de validation conservés pour enveloppes invalides/inconnues/illisibles, doublons non filtrés, ACK après insert avec write concern configuré; retries espacés et quarantaine durable. Topologie déclarée par l'application. Voir [configuration](../deployment-configuration.md) et [OPS](../ops/README.md).

À la révision source R4 `b9404eff29b2633da23f031d76c3553107457cb26216cec159489517ab1bcbee`, revue indépendante acceptée. CSharpier: 67 fichiers réussis. Build Release séparé: 0 avertissement, 0 erreur. Tests: 88 réussis, 0 échec, 0 ignoré (la commande intégrée avec flags MSBuild avait échoué au handshake MTP sans démarrer les tests; la séquence build puis test `--no-build` a réussi). Construction `docker compose build api` réussie. Les tests couvrent notamment les 16 routes, réceptions réelles, octets exacts, ACK après insert, redémarrage/reprise et quarantaine. RabbitMQ 4.3.6 et MongoDB 8.0.34. Aucune exécution CI distante revendiquée.

## AC4 — charge soutenue NON VALIDÉE

Seuls les critères p95 fin <= p95 début et pente <=0 ms/min sont explicitement confirmés par l'utilisateur. Le banc applique aussi son seuil de ponctualité de cadence <=200 ms.

R3 a été exécuté 600 s: 30 000 publiés, confirmés et stockés, 8 190 000 octets identiques, zéro mismatch, erreur metadata, backlog ou DLQ. p95: 32.3356→27.9661 ms; pente -0.3506569697 ms/min. Le run est néanmoins **FAIL**, retard maximal de publication 2592.7408 ms (>200 ms). Rapport: [`6fdb38fc-d57e-44d9-b857-7033395375bd.json`](../../scripts/collection/results/6fdb38fc-d57e-44d9-b857-7033395375bd.json).

R4 a été interrompu à la demande de l'utilisateur vers trois minutes (dernier état: 9000 envoyés, 8999 confirmés, un en attente). Arrêt des processus confirmé; aucun rapport final R4. Le critère complet 50/s pendant 600 s demeure non validé. Waiver/arrêt utilisateur enregistré séparément, jamais comme PASS. Aucun nouveau run long sans demande explicite.
