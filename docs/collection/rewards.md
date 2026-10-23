# Événements Rewards

Exchange `rewards.events` (topic durable); publier après persistance de l'inventaire, de l'équipement ou du portefeuille. Types partagés : [contrat commun](contrat-commun.md).

| Clé de routage / message | Déclencheur et champs | Fixture JSON |
|---|---|---|
| `rewards.item-granted.v1` / `ItemGranted` | Objet crédité durablement. `reward_id: string` GUID; `actor: Actor`; `item: ItemReference`; `quantity: optional uint32` présent >0 (instancié =1); `source: RewardSource` requis. | [item-stackable](../fixtures/us-collecte-01/item-stackable.json), [item-instanced](../fixtures/us-collecte-01/item-instanced.json) |
| `rewards.item-equipped.v1` / `ItemEquipped` | Équipement persisté. `actor: Actor`; `item_instance_id: string` GUID; `slot: EquipmentSlot` = `EQUIPMENT_SLOT_HEAD`, `EQUIPMENT_SLOT_CHEST`, `EQUIPMENT_SLOT_HANDS`, `EQUIPMENT_SLOT_LEGS`, `EQUIPMENT_SLOT_FEET`, `EQUIPMENT_SLOT_MAIN_HAND`, `EQUIPMENT_SLOT_OFF_HAND`, `EQUIPMENT_SLOT_ACCESSORY`; `previous_item_instance_id: optional string` GUID ou absent si slot vide. | [item-equipped](../fixtures/us-collecte-01/item-equipped.json), [item-equipped-empty-slot](../fixtures/us-collecte-01/item-equipped-empty-slot.json) |
| `rewards.gold-earned.v1` / `GoldEarned` | Crédit d'or persisté. `gold_transaction_id: string` GUID métier; `actor: Actor`; `amount: optional uint64` présent >0 en unités entières; `source: RewardSource`; `wallet_owner: oneof` exactement `player_wallet_id` ou `hero_wallet_id`, string GUID. | [gold-player-wallet](../fixtures/us-collecte-01/gold-player-wallet.json), [gold-hero-wallet](../fixtures/us-collecte-01/gold-hero-wallet.json) |
