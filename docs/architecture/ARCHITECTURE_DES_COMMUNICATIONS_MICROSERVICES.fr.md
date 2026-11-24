# Architecture des communications microservices

> Référence détaillée de la cible MVP : frontend, Traefik, Keycloak, Player, Dungeon, Combat, Rewards, Progression et RabbitMQ.

## 1. Vue macro

Le produit compte exactement cinq microservices métier : **Player**, **Dungeon**, **Combat**, **Rewards** et **Progression**. Ils sont privés dans Kubernetes. **Traefik** est l'unique entrée backend publique. **Keycloak** est le fournisseur d'identité technique, pas un microservice métier.

| Besoin | Transport | Frontière |
|---|---|---|
| Fonction frontend hors gameplay | HTTPS/REST, JSON | Frontend → Traefik → service propriétaire |
| Gameplay solo et multijoueur | SignalR sur WebSocket | Client de jeu → Traefik → Hub propriétaire |
| Résultat interne immédiat | gRPC, Protobuf | Service → service, réseau interne |
| Fait métier différable | RabbitMQ, Protobuf versionné | Producteur → consommateur indépendant |

```mermaid
flowchart TB
  Site[Site public]
  Client[Client de jeu\nsolo et multijoueur]
  Keycloak[Keycloak OIDC]
  Gateway[Traefik API Gateway\nunique entrée publique]
  Player[Player\njoueurs, héros, GameSessions]
  Dungeon[Dungeon\nDungeonRuns, salles, déplacements]
  Combat[Combat\ntours, état et résolution]
  Rewards[Rewards\nloot, inventaire, équipement, marketplace]
  Progression[Progression\nXP, statistiques et leaderboard]
  RabbitMQ[(RabbitMQ)]
  Site -->|OIDC / HTTPS| Keycloak
  Client -->|OIDC / HTTPS| Keycloak
  Site -->|HTTPS REST| Gateway
  Client -->|HTTPS REST hors gameplay| Gateway
  Client -->|WSS SignalR gameplay| Gateway
  Gateway -->|REST + claims| Player
  Gateway -->|REST + claims| Rewards
  Gateway -->|REST + claims| Progression
  Gateway -->|SignalR + claims| Player
  Gateway -->|SignalR + claims| Dungeon
  Gateway -->|SignalR + claims| Combat
  Player -->|gRPC| Dungeon
  Dungeon -->|gRPC| Combat
  Dungeon -->|gRPC| Rewards
  Combat -->|gRPC| Player
  Combat -->|gRPC| Rewards
  Combat -->|gRPC| Dungeon
  Player -->|publie faits de session| RabbitMQ
  Dungeon -->|publie faits de run| RabbitMQ
  Combat -->|publie faits de combat| RabbitMQ
  Rewards -->|publie faits de récompense| RabbitMQ
  RabbitMQ -->|livre| Rewards
  RabbitMQ -->|livre| Progression
  RabbitMQ -->|livre| Player
```

### Règles de frontière

- Traefik valide le jeton Keycloak, retire/remplace les headers internes forgés par le client et transmet `UserId`, `Username`, rôles et `CorrelationId` comme claims de confiance.
- Traefik proxy les upgrades WebSocket et les Hubs ; il ne conserve pas d'état gameplay, ne consomme pas les messages RabbitMQ et ne devient pas un BFF.
- Chaque service autorise sa ressource. Un jeton valide ne donne jamais accès à la session, au donjon ou au combat d'un autre joueur.
- Aucun microservice n'est exposé directement à Internet et aucun ne lit/écrit la base d'un autre service.

## 2. Ownership et responsabilités

| Propriétaire | Source de vérité | Ne possède pas |
|---|---|---|
| Player | Identité métier, héros, `GameSession`, créateur, roster et héros choisis | Jetons, équipement, donjon, XP |
| Dungeon | `DungeonRun`, génération, environ 40 salles/intérieurs, positions, salles et cycle du run | Héros, combat, loot, XP |
| Combat | Instance, tours, état autoritatif, résultat et snapshots de début | Identité durable, inventaire, DungeonRun, XP |
| Rewards | Droit à récompense, loot, objets, consommables, inventaire, équipement, marketplace/paiement | Combat, cycle de run, XP |
| Progression | XP, niveau, historique des faits et projections leaderboard | Authentification, session, héros, inventaire |

