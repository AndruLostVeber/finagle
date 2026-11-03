# SDD ledger — plan: docs/superpowers/plans/2026-11-03-quarter.md

- Rules agreed in two user rounds; final correction: one workday = one week, total 12.
- Read-only inspection completed by three agents. Existing source is not a Git repository.
- Backup: /private/tmp/kuro-before-quarter-20261103.tar.gz
- Baseline: npm test, 7 passed, 0 failed.

| Pair/task | Interface/file check | Ruling |
|---|---|---|
| A/C | WebBridge messages, no shared files | Preserve bridge; C owns templates, A owns C# |
| B/C | documented round/company/API fields | Contract above; report deviations immediately |
| B/D | llm exports/context and persistence | D owns provider, B owns authorization and immutable snapshots |
| C/D | new UI assets and generated copies | C source only; D builds/syncs |
| A | procedural scene with existing built-in pipeline | No URP migration |
| B | financial test authority and finalization | One owner, no client-submitted money |
| C | responsive UI and backend state | No legacy score application in new games |
| D | secrets and real provider verification | Missing access reported; fallback explicitly labeled |

Ruling: Work in supplied non-Git project with source backup — avoids inventing repo metadata and preserves open Unity workspace.
Ruling: Proceed after user's explicit agreement — requested confirmation already received; no repeated permission question.
