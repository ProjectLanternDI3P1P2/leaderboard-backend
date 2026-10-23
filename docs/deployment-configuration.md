# Configuration — contrats, stockage brut et warehouse

## Contrats

Le corps RabbitMQ est le Protobuf `leaderboard.collection.v1.GameEventEnvelope`.
Le package de contrats est construit dans ce dépôt. Aucun package `Combat.Contracts`
ni dépôt producteur voisin n'est nécessaire. Voir [contrat commun](collection/contrat-commun.md).

## Réception RabbitMQ (PR #10)

Activer `RabbitMq__Enabled=true` et `RawEventConsumer__Enabled=true`.
Les paramètres de connexion sont `RabbitMq__HostName`, `Port`, `VirtualHost`,
`UserName` et `Password`. Les exemples complets sont dans
[.env.rabbitmq.example](../.env.rabbitmq.example) ; ils ne sont pas chargés automatiquement.

Le consommateur déclare sa file `leaderboard.raw-events` et sa file de rejet
`leaderboard.raw-events.dead`. Par défaut, il s'abonne aux cinq exchanges durables
`dungeon.events`, `combat.events`, `player.events`, `rewards.events`, `progression.events`.
Le binding `#` conserve aussi les versions/types inconnus pour diagnostic.
`RawEventConsumer__SourceExchanges__0`, etc. permettent la configuration du routage.
Une liste vide utilise `RabbitMq__ExchangeName` pour un exchange agrégateur provisionné par OPS.

`BatchSize=100`, `BatchTimeout=00:00:00.500`, `MaxAttempts=5`,
`InitialBackoff=00:00:00.500`, `ReconnectDelay=00:00:05` sont les valeurs par défaut.
L'ACK suit exclusivement l'écriture MongoDB confirmée. Les erreurs d'écriture
suivent les retries par lot de la PR #10 puis la file de rejet durable.
Les doublons du journal brut sont conservés. Aucun TTL/purge n'est configuré.

## MongoDB (PR #10)

`ConnectionStrings__Mongo` contient l'URI du replica set ; `Mongo__DatabaseName`
vaut `progression_lake` et `Mongo__CollectionName` vaut `raw_events` par défaut.
Les écritures utilisent majority + journal. Voir [.env.mongodb.example](../.env.mongodb.example).
Le champ `payload` contient le corps AMQP complet, sans résérialisation.
Les métadonnées extraites et le statut de validation sont stockés séparément.

Le rôle applicatif doit autoriser `insert` et `find`, sans update/delete/DDL.
En développement l'application initialise la collection ; en déploiement, un compte
de migration exécute `RawEventCollectionInitializer.EnsureAsync` avant le démarrage.
OPS fournit TLS, secrets, permissions de déclaration RabbitMQ, sauvegardes et capacité.
Le mode `--migrate` existant applique uniquement les migrations PostgreSQL.

## PostgreSQL et reconstruction (PR #14)

`ConnectionStrings__DefaultConnection` pointe vers PostgreSQL ;
`ConnectionStrings__PasswordFile` permet un mot de passe monté par secret.
Voir [ADR-0038](adr/0038-rebuild-the-warehouse-into-a-new-generation-from-the-raw-event-store.md).

- `Warehouse__OperatorRole=operator` : rôle attendu dans `X-Roles` fourni par la gateway.
- `Warehouse__Rebuild__BatchSize=5000` : événements par transaction/checkpoint.
- `Warehouse__Rebuild__IsWorkerEnabled=true` : exécution des reconstructions en attente.
- `Warehouse__Rebuild__PollingInterval=00:00:02`.
- `Warehouse__Rebuild__HeartbeatTimeout=00:02:00`.

La reconstruction conserve générations, activation atomique, reprise et déduplication
par message de la PR #14. Elle ne redemande pas les événements aux producteurs.
Le classement reste celui de la dernière génération reconstruite ; aucune projection
incrémentale supplémentaire n'est introduite par cette fusion.

## Développement et tests

`docker compose up --build` démarre PostgreSQL, RabbitMQ, MongoDB 8 en replica set
et l'API HTTP sur 8080. Les accès aux bases et au broker sont limités au loopback.
Les volumes sont persistants. Ne pas réutiliser un ancien volume Mongo incompatible
avec la version/configuration du serveur.

Les tests créent des bases et des files isolées. Variables disponibles :
`LEADERBOARD_TEST_DATABASE_CONNECTION`, `LEADERBOARD_TEST_MONGO_CONNECTION`,
`LEADERBOARD_TEST_MONGO_USER`, `LEADERBOARD_TEST_MONGO_SECRET`,
`LEADERBOARD_TEST_RABBITMQ_USER`, `LEADERBOARD_TEST_RABBITMQ_PASSWORD`.
L'authentification MongoDB est nécessaire pour vérifier les permissions append-only.
La CI démarre un unique MongoDB avec replica set et authentification.