Une `GameSession` n'est ni une session de connexion ni un `DungeonRun`. Player possède le groupe et les héros choisis ; Dungeon possède le run généré. La session démarrée référence un run. Seul le créateur démarre ou abandonne et le roster est verrouillé au démarrage. Voir ADR-GLOB-011.

## 3. Frontend : HTTPS/REST et SignalR

### HTTPS/REST, exclusivement hors gameplay

| Routes Gateway | Propriétaire | Opérations |
|---|---|---|
| `/api/player/...` | Player | Profil, création, lecture et sélection héros |
| `/api/rewards/...` | Rewards | Inventaire, équiper/déséquiper hors combat, marketplace/achat |
| `/api/leaderboard/v1/...` | Leaderboard | Statistiques et classement |
| Endpoints OIDC | Keycloak | Connexion, acquisition/renouvellement token |

Les payloads REST sont JSON. Les contrôleurs sont des frontières applicatives fines et ne doivent pas dupliquer en HTTP une commande de gameplay déjà portée par un Hub.

### SignalR/WebSocket, pour tout gameplay

Les mêmes commandes servent en solo et en multijoueur. Le Hub autorise le participant, l'ajoute à un groupe créé par le service, puis retourne un snapshot initial complet. Le client ne choisit jamais un groupe.

| Hub | Route | Propriétaire | Groupe | Commandes | Événement |
|---|---|---|---|---|---|
| `PlayerHub` | `/hubs/player` | Player | `session:{SessionId}` | Créer, rejoindre, démarrer, abandonner | `SessionStateChanged` |
| `DungeonHub` | `/hubs/dungeons` | Dungeon | `dungeon:{DungeonId}` | Snapshot, déplacement, salle, coffre | `DungeonStateChanged` |
| `CombatHub` | `/hubs/combats` | Combat | `combat:{CombatId}` | Snapshot, action, passer, potion | `CombatStateChanged` |

Chaque commande porte un `CommandId` unique. Le Hub retourne explicitement acceptation/refus. Chaque changement accepté diffuse un snapshot complet autoritatif : les retries et resynchronisations deviennent sûrs.

```mermaid
sequenceDiagram
  participant C as Client de jeu
  participant T as Traefik
  participant H as Hub du service
  participant A as Cas d'usage
  C->>T: Connexion WSS + jeton Keycloak
  T->>T: Valide jeton et prépare claims
  T->>H: Proxy connexion + claims
  H->>A: Autorise participant et charge état
  A-->>H: État autorisé
  H-->>C: Groupe autorisé + snapshot initial
  C->>H: Commande(CommandId, payload)
  H->>A: Valide et exécute une seule fois
  A-->>H: Acceptation/refus + snapshot courant
  H-->>C: Ack et StateChanged(snapshot)
```

Dungeon et Combat ont un replica chacun dans le MVP. Aucun backplane SignalR Redis n'est actif, mais les groupes existent déjà afin de l'ajouter plus tard sans changer le protocole frontend.

## 4. Échanges gRPC synchrones

Les contrats gRPC sont des packages NuGet Protobuf versionnés et possédés par leur producteur. Chaque appel transporte la corrélation, a une deadline et ne peut être retry qu'avec la même clé d'idempotence lorsque cela est sûr.

