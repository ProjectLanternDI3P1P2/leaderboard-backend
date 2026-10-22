# Choix du broker asynchrone à 100 000 joueurs simultanés

> Note de décision et de pré-dimensionnement. Les calculs sont des hypothèses
> applicatives, et non des résultats de benchmark. Sources consultées le 22
> septembre 2026.

## Décision : RabbitMQ est retenu

**Le projet retient RabbitMQ avec des quorum queues pour les flux asynchrones
vers Progression.** Cette décision confirme
[ADR-GLOB-002](../adr/ADR-GLOB-002-rabbitmq-as-message-broker.md) et couvre le
scénario de 100 000 joueurs simultanés avec la topologie shardée définie ici.

Le choix est justifié par le besoin réel : des faits métier courts, routés vers
un nombre limité de consommateurs, traités de manière idempotente, avec retry,
dead-lettering et rétention courte. Kafka résout très bien la rétention longue,
le replay et les nombreux lecteurs indépendants, mais ces capacités ne sont pas
des exigences du produit actuel et alourdiraient son exploitation.

Le nombre de joueurs connectés n'est pas une unité de capacité broker : la
décision s'appuie sur le débit, la taille, le fan-out, la persistance et le lag
acceptable. Le benchmark de validation ne remet pas ce choix en concurrence ;
il dimensionne le cluster RabbitMQ et vérifie le SLO avant production.

## 1. Ce qui passe par le broker

Les commandes restent synchrones via SignalR/gRPC. Le broker ne reçoit que des
faits validés et écrits dans une outbox.

| Famille | Exemples | Statut |
|---|---|---|
| Action Combat | `combat.action-resolved.v1` : attaque, compétence, défense, passe, potion | Un fait par action acceptée et résolue |
| Cycle Combat | début et fin de combat | Événements statistiques |
| Cycle session/run | session, salle, coffre, fin de run | Événements statistiques |
| Récompense/inventaire | reward, consommation, équipement, achat | Événements statistiques |
| Snapshot, reconnect, commande refusée, rediffusion SignalR | `CombatStateChanged`, lecture de combat | **Exclus** : pas de valeur métier, volume inutile |

## 2. Modèle de calcul

| Symbole | Signification |
|---|---|
| `N` | Joueurs simultanés |
| `f` | Fraction en combat |
| `a` | Actions résolues par joueur en combat et par seconde |
| `o` | Autres faits statistiques par seconde |
| `P` | Publications/s = `N × f × a + o` |
| `s` | Taille sérialisée moyenne, enveloppe incluse |
| `B` | Octets applicatifs entrants/s = `P × s` |

La taille à mesurer est le Protobuf réellement sérialisé avec son enveloppe
(`messageId`, corrélation, causation, version et horodatage), pas l'objet C#.

| Profil de message | Taille de planification |
|---|---:|
| Identifiants, action, compteurs/dégâts | 0,5 KiB |
| Cible : enveloppe, cibles et effets agrégés | 0,75 KiB |
| Snapshot, liste non bornée, inventaire complet | > 4 KiB — interdit pour les statistiques |

