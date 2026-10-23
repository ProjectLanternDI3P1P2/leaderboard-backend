# Paramètres à fournir à OPS

Voir [configuration](../deployment-configuration.md), [exemple RabbitMQ](../../.env.rabbitmq.example)
et [exemple MongoDB](../../.env.mongodb.example). Les fichiers .env ne sont pas chargés automatiquement.
Injecter secrets et configuration dans le conteneur ; remplacer tous les placeholders.

Le compte RabbitMQ doit consommer sa file et déclarer les exchanges/files/bindings configurés.
Par défaut : cinq exchanges producteurs, binding `#`, file quorum `leaderboard.raw-events`,
file quorum de rejet `leaderboard.raw-events.dead`, dead-lettering at-least-once via l'exchange
par défaut, sans TTL. Une topologie préprovisionnée doit correspondre aux déclarations.
Le client AMQP utilise le port 5672 ; il ne configure pas TLS RabbitMQ.

MongoDB requiert un replica set pour les écritures majority + journal. Configurer TLS dans l'URI.
Le compte applicatif dispose seulement de `insert` et `find` sur `progression_lake.raw_events`.
Un compte de migration distinct initialise le schéma et les index avant déploiement ;
la création d'index n'est pas une permission du compte applicatif.

OPS confirme DNS, vhost, secrets, permissions, stockage persistant, capacité, sauvegardes et restauration.
La projection finale utilise PostgreSQL et les migrations de la PR #14 ; les endpoints opérateur
attendent le header `X-Roles` fiable fourni par la gateway. Voir ADR-0038.
