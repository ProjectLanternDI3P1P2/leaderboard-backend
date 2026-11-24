# Validation des contrats et de l'intégration

## Périmètre de l'US

US-COLLECTE-01 livre les contrats des événements : schéma Protobuf, règles de validation,
16 routes, exemples et documentation des cinq squads. MongoDB appartient à la PR #10 ;
la projection PostgreSQL à la PR #14. Voir [décisions de fusion](integration-pr-10-14.md).

L'adoption des contrats par les producteurs, leurs outbox et leur publication fiable
restent à vérifier dans les dépôts concernés. Ce dépôt ne prouve pas leur intégration.

## Résultats locaux du 22 octobre 2026

- Build Release : 0 avertissement, 0 erreur après rafraîchissement des données NuGet.
- Suite : 142 tests, 141 réussis, 0 échec, 1 ignoré (performance warehouse sur cinq millions d'événements, activable explicitement).
- CSharpier : 96 fichiers formatés.
- Image API construite avec `docker compose build api`, sans dépendance à un package privé.
- Mode `--migrate` exécuté deux fois en Production sur une base PostgreSQL isolée :
  deux migrations appliquées, deuxième passage réussi, aucune ligne dans les tables de statistiques/reconstruction.
- Aucun essai de charge prolongé exécuté et aucune CI distante revendiquée.

Commandes principales :

```powershell
dotnet build Leaderboard.Test/Leaderboard.Test.csproj --configuration Release --no-restore --maxcpucount:1 -p:UseSharedCompilation=false
dotnet test --project Leaderboard.Test/Leaderboard.Test.csproj --configuration Release --no-build --minimum-expected-tests 140
```

Les tests utilisent PostgreSQL, RabbitMQ et un replica set MongoDB authentifié dans
une pile Docker isolée. Les variables de connexion figurent dans
[configuration](../deployment-configuration.md). Aucun volume de développement existant n'est utilisé.

## Preuves par responsabilité

| Partie | Vérifications |
|---|---|
| Contrats | Fixtures valides/invalides, présence des champs, versions/types/producteurs, round-trip Protobuf, 16 routes |
| Réception et MongoDB | Corps complet inchangé, métadonnées, statut des messages invalides, ACK après confirmation, retry après insertion partielle, droits sans update/delete, lecture par période et absence de TTL |
| Projection PostgreSQL | Participants, sommes par type et unités, absence de total numérique global, grands uint64 exacts, déduplication, rapports, interruption/reprise, génération active et endpoints |
| Raccordement | 16 événements envoyés via RabbitMQ, conservés dans MongoDB puis reconstruits dans PostgreSQL ; doublon et trois messages non projetables également vérifiés |

Les scénarios de reprise couvrent un arrêt avant l'ACK, avec et sans insertion Mongo
déjà effectuée. La redélivraison reste archivée ; la projection ne recompte pas l'événement.

## Distribution NuGet — validation locale du 23 octobre 2026

- Build de la solution en Release : 0 avertissement, 0 erreur.
- Package construit avec README, deux fichiers `.proto`, DLL .NET 10 et lien au dépôt.
- Dépendance exportée unique : Google.Protobuf ; aucun composant MongoDB/PostgreSQL/RabbitMQ.
- Installation depuis le `.nupkg` dans un projet indépendant et un cache NuGet isolé réussie.
- Exemple producteur, 42 fixtures (29 valides, 12 invalides, 1 illisible) et 16 routes vérifiés.
- Tag historique/incohérent et répertoire de sortie déjà existant refusés avant publication.
- CSharpier : 135 fichiers vérifiés. Audit zizmor 1.29.0 des workflows : aucun constat signalé.
- Publication GitHub Packages et installation dans les cinq dépôts producteurs non exécutées.

Voir [distribution du package](package-nuget.md) pour le circuit de release et les droits à configurer.

## Historique

Les anciens résultats R3/R4 et les 88 tests de la collecte autonome concernent la
révision antérieure. Ils sont conservés dans [le suivi historique](../historique-implementation/US-COLLECTE-01-plan.md)
et [les notes de l'ancien banc](../../scripts/collection/README.md). Ils ne valident
pas les performances de la chaîne fusionnée et ne conditionnent pas à eux seuls
la livraison des contrats.
