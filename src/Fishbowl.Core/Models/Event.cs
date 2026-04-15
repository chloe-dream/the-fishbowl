using System;

namespace Fishbowl.Core.Models;

public class Event
{
    public string Id { get; set; } = string.Empty; // ULID
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public bool AllDay { get; set; }
    public string? RRule { get; set; } // iCal RRULE
    public string? Location { get; set; }
    public int? ReminderMinutes { get; set; }
    public string? ExternalId { get; set; }
    public string? ExternalSource { get; set; } // 'google' | 'ical'
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
