# Configuration des communications — Leaderboard

Référence : [architecture des communications](architecture/ARCHITECTURE_DES_COMMUNICATIONS_MICROSERVICES.fr.md), ADR-GLOB-002, 006, 008 et 009.

Cette configuration décrit la cible, même lorsque les adaptateurs sont absents.
Les futures implémentations devront lire ces sections via la configuration .NET.
Ajouter une clé ne crée pas le client, le consommateur ou l'instrumentation.
Le Compose de `frontend-game` fournit les valeurs de déploiement.

## gRPC sortant

Aucun appel gRPC sortant dans la matrice cible. Les anciennes sections
`Player`/`Placeholder` sont retirées des paramètres de déploiement.

## RabbitMQ

Paramètres communs : `RabbitMq__Enabled`, `RabbitMq__HostName`,
`RabbitMq__Port`, `RabbitMq__VirtualHost`, `RabbitMq__UserName`,
`RabbitMq__Password`, `RabbitMq__ExchangeName`.
Exchange de ce service : `progression.events`.

File réservée : `progression.events.v1`.
Les futurs consommateurs lisent `RabbitMq__QueueName`,
`RabbitMq__PrefetchCount=16`, `RabbitMq__DeadLetterExchangeName=lantern.dead-letter`,
`RabbitMq__DeadLetterQueueName=progression.events.v1.dead` et les bindings indexés
`RabbitMq__Subscriptions__N__ExchangeName` / `RabbitMq__Subscriptions__N__RoutingKey` :

| N | Exchange | Routing key |
| --- | --- | --- |
| 0 | `player.events` | `player.game-session-started.v1` |
| 1 | `player.events` | `player.game-session-ended.v1` |
| 2 | `player.events` | `player.hero-created.v1` |
| 3 | `player.events` | `player.game-session-member-joined.v1` |
| 4 | `dungeon.events` | `dungeon.dungeon-run-started.v1` |
| 5 | `dungeon.events` | `dungeon.room-entered.v1` |
| 6 | `dungeon.events` | `dungeon.chest-opened.v1` |
| 7 | `dungeon.events` | `dungeon.dungeon-run-ended.v1` |
| 8 | `combat.events` | `combat.combat-started.v1` |
| 9 | `combat.events` | `combat.action-resolved.v1` |
| 10 | `combat.events` | `combat.combat-completed.v1` |
| 11 | `rewards.events` | `rewards.reward-granted.v1` |
| 12 | `rewards.events` | `rewards.inventory-item-consumed.v1` |
| 13 | `rewards.events` | `rewards.equipment-changed.v1` |
| 14 | `rewards.events` | `rewards.purchase-completed.v1` |

Le broker prépare ces files et bindings. Les futurs consommateurs doivent utiliser
les acquittements manuels après traitement et dédupliquer les messages. La politique
locale des files quorum limite les redélivrances à 5, puis dead-letter ; les messages
expirent après 7 jours. La DLQ conserve les messages sans expiration automatique.

## Autres paramètres fournis par Compose

- `ConnectionStrings__DefaultConnection` : uniquement la base de ce service.
- `Kestrel__Endpoints__Http__Url=http://+:8080`, `Protocols=Http1`.
- `Cors__AllowedOrigins__0` et `__1` : origines des deux fronts locaux.
- `Serilog__MinimumLevel__Default` : niveau de logs.
- `OTEL_SERVICE_NAME=progression`.
- Variables standard réservées à l'instrumentation OpenTelemetry :
  `OTEL_RESOURCE_ATTRIBUTES`, `OTEL_SDK_DISABLED`, `OTEL_EXPORTER_OTLP_ENDPOINT`,
  `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_TRACES_EXPORTER`, `OTEL_METRICS_EXPORTER`,
  `OTEL_LOGS_EXPORTER`, `OTEL_TRACES_SAMPLER`.
  SDK désactivé par défaut ; aucun collecteur installé. Pour l'activer, fournir
  un endpoint joignable depuis le réseau interne et intégrer le SDK dans le service.

Keycloak est validé à la gateway (ADR-GLOB-004), pas dans ce service :
aucune variable de validation JWT ou de client secret n'est injectée ici.

## Image et dépendances

Le Dockerfile expose le port HTTP 8080. Les exemples gRPC et la dépendance Combat.Contracts ont été retirés.
Les dépendances actuelles viennent de nuget.org : aucun secret GitHub Packages requis pour construire cette image.
Les contrats producteurs seront intégrés lorsqu'ils seront fournis et validés.

```sh
docker compose up -d --build
```
