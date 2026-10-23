# Événements Player

Exchange `player.events` (topic durable); faits de héros et lobby persistés, pas les connexions transitoires. Voir [contrat commun](contrat-commun.md).

| Clé de routage / message | Déclencheur et champs | Fixture JSON |
|---|---|---|
| `player.hero-created.v1` / `HeroCreated` | Héros créé et persisté. `actor: Actor` et `hero_class_id: string` GUID requis. | [hero-created](../fixtures/us-collecte-01/hero-created.json) |
| `player.game-session-participant-joined.v1` / `ParticipantJoined` | Entrée lobby persistée avec héros. `game_session_id: string` GUID, `actor: Actor`, `is_creator: optional bool` explicitement présent (true/false); créateur: premier join true. Départ du lobby ne décrit pas une déconnexion. | [participant-creator](../fixtures/us-collecte-01/participant-creator.json), [participant-second](../fixtures/us-collecte-01/participant-second.json) |
| `player.game-session-participant-left.v1` / `ParticipantLeft` | Départ métier lobby persisté. `game_session_id: string` GUID, `actor: Actor`, `reason: ParticipantLeaveReason` = `PARTICIPANT_LEAVE_REASON_VOLUNTARY`, `PARTICIPANT_LEAVE_REASON_REMOVED` ou `PARTICIPANT_LEAVE_REASON_SESSION_CLOSED`. Déconnexion/reconnexion réseau exclue. | [participant-left](../fixtures/us-collecte-01/participant-left.json) |
