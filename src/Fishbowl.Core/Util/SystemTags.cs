namespace Fishbowl.Core.Util;

// Reserved tag names seeded at user-DB migration v3. Each system tag carries
// three independent flags:
//
//   is_system        — the NAME is load-bearing. Rename/delete of the tag row
//                      is rejected by TagRepository. Colour edits are allowed.
//   user_assignable  — the user can attach this tag to a note via the UI.
//                      When false, only system/MCP writes can put it on a note.
//   user_removable   — the user can detach this tag from a note via the UI
//                      (the × on fb-tag-chip). When false, the chip renders
//                      without the remove button.
//
// Default for user-created tags is (false, true, true): a regular user tag is
// not system, is user-assignable, and is user-removable. Column defaults in
// the v3 schema encode that.
public readonly record struct SystemTagSpec(
    string Name,
    string Color,
    bool UserAssignable,
    bool UserRemovable);

public static class SystemTags
{
    public static readonly IReadOnlyList<SystemTagSpec> Seeds = new[]
    {
        // Marks a note awaiting human review after an MCP write. Humans
        // remove the tag to approve — hence user_removable = true.
        new SystemTagSpec("review:pending", "yellow", UserAssignable: false, UserRemovable: true),

        // Provenance marker for notes written via MCP. Locked on — users
        // cannot attach it themselves and cannot strip it off.
        new SystemTagSpec("source:mcp",     "purple", UserAssignable: false, UserRemovable: false),
    };

    public static readonly IReadOnlyList<string> ReservedNames =
        Seeds.Select(s => s.Name).ToList();

    public static bool IsReserved(string name) => ReservedNames.Contains(name);
}
