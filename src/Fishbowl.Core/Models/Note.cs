using System;
using System.Collections.Generic;

namespace Fishbowl.Core.Models;

public class Note
{
    public string Id { get; set; } = string.Empty; // ULID
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; } // Markdown (public part)
    public byte[]? ContentSecret { get; set; } // AES-256 encrypted
    public string Type { get; set; } = "note"; // note | idea | journal | password
    public List<string> Tags { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Pinned { get; set; }
    public bool Archived { get; set; }
}
