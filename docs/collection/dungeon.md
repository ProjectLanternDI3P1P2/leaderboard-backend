# Événements Dungeon

Exchange `dungeon.events` (topic durable); publier après persistance du run ou de la transition d'étage. Types et enveloppe : [contrat commun](contrat-commun.md).

| Clé de routage / message | Déclencheur et champs | Fixture JSON |
|---|---|---|
| `dungeon.dungeon-run-started.v1` / `DungeonRunStarted` | Run démarré effectivement. `dungeon_id: string` et `game_session_id: string` GUID requis; `initial_floor_number: optional uint32` explicitement présent, >=1; `participants: repeated Actor` non vide, héros uniques. | [run-started](../fixtures/us-collecte-01/run-started.json) |
| `dungeon.floor-entered.v1` / `FloorEntered` | Transition appliquée. `dungeon_id`, `game_session_id`, `transition_id: string` GUID requis; `from_floor_number`, `to_floor_number: optional uint32` explicitement présents, >=1, distincts; `participants: repeated Actor` non vide. Nouveau `transition_id` par transition, y compris retour 1→2. | [floor-first](../fixtures/us-collecte-01/floor-first.json), [floor-repeat](../fixtures/us-collecte-01/floor-repeat.json) |
| `dungeon.dungeon-run-ended.v1` / `DungeonRunEnded` | Run terminal persisté. `dungeon_id`, `game_session_id: string` GUID requis; `outcome: RunOutcome` = `RUN_OUTCOME_WON`, `RUN_OUTCOME_LOST`, `RUN_OUTCOME_ABANDONED`; `final_floor_number: optional uint32` présent, >=1; `participants: repeated Actor` non vide. | [run-ended-won](../fixtures/us-collecte-01/run-ended-won.json), [run-ended-lost](../fixtures/us-collecte-01/run-ended-lost.json), [run-ended-abandoned](../fixtures/us-collecte-01/run-ended-abandoned.json) |
