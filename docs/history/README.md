# History

Documents kept for the reasoning they record, not as a description of the
current code. Nothing in this folder is a backlog or a plan.

| Document | What it is | Status |
|---|---|---|
| [backend-review.md](backend-review.md) | The staff-level backend review and its implementation backlog (items `BE-001`–`BE-039`, `XC-1`–`XC-3`) | **Completed.** Landed across PRs #200 and #203 and follow-ups; commit subjects carry the item ids. Kept because docs and code comments cite the ids for their rationale. |
| [frontend-review.md](frontend-review.md) | The frontend review and backlog (`FE-001`–`FE-021`, `CC-1`–`CC-3`) | **Completed.** Same PRs. |
| [frontend-design-system.md](frontend-design-system.md) | A design-system rebuild proposal (tokens file, page primitives, `EventDetailPage`) | **Never implemented as written.** The SPA uses stock MUI with the admin-defined site theme (see [../frontend.md](../frontend.md#theming)); the proposal's file names do not exist. |
| [flows/](flows/README.md) | Screenshot walkthroughs of the main web flows | **Captured against an earlier UI** (three admin tabs; no Calendar, Rules, Scoreboard or Catalog tabs). The steps are still broadly right; the screenshots are not. |

When a doc elsewhere says "see BE-025" or "FE-013", it refers to an item in
the first two files above.
