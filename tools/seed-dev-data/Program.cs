using Dapper;
using Fishbowl.Core;
using Fishbowl.Core.Models;
using Fishbowl.Data;
using Fishbowl.Data.Repositories;

// Dev utility: seeds a variety of notes, todos, and contacts into a
// context so the UI and MCP tools have real data to work against.
//
// Idempotent: every seeded row carries the `seed:dev` tag (notes) or
// the "[seed-dev]" name prefix (contacts/todos). Re-running is a no-op
// when the sentinel is already present — counts report the pre-existing
// state rather than re-inserting.
//
// Usage:
//   dotnet run --project tools/seed-dev-data -- \
//       [--data <path>] \
//       [--user <id>] \
//       [--context user|team] [--context-id <slug>] \
//       [--force]              (reseed even if sentinels already exist)
//
// Defaults:
//   --data fishbowl-data             (matches Fishbowl.Host's default)
//   --user <first user in system.db>
//   --context user                   (--context-id required when team)

var args_ = args;
var dataPath = GetArg("--data") ?? "fishbowl-data";
var userIdArg = GetArg("--user");
var contextArg = (GetArg("--context") ?? "user").ToLowerInvariant();
var contextIdArg = GetArg("--context-id");
var force = args_.Contains("--force");

if (!Directory.Exists(dataPath) || !File.Exists(Path.Combine(dataPath, "system.db")))
{
    Console.Error.WriteLine($"error: no system.db found at {Path.GetFullPath(dataPath)}/system.db — start the host at least once first.");
    return 2;
}

var factory = new DatabaseFactory(dataPath);

string userId;
if (!string.IsNullOrEmpty(userIdArg))
{
    userId = userIdArg;
}
else
{
    using var sys = factory.CreateSystemConnection();
    var first = sys.QueryFirstOrDefault<string>(
        "SELECT id FROM users ORDER BY created_at LIMIT 1");
    if (string.IsNullOrEmpty(first))
    {
        Console.Error.WriteLine("error: no users in system.db — log in via the web UI first, then retry.");
        return 3;
    }
    userId = first;
}

ContextRef ctx;
string contextDisplay;
if (contextArg == "user")
{
    ctx = ContextRef.User(userId);
    contextDisplay = $"user:{userId}";
}
else if (contextArg == "team")
{
    if (string.IsNullOrEmpty(contextIdArg))
    {
        Console.Error.WriteLine("error: --context team requires --context-id <slug>");
        return 5;
    }

    using var sys = factory.CreateSystemConnection();
    var teamRow = sys.QueryFirstOrDefault<(string Id, string Slug)>(
        "SELECT id AS Id, slug AS Slug FROM teams WHERE slug = @slug OR id = @slug",
        new { slug = contextIdArg });
    if (string.IsNullOrEmpty(teamRow.Id))
    {
        Console.Error.WriteLine($"error: no team found with slug or id '{contextIdArg}'");
        return 6;
    }
    ctx = ContextRef.Team(teamRow.Id);
    contextDisplay = $"team:{teamRow.Slug}";
}
else
{
    Console.Error.WriteLine($"error: --context must be 'user' or 'team' (got '{contextArg}')");
    return 8;
}

const string NoteSentinelTag  = "seed:dev";
const string NamePrefix        = "[seed-dev] ";

// Check the existing seed footprint. If we see the sentinel and --force
// isn't set, report and bail — reseeds would duplicate rows because none
// of the repos have upsert-by-name semantics.
using (var probe = factory.CreateContextConnection(ctx))
{
    var existingNotes = probe.ExecuteScalar<long>(
        "SELECT COUNT(*) FROM notes WHERE tags LIKE '%' || @tag || '%'",
        new { tag = NoteSentinelTag });
    var existingContacts = probe.ExecuteScalar<long>(
        "SELECT COUNT(*) FROM contacts WHERE name LIKE @pfx || '%'",
        new { pfx = NamePrefix });
    var existingTodos = probe.ExecuteScalar<long>(
        "SELECT COUNT(*) FROM todos WHERE title LIKE @pfx || '%'",
        new { pfx = NamePrefix });
    var existingEvents = probe.ExecuteScalar<long>(
        "SELECT COUNT(*) FROM events WHERE title LIKE @pfx || '%'",
        new { pfx = NamePrefix });

    if (!force && (existingNotes + existingContacts + existingTodos + existingEvents) > 0)
    {
        Console.Error.WriteLine(
            $"# already seeded in {contextDisplay}: " +
            $"{existingNotes} notes, {existingContacts} contacts, " +
            $"{existingTodos} todos, {existingEvents} events. " +
            "Pass --force to add another batch.");
        Console.WriteLine(
            $"{{\"notes\":{existingNotes},\"contacts\":{existingContacts}," +
            $"\"todos\":{existingTodos},\"events\":{existingEvents},\"reseeded\":false}}");
        return 0;
    }
}

var tagRepo     = new TagRepository(factory);
var noteRepo    = new NoteRepository(factory, tagRepo);
var todoRepo    = new TodoRepository(factory);
var contactRepo = new ContactRepository(factory);
var eventRepo   = new EventRepository(factory);

