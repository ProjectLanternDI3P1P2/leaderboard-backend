# Lancer le projet en local

Exécuter les commandes depuis la racine du dépôt, dans PowerShell.

## Prérequis

- Docker Desktop démarré, avec les conteneurs Linux.
- Pour développer ou lancer les tests hors Docker : SDK .NET 10 compatible avec `global.json` (`10.0.300` ou une version ultérieure de .NET 10, par exemple `10.0.402`).

Aucun jeton GitHub ni fichier `.env` n'est nécessaire.

## Démarrer avec Docker

```powershell
docker compose up -d --build --wait
```

Cette commande lance l'API, PostgreSQL et RabbitMQ. Le premier démarrage télécharge les images et dépendances. En développement, l'API applique les migrations et ajoute les données d'exemple automatiquement.

| Service | Accès |
| --- | --- |
| API / documentation Scalar | http://localhost:8080/scalar |
| RabbitMQ / interface de gestion | http://localhost:15672 — `leaderboard` / `leaderboard` |
| PostgreSQL | `localhost:5433`, base et utilisateur `leaderboard`, sans mot de passe en local |
| RabbitMQ / AMQP | `localhost:5672` |

## Vérifier

```powershell
docker compose ps
powershell -NoProfile -File scripts/Test-ContainerCommunication.ps1
```

Le script vérifie l'API, la création et la lecture d'un joueur dans PostgreSQL, puis un aller-retour RabbitMQ indépendant. Il conserve le joueur de test et supprime sa file temporaire. Le futur consommateur applicatif RabbitMQ n'est pas encore implémenté ; MongoDB n'est pas encore inclus.

Pour consulter les logs :

```powershell
docker compose logs --tail 100 api
```

## Développer avec l'API hors Docker

Arrêter l'API Docker pour libérer le port 8080, puis lancer :

```powershell
docker compose stop api
docker compose up -d --wait postgres rabbitmq
dotnet restore Leaderboard.Presentation.slnx
dotnet run --project Leaderboard.Presentation --launch-profile http
```

L'API reste accessible sur http://localhost:8080/scalar. `Ctrl+C` l'arrête.

Pour exécuter les tests avec PostgreSQL démarré :

```powershell
dotnet test --solution Leaderboard.Presentation.slnx
```

## Arrêter

```powershell
docker compose down
```

Les données PostgreSQL sont conservées dans le volume Docker. Éviter `down -v` sauf pour les effacer volontairement. RabbitMQ n'a pas de volume persistant dans ce Compose.
