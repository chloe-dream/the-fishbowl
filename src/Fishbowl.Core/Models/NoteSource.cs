namespace Fishbowl.Core.Models;

// Who/what is writing this note. Drives automatic tag manipulation in
// NoteRepository: Mcp writes get `source:mcp` + `review:pending` so the
// human can catch them in the review inbox; Human edits strip any
// `review:pending` tag (approval-by-editing).
public enum NoteSource
{
    Human,
    Mcp,
}
