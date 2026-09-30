namespace AIMLAPP.Learning.LocalAiDemo.Llm;

public sealed record ModelCard(string Name, string FileFormat, string Quantization, string Memory, string Use);

// A local LLM is chosen by RAM and by the file format the runtime can load.
// GGUF is what Ollama runs. ONNX is what ONNX Runtime runs.
// Q4 stores each weight in about 4 bits, so the file fits on a laptop and loses some quality.
// Q4_K_M is llama.cpp's 4-bit "K-quant, medium" scheme. F16 = 16-bit, FP32 = full 32-bit.
// Rough RAM = parameters x bits / 8: an 8B model is ~16 GB at F16 but ~5 GB at Q4.
public static class ModelCards
{
    public static readonly IReadOnlyList<ModelCard> All =
    [
        new("llama3.2", "GGUF", "Q4_K_M", "about 2 GB", "Chat on a laptop CPU"),
        new("llama3.1:8b", "GGUF", "Q4_K_M", "about 5 GB", "Stronger chat, needs more RAM"),
        new("nomic-embed-text", "GGUF", "F16", "about 300 MB", "Local embeddings through Ollama"),
        new("all-MiniLM-L6-v2", "ONNX", "FP32", "about 90 MB", "Local embeddings through ONNX Runtime"),
    ];
}