// ────────── Notes ──────────
var notes = new[]
{
    new Note
    {
        Title   = NamePrefix + "Welcome to the Fishbowl",
        Content = "This is a seeded note so the editor has something to open.\n" +
                  "Tags are regular user tags plus the `seed:dev` sentinel.",
        Tags    = new List<string> { "welcome", NoteSentinelTag },
    },
    new Note
    {
        Title   = NamePrefix + "Meeting notes — venue walkthrough",
        Content = "- stage left is the load-in door\n- power drops at both sides\n- sound check from 4pm",
        Tags    = new List<string> { "work", "venue", NoteSentinelTag },
    },
    new Note
    {
        Title   = NamePrefix + "Groceries",
        Content = "- eggs\n- oat milk\n- tomatoes\n- something for pasta",
        Tags    = new List<string> { "personal", "shopping", NoteSentinelTag },
    },
    new Note
    {
        Title   = NamePrefix + "Reading list",
        Content = "Anything about distributed consensus — the usual suspects.\n" +
                  "Tag for quick filter.",
        Tags    = new List<string> { "reading", NoteSentinelTag },
    },
    new Note
    {
        Title   = NamePrefix + "A note with a secret block",
        Content = "Public bit first.\n\n::secret\nsecretuser: alice\nsecretpass: hunter2\n::end\n\nPublic tail.",
        Tags    = new List<string> { "auth", NoteSentinelTag },
    },
};
var noteCount = 0;
foreach (var n in notes) { await noteRepo.CreateAsync(ctx, userId, n); noteCount++; }

// ────────── Todos ──────────
var now = DateTime.UtcNow;
var todos = new[]
{
    new TodoItem { Title = NamePrefix + "Ship the contacts UI" },
    new TodoItem { Title = NamePrefix + "Wire /api/v1/search into notes-view",
                   DueAt = now.AddDays(2) },
    new TodoItem { Title = NamePrefix + "Add settings download button",
                   DueAt = now.AddDays(7) },
    new TodoItem { Title = NamePrefix + "Closed — already done",
                   CompletedAt = now.AddDays(-1) },
};
var todoCount = 0;
foreach (var t in todos) { await todoRepo.CreateAsync(ctx, userId, t); todoCount++; }

// ────────── Contacts ──────────
var contacts = new[]
{
    new Contact
    {
        Name  = NamePrefix + "Alice Example",
        Email = "alice@studio.example",
        Phone = "+49-30-111-222",
        Notes = "Venue sound engineer. Met at Q1 show. Prefers morning calls.",
    },
    new Contact
    {
        Name  = NamePrefix + "Bob Partner",
        Email = "bob@partner.example",
        Phone = "+1-555-0100",
        Notes = "Backup label contact. Responds fast on email, slow on WhatsApp.",
    },
    new Contact
    {
        Name  = NamePrefix + "Carol Caterer",
        Phone = "+33-1-40-55",
        Notes = "Event catering — the one with the great paella.",
    },
    new Contact
    {
        Name     = NamePrefix + "Dave Old-friend (archived)",
        Email    = "dave@archive.example",
        Notes    = "Not in touch anymore. Kept for history.",
        Archived = true,
    },
};
var contactCount = 0;
foreach (var c in contacts) { await contactRepo.CreateAsync(ctx, userId, c); contactCount++; }

// ────────── Events ──────────
var weekStart = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek);
var events = new[]
{
    new Event
    {
        Title    = NamePrefix + "Team sync",
        StartAt  = weekStart.AddDays(1).AddHours(10),
        EndAt    = weekStart.AddDays(1).AddHours(10).AddMinutes(30),
        RRule    = "FREQ=WEEKLY;BYDAY=MO",
        Location = "Meet link",
        ReminderMinutes = 10,
    },
    new Event
    {
        Title   = NamePrefix + "Venue walkthrough",
        StartAt = weekStart.AddDays(3).AddHours(16),
        EndAt   = weekStart.AddDays(3).AddHours(17),
        Location = "Alpine lodge",
    },
    new Event
    {
        Title       = NamePrefix + "Offsite",
        StartAt     = weekStart.AddDays(10).AddHours(9),
        EndAt       = weekStart.AddDays(11).AddHours(17),
        AllDay      = true,
        Description = "Two-day offsite — agenda in the shared doc.",
    },
};
var eventCount = 0;
foreach (var e in events) { await eventRepo.CreateAsync(ctx, userId, e); eventCount++; }

Console.Error.WriteLine(
    $"# seeded in {contextDisplay}: {noteCount} notes, {contactCount} contacts, " +
    $"{todoCount} todos, {eventCount} events");
Console.WriteLine(
    $"{{\"notes\":{noteCount},\"contacts\":{contactCount}," +
    $"\"todos\":{todoCount},\"events\":{eventCount},\"reseeded\":true}}");
return 0;

string? GetArg(string flag)
{
    var i = Array.IndexOf(args_, flag);
    if (i < 0 || i + 1 >= args_.Length) return null;
    return args_[i + 1];
}
