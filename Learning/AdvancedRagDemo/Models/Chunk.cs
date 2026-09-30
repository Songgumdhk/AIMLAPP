namespace AIMLAPP.Learning.AdvancedRagDemo.Models;

// One indexable unit — text + embedding + metadata + IDs.
// Metadata is copied from the parent SourceDoc onto every chunk so filters (§9)
// work per chunk. SourceDocId lets you fetch the full parent doc (§4.5).
public record Chunk(
    string Id,
    string SourceDocId,
    string Content,
    float[] Embedding,
    string Category,
    string Department,
    DateTime CreatedAt);
