using System.Text.Json;

namespace AIMLAPP.Learning.LocalAiDemo.Onnx;

// A local model is weights in a file plus a runtime that multiplies them on this CPU.
// ONNX Runtime loads a .onnx file and does this. Ollama loads a GGUF file and does this.
// The 2-by-2 layer below is small enough to run with no download.
// See 08-LocalAI.md §3. An LLM is billions of these numbers in many stacked layers.
public sealed class LocalWeightModel
{
    // Weights (W) and Bias (b) ARE the model. Training is what picked these numbers;
    // inference just multiplies with them. Nothing here changes at run time.
    public required float[,] Weights { get; init; }
    public required float[] Bias { get; init; }

    public static LocalWeightModel TinyLinear { get; } = new()
    {
        // y = W x + b
        // W = | 1  0 |
        //     | 0  2 |
        // b = [0.5, -1]
        Weights = new float[,] { { 1f, 0f }, { 0f, 2f } },
        Bias = [0.5f, -1f],
    };

    // This method is the "runtime": inference = y = W x + b.
    // Each output y[r] = b[r] + sum over c of W[r, c] * x[c]  (one dot product per row).
    // ONNX Runtime and llama.cpp do the same maths, optimized for CPU/GPU.
    public float[] Predict(float[] input)
    {
        var rows = Weights.GetLength(0);
        var cols = Weights.GetLength(1);
        if (input.Length != cols)
            throw new ArgumentException($"Expected {cols} inputs.");

        var output = new float[rows];
        for (var r = 0; r < rows; r++)
        {
            var sum = Bias[r];
            for (var c = 0; c < cols; c++)
                sum += Weights[r, c] * input[c];
            output[r] = sum;
        }

        return output;
    }

    // Save + Load show that "the file is the model". A .onnx or .gguf file is the same
    // idea: shapes plus a flat array of numbers, in a binary format instead of JSON.
    // Q4 / F16 / FP32 (see ModelCards) is how many bits each of those numbers uses.
    public void Save(string path)
    {
        var payload = new SavedModel
        {
            Weights = Flatten(Weights),
            Rows = Weights.GetLength(0),
            Cols = Weights.GetLength(1),
            Bias = Bias,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }

    public static LocalWeightModel Load(string path)
    {
        var payload = JsonSerializer.Deserialize<SavedModel>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Model file was empty.");

        // Rebuild the 2-D matrix from the flat, row-major array (index = r * cols + c).
        var weights = new float[payload.Rows, payload.Cols];
        for (var i = 0; i < payload.Weights.Length; i++)
            weights[i / payload.Cols, i % payload.Cols] = payload.Weights[i];

        return new LocalWeightModel { Weights = weights, Bias = payload.Bias };
    }

    private static float[] Flatten(float[,] matrix)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        var flat = new float[rows * cols];
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
                flat[(r * cols) + c] = matrix[r, c];
        return flat;
    }

    private sealed class SavedModel
    {
        public float[] Weights { get; set; } = [];
        public int Rows { get; set; }
        public int Cols { get; set; }
        public float[] Bias { get; set; } = [];
    }
}
