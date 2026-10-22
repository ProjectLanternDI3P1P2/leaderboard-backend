# US-COLLECTE-01 — catalogue d'événements

Leaderboard définit le format central d'ingestion avant son intégration par les producteurs : [contrat et guides détaillés](collection/README.md). Le contrat protobuf `leaderboard.collection.v1` et ses fixtures sont présents dans ce dépôt. Ils ne constituent pas une preuve d'adoption par les squads externes ni de publication de packages producteurs.

Les cinq fiches couvrent les déclencheurs, champs, types, présence, bornes, unités, enums, routage et exemples : [Dungeon](collection/dungeon.md), [Combat](collection/combat.md), [Player](collection/player.md), [Rewards](collection/rewards.md), [Progression](collection/progression.md). Le manifeste des exemples est [ici](fixtures/us-collecte-01/manifest.json).

La collecte stocke le corps brut append-only dans MongoDB, y compris les doublons, puis ACK après insertion durable. Elle n'attribue pas d'XP et ne calcule pas les classements. Les exigences de publication fiable et de traitement figurent dans le [guide de validation](collection/validation-us-collecte-01.md).

## État

- Format central, schéma, exemples et implémentation de réception MongoDB disponibles dans ce dépôt.
- Vérifications techniques : revue indépendante acceptée; 67 fichiers formatés; build Release sans avertissement ni erreur; 88 tests réussis, aucun échec ni ignoré; construction de l'image API réussie. Les commandes et limites sont consignées dans le plan de suivi. Cela ne prouve pas une exécution CI distante.
- AC4 charge réelle stricte (50/s pendant 600 s, p95 final <= premier et pente <=0) non validée. R3 a échoué à la ponctualité de publication (retard max 2592.7408 ms); R4 a été arrêté avant 600 s à la demande de l'utilisateur. Voir [notes du banc](../scripts/collection/README.md). Aucun résultat final R4 n'existe.
- L'adoption et la validation par les squads productrices restent à obtenir, sans bloquer la disponibilité de la définition centrale.

## Décisions de portée

Les faits sont émis après persistance métier. `dungeon-run-started` signifie run effectivement commencé, non donjon généré. `floor-entered` signifie transition appliquée; une répétition légitime reçoit un `transition_id` distinct. `skill-used` inclut une compétence esquivée mais exclut une commande refusée; mana nul est une valeur explicite valide. `combat-completed` est recevable pour victoire et défaite; Dungeon accepte les deux, Rewards confirme uniquement la victoire. Aucun événement de combat n'attribue lui-même l'XP.

Le créateur est le premier participant; entrée/sortie sont des faits lobby persistés. Déconnexion et reconnexion ne signifient pas départ. Chaque événement capture le clan à l'occurrence. Les objets instanciés ont quantity 1; le gain d'or identifie explicitement wallet joueur ou héros.

Restent hors des 16 routes: sélection de héros, équipement retiré, dépense d'or, ouverture de coffre, départ individuel pendant un run et notifications de classement. Aucun `leaderboard-updated` n'est requis par le périmètre actuel. Toute extension demande un besoin et un propriétaire définis.

## Livraison et archivage

Les exigences aux producteurs sont transaction métier + outbox atomiques, publication après commit, confirms et traitement des non-routables, retries avec mêmes octets et même MessageId. Elles ne prouvent pas l'implémentation externe. Le collecteur a sa propre file, conserve octets complets et toutes les réceptions/doublons, puis ACK après insert Mongo confirmé. Pas de déduplication du journal brut, TTL ou purge. Enveloppes inconnues/invalides/illisibles sont archivées avec statut; panne d'archivage suit retries espacés et quarantaine durable.
