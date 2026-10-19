# Plan de suivi — US-COLLECTE-01

Date : 2026-10-19. Statut : préparation documentaire ; aucune implémentation de collecte ou de stockage déclarée terminée.

## Objectif et périmètre

**List the events that each microservice needs to provide to us.**

Livrer un catalogue exploitable par les squads productrices et le futur collecteur RabbitMQ : événements, déclencheurs, données, contrats, routage et exemples vérifiables.

Référence : [catalogue des événements](../US-COLLECTE-01-catalogue-evenements.md).

L'utilisateur fournit les noms et versions des packages des autres microservices. Les noms de propriétés du catalogue ne constituent pas encore des schémas Protobuf publiés. Le contrat local et les exemples Player/Combat sont ceux d'un template : leur remplacement n'est pas un prérequis de cette US documentaire.

Hors périmètre : implémentation MongoDB (`US-DATA-01`), calcul XP/niveaux, projections de classement, développement des producteurs dans les autres dépôts, nettoyage général du template. La réalisation du consommateur et du banc RabbitMQ est un lot technique associé à attribuer, pas un élargissement implicite de cette US.

Convention : cocher uniquement après réalisation et vérification ; préciser l'artefact ou le résultat. Les cases cochées ci-dessous concernent uniquement les documents déjà modifiés.

Intervention technique distincte demandée ensuite par l'utilisateur : suppression du package Combat et de ses exemples, compilation, tests et démarrage du template. Voir [le rapport de validation](2026-10-19-suppression-contrat-combat.md). Ces résultats ne clôturent pas les tâches de collecte ci-dessous.

## 1. Cadrage documentaire — cette US

- [x] Recentrer le catalogue sur la liste des événements à fournir ; distinguer `US-DATA-01` et les futures projections.
- [x] Corriger `combat-completed` : victoire et défaite ; acceptation Dungeon dans les deux cas, confirmation Rewards uniquement pour une victoire.
- [x] Documenter la publication fiable par outbox transactionnelle côté producteur, confirmations, gestion des non-routables et reprises avec `MessageId` stable.
- [x] Remplacer l'exigence de déduplication du dépôt brut par la conservation des soumissions, y compris celles partageant un identifiant de transaction.
- [x] Documenter l'interface attendue avec le futur dépôt MongoDB immuable.
- [ ] Compléter la matrice ci-dessous à partir des packages et versions fournis par l'utilisateur.
- [ ] Pour chaque événement, indiquer : proposé / validé par la squad / disponible dans un package / vérifié avec un message réel. Ne pas confondre ces statuts.

### Matrice des contrats à compléter

| Producteur | Catalogue actuel | Package / version | Type Protobuf / enveloppe | Exchange / routing keys confirmés | Validation squad |
|---|---|---|---|---|---|
| Dungeon | Run commencé, étage entré, run terminé | À fournir | À confirmer | À confirmer | À obtenir |
| Combat | Combat commencé, compétence utilisée, combat terminé, consommable utilisé ; compatibilité `combat-completed` | À fournir | À confirmer | À confirmer | À obtenir |
| Player | Héros créé, participant entré, participant parti | À fournir | À confirmer | À confirmer | À obtenir |
| Rewards | Objet attribué, objet équipé, or gagné | À fournir | À confirmer | À confirmer | À obtenir |
| Progression | XP attribuée, changement de niveau : compléments proposés | À définir par le propriétaire | À confirmer | À confirmer | À obtenir |

## 2. Décisions contractuelles — cette US avec les squads

- [ ] Fixer les types, champs obligatoires, nullabilité, enums et versions de chaque contrat ; conserver les contrats dans le package de leur producteur.
- [ ] Définir explicitement l'identifiant de transaction, distinct de l'identifiant interne de réception ; confirmer sa relation éventuelle avec `MessageId` et les identifiants métier.
- [ ] Confirmer les noms/casses de `Producer`, l'enveloppe et la représentation des GUID et dates UTC sur le transport.
- [ ] Résoudre `player.game-session-participant-joined` versus `player.game-session-member-joined` ; préciser l'événement du créateur et les changements de héros.
- [ ] Définir départ, reconnexion et réentrée ; ne pas assimiler une coupure réseau à un départ métier.
- [ ] Confirmer le déclencheur du début de run, le modèle d'étage et l'identité d'une transition répétée légitime.
- [ ] Confirmer les résultats et snapshots des monstres, ainsi que les rangs autorisés.
- [ ] Définir la coexistence ou migration de `combat-ended` et `combat-completed` ; aucune perte de défaite et aucun calcul d'XP fondé sur une confirmation insuffisante.
- [ ] Confirmer l'identité des objets, l'ownership de l'or et du clan, et figer `ClanIdAtOccurrence` dans les faits.
- [ ] Identifier explicitement les extensions retenues ou différées : équipement retiré, dépenses, coffres, sélection de héros. Aucun `leaderboard-updated` à ajouter sans besoin.
- [ ] Aligner catalogue, architecture et configuration après validation ; ne pas remplacer les anciens abonnements sur la seule base d'une proposition.

