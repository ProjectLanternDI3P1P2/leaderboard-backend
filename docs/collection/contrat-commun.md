# Contrat commun

Schéma : [`collection_events_v1.proto`](../../Leaderboard.Contracts/Protos/collection_events_v1.proto), package `leaderboard.collection.v1`, C# `Leaderboard.Contracts.Collection.V1`. Contrat distinct de `leaderboard.events.v1`.

## Enveloppe `GameEventEnvelope`

| Champ proto | Type | Présence / règle |
|---|---|---|
| `envelope_version` | `uint32` | Obligatoire, exactement 1 |
| `message_id` | `string` | Obligatoire, GUID canonique D minuscule non vide, identité de fait globale et stable aux reprises |
| `transaction_id` | `string` | Obligatoire, même format GUID; peut être partagé par plusieurs événements |
| `producer_squad` | `ProducerSquad` | Obligatoire: DUNGEON, COMBAT, PLAYER, REWARDS ou PROGRESSION |
| `event_type` | `string` | Obligatoire, type exact sans suffixe version, ex. `combat.skill-used` |
| `event_version` | `uint32` | Obligatoire, exactement 1 |
| `occurred_at` | `google.protobuf.Timestamp` | Obligatoire, instant UTC normalisé du fait métier |
| `correlation_id` | `optional string` | Optionnel, GUID si fourni; jamais clé de déduplication |
| `causation_id` | `optional string` | Optionnel, GUID si fourni |
| `payload` | `oneof` | Exactement un message correspondant au type et producteur |

Les singular messages/champs, IDs, enums, nombres et booléens sont obligatoires par validation (les champs `optional` numériques/booléens doivent être explicitement présents). `repeated` est requis non vide. Tous les identifiants utilisent des chaînes GUID canoniques minuscules en format D; les chaînes optionnelles documentées peuvent manquer. `ReceiptId` et `ReceivedAt` sont attribués par le collecteur et ne sont jamais envoyés par le producteur.

## Structures partagées

| Type | Champs |
|---|---|
| `Actor` | `player_id: string` requis GUID; `hero_id: string` requis GUID (un héros par participant); `clan_id_at_occurrence: optional string` GUID ou absent si aucun clan à l'instant du fait. Ne pas réécrire un clan historique avec l'état actuel. Dans chaque liste `participants`, les `hero_id` sont uniques. |
| `RewardSource` | `type: RewardSourceType` requis, valeurs COMBAT/CHEST/DUNGEON_RUN/QUEST/SYSTEM_GRANT; `source_id: string` requis GUID. UNSPECIFIED interdit. |
| `ItemReference` | `item_definition_id: string` requis GUID; `item_instance_id: optional string` requis pour exemplaire unique/instancié, absent pour item empilable. |
| `MonsterSnapshot` | `monster_instance_id: string` GUID; `monster_type_id: string` GUID; `level: optional uint32` explicitement présent, >=1; `rank: MonsterRank` NORMAL/MINI_BOSS/BOSS. |

Les noms d'enum de chaque fiche donnent les valeurs complètes (préfixe du type + suffixe listé); valeur `*_UNSPECIFIED` et valeurs numériques inconnues sont rejetées. Champs `optional uint32/uint64` ont généralement une borne strictement positive; exceptions: `mana_spent >=0`, `turn_count >=0`, `total_xp_after >= amount` (avec `amount >0`). Les montants `amount` et mana sont des unités entières sans fraction. Timestamps normalisés; `ended_at >= started_at`, et `occurred_at` ne précède pas la fin du combat. Dans `SkillUsed.targets`, les couples type/cible sont uniques. `ItemEquipped.previous_item_instance_id`, lorsqu'il est présent, diffère de `item_instance_id`. Voir chaque fiche pour les autres bornes.

## Transport et publication

Exchanges topic durables `<squad>.events`; clés de routage `<event_type>.v<event_version>`. Corps AMQP: octets protobuf générés, content type `application/x-protobuf`, message persistant, AMQP `MessageId` égal à `message_id`. Pour obtenir les retours de messages non routables, publier avec `mandatory=true` et traiter le retour. Les JSON fixtures sont descriptifs; ne pas les envoyer en corps. État métier et outbox doivent être enregistrés atomiquement, puis publié après commit avec publisher confirms. Reprendre avec octets sérialisés et MessageId identiques. C'est l'exigence de contrat, pas une preuve de code des squads externes.