| Appelant → appelé | Opération | Déclencheur / requête | Résultat et règle |
|---|---|---|---|
| Player → Dungeon | `CreateDungeonRun` | Démarrage ; `CommandId`, `GameSessionId`, groupe, héros | `DungeonId` + snapshot. Même commande = run existant, jamais second run. |
| Player → Dungeon | `AbandonDungeonRun` | Abandon créateur ; commande, session, donjon | Confirmation `Abandoned`, répétition = même état final. |
| Dungeon → Combat | `CreateCombat` | Salle combat ; commande, run, salle/rencontre, participants | `CombatId` + snapshot. Une cause de salle ne crée pas deux combats. |
| Combat → Player | `GetCombatantSnapshot` | Initialisation ; héros/participants | Snapshot héros immuable ; échec interdit l'initialisation sûre. |
| Combat → Rewards | `GetCombatInventorySnapshot` | Initialisation ; héros | Snapshot équipement/consommables ; échec interdit l'initialisation sûre. |
| Dungeon → Rewards | `GrantChestReward` | Coffre ; commande, cause run/salle/coffre, destinataire | Loot persisté. Coffre ouvert après confirmation. |
| Combat → Rewards | `GrantCombatReward` | Victoire ; commande, combat/cause, destinataires | Loot avant finalisation. Boss final : reward de boss seulement. |
| Combat → Dungeon | `ReportCombatResult` | Fin ; commande, combat, salle, `Won`/`Lost` | Transition salle/run. `Won` après reward confirmée. |

Combat persiste les snapshots Player et Rewards obtenus à sa création. Un changement d'équipement ultérieur ne modifie pas ce combat. Le frontend bloque équiper/déséquiper en combat ; le MVP n'ajoute pas de réservation backend.

## 5. Échanges RabbitMQ asynchrones

### 5.1 Flux statistiques centralisés vers Progression

**Player, Dungeon, Combat et Rewards publient leurs faits statistiques vers
Progression.** Progression centralise les statistiques, l'historique, l'XP et
les projections de leaderboard. Cette centralisation ne transfère pas
l'ownership : le producteur reste la source de vérité et Progression conserve
des faits immuables avec ses propres projections dérivées.

Chaque producteur écrit son changement d'état et un message d'outbox dans sa
transaction locale ; un worker publie l'outbox vers RabbitMQ. Ainsi, un fait
statistique n'est pas perdu si la publication échoue après le changement
métier. Les files durables de Progression sont liées à toutes les clés de
routage statistiques, indépendamment des files Player et Rewards.

| Clé de routage | Producteur | Déclencheur | Projection Progression |
|---|---|---|---|
| `player.game-session-started.v1` | Player | Session démarrée et liée à un run | Participation et compteurs de session |
| `player.game-session-ended.v1` | Player | Session fermée | Fin effective de participation |
| `player.hero-created.v1` | Player | Héros créé | Créations de héros et répartition des archétypes |
| `player.game-session-member-joined.v1` | Player | Participant ajouté au roster avant verrouillage | Taille et composition des groupes |
| `dungeon.dungeon-run-started.v1` | Dungeon | Run créé | Run et dimensions statistiques non sensibles |
| `dungeon.room-entered.v1` | Dungeon | Salle effectivement entrée | Exploration, progression et compteurs de salles |
| `dungeon.chest-opened.v1` | Dungeon | Coffre marqué ouvert après confirmation Rewards | Exploration des coffres, distincte du loot attribué |
| `dungeon.dungeon-run-ended.v1` | Dungeon | Run `Won`, `Lost` ou `Abandoned` | Issue, durée et compteurs finaux |
| `combat.combat-started.v1` | Combat | Snapshots de début persistés et combat jouable | Nombre de combats, participants, rencontre et contexte de difficulté |
| `combat.action-resolved.v1` | Combat | Toute action acceptée et résolue : attaque, compétence, défense, passe ou potion | Actions, dégâts/soins, cibles, effets, tours et performance par héros |
| `combat.combat-completed.v1` | Combat | Combat terminé, résultat Dungeon accepté | Victoire/défaite, statistiques de combat et XP applicable |
| `rewards.reward-granted.v1` | Rewards | Droit à récompense persisté | Loot et valeur de récompense |
| `rewards.inventory-item-consumed.v1` | Rewards | Consommation durable appliquée | Usage de consommable |
| `rewards.equipment-changed.v1` | Rewards | Équipement/déséquipement hors combat confirmé | Statistiques d'équipement |
| `rewards.purchase-completed.v1` | Rewards | Achat marketplace/paiement confirmé | Statistique économique |

