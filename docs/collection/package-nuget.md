# Distribution du contrat commun

La décision retenue est un package `Leaderboard.Contracts`, maintenu ici et utilisé
par Dungeon, Combat, Player, Rewards et Progression pour les événements destinés
à Leaderboard. Les cinq services ciblent .NET 10. Voir la précision de responsabilité
dans [ADR-GLOB-007](../adr/ADR-GLOB-007-protobuf-contract-packages.md).

Le [guide embarqué dans le package](../../Leaderboard.Contracts/README.md) décrit
l'installation, les droits, un événement complet et sa publication par le producteur.
Le dépôt Leaderboard conserve ses `ProjectReference` internes ; les services
externes utilisent un `PackageReference` vers une version publiée.

## Vérifier avant publication

Depuis la racine du dépôt, avec PowerShell et le SDK .NET 10 :

```powershell
./scripts/contracts/verify-package.ps1
```

Le script construit le `.nupkg` dans un nouveau répertoire temporaire, vérifie son
README, ses schémas, sa DLL et sa dépendance unique à Google.Protobuf. Il installe
ensuite le package dans un projet indépendant avec un cache NuGet isolé, exécute
l'exemple producteur et vérifie les 42 fixtures couvrant les 16 routes.
Il ne démarre ni broker ni base de données et ne publie rien.

Ce contrôle est inclus dans le job Build des PR et dans la publication.
La suite principale continue de vérifier le contrat et son raccordement au stockage.

## Release et publication

Les anciennes étiquettes `contracts-v1.0.0` et `contracts-v2.0.0` appartiennent à
l'historique Combat. Elles restent intactes. Les nouvelles releases utilisent
**`leaderboard-contracts-vN.0.0`** et le composant release-please `leaderboard-contracts`.
La version NuGet reste gérée par release-please, indépendamment de la version applicative.
Ne pas recréer un tag historique ou republier une version existante.

1. Intégrer les changements via les PR habituelles vers `dev`, puis `main`.
2. Utiliser la PR de release du composant `leaderboard-contracts`. L'ancienne PR
   ouverte pour le composant `contracts`, si elle existe encore, doit être fermée
   comme remplacée ; ne pas fusionner ses anciens numéros/tags.
3. Vérifier le changelog et la version proposée, et vérifier qu'elle est libre dans
   GitHub Packages. La base actuelle est `1.0.0` ; la stratégie existante propose
   normalement ensuite `2.0.0`, avec le nouveau préfixe de tag.
4. Fusionner la PR de release. Le workflow `Publish Leaderboard contracts` vérifie
   que le commit appartient à `main`, que tag et version correspondent, puis teste
   le package avant de publier avec le `GITHUB_TOKEN` du dépôt.
5. Donner aux équipes la version publiée et le guide d'installation. Configurer
   les droits de lecture et les accès Actions des cinq dépôts consommateurs.
6. Chaque équipe ajoute la référence exacte, adapte ses événements et vérifie sa CI.
   Avec un premier service, vérifier un événement réel jusqu'à MongoDB et au warehouse.

Le workflow refuse une version déjà publiée au lieu de masquer le conflit.
Une nouvelle tentative après une publication réussie doit donc commencer par
vérifier le registre ; elle ne doit ni remplacer le package ni déplacer son tag.
`BOT_TOKEN` reste nécessaire au workflow release-please pour que les tags qu'il
crée déclenchent les workflows suivants.

La présence du workflow et la réussite du test local ne prouvent pas une publication
distante, ni l'adoption par les producteurs. Ces deux validations doivent être constatées
dans GitHub Packages et dans leurs dépôts.
