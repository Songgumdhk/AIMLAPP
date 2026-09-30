using System.Text.Json;

namespace AIMLAPP.Learning.LocalAiDemo.Embeddings;

public sealed record StoredVector(string Id, string Text, float[] Vector);

// The smallest possible vector database: one JSON file of document vectors.
// Embed a document once, save it, and later runs read it back instead of calling the model.
// A vector is only reusable with the model that made it, so the model name is saved too.
public sealed class VectorStore
{
    public string Model { get; set; } = "";
    public int Dimensions { get; set; }
    public List<StoredVector> Items { get; set; } = [];

    // WHY cache at all: embedding every document is the slow part of local RAG on a CPU.
    // Documents rarely change, the question does. Embed docs once, embed only the query per run.
    public static VectorStore Open(string path, string model)
    {
        if (File.Exists(path))
        {
            var saved = JsonSerializer.Deserialize<VectorStore>(File.ReadAllText(path));
            // CRITICAL: a cache built by a different embedding model is discarded, not reused.
            // Its vectors live in another space, so cosine against the new query would be noise.
            if (saved is not null && saved.Model == model)
                return saved;
        }

        return new VectorStore { Model = model };
    }

    // Matching on the text as well as the id means an edited document gets embedded again.
    public float[]? Find(string id, string text) =>
        Items.FirstOrDefault(item => item.Id == id && item.Text == text)?.Vector;

    public void Upsert(string id, string text, float[] vector)
    {
        Items.RemoveAll(item => item.Id == id);
        Items.Add(new StoredVector(id, text, vector));
        Dimensions = vector.Length;
    }

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this));
}
