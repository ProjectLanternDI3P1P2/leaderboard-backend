# Catalogue des événements de jeu

US : `US-COLLECTE-01` · Révision : 2026-10-19 · Transport : RabbitMQ / Protobuf.

Objectif de cette US : **List the events that each microservice needs to provide to us**. Elle définit les événements attendus et leur livraison au collecteur ; elle n'implémente ni le stockage MongoDB de `US-DATA-01`, ni les calculs d'XP ou de classement. Le suivi est dans [le plan d'implémentation](historique-implementation/US-COLLECTE-01-plan.md).

**Statut : proposition de contrats à valider avec les squads.** « Existant » signifie décrit dans l’architecture fournie, sans présumer de son implémentation.

Périmètre confirmé : informations sur les **monstres**, progression par **étage**, joueur **rejoignant une partie avec un héros**. Les champs ci-dessous désignent les propriétés C# ; le schéma Protobuf reste à définir.

## Contrat commun

| Champ d’enveloppe | Règle |
|---|---|
| `MessageId` | Identifiant unique d’événement, conservé lors des retries de publication |
| `MessageType`, `Version` | Type et version du contrat producteur |
| `OccurredAt` | Date UTC du fait métier |
| `Producer` | Service/squad émetteur : `Dungeon`, `Combat`, `Player`, `Rewards`, `Progression` |
| `CorrelationId`, `CausationId` | Corrélation du parcours et message/commande à l’origine de l’événement |
| `Payload` | Données métier définies ci-dessous |

Conventions proposées pour les payloads :

- `Actor` = `PlayerId`, `HeroId`, `ClanIdAtOccurrence` ; ce dernier est absent si le héros n’a pas de clan, jamais remplacé par son clan actuel lors d’un rejeu.
- `Participants[]` = liste d’`Actor`. Un événement collectif doit permettre d’identifier chaque héros concerné.
- `MonsterSnapshot` = `MonsterInstanceId`, `MonsterTypeId`, `Level`, `Rank` (normal, sous-boss, boss ; valeurs à confirmer).
- `ItemInstanceId` identifie un exemplaire ; `ItemDefinitionId` identifie son type. Leur correspondance avec le `ItemId` existant doit être précisée par Rewards.
- Identifiants métier : `Guid` selon les conventions du projet ; dates C# : `DateTimeOffset`. Les timestamps du fait sont dans l’enveloppe, sans duplication systématique dans le payload.

## Dungeon

| Routing key | Déclencheur | Payload minimal proposé |
|---|---|---|
| `dungeon.dungeon-run-started.v1` | Le groupe commence effectivement le run ; la seule génération du donjon ne suffit pas | `DungeonId`, `GameSessionId`, `InitialFloorNumber`, `Participants[]` |
| `dungeon.floor-entered.v1` | Une transition vers un autre étage est appliquée | `DungeonId`, `GameSessionId`, `FromFloorNumber`, `ToFloorNumber`, `Participants[]` concernés |
| `dungeon.dungeon-run-ended.v1` **[existant]** | Le run atteint un état terminal | `DungeonId`, `GameSessionId`, `Outcome` (`Won`, `Lost`, `Abandoned`), `FinalFloorNumber`, `Participants[]` |

La documentation actuelle décrit surtout des salles : le modèle d’étage doit être ajouté au contrat Dungeon. `floor-entered` décrit une transition ; une reconnexion ou un renvoi de snapshot ne doit pas créer une nouvelle entrée.

## Combat

| Routing key | Déclencheur | Payload minimal proposé |
|---|---|---|
| `combat.combat-started.v1` | Le combat devient actif, après persistance des snapshots | `CombatId`, `DungeonId`, `GameSessionId`, `RoomId`, `Participants[]`, `Monsters[]` de type `MonsterSnapshot` |
| `combat.skill-used.v1` | Une compétence est effectivement exécutée | `CombatId`, `CommandId`, `Actor`, `SkillId`, `TurnNumber`, `Targets[]` (type + identifiant), `ManaSpent` |
| `combat.combat-ended.v1` | Un résultat terminal est persisté, victoire ou défaite | `CombatId`, `DungeonId`, `GameSessionId`, `Outcome` (`Won`, `Lost`), `Participants[]`, `MonsterResults[]` (snapshot + résultat), `StartedAt`, `TurnCount` |
| `combat.consumable-used.v1` **[existant]** | Le consommable a été appliqué au snapshot de combat | `CombatId`, `CommandId`, `Actor`, `ItemId`, `Quantity` |

`ManaSpent` est le coût réellement appliqué, y compris zéro. Une commande refusée n’émet pas `skill-used`. Un sort exécuté mais esquivé reste une utilisation. Pas d’événement `mana-used` séparé pour la même action.

**Compatibilité :** `combat.combat-completed.v1` couvre une victoire **comme une défaite**, après acceptation du résultat par Dungeon. Une victoire attend aussi la confirmation Rewards ; une défaite ne crée aucune récompense. Progression décide si le résultat autorise de l'XP. La proposition `combat-ended` couvre les résultats analytiques après persistance de l'état terminal ; elle ne prouve pas à elle seule que les préconditions du flux d'XP sont satisfaites. Conserver les contrats existants tant que leurs consommateurs ne sont pas migrés. Le stockage brut conserve les deux types s'ils sont reçus ; les futures projections doivent choisir leur source de comptage pour ne pas additionner deux fois le même combat. Une migration vers un seul contrat de fin doit préserver les préconditions métier du flux d'XP.

L’XP effectivement accordée est publiée par Progression. Les informations des monstres nécessaires à son calcul viennent de Combat ; ne pas annoncer une attribution d’XP avant son enregistrement.

## Player

| Routing key | Déclencheur | Payload minimal proposé |
|---|---|---|
| `player.hero-created.v1` | Le héros est créé et persisté | `Actor`, `HeroClassId` |
| `player.game-session-participant-joined.v1` | L’entrée dans la partie avec le héros est acceptée et persistée | `GameSessionId`, `Actor` |
| `player.game-session-participant-left.v1` | Le départ métier de la partie est accepté et persisté | `GameSessionId`, `Actor`, `Reason` |

Le créateur compte comme premier participant. Le modèle suppose ici un héros par participant ; adapter en liste si plusieurs héros sont autorisés. Le roster étant verrouillé au démarrage dans l’architecture actuelle, le départ pendant un run reste à définir. Une coupure réseau ne signifie pas automatiquement un départ métier.

## Rewards

| Routing key | Déclencheur | Payload minimal proposé |
|---|---|---|
| `rewards.item-granted.v1` | L’objet est ajouté durablement à l’inventaire | `RewardId`, `Actor`, `ItemDefinitionId`, `ItemInstanceId` si applicable, `Quantity`, `SourceType`, `SourceId` |
| `rewards.item-equipped.v1` | L’équipement du héros est modifié et persisté | `Actor`, `ItemInstanceId`, `Slot`, `PreviousItemInstanceId` si remplacement |
| `rewards.gold-earned.v1` | Un gain d’or est crédité durablement | `GoldTransactionId`, `Actor`, `Amount`, `SourceType`, `SourceId` |

`SourceType`/`SourceId` identifient la cause métier, par exemple combat ou coffre. Rewards doit confirmer qu’il possède l’or et si le portefeuille appartient au joueur ou au héros. `Actor` décrit le bénéficiaire/contexte de l’action, sans imposer le propriétaire du portefeuille.

## Progression — compléments proposés

| Routing key | Déclencheur | Payload minimal proposé |
|---|---|---|
| `progression.xp-awarded.v1` | Le gain d’XP est persisté une seule fois | `XpAwardId`, `Actor`, `Amount`, `TotalXpAfter`, `SourceType`, `SourceId` |
| `progression.level-up.v1` | L’attribution fait changer de niveau | `Actor`, `PreviousLevel`, `NewLevel`, `XpAwardId` |

Un même gain peut traverser plusieurs niveaux : un événement porte la transition complète. `xp-awarded` sert à mesurer l’XP ; il ne doit pas déclencher à nouveau son attribution.

## Livraison et collecte

- Le collecteur utilise sa propre file durable, liée aux routing keys retenues. Il ne partage pas les files de traitement de Rewards, Player ou Progression.
- Chaque producteur enregistre son changement métier et son événement d'outbox dans la même transaction locale. Un worker publie ensuite avec confirmations RabbitMQ, gestion des messages non routables et reprises conservant le même `MessageId`. Cette garantie relève des squads productrices ; aucune publication réseau ne doit précéder le commit métier. La livraison reste au moins une fois.
- Le stockage brut relève de `US-DATA-01` et utilise MongoDB. Il conserve le payload original octet pour octet, avec `ReceivedAt` UTC, un identifiant interne de réception distinct et les métadonnées séparées. Chaque soumission est ajoutée ; aucun filtrage par identifiant de transaction ne doit supprimer un événement avant sa persistance. Deux événements soumis avec le même identifiant de transaction sont tous deux conservés. Une redélivrance peut donc produire plusieurs réceptions conservées ; leur déduplication éventuelle appartient aux projections, pas au dépôt brut.
- ACK uniquement après confirmation d'écriture durable par le dépôt. Une erreur ou une réponse d'écriture incertaine ne doit pas provoquer d'ACK anticipé ; une reprise peut créer une réception supplémentaire conservée.
- Les futures projections dédupliquent par identité de message et clé métier pour éviter les effets doubles : par exemple attribution XP par cause + héros. `CorrelationId` n'est pas une clé de déduplication. Cette logique ne modifie ni ne supprime les réceptions brutes.
- L'« ID de transaction » doit être confirmé avec les squads et le responsable de `US-DATA-01` : `MessageId`, identifiant métier ou champ distinct. Quelle que soit cette correspondance, aucune contrainte d'unicité sur cet identifiant ne doit empêcher la conservation des deux soumissions exigée par `US-DATA-01`.
- Le dépôt MongoDB est immuable depuis le code applicatif : modification et suppression rejetées, aucune purge ni expiration TTL planifiée. Les lectures par période portent sur `ReceivedAt` et retournent toutes les réceptions triées par date de réception, avec un départage stable en cas d'égalité. L'implémentation de ces garanties appartient à `US-DATA-01`.

## Décisions et extensions

| Sujet | Proposition / point restant |
|---|---|
| Clan | Confirmer le service propriétaire ; figer le clan par héros au moment de l’action et le conserver dans les événements dérivés |
| Changement de héros dans le lobby | Ajouter `player.game-session-hero-selected.v1` avec héros précédent/nouveau, si autorisé |
| Équipement retiré | Ajouter `rewards.item-unequipped.v1` pour représenter un slot vidé sans remplacement |
| Dépenses | Ajouter `rewards.gold-spent.v1` si le solde ou l’économie du jeu sont analysés |
| Coffres | Ajouter `dungeon.chest-opened.v1` si l’on compte les ouvertures, y compris sans loot |
| Départ pendant un run | Définir abandon individuel, déconnexion et reconnexion avant d’élargir l’événement Player |

Pas d’événement `leaderboard-updated` dans le périmètre de collecte actuel : aucun consommateur ni besoin de notification n’est défini.

Références : `ARCHITECTURE_DES_COMMUNICATIONS_MICROSERVICES.fr.md`, `Player.md`, `BACKEND_TECHNICAL_DECISIONS.md`, `BACKEND_NAMING_CONVENTIONS.md`. Les nouvelles clés et nouveaux champs de ce catalogue restent des propositions, à l’exception des besoins explicitement confirmés.
