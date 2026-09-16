# No magic strings (or numbers)

**Rule:** if a literal appears more than once, or carries domain meaning, or
maps to a server-side enum, it MUST be named.

## Why
Magic strings are the most common source of agent-introduced bugs in this repo:
typos in one of three call sites, drift between client and server, broken
refactors. Named symbols give you compile-time checks and discoverability.

## C# (server)

Server-side enums are the source of truth.

```csharp
// src/Soulsjwa.Api/Features/Events/Entities/TieBreakMode.cs
public enum TieBreakMode { ByTime = 0, SharedPlace = 1 }
```

On the wire we expose the enum's name (`enum.ToString()` →
`"ByTime" | "SharedPlace"`). When parsing in endpoints:

```csharp
if (!Enum.TryParse<TieBreakMode>(req.TieBreakMode, ignoreCase: false, out var mode))
    return TypedResults.ValidationProblem(...);
```

**In tests, use `nameof`** so a rename catches at compile time:

```csharp
// ✅
await client.PatchAsJsonAsync(url, new { tieBreakMode = nameof(TieBreakMode.SharedPlace) });
body.TieBreakMode.Should().Be(nameof(TieBreakMode.SharedPlace));

// ❌ — silently rots if the enum is renamed
await client.PatchAsJsonAsync(url, new { tieBreakMode = "SharedPlace" });
```

## TypeScript (client)

Mirror server enums as a `const` tuple + derived union so the list is both
iterable and type-safe:

```ts
// src/Soulsjwa.Web/src/types/index.ts
export const TIE_BREAK_MODES = ['ByTime', 'SharedPlace'] as const
export type TieBreakMode = (typeof TIE_BREAK_MODES)[number]

export const TIE_BREAK_MODE_LABELS: Record<TieBreakMode, string> = {
  ByTime: 'By completion time (1st, 2nd, 3rd)',
  SharedPlace: 'Shared place (ties share rank: 1, 1, 3)',
}
```

Use it in components — iterate the tuple, don't hand-write `<option>` lists:

```tsx
// ✅
{TIE_BREAK_MODES.map((mode) => (
  <option key={mode} value={mode}>{TIE_BREAK_MODE_LABELS[mode]}</option>
))}

// ❌
<option value="ByTime">By completion time</option>
<option value="SharedPlace">Shared place</option>
```

Cast `as TieBreakMode` **exactly once**, at the input boundary
(e.g. `e.target.value as TieBreakMode`), never sprinkled through call sites.

## Other repeated literals

| Kind | Where it lives |
|------|----------------|
| API base path | `src/Soulsjwa.Web/src/lib/axios/apiClient.ts` (`/api/v1`) |
| Per-feature endpoints | `src/features/<area>/api/*.ts` modules |
| TanStack query keys | `*Keys` object next to the feature's hooks |
| Repeated user-facing labels | `*_LABELS: Record<EnumType, string>` next to the type |

## Definition of done

When you introduce or touch a domain literal, run a repo-wide search
(`rg "'YourLiteral'"`) and fix every pre-existing magic-string occurrence in
the same PR. Don't leave half the codebase using the constant and half using
the bare literal.
