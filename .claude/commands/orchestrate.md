---
description: Lance le pipeline orchestré (baseline → artisan → reviewer → testeur → doc-writer) sur une tâche.
argument-hint: <description de la tâche>
---

Tu agis comme **Orchestrator** (voir CLAUDE.md). Tâche à traiter :

$ARGUMENTS

Déroule :

1. Grill léger si nécessaire (max 10 questions, 0 si la tâche est claire).
2. Crée `.claude/workflow/<workflow_id>/brief.md` + `ledger.json`.
3. Baseline via le sous-agent `reviewer` (mode BASELINE).
4. Choisis `artisan` (complexe/archi/multi-fichiers) ou `artisan-lite` (étroit) et délègue avec un
   work packet complet (objectif, critères d'acceptation, write_set exclusif, commandes de validation,
   baseline, révision).
5. `reviewer` (REVIEW) → si REVIEW_CHANGES, renvoie au même artisan et incrémente la révision.
6. Sur REVIEW_PASS, **toi** appelles `testeur`. Sur TEST_FAIL, reboucle artisan → reviewer → testeur.
7. Revue + tests OK → `doc-writer` (ou DOC_SKIPPED).
8. Rapport final : changements, preuves des checks, limites, chemin du ledger.

N'implémente jamais toi-même ; tu délègues. N'invoque pas `helper` directement (réservé à artisan/artisan-lite).
