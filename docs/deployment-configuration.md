# Configuration des communications — Leaderboard

Référence : [architecture des communications](architecture/ARCHITECTURE_DES_COMMUNICATIONS_MICROSERVICES.fr.md), ADR-GLOB-002, 006, 008 et 009. Ces clés décrivent les options effectivement lues par le service. Ajouter une clé ne crée pas un composant; les fichiers `.env` exemples ne sont chargés automatiquement ni par .NET ni par RabbitMQ.

## gRPC sortant

Aucun appel gRPC sortant dans la matrice actuelle.

## RabbitMQ — client de collecte

Les variables `RabbitMq__*` configurent le client Leaderboard. Le broker, ses comptes et ses politiques nécessitent une configuration OPS distincte. Le DNS prévu est `rabbitmq.rabbitmq.svc.cluster.local`; OPS confirme sa disponibilité, le vhost et les identifiants.

| Variable | Type / valeur sûre par défaut | Rôle / contrainte |
|---|---|---|
| `RabbitMq__Enabled` | bool, `false` | Active le consommateur. |
| `RabbitMq__HostName` | string, `localhost` | Hôte broker; en cluster, DNS convenu avec OPS. |
| `RabbitMq__Port` | int, `5672` | Port AMQP; 1–65535. Client actuel sans TLS. |
| `RabbitMq__VirtualHost` | string, `/` | Vhost non vide. |
| `RabbitMq__UserName` | string, `guest` | Compte client; remplacer en déploiement. |
| `RabbitMq__Password` | string, `guest` | Secret client; fournir via Kubernetes Secret. |
| `RabbitMq__QueueName` | string, `leaderboard.collection.v1` | File quorum durable de collecte. |
| `RabbitMq__DeadLetterExchangeName` | string, `leaderboard.collection.dead-letter` | Exchange direct durable de quarantaine. |
| `RabbitMq__DeadLetterQueueName` | string, `leaderboard.collection.v1.dead` | File quorum durable, distincte de la file de collecte; binding `quarantine`. Aucune TTL configurée. |
| `RabbitMq__PrefetchCount` | ushort, `64` | QoS; 1–1000. |
| `RabbitMq__RetryDelaySeconds` | int, `5` | Délai avant rejet/redélivrance espacée; 1–300 s. |
| `RabbitMq__MaxDeliveryAttempts` | int, `10` | Limite de livraison quorum; 1–100, transfert durable vers DLQ. |
| `RabbitMq__ReconnectDelaySeconds` | int, `5` | Temporisation reconnexion; 1–300 s. |
| `RabbitMq__Subscriptions__N__ExchangeName` | string | Binding `N`; exchange attendu selon squad. |
| `RabbitMq__Subscriptions__N__RoutingKey` | string | Binding `N`; exactement 16 routes du [contrat central](collection/README.md). |

La liste par défaut est dérivée du contrat, et si elle est configurée explicitement doit comporter exactement ces 16 routes. Au démarrage, l'application déclare les exchanges topic durables `dungeon.events`, `combat.events`, `player.events`, `rewards.events`, `progression.events`, la file quorum durable, les bindings et l'exchange/file de quarantaine durables. La file principale utilise une delivery-limit, dead-letter strategy at-least-once et overflow `reject-publish`. Le compte doit pouvoir déclarer/inspecter cette topologie, consommer la file et utiliser les ressources nécessaires. Prévoir aussi les droits de l'OPS pour créer comptes/vhost/politiques et fournir DNS/secrets. Pas de TLS RabbitMQ pris en charge actuellement; ne pas annoncer AMQPS.

## MongoDB — archivage

| Variable | Type / valeur sûre par défaut | Rôle / contrainte |
|---|---|---|
| `MongoArchive__ConnectionString` | URI, `mongodb://localhost:27017` | URI MongoDB (`mongodb` ou `mongodb+srv`); secrets encodés dans l'URI. |
| `MongoArchive__Database` | string, `leaderboard_datalake` | Base, non vide et sans `/`. |
| `MongoArchive__Collection` | string, `raw_events` | Collection non vide. |
| `MongoArchive__WriteConcern` | enum string, `Journaled` | `Journaled` (w=1+journal) ou `Majority` (w=majority+journal); timeout écriture 15 s. `Majority` exige explicitement `replicaSet` dans l'URI. |

Mongo archive les corps originaux, sans TTL/purge configurée. Le service crée l'index `ReceivedAt, _id`; le compte doit insérer, lire et créer cet index. OPS fournit DNS joignable, authentification, TLS URI/serveur et sauvegardes/capacité adaptés à la rétention. Les comptes doivent être autorisés sur la base et collection.

## Autres paramètres fournis par Compose

- `ConnectionStrings__DefaultConnection` : base de service PostgreSQL.
- `Kestrel__Endpoints__Http__Url=http://+:8080`, `Protocols=Http1`.
- `Cors__AllowedOrigins__0` et `__1` : origines front locales.
- `Serilog__MinimumLevel__Default` : niveau des logs.
- `OTEL_SERVICE_NAME` et variables standard : `OTEL_RESOURCE_ATTRIBUTES`, `OTEL_SDK_DISABLED`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_TRACES_EXPORTER`, `OTEL_METRICS_EXPORTER`, `OTEL_LOGS_EXPORTER`, `OTEL_TRACES_SAMPLER`. SDK désactivé par défaut; aucun collecteur installé. Activation requiert endpoint joignable et intégration SDK.
- Keycloak est validé à la gateway (ADR-GLOB-004); aucune variable de validation JWT/client secret n'est injectée ici.

## Image et dépendances

Le Dockerfile expose HTTP 8080. Les dépendances actuelles restaurent depuis nuget.org; aucun secret GitHub Packages n'est requis pour construire cette image. Consultez les [notes OPS](ops/README.md) et les [.env exemples](../.env.rabbitmq.example), [MongoDB](../.env.mongodb.example). Ces exemples ne valent ni déploiement Kubernetes ni configuration serveur.