Le nombre d'événements/s et les MiB/s doivent être suivis ensemble. Les petits
messages augmentent les messages/s, les grands messages déplacent le goulot vers
le disque et le réseau. RabbitMQ recommande de benchmarker le workload réel :
taille, clients, acks et confirms changent fortement le résultat
[How to run benchmarks](https://www.rabbitmq.com/blog/2020/06/04/how-to-run-benchmarks).

## 3. Calculs à 100 000 joueurs

Les chiffres utilisent 0,5 puis 0,75 KiB sans compression, avant protocole,
réplication, retry, dead-letter et livraisons consommateurs.

| Scénario | Hypothèse | `P` | à 0,5 KiB | à 0,75 KiB |
|---|---|---:|---:|---:|
| Calme | 1 fait/joueur/minute | 1 667/s | 0,81 MiB/s | 1,22 MiB/s |
| Jeu régulier | 1 fait/joueur/10 s | 10 000/s | 4,88 MiB/s | 7,32 MiB/s |
| Combat réaliste | `N=100 000`, `f=30 %`, `a=1`, `o≈3 500/s` | 33 500/s | 16,36 MiB/s | 24,54 MiB/s |
| Pic extrême | tous les joueurs, une action/s, `o≈5 000/s` | 105 000/s | 51,27 MiB/s | 76,90 MiB/s |

```text
actions Combat/s = 100 000 × 0,30 × 1 = 30 000
autres faits/s     ≈ 3 500
publications/s     = 33 500
octets/s à 0,75 KiB = 33 500 × 768 = 25 728 000 B/s = 24,54 MiB/s
```

Une quorum queue à trois réplicas persiste et réplique les messages avant le
confirm de majorité. Le fan-out, les confirmations, les acks et les replays
ajoutent du travail : dimensionner sur p95/p99 et lag de bout en bout, jamais
sur le seul débit producteur.

## 4. Démonstration du choix RabbitMQ

| Critère du projet | Constat chiffré ou fonctionnel | Pourquoi RabbitMQ répond au besoin |
|---|---|---|
| Débit nominal | 33 500 publications/s au scénario Combat réaliste | Huit shards limitent la moyenne à 4 188/s/shard, très en dessous du repère directionnel de 80 000/s pour une quorum queue. |
| Pic à absorber | 105 000 publications/s | Huit shards limitent la moyenne à 13 125/s/shard ; le cluster est validé par test de pointe et de panne, sans changer de modèle de broker. |
| Types de messages | Petits faits Protobuf de 0,5 à 0,75 KiB ; snapshots exclus | Les queues, exchanges, bindings et backpressure RabbitMQ sont précisément adaptés à ce routage de messages. |
| Consommateurs | Progression est le consommateur principal ; Player/Rewards reçoivent seulement certains faits | Une file durable par consommateur isole les vitesses et les pannes sans introduire une plateforme de streaming. |
| Fiabilité | Outbox, confirms, ack manuel, idempotence, retry et DLQ | Les quorum queues apportent réplication majoritaire et confirms ; la déduplication applicative couvre l'at-least-once. |
| Ordre nécessaire | Ordre seulement par héros/joueur, pas d'ordre global | Le hash stable vers un shard préserve la séquence métier sans imposer une partition globale. |
| Exploitation | RabbitMQ est déjà l'ADR accepté et le broker prévu dans les services | Kafka ajouterait topics, partitions, rétention, offsets et rebalancing sans besoin fonctionnel correspondant. |

Le repère de 80 000/s est une mesure publiée par RabbitMQ pour **une** quorum
queue et ne doit pas être multiplié mécaniquement par huit : les shards partagent
les ressources du cluster. Il démontre néanmoins que le débit par shard prévu
(4 188/s nominal, 13 125/s au pic) est d'un ordre de grandeur compatible avec le
modèle quorum. La preuve de capacité du déploiement reste le benchmark avec le
matériel, les schémas et les garanties réelles du projet.

## 5. RabbitMQ : capacité et topologie

Les quorum queues sont des files durables, répliquées et sûres. Avec trois
membres elles tolèrent un nœud perdu ; les publisher confirms arrivent après
réplication à une majorité. RabbitMQ déconseille les grands fan-out et les très
longs backlogs (ordre de millions), cas où Streams est plus adapté
[Quorum queues](https://www.rabbitmq.com/docs/quorum-queues).

La comparaison officielle RabbitMQ/Kafka donne l'ordre de grandeur de **80 000
messages/s pour une quorum queue**, en répliquant et `fsync`ant chaque message.
Ce n'est pas une garantie de dimensionnement : matériel, taille, confirms, acks
et topologie changent la capacité [RabbitMQ vs Kafka](https://www.rabbitmq.com/docs/compare/kafka).

| Charge | Une seule quorum queue Progression | Conclusion |
|---|---|---|
| 1 667–10 000/s | A priori confortable | RabbitMQ quorum queues |
| ~33 500/s | Plausible, mais à mesurer | RabbitMQ shardé + test de soak |
| ~105 000/s soutenues | Réparties sur huit shards | RabbitMQ shardé, validé par benchmark de pointe |

Le plafond opérationnel du projet est **50 % de la capacité durable mesurée**,
pas 80 000/s. Cette marge interne absorbe panne, replay, pic et croissance.

Topologie proposée :

1. Exchange topic durable `game.events.v1`.
2. Huit quorum queues `progression.stats.0` à `progression.stats.7`, trois
   réplicas chacune.
3. Shard stable = hash de `HeroId`, sinon `PlayerId` ; ordre garanti par clé,
   jamais globalement.
4. Outbox, publisher confirms, ack manuel après historique Progression,
   déduplication `messageId` + clé métier, retry borné et DLQ.

Huit shards répartissent environ 33 500/s en 4 188/s/shard et 105 000/s en
13 125/s/shard, avant déséquilibre de clé. Ils évitent un leader unique et ne
remplacent pas le test. Ne pas créer une queue par joueur/combat/session : la
documentation conseille de revoir la topologie au-delà d'environ 5 000 quorum
queues [Quorum queues](https://www.rabbitmq.com/docs/quorum-queues).

## 6. Pourquoi Kafka et Streams ne sont pas retenus maintenant

Kafka est un journal partitionné et rejouable. L'ordre est garanti dans une
partition, et un consumer group confie une partition à un seul consumer. Des
groupes indépendants peuvent relire le même topic : c'est préférable si
Progression, BI, anticheat et data warehouse doivent conserver/rejouer le même
historique à des vitesses différentes [Apache Kafka Design](https://kafka.apache.org/41/design/design/).

Kafka ne publie pas une capacité universelle en messages/s. Son débit dépend des
partitions, de la taille en octets, du batching, de la compression, de la
réplication et du matériel. Le producer batche par partition ; batching et
compression améliorent le débit avec un compromis de latence
[Kafka Producer configuration](https://kafka.apache.org/40/configuration/producer-configs/).

| Besoin dominant | RabbitMQ quorum queues | Kafka |
|---|---|---|
| Routage, ack/retry/DLQ vers quelques services | Excellent | Possible, plus lourd |
| Rétention/replay semaines ou mois | Faible adéquation | Excellent |
| Nombreux consommateurs indépendants | Une file par consommateur augmente le fan-out | Naturel via consumer groups et offsets |
| Grand backlog / fan-out | Préférer Streams ou Kafka | Natif |
| MVP actuel | Déjà retenu et plus simple à opérer | Plus de sujets : partitions, rétention, offsets |

Kafka ne supprime pas l'idempotence de la base Progression : l'exactly-once est
principalement un mécanisme Kafka-vers-Kafka ; une projection vers une base
externe demande toujours coordination ou déduplication applicative
[Apache Kafka Design](https://kafka.apache.org/41/design/design/).

RabbitMQ Streams est le compromis : journal persistant, répliqué et rejouable,
adapté aux gros fan-out, replays et gros backlogs. Les super streams ajoutent le
partitionnement [Streams and Superstreams](https://www.rabbitmq.com/docs/streams).

## 7. Benchmark de validation RabbitMQ

Le test emploie les schémas Protobuf, outbox, confirmations et écritures
Progression réelles ; un générateur synthétique seul ne suffit pas.

| Étape | Charge | Acceptation |
|---|---|---|
| Baseline | 10 000/s, 30 min | backlog stable, aucune DLQ, p99 mesuré |
| Cible | 33 500/s, 2 h | lag stable, CPU/disque/réseau sous budget |
| Pointe | 105 000/s, 15 min | backpressure contrôlée, aucune perte |
| Soak | cible, 24 h | aucune dérive mémoire, disque, backlog ou p99 |
| Panne | perte d'un nœud et restart consumer | zéro double XP, rattrapage dans le SLO |

Mesurer : publications/s et MiB/s par shard, confirm latency p50/p95/p99,
taille/âge du backlog, redelivery/DLQ, latence outbox→projection, disque,
réseau, CPU, GC et connexions. Pour RabbitMQ, utiliser SSD/NVMe local stable :
la checklist de production recommande un stockage local rapide pour quorum
queues et Streams [RabbitMQ Production checklist](https://www.rabbitmq.com/docs/production-checklist).

## 8. Conditions de réévaluation futures

RabbitMQ est le choix de cette architecture. Il sera réévalué seulement si le
produit demande une rétention/replay comme fonctionnalité, au moins trois
consommateurs analytiques indépendants, un backlog normal de millions de
messages, ou si les tests prouvent qu'un cluster RabbitMQ correctement shardé
ne respecte pas le SLO. Ces conditions décrivent une évolution de besoin, pas
une incertitude sur le choix actuel.

## Sources primaires

- [RabbitMQ — RabbitMQ vs Kafka](https://www.rabbitmq.com/docs/compare/kafka)
- [RabbitMQ — Quorum queues](https://www.rabbitmq.com/docs/quorum-queues)
- [RabbitMQ — Streams and Superstreams](https://www.rabbitmq.com/docs/streams)
- [RabbitMQ — How to run benchmarks](https://www.rabbitmq.com/blog/2020/06/04/how-to-run-benchmarks)
- [RabbitMQ — Production checklist](https://www.rabbitmq.com/docs/production-checklist)
- [Apache Kafka — Design](https://kafka.apache.org/41/design/design/)
- [Apache Kafka — Producer configuration](https://kafka.apache.org/40/configuration/producer-configs/)
