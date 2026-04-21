using System;

namespace Fishbowl.Core.Models;

public class Tag
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UsageCount { get; set; }

    // Protection flags — see SystemTags for semantics.
    public bool IsSystem { get; set; }
    public bool UserAssignable { get; set; } = true;
    public bool UserRemovable { get; set; } = true;
}
