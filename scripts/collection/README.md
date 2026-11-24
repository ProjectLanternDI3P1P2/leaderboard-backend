# Ancien banc de collecte

Le rapport sous `results/` documente l'implémentation locale avant fusion des PR #10 et #14.
Il n'est pas une preuve de performance de la chaîne intégrée actuelle.
Le programme de charge associé au second collecteur a été retiré lors de l'intégration.
Les tests du contrat restent sous `Leaderboard.Test/Collection/ContractFixtureTests.cs`.
Les tests d'intégration utilisent désormais le stockage de la PR #10 et le warehouse de la PR #14.
Aucun test de charge prolongé ne doit être lancé sans demande explicite.
