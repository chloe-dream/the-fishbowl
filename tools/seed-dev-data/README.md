# seed-dev-data

Populates a Fishbowl context (personal or team) with a small, opinionated set
of sample notes, todos, and contacts so the UI and MCP tools have something
to work against during development.

## Usage

```bash
# Personal workspace of the first user in system.db
dotnet run --project tools/seed-dev-data

# Specific user
dotnet run --project tools/seed-dev-data -- --user 01HZ123...

# Team workspace by slug
dotnet run --project tools/seed-dev-data -- --context team --context-id my-team

# Custom data directory
dotnet run --project tools/seed-dev-data -- --data ./fishbowl-data-alt

# Reseed on top of an already-seeded context (adds a fresh batch)
dotnet run --project tools/seed-dev-data -- --force
```

## What gets seeded

| Kind       | Count | Markers                                                   |
|------------|-------|-----------------------------------------------------------|
| Notes      | 5     | `seed:dev` tag + `[seed-dev] ` title prefix               |
| Todos      | 4     | `[seed-dev] ` title prefix, one completed                 |
| Contacts   | 4     | `[seed-dev] ` name prefix, one archived                   |

One note includes a `::secret` block so you can verify the secret-strip path.

## Idempotency

The tool refuses to reseed if any row already carries the sentinel — otherwise
every rerun would double the sample set. `--force` overrides that check.

Exit codes match the other dev tools:

| Code | Meaning                                |
|------|----------------------------------------|
| 0    | Seeded (or already seeded — see stdout) |
| 2    | `system.db` missing                    |
| 3    | No users in `system.db`                |
| 5    | `--context team` without `--context-id` |
| 6    | Team slug not found                    |
| 8    | Unknown `--context` value              |

stdout is a single-line JSON summary: `{"notes":N,"contacts":N,"todos":N,"reseeded":true|false}`.