`combat.combat-completed.v1` est désormais produit pour une victoire comme pour
une défaite, après acceptation du résultat par Dungeon. Une victoire attend la
confirmation de reward ; une défaite ne crée aucune reward. Progression décide
à partir du résultat si une attribution d'XP est applicable. La ligne du
catalogue historique ci-dessous doit être lue avec cette sémantique.

`combat.action-resolved.v1` est émis **une fois par action acceptée et résolue**,
après la mise à jour atomique de l'état Combat et de l'outbox. Il couvre les
attaques, compétences, défenses, passages de tour et potions. Le payload porte
notamment `CombatId`, `TurnNumber`, acteur, type d'action, cibles, résultats
agrégés (dégâts, soins, effets) et `CommandId`. Les commandes refusées, les
lectures de snapshot et les rediffusions SignalR n'émettent aucun fait
statistique. La consommation durable d'une potion reste confirmée par Rewards
via `rewards.inventory-item-consumed.v1` ; elle ne doit pas être comptée deux
fois par Progression.

Les événements de statistiques ne portent que les identifiants et mesures utiles
(`PlayerId`/`HeroId` pseudonymisés si nécessaire, identifiants de session, run
ou combat, résultat, compteurs et horodatages) ; ils ne transportent ni secrets,
ni jetons, ni snapshots complets d'inventaire ou de combat. Une correction ou un
enrichissement est un nouvel événement : Progression ne modifie jamais les
données détenues par un autre service.

RabbitMQ transporte des faits métier sans réponse immédiate. Sa redélivrance fournit un traitement at-least-once et impose des consommateurs idempotents. Chaque payload est un contrat Protobuf versionné du package producteur, dans une enveloppe avec `messageId`, `correlationId`, `causationId`, `messageType`, `version`, `occurredAt`, `producer` et payload typé.

Les événements sont publiés dans des exchanges durables avec des clés de routage versionnées. Chaque service consommateur possède une file durable liée aux clés de routage qu'il consomme. Les messages ne sont acquittés qu'après un traitement réussi ; les retries et le routage vers une dead-letter exchange prennent en charge les échecs sans coupler producteurs et consommateurs.

| Clé de routage | Producteur → file du consommateur | Publication | Effet consommateur |
|---|---|---|---|
| `combat.consumable-used.v1` | Combat → Rewards | Potion déjà appliquée au snapshot Combat local | Consomme une fois l'objet ; `CommandId`, héros, item, quantité. |
| `combat.combat-completed.v1` | Combat → Progression | Combat terminé, résultat Dungeon accepté ; reward confirmée en cas de victoire | Enregistre les statistiques ; attribue l'XP une fois si le résultat l'autorise. |
| `dungeon.dungeon-run-ended.v1` | Dungeon → Player et Progression | Run `Won`, `Lost` ou `Abandoned` | Player ferme `GameSession` ; Progression enregistre issue, durée et compteurs finaux. |

La Gateway ne consomme jamais de messages RabbitMQ. Un consommateur ne modifie que son propre état puis expose un effet client par son API REST ou son Hub.

## 6. Séquences critiques

### Démarrage du run

```mermaid
sequenceDiagram
  participant F as Client de jeu
  participant P as PlayerHub / Player
  participant D as Dungeon
  F->>P: StartSession(CommandId)
  P->>P: Vérifie créateur, héros, roster non verrouillé
  P->>D: gRPC CreateDungeonRun(CommandId, GameSessionId, groupe)
  D->>D: Génère et persiste DungeonRun
  D-->>P: DungeonId + snapshot initial
  P->>P: Lie et démarre GameSession
  P-->>F: SessionStateChanged(snapshot)
  F->>D: Connexion DungeonHub
  D-->>F: Groupe autorisé + snapshot Dungeon
```

### Coffre et reward

