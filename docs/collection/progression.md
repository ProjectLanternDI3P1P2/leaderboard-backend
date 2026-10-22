# Événements Progression

Exchange `progression.events` (topic durable); progression déjà persistée, sans calcul ni attribution d'XP par le collecteur. [Contrat commun](contrat-commun.md).

| Clé de routage / message | Déclencheur et champs | Fixture JSON |
|---|---|---|
| `progression.xp-awarded.v1` / `XpAwarded` | Attribution déjà persistée. `xp_award_id: string` GUID; `actor: Actor`; `amount: optional uint64` présent >0 XP entiers; `total_xp_after: optional uint64` présent >= amount; `source: RewardSource`. Réception ne réattribue pas XP. | [xp-awarded](../fixtures/us-collecte-01/xp-awarded.json) |
| `progression.level-up.v1` / `LevelUp` | Transition de niveau persistée. `actor: Actor`; `previous_level: optional uint32` présent >=1; `new_level: optional uint32` présent > précédent (peut franchir plusieurs niveaux); `xp_award_id: string` GUID. | [level-up](../fixtures/us-collecte-01/level-up.json) |
