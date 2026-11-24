# Vérification locale des conteneurs — 2026-10-19

Rapport initial. Le blocage de compilation décrit ici a ensuite été levé par la suppression demandée du package et de ses exemples : voir [le rapport final de validation](2026-10-19-suppression-contrat-combat.md).

Statut : PostgreSQL et RabbitMQ opérationnels ; API non construite, test API → PostgreSQL/RabbitMQ non exécuté.

## Actions et résultats

- Accès au moteur Docker Desktop Linux autorisé après un refus du bac à sable.
- `docker compose up -d --build --wait` : images des dépendances téléchargées, puis construction API arrêtée sur `unexpected key 'env'` au montage du secret du Dockerfile.
- Dockerfile corrigé : lecture du secret depuis `/run/secrets/github_packages_credentials`, compatible avec le frontend Dockerfile 1.7 déclaré. Secret rendu obligatoire. Construction corrigée non encore validée.
- Ajout de `compose.build-secrets.yaml` pour transmettre le secret de construction depuis `GITHUB_PACKAGES_CREDENTIALS`, sans stocker sa valeur dans le dépôt.
- `docker compose up -d --wait postgres rabbitmq` : deux conteneurs healthy ; volume PostgreSQL local créé.
- SQL dans PostgreSQL : connexion réussie, base/utilisateur `leaderboard`, PostgreSQL **17.11**.
- `rabbitmq-diagnostics -q ping` et API de gestion : succès, RabbitMQ **4.3.6**.
- DNS/TCP sur le réseau Compose : connexion depuis RabbitMQ vers `postgres:5432` réussie. Vérifie le réseau ; ces deux services n'ont pas de flux métier direct.
- RabbitMQ : publication HTTP, routage vers une file de test et lecture du même payload réussis. File supprimée après le test. Une première file non durable a été refusée par RabbitMQ 4.3 ; test corrigé avec file durable temporaire.

## Blocage restant

Le template référence `Combat.Contracts` **2.0.0** dans Application, Infrastructure et Presentation. Ce package est téléchargé sur GitHub Packages, qui nécessite des identifiants NuGet. Aucun secret de build, identifiant NuGet utilisateur ou cache local de ce package n'a été trouvé dans les emplacements vérifiés.

Un accès GitHub autorisé à lire le package est nécessaire pour poursuivre. Ne pas supprimer cette dépendance ni fabriquer un contrat local pour contourner ce prérequis.

## Reprise

Depuis un terminal disposant de `GITHUB_PACKAGES_CREDENTIALS` au format NuGet attendu :

```powershell
docker compose -f compose.yaml -f compose.build-secrets.yaml build api
docker compose up -d --no-build --wait
powershell -NoProfile -File scripts/Test-ContainerCommunication.ps1
```

Le script préparé vérifie : liveness HTTP, création HTTP 201, lecture REST, présence SQL du joueur, réception du message correspondant dans RabbitMQ. Syntaxe PowerShell vérifiée ; exécution complète encore non réalisée. Il utilise les noms d'exchange et d'événement du template en environnement Development, conserve le joueur de test et supprime sa file de test. Il ne valide ni les futurs consommateurs ni une atomicité outbox.

## État laissé

- PostgreSQL : `127.0.0.1:5433`, volume conservé.
- RabbitMQ : AMQP `127.0.0.1:5672`, gestion `http://localhost:15672`, identifiants locaux Compose `leaderboard` / `leaderboard`.
- API : non démarrée.
- Aucun changement de code métier ; aucun jeton écrit ; aucun volume supprimé.

Base utilisée ici : PostgreSQL du template. MongoDB de `US-DATA-01` n'est pas encore configuré dans ce Compose et n'a pas été testé.