```mermaid
sequenceDiagram
  participant F as Client de jeu
  participant D as DungeonHub / Dungeon
  participant R as Rewards
  F->>D: OpenChest(CommandId, chestId)
  D->>D: Autorise et vérifie coffre disponible
  D->>R: gRPC GrantChestReward(CommandId, cause coffre)
  R->>R: Crée ou retrouve droit idempotent
  R-->>D: Loot confirmé
  D->>D: Marque coffre ouvert
  D-->>F: DungeonStateChanged(snapshot avec loot)
```

### Combat gagné

```mermaid
sequenceDiagram
  participant D as Dungeon
  participant C as Combat
  participant P as Player
  participant R as Rewards
  participant M as RabbitMQ
  participant X as Progression
  D->>C: gRPC CreateCombat(contexte salle)
  C->>P: gRPC GetCombatantSnapshot
  C->>R: gRPC GetCombatInventorySnapshot
  C->>C: Persiste snapshots de début
  Note over C: Actions via CombatHub
  C->>R: gRPC GrantCombatReward(victoire)
  R-->>C: Reward confirmée
  C->>D: gRPC ReportCombatResult(Won)
  D-->>C: Transition acceptée
  C->>M: Publie combat.combat-completed.v1
  M->>X: Livre CombatCompleted
  X->>X: Attribution XP unique
```

`CombatCompleted` est produit après acceptation Dungeon, pour une victoire comme pour une défaite, afin que Progression dispose de statistiques complètes. Une victoire ne le publie qu'après confirmation de reward ; une défaite ne crée aucune reward. Progression décide, à partir du résultat, si une attribution d'XP est applicable.

### Potion et fin de run

```mermaid
sequenceDiagram
  participant F as Client de jeu
  participant C as CombatHub / Combat
  participant M as RabbitMQ
  participant R as Rewards
  participant D as Dungeon
  participant P as Player
  participant X as Progression
  F->>C: ConsumePotion(CommandId, itemId)
  C->>C: Met à jour snapshot Combat local
  C-->>F: CombatStateChanged(snapshot)
  C->>M: Publie combat.action-resolved.v1
  M->>X: Livre ActionResolved
  C->>M: Publie combat.consumable-used.v1
  M->>R: Livre ; consomme item une seule fois
  R->>M: Publie rewards.inventory-item-consumed.v1
  M->>X: Livre InventoryItemConsumed
  D->>M: Publie dungeon.dungeon-run-ended.v1
  M->>P: Livre DungeonRunEnded
  M->>X: Livre DungeonRunEnded
  P->>P: Ferme GameSession
  P-->>P: PlayerHub SessionStateChanged
```

## 7. Fiabilité, cohérence et observabilité

### Statistiques asynchrones

Progression déduplique chaque fait reçu par `messageId` et par clé métier avant
de le persister dans son historique et de mettre à jour ses projections. Le
rejeu de l'outbox et la redélivrance RabbitMQ ne doivent donc ni doubler l'XP,
ni les compteurs, ni les classements. Progression ne rappelle pas un producteur
pour reconstruire une statistique ; l'événement versionné est son contrat de
lecture.

| Sujet | Règle |
|---|---|
| Replay commande | Conserver `CommandId` et retourner résultat antérieur plutôt que répéter transition. |
| Doublon événement | Dédupliquer par `messageId` ou clé métier : jamais deux consommations, XP ou clôtures incohérentes. |
| Timeout gRPC | Résultat inconnu ; retry seulement avec même clé d'idempotence. |
| Reward obligatoire | Coffre/victoire = barrières synchrones ; échec = pending/retryable, jamais faussement terminé. |
| Délai RabbitMQ | Cohérence éventuelle dans état propriétaire ; Gateway n'attend jamais un consommateur. |
| Traces | `CorrelationId` traverse Gateway, Hub, gRPC et RabbitMQ ; `CausationId` relie les événements dérivés. |
| Logs | Inclure `GameSessionId`, `DungeonId`, `CombatId`, héros, `CommandId`, cause reward et `messageId`. |

## 8. Non mis en place à l'initialisation

- Pas de backplane Redis ni de Hub Dungeon/Combat multi-replica initialement.
- Aucun join d'un `DungeonRun` actif et aucun abandon individuel.
