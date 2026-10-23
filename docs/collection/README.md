# Contrat de collecte pour les squads productrices

Ce dépôt définit le format commun d'ingestion `leaderboard.collection.v1.GameEventEnvelope`; namespace C# généré : `Leaderboard.Contracts.Collection.V1`. Il est distinct du contrat `leaderboard.events.v1`, inchangé. Leaderboard maintient ce format et sa validation dans le package NuGet `Leaderboard.Contracts`, que les cinq services .NET 10 doivent installer pour produire leurs événements. Leur intégration effective reste à vérifier dans leurs dépôts.

## Guides

- [Package NuGet partagé](package-nuget.md) : installation, exemple producteur, validation et publication.
- [Dungeon](dungeon.md), [Combat](combat.md), [Player](player.md), [Rewards](rewards.md), [Progression](progression.md) : déclencheurs, champs et fixtures.
- [Contrat commun](contrat-commun.md): enveloppe, structures, champs et transport.
- [Validation US-COLLECTE-01](validation-us-collecte-01.md): preuves techniques et acceptation.
- [Schéma Proto](../../Leaderboard.Contracts/Protos/collection_events_v1.proto) et [manifeste des fixtures](../fixtures/us-collecte-01/manifest.json).

Il existe exactement 16 routes : Dungeon 3, Combat 5 (dont `combat-completed`), Player 3, Rewards 3, Progression 2. Exchanges : `<squad>.events`; clés : `<event-type>.v1`. Chaque fixture est du JSON descriptif des champs protobuf; les producteurs publient les octets Protobuf sérialisés, jamais le JSON.

Répartition des responsabilités et raccordement des PR : [décisions d’intégration](integration-pr-10-14.md).
