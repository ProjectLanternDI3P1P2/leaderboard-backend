# Variables de collecte à fournir à l'OPS

Exemples sûrs pour le client Leaderboard : [RabbitMQ](../../.env.rabbitmq.example), [MongoDB](../../.env.mongodb.example). Remplacer toutes les valeurs `REPLACE_*`. Les exemples sont des modèles de variables, pas des secrets valides. Voir le tableau exhaustif des options dans [configuration de déploiement](../deployment-configuration.md).

## Injection et responsabilités

Ni .NET ni le broker ne charge automatiquement `.env.rabbitmq` ou `.env.mongodb`. Injecter les variables dans l'environnement du conteneur Leaderboard. En Kubernetes, secrets d'accès dans un Secret, paramètres non confidentiels dans une ConfigMap; Mongo URI contenant mot de passe et Rabbit password restent secrets. `envFrom` peut référencer ces ressources après leur création par OPS. PostgreSQL reste une configuration supplémentaire.

Le DNS prévu est `rabbitmq.rabbitmq.svc.cluster.local`; OPS confirme le Service, le namespace, le vhost et les credentials. Les variables client ne créent pas utilisateur, vhost ou politiques côté serveur. Les comptes producteurs sont distincts. Le compte Leaderboard doit pouvoir déclarer les exchanges/files/bindings, consommer sa file et accéder à sa quarantaine; Mongo doit permettre insert, read et création d'index. OPS fournit aussi URI, DNS, TLS, permissions et capacité de sauvegarde Mongo.

## Topologie RabbitMQ réellement déclarée

L'application déclare au démarrage cinq exchanges durables de type topic (`dungeon.events`, `combat.events`, `player.events`, `rewards.events`, `progression.events`) et les bindings des 16 routes définies dans [l'index de contrat](../collection/README.md). Elle déclare la file de collecte quorum durable `leaderboard.collection.v1`, avec delivery-limit configurée et dead-lettering at-least-once vers l'exchange direct durable `leaderboard.collection.dead-letter`, routing key `quarantine`, et file quorum durable `leaderboard.collection.v1.dead`. Aucune expiration TTL n'est configurée pour la quarantaine. Une topologie précréée doit correspondre aux déclarations de l'application.

La configuration du client n'offre pas TLS RabbitMQ. Le port standard configuré est 5672 en AMQP; ne pas supposer AMQPS. Pour MongoDB, `Majority` requiert un replica set explicitement nommé dans l'URI; `Journaled` utilise w=1 avec journalisation pour un serveur standalone. Collection `leaderboard_datalake.raw_events`, aucun TTL; compte Mongo avec insertion, lecture et création d'index.

## Checklist de préparation du serveur

- [ ] Dans le namespace Kubernetes `rabbitmq`, exposer un Service nommé `rabbitmq` de type `ClusterIP`, port 5672 et `targetPort: 5672` pour AMQP. Ce nom fournit le DNS attendu `rabbitmq.rabbitmq.svc.cluster.local` depuis les namespaces du cluster.
- [ ] Fournir stockage persistant (PV/PVC) aux données du broker RabbitMQ et MongoDB. Longhorn est une possibilité à confirmer pour l'environnement; ne pas supposer une StorageClass ou politique de rétention avant décision OPS.
- [ ] Créer le vhost/comptes et secrets client; appliquer les permissions nécessaires à la topologie/consommation décrites ci-dessus et aux opérations Mongo.
- [ ] Confirmer avec OPS les tailles de disques, ressources et nombre de nœuds, ainsi que le type/topologie MongoDB (standalone ou replica set). Ces choix restent à déterminer selon l'environnement; aucun manifeste Kubernetes n'est fourni ici.
