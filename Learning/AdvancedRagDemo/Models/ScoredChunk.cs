namespace AIMLAPP.Learning.AdvancedRagDemo.Models;

// A chunk plus a retrieval score (cosine, BM25, RRF, or rerank score).
public record ScoredChunk(Chunk Chunk, double Score);
