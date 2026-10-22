# Contrat de collecte pour les squads productrices

Ce dépôt définit le format central d'ingestion `leaderboard.collection.v1.GameEventEnvelope`; namespace C# généré : `Leaderboard.Contracts.Collection.V1`. Il est distinct du contrat `leaderboard.events.v1`, inchangé. Le format appartient à Leaderboard et est proposé pour intégration; adoption et implémentations productrices restent à confirmer.

## Guides

- [Dungeon](dungeon.md), [Combat](combat.md), [Player](player.md), [Rewards](rewards.md), [Progression](progression.md) : déclencheurs, champs et fixtures.
- [Contrat commun](contrat-commun.md): enveloppe, structures, champs et transport.
- [Validation US-COLLECTE-01](validation-us-collecte-01.md): preuves techniques et acceptation.
- [Schéma Proto](../../Leaderboard.Contracts/Protos/collection_events_v1.proto) et [manifeste des fixtures](../fixtures/us-collecte-01/manifest.json).

Il existe exactement 16 routes : Dungeon 3, Combat 5 (dont `combat-completed`), Player 3, Rewards 3, Progression 2. Exchanges : `<squad>.events`; clés : `<event-type>.v1`. Chaque fixture est du JSON descriptif des champs protobuf; les producteurs publient les octets Protobuf sérialisés, jamais le JSON.