## 3. Livraison RabbitMQ — contrat de transfert et lot technique associé

- [ ] Définir une file durable propre au collecteur, distincte des files métier ; confirmer qui provisionne exchanges, bindings, politique de livraison et DLQ.
- [ ] Lister uniquement les abonnements validés ; spécifier préfetch, acquittements manuels, reprises espacées et bornées, gestion des non-routables et des indisponibilités.
- [ ] Prévoir la prise en charge effective de ces paramètres par les options et le consommateur ; une clé de configuration seule n'implémente rien.
- [ ] Définir le traitement des enveloppes illisibles et versions inconnues : conserver le corps brut si possible, sinon mise en quarantaine durable explicite ; aucune perte silencieuse ni boucle immédiate de requeue.
- [ ] Documenter les garanties attendues des producteurs : état métier + outbox atomiques, publication après commit, confirmation broker, reprises conservant l'identité.
- [ ] Distinguer validation documentaire de ces garanties et preuve d'implémentation dans les autres dépôts ; cette US ne peut pas garantir leur code.

## 4. Passage de relais à US-DATA-01 — exigences à respecter

Parcours cible : `RabbitMQ → réception brute → ajout MongoDB confirmé durable → ACK`.

Le stockage reste à implémenter par le responsable de `US-DATA-01`. Un double ne prouve pas la durabilité MongoDB. Les points suivants constituent le contrat de transfert, sans imposer de bibliothèque ou schéma définitif.

- [ ] Convenir d'une opération d'ajout et d'une lecture par période avec le responsable de `US-DATA-01` ; aucune opération applicative de modification ou suppression d'événement engagé.
- [ ] Transmettre le corps original en octets, l'enveloppe/métadonnées disponibles, exchange/routing key, identifiant de transaction et `ReceivedAt` UTC. Convenir du point exact de capture de `ReceivedAt` et le conserver pendant une même tentative d'ajout.
- [ ] Attribuer un identifiant interne de réception distinct ; ne jamais utiliser transaction ID, `MessageId` ou clé métier comme filtre d'unicité du dépôt brut. Deux soumissions partageant ces valeurs restent conservables.
- [ ] Exiger un résultat d'ajout confirmant l'écriture durable avant ACK ; pas d'ACK en cas d'erreur ou résultat incertain. Documenter qu'une reprise après résultat incertain peut conserver une réception supplémentaire.
- [ ] Préserver les événements bruts pendant les rejeux et calculs ; toute déduplication des effets s'applique aux projections séparées, jamais en suppression ou en filtrage de l'historique brut.
- [ ] Confirmer l'absence de purge/TTL et la conservation sans limite planifiée, y compris des événements vieux d'un an.
- [ ] Fixer les bornes de période (proposition : début inclus, fin exclue), le tri croissant sur `ReceivedAt`, le départage stable et la pagination restituant toutes les réceptions sans omission.
- [ ] Obtenir la validation de ce contrat par le responsable de `US-DATA-01` avant de déclarer la chaîne complète opérationnelle.

## 5. Jeu de test contractuel — livrable de cette US

Préparer des fixtures avec données fictives, dates UTC fixes et identifiants explicites. Tant que les Protobuf ne sont pas validés, les exemples sont des descriptions, pas des preuves de compatibilité binaire.

- [ ] Créer un manifeste lisible des fixtures indiquant contrat/version, routing key, champs attendus et résultat attendu.
- [ ] Après réception des packages, produire les messages binaires avec les types générés des producteurs, sans dupliquer leurs `.proto`.
- [ ] Fournir au minimum un exemple par événement retenu et les variantes ci-dessous.
- [ ] Documenter les commandes de lancement, prérequis, résultat attendu et nettoyage limité à l'environnement de test.

