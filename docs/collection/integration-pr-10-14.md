# Intégration des contrats, du journal MongoDB et du warehouse

## Décisions retenues

- Contrat de référence : `leaderboard.collection.v1.GameEventEnvelope` et ses 16 événements.
- Aucun historique de données à migrer et aucune compatibilité runtime avec l'ancienne enveloppe Combat.
- Le corps AMQP complet est conservé octet pour octet, y compris l'enveloppe.
- Les messages invalides, inconnus et illisibles sont archivés avec statut/erreurs, puis exclus des statistiques.
- Le classement reste basé sur le nombre d'événements par joueur. Les sommes sont séparées par type d'événement.
- Aucun total global n'additionne des unités différentes.

## Responsabilités conservées

| Contribution | Composants |
|---|---|
| US contrats | Protobuf, `GameEventContract`, exemples JSON, tests des fixtures et documentation des cinq squads |
| PR #10 | `RabbitMqRawEventConsumer`, traitement par lots/retries, `IRawEventStore`, `MongoRawEventStore`, schéma/index et tests d'immuabilité |
| PR #14 | Warehouse PostgreSQL, transactions, checkpoints, déduplication, reconstruction par générations, endpoints et tests |

Le second collecteur `Infrastructure/Collection`, son dépôt Mongo, son interface et
son programme de charge ont été retirés. Les contrats métier sont conservés.
Les anciens résultats du banc sont historiques, sans valeur de validation pour la nouvelle chaîne.
Le package `leaderboard.events.v1` reste défini comme auparavant, mais aucun consommateur
ou décodeur de la chaîne intégrée ne l'utilise.

## Adaptations du journal brut

`RawEvent.Payload` et le champ Mongo `payload` contiennent désormais le corps reçu complet.
Le consommateur extrait transaction, MessageId, producteur, type/version, corrélation
et date métier de `GameEventEnvelope`. Il ajoute la date de réception, l'exchange,
la clé de routage, le MessageId AMQP et le marqueur de redélivraison.

Les métadonnées impossibles à extraire restent nulles ; elles ne sont pas inventées.
`parseStatus` vaut `Valid`, `InvalidOrUnknown` ou `Unreadable` ; `validationErrors`
conserve les diagnostics. Le schéma Mongo accepte ces reçus tout en imposant
la présence des octets, de la date de réception et du statut.

Les écritures conservent majority + journal et l'ACK après confirmation. Chaque tentative
après une erreur reçoit de nouveaux identifiants de reçu, car une tentative partielle
peut déjà avoir inséré des documents. Les MessageId et les octets restent identiques.
Les files quorum conservent un dead-lettering at-least-once sans TTL.

## Projection des 16 événements

Les événements de groupe (`dungeon-run-started`, `floor-entered`, `dungeon-run-ended`,
`combat-started`, `combat-ended`, `combat-completed`) comptent une contribution pour
chaque joueur participant distinct. Les dix autres comptent pour le joueur de `actor`.
Deux héros d'un même joueur dans un événement ne doublent pas sa contribution.
`combat-ended` et `combat-completed` sont deux faits distincts, comptés sous deux types distincts.

| Type | Valeur additionnée par joueur et par type | Unité |
|---|---|---|
| `combat.skill-used` | `mana_spent` | points de mana dépensés |
| `combat.consumable-used` | `quantity` | objets consommés |
| `rewards.item-granted` | `quantity` | objets reçus |
| `rewards.gold-earned` | `amount` | unités d'or reçues |
| `progression.xp-awarded` | `amount` | points d'XP reçus |
| `progression.level-up` | `new_level - previous_level` | niveaux gagnés |
| Les dix autres types | 0 ; seul le nombre d'événements augmente | aucune somme métier |

`TotalActions` compte les contributions par joueur. `PlayerActionStatistics.ValueTotal`
conserve les sommes par type en `decimal`/PostgreSQL `numeric`, y compris les grands uint64
sans conversion en double. Aucune attribution d'XP ni modification de portefeuille n'est faite.
Le champ global `valueTotal` a été retiré des statistiques joueur et du classement HTTP.
La migration initiale warehouse et son snapshot sont adaptés, conformément au démarrage sans historique.

La déduplication PostgreSQL reste par `messageId` et génération. Elle s'applique à
l'événement complet avant ses contributions aux participants. Les compteurs de rapport
restent des compteurs d'événements, pas de joueurs : traités = appliqués + doublons + ignorés.
Les checkpoints et l'activation atomique de génération de la PR #14 sont conservés.

## Validation

Voir [bilan courant](validation-us-collecte-01.md). La validation d'intégration couvre
les 16 routes, les octets exacts, les métadonnées, les messages invalides, la reprise
avant ACK avec/sans insertion déjà réalisée, la quarantaine, la déduplication d'événements
à plusieurs joueurs, les sommes par type et les droits Mongo sans update/delete.
Les tests de reconstruction de la PR #14 restent présents, avec des fixtures du nouveau contrat.
Aucun test de charge prolongé n'est lancé sans demande explicite.
