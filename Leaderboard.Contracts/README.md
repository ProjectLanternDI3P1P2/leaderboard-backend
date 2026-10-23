# Leaderboard.Contracts

Contrat d'entrée commun de Leaderboard pour les services Dungeon, Combat, Player,
Rewards et Progression. Les cinq services ciblent .NET 10 et installent le même
package pour construire leurs événements.

Le package fournit `GameEventEnvelope`, les 16 événements typés, leurs structures
partagées et `GameEventContract.Validate`. Les classes C# sont déjà générées ;
les fichiers `.proto` sont aussi fournis dans `proto/`. `Google.Protobuf` est la
seule dépendance d'exécution. Le package ne contient aucun accès aux bases ni client RabbitMQ.

Utiliser `Leaderboard.Contracts.Collection.V1`. Le namespace historique
`Leaderboard.Contracts.Events.V1` est encore présent pour compatibilité du package ;
son `GameEventMessage` n'est pas le contrat de la collecte actuelle.

## Installer dans un service

Configurer GitHub Packages dans le `NuGet.config` du service, en conservant les
autres sources et mappings dont ce service a besoin :

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github" value="https://nuget.pkg.github.com/ProjectLanternDI3P1P2/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    <packageSource key="github"><package pattern="Leaderboard.Contracts" /></packageSource>
  </packageSourceMapping>
</configuration>
```

Le compte développeur doit avoir accès au package. Pour une restauration locale,
injecter un PAT GitHub **classic** avec `read:packages` via la variable NuGet
`NuGetPackageSourceCredentials_github` (format ci-dessous), sans l'enregistrer
dans le dépôt :

```text
Username=VOTRE_COMPTE;Password=VOTRE_TOKEN;ValidAuthenticationTypes=Basic
```

En GitHub Actions, autoriser d'abord le dépôt consommateur dans les accès Actions
du package. Dans son job existant, utiliser :

```yaml
permissions:
  contents: read
  packages: read
steps:
  # Après le checkout et l'installation du SDK .NET 10 du service.
  - name: Restore
    env:
      NuGetPackageSourceCredentials_github: Username=${{ github.actor }};Password=${{ secrets.GITHUB_TOKEN }};ValidAuthenticationTypes=Basic
    run: dotnet restore
```

Choisir une version **effectivement publiée** sur GitHub Packages. Remplacer
`N.0.0` ci-dessous par cette version ; les crochets imposent la version exacte :

```xml
<PackageReference Include="Leaderboard.Contracts" Version="[N.0.0]" />
```

Ne pas recopier les `.proto` ou les classes générées dans le service C#.
Une mise à jour du package se fait par une modification explicite de cette référence.
Si le service restaure ses dépendances dans Docker, fournir les identifiants
pendant le build avec un secret BuildKit ; ne pas les passer par `ARG`/`ENV` persistés
dans l'image. Les identifiants NuGet ne sont pas nécessaires à l'exécution du service.

## Construire et valider un événement

Exemple après la création effective d'un héros. Remplacer les identifiants et la
date par ceux de la transaction métier ; conserver les mêmes valeurs et octets
lors des reprises de publication.

```csharp
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Leaderboard.Contracts.Collection.V1;

var message = new GameEventEnvelope
{
    EnvelopeVersion = 1,
    MessageId = Guid.NewGuid().ToString("D"),
    TransactionId = Guid.NewGuid().ToString("D"),
    ProducerSquad = ProducerSquad.Player,
    EventType = "player.hero-created",
    EventVersion = 1,
    OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow),
    HeroCreated = new HeroCreated
    {
        Actor = new Actor
        {
            PlayerId = "00000000-0000-4000-8000-000000000001",
            HeroId = "00000000-0000-4000-8000-000000000002",
        },
        HeroClassId = "00000000-0000-4000-8000-000000000080",
    },
};

var errors = GameEventContract.Validate(message);
if (errors.Count != 0)
    throw new InvalidOperationException(string.Join("; ", errors));

byte[] body = message.ToByteArray();
```

La validation doit être appelée explicitement : construire ou sérialiser un objet
Protobuf ne vérifie pas les règles métier du contrat.

Enregistrer `body`, `message.MessageId` et les informations de routage dans l'outbox,
dans la même transaction que l'état métier. Le worker du service publie après commit :

| Propriété | Valeur de cet exemple |
|---|---|
| Exchange topic durable | `player.events` |
| Routing key | `player.hero-created.v1` |
| Corps | `body`, octets Protobuf de l'enveloppe complète |
| Content type | `application/x-protobuf` |
| AMQP MessageId | `message.MessageId` |
| Livraison | Message persistant, `mandatory=true`, publisher confirms et traitement des retours |

Une reprise conserve le même MessageId et les mêmes octets. Les fixtures JSON
décrivent les événements ; elles ne constituent pas le corps envoyé à RabbitMQ.

## Responsabilités et évolution

Leaderboard maintient le format commun et sa validation. Chaque producteur reste
responsable des valeurs métier, du bon déclencheur et de la fiabilité de l'envoi.
La réception/MongoDB et la projection PostgreSQL restent dans leurs composants respectifs.

Le numéro NuGet et la version du message sont distincts : une nouvelle release
NuGet peut toujours fournir `GameEventEnvelope` V1 avec `event_version = 1`.
Les ajouts compatibles conservent la version du message. Une rupture exige un
nouveau format et une transition organisée entre producteurs et consommateurs.
Ne jamais réutiliser un numéro de champ Protobuf ; réserver les numéros supprimés.
Un durcissement de validation peut aussi être incompatible, même sans changer le `.proto`.

Les types inconnus sont archivés par Leaderboard mais exclus des statistiques :
déployer le support d'un nouveau type côté Leaderboard avant que les producteurs l'émettent.

Les fiches des cinq services, leurs routes et les fixtures sont dans le
[catalogue de collecte](https://github.com/ProjectLanternDI3P1P2/leaderboard-backend/tree/main/docs/collection).
Les règles d'accès suivent la [documentation GitHub Packages](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry).