| Cas | Données de test | Résultat attendu |
|---|---|---|
| Participation | Créateur et second joueur rejoignent avec leurs héros | Les deux faits et leurs acteurs sont identifiables |
| Étages | Début, passage 1→2, puis autre transition légitime 1→2 | Trois faits distincts, aucune déduplication par simple paire d'étages |
| Monstres | Combat avec monstre normal et boss | Instances, types, niveaux et rangs présents |
| Victoire / défaite | Un combat gagné, un perdu | Deux fins collectables ; défaite sans reward exigée |
| Compatibilité | `combat-ended` et `combat-completed` pour un même combat | Les deux événements bruts sont conservables ; pas d'attribution d'XP dans la collecte |
| Compétences | Coût positif, coût nul, compétence esquivée | Utilisations exécutées présentes ; commande refusée sans fait producteur |
| Rewards | Objet empilable, objet instancié et gain d'or | Identifiants, quantités et causes interprétables |
| Clan | Événement avec clan, puis changement de clan | Le premier événement conserve le clan à l'occurrence |
| Transaction identique | Deux payloads sous le même transaction ID | Deux réceptions conservables, aucune contrainte d'unicité sur cet ID |
| Redélivraison | Deux soumissions du même message | Deux réceptions conservables ; aucun filtre en amont du dépôt brut |
| Dates | Occurrence ancienne, réception récente, égalité de dates de réception | Occurrence et réception distinctes ; tri et bornes vérifiables |
| Invalide / inconnu | Corps illisible et version inconnue | Politique de conservation/quarantaine explicite, aucune perte silencieuse |

## 6. Tests exécutables — lot technique associé, à attribuer

Ces cases restent ouvertes jusqu'à l'existence du consommateur. Elles ne conditionnent pas artificiellement la clôture du catalogue documentaire.

- [ ] Monter un RabbitMQ de test isolé et un publisher utilisant les packages validés.
- [ ] Brancher le consommateur sur un dépôt de réception de test append-only pour vérifier routage et conservation des octets ; indiquer clairement qu'il ne valide pas MongoDB.
- [ ] Publier les fixtures et vérifier type, routage, contenu brut et métadonnées reçus.
- [ ] Bloquer puis libérer l'ajout dans le dépôt de test : aucun ACK avant succès ; message réessayable après échec.
- [ ] Tester broker indisponible, reconnexion, redémarrage et message poison sans boucle de requeue incontrôlée.
- [ ] Tester l'arrêt après écriture et avant ACK : nouvelle livraison conservable, aucune suppression de la réception précédente.
- [ ] Ajouter un smoke test avec commande unique et résultat explicite ; distinguer tests sur double et tests de bout en bout réels.

## 7. Acceptation intégrée — avec le responsable de US-DATA-01

À exécuter sur MongoDB réel lorsque son US fournit le dépôt. La responsabilité d'implémentation reste à son équipe.

- [ ] Deux événements sous le même transaction ID sont présents et inchangés.
- [ ] Les tentatives applicatives de modification et suppression d'un événement engagé sont rejetées.
- [ ] Un événement reçu il y a un an est présent et inchangé ; aucun mécanisme de purge ou TTL n'est planifié. Utiliser une fixture historique et vérifier la configuration, sans prétendre simuler une année d'exploitation.
- [ ] La lecture d'une période restitue toutes les réceptions dans l'ordre convenu, y compris doublons, égalités et limites de pagination.
- [ ] Les octets persistés correspondent aux octets soumis.
- [ ] Panne/reprise MongoDB et résultat d'écriture incertain ne provoquent aucun ACK anticipé ; les réceptions supplémentaires restent conservées.

## Clôture

- [ ] US-COLLECTE-01 : catalogue validé avec les squads, matrice renseignée, exemples livrés et contrat de transfert accepté ; lister explicitement tout événement différé.
- [ ] Lot technique RabbitMQ, si attribué : fixtures publiées/reçues et scénarios de panne exécutés avec preuves.
- [ ] Intégration US-DATA-01 : tests MongoDB exécutés conjointement ; ne pas marquer ce lot terminé avec un dépôt simulé.
