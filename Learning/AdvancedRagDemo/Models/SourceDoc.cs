namespace AIMLAPP.Learning.AdvancedRagDemo.Models;

// A raw document before chunking. Lives in SampleCorpus and is split
// into Chunk records during ingestion.
public record SourceDoc(
    string Id,
    string Content,
    string Category,     // e.g. "policy", "howto", "product"
    string Department,   // e.g. "HR", "Engineering", "IT", "Sales", "All"
    DateTime CreatedAt);
