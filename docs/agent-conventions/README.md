# Agent convention docs

Small, focused convention files for AI agents. Load only the file relevant to
your current change — don't load all of them eagerly. The high-level operating
manual is at [`/AGENTS.md`](../../AGENTS.md).

| File | Read when you're touching… |
|------|----------------------------|
| [`no-magic-strings.md`](./no-magic-strings.md) | adding a new enum, union, status string, or repeated literal |
| [`backend-endpoints.md`](./backend-endpoints.md) | adding/changing an ASP.NET minimal API endpoint |
| [`frontend-components.md`](./frontend-components.md) | adding/changing a React component, hook, or page |
| [`testing.md`](./testing.md) | writing or modifying tests |

If you find yourself wanting a convention that isn't documented here, add a new
small file in this folder rather than expanding `AGENTS.md` — keep the root file
short.
