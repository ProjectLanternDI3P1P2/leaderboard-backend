# US-COLLECTE-01 — catalogue d'événements

Leaderboard définit le format central d'ingestion avant son intégration par les producteurs : [contrat et guides détaillés](collection/README.md). Le contrat protobuf `leaderboard.collection.v1` et ses fixtures sont présents dans ce dépôt. Ils ne constituent pas une preuve d'adoption par les squads externes ni de publication de packages producteurs.

Les cinq fiches couvrent les déclencheurs, champs, types, présence, bornes, unités, enums, routage et exemples : [Dungeon](collection/dungeon.md), [Combat](collection/combat.md), [Player](collection/player.md), [Rewards](collection/rewards.md), [Progression](collection/progression.md). Le manifeste des exemples est [ici](fixtures/us-collecte-01/manifest.json).

La collecte stocke le corps brut append-only dans MongoDB, y compris les doublons, puis ACK après insertion durable. Elle n'attribue pas d'XP et ne calcule pas les classements. Les exigences de publication fiable et de traitement figurent dans le [guide de validation](collection/validation-us-collecte-01.md).

## État et responsabilités

Le périmètre de l'US est le contrat des 16 événements : schéma, validation, exemples
et documentation des squads. La réception et MongoDB sont fournis par la PR #10 ;
la projection PostgreSQL et sa reconstruction par la PR #14.
Voir [décisions de fusion](collection/integration-pr-10-14.md) et
[bilan de validation courant](collection/validation-us-collecte-01.md).
L'adoption et la validation par les producteurs restent externes à ce dépôt.
Les anciens essais de charge concernent une implémentation remplacée et ne valident
pas les performances de la chaîne fusionnée.

## Décisions de portée

Les faits sont émis après persistance métier. `dungeon-run-started` signifie run effectivement commencé, non donjon généré. `floor-entered` signifie transition appliquée; une répétition légitime reçoit un `transition_id` distinct. `skill-used` inclut une compétence esquivée mais exclut une commande refusée; mana nul est une valeur explicite valide. `combat-completed` est recevable pour victoire et défaite; Dungeon accepte les deux, Rewards confirme uniquement la victoire. Aucun événement de combat n'attribue lui-même l'XP.

Le créateur est le premier participant; entrée/sortie sont des faits lobby persistés. Déconnexion et reconnexion ne signifient pas départ. Chaque événement capture le clan à l'occurrence. Les objets instanciés ont quantity 1; le gain d'or identifie explicitement wallet joueur ou héros.

Restent hors des 16 routes: sélection de héros, équipement retiré, dépense d'or, ouverture de coffre, départ individuel pendant un run et notifications de classement. Aucun `leaderboard-updated` n'est requis par le périmètre actuel. Toute extension demande un besoin et un propriétaire définis.

## Livraison et archivage

Les exigences aux producteurs sont transaction métier + outbox atomiques, publication après commit, confirms et traitement des non-routables, retries avec mêmes octets et même MessageId. Elles ne prouvent pas l'implémentation externe. Le collecteur a sa propre file, conserve octets complets et toutes les réceptions/doublons, puis ACK après insert Mongo confirmé. Pas de déduplication du journal brut, TTL ou purge. Enveloppes inconnues/invalides/illisibles sont archivées avec statut; panne d'archivage suit retries espacés et quarantaine durable.
