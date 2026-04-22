# mint-dev-key

Dev utility. Mints a personal-scope API key and prints the raw token to stdout.

## Usage

```bash
# uses the first user in src/Fishbowl.Host/fishbowl-data/system.db (typical dev setup)
dotnet run --project tools/mint-dev-key -- --data src/Fishbowl.Host/fishbowl-data

# pick a specific user or data root
dotnet run --project tools/mint-dev-key -- \
  --data src/Fishbowl.Host/fishbowl-data \
  --user 10d3e1b2-a2e5-4bde-90a4-d8067dcdabfd \
  --name my-test-key
```

The first line of stdout is the raw token (`fb_live_…`). Everything else (including the minted key's id and scopes) goes to stderr so the output of the tool stays pipe-friendly:

```bash
TOKEN=$(dotnet run --project tools/mint-dev-key -- --data src/Fishbowl.Host/fishbowl-data 2>/dev/null)
```

## Scopes

Fixed at `read:notes` + `write:notes`. Matches what Claude Code needs for every current tool (`search_memory`, `remember`, `get_memory`, `update_memory`, `list_pending`). If you need a narrower key for scope-enforcement testing, mint via the UI (Settings → API Keys).

## Not a production path

Production keys get minted through the web UI — users see the token exactly once, then it's SHA-256 hashed at rest. This tool bypasses the UI for local development convenience only; the underlying storage + hashing goes through the real `ApiKeyRepository`, so tokens minted here are indistinguishable at runtime from UI-minted ones.
