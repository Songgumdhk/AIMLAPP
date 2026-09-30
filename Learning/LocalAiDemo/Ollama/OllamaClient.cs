using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIMLAPP.Configuration;

namespace AIMLAPP.Learning.LocalAiDemo.Ollama;

// Talks to a local Ollama process. The shape is the same as a cloud chat call.
// The bytes stay on this machine: the host is 127.0.0.1, not api.openai.com.
// Plain HttpClient + JSON, so every request Ollama receives is visible here.
// See 08-LocalAI.md §2.
public sealed class OllamaClient : IDisposable
{
    // WHY loopback (127.0.0.1 / localhost): requests never leave this computer.
    // LocalAiChecks refuses to start if appsettings.json → Ollama:BaseUrl points elsewhere,
    // because a remote host would send your prompts over the network.
    public static string DefaultBaseUrl => AppSettings.Current.Ollama.BaseUrl;

    private readonly HttpClient _http;

    // Long timeout (Ollama:RequestTimeoutSeconds, 120 by default): the first call after
    // startup loads the model weights from disk into RAM, which can take a while on CPU.
    public OllamaClient()
        : this(DefaultBaseUrl, TimeSpan.FromSeconds(AppSettings.Current.Ollama.RequestTimeoutSeconds))
    {
    }

    public OllamaClient(string baseUrl, TimeSpan timeout)
    {
        // No API key header: a local server has no account or billing to check.
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = timeout,
        };
    }

    // Health check. Ollama is optional in this chapter, so "not running" must be a
    // quick false (2 s), not a 120 s hang or an exception that ends the demo.
    public async Task<bool> IsReachableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await _http.GetAsync("/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    // /api/tags lists the model files already pulled to disk (same as `ollama list`).
    // Names include a tag, e.g. "llama3.2:latest", which is why callers use StartsWith.
    public async Task<IReadOnlyList<string>> ListModelsAsync()
    {
        using var doc = await GetJsonAsync("/api/tags");
        if (!doc.RootElement.TryGetProperty("models", out var models))
            return [];

        var names = new List<string>();
        foreach (var model in models.EnumerateArray())
        {
            if (model.TryGetProperty("name", out var name))
            {
                var value = name.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    names.Add(value);
            }
        }

        return names;
    }

    public Task<string> ChatAsync(string model, string prompt) =>
        ChatAsync(model, [new OllamaMessage("user", prompt)]);

    // Sends the whole conversation. Ollama does not remember the previous call
    // unless these messages are sent again.
    // (Same as the OpenAI chat API: the server is stateless, the caller owns the history.)
    public async Task<string> ChatAsync(string model, IReadOnlyList<OllamaMessage> messages)
    {
        // stream = false: wait for the finished reply as one JSON object.
        var body = new
        {
            model,
            stream = false,
            messages = messages.Select(message => new { role = message.Role, content = message.Content }).ToArray(),
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", body);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (doc.RootElement.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content))
        {
            return content.GetString() ?? "";
        }

        return "";
    }

    // stream:true asks Ollama to send one JSON line per chunk instead of the finished reply.
    // ResponseHeadersRead lets us print those lines before the response body is complete.
    public async Task<string> ChatStreamAsync(
        string model,
        IReadOnlyList<OllamaMessage> messages,
        Action<string> onChunk)
    {
        var body = new
        {
            model,
            stream = true,
            messages = messages.Select(message => new { role = message.Role, content = message.Content }).ToArray(),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(body),
        };
        // WHY streaming: the model generates one token at a time. Printing chunks as they
        // arrive means the user sees words in ~a second instead of waiting for the whole reply.
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        // The body is NDJSON (newline-delimited JSON), e.g.
        //   {"message":{"role":"assistant","content":"Hel"},"done":false}
        //   {"message":{"role":"assistant","content":"lo"},"done":false}
        //   {"done":true, ...timing stats...}
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        var full = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync();
            if (line is null)
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var chunk = ReadChunk(doc.RootElement);
            // Two destinations: onChunk shows text live; `full` rebuilds the complete reply
            // so the caller can append it to the chat history for the next turn.
            if (chunk.Length > 0)
            {
                full.Append(chunk);
                onChunk(chunk);
            }

            if (doc.RootElement.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True)
                break;
        }

        return full.ToString();
    }

    public async Task<float[]> EmbedAsync(string model, string text) =>
        (await EmbedAsync(model, [text]))[0];

    // /api/embed takes a batch and returns one vector per input, in the same order.
    // `model` must be an embedding model (e.g. nomic-embed-text), not a chat model.
    // Vectors from different models live in different spaces and can't be compared.
    public async Task<IReadOnlyList<float[]>> EmbedAsync(string model, IReadOnlyList<string> inputs)
    {
        var body = new { model, input = inputs };

        using var response = await _http.PostAsJsonAsync("/api/embed", body);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("embeddings", out var embeddings))
            return [];

        var vectors = new List<float[]>();
        foreach (var row in embeddings.EnumerateArray())
        {
            var vector = new float[row.GetArrayLength()];
            var i = 0;
            foreach (var value in row.EnumerateArray())
                vector[i++] = value.GetSingle();
            vectors.Add(vector);
        }

        return vectors;
    }

    private static string ReadChunk(JsonElement root)
    {
        if (root.TryGetProperty("message", out var message)
            && message.TryGetProperty("content", out var content))
        {
            return content.GetString() ?? "";
        }

        return "";
    }

    public void Dispose() => _http.Dispose();

    private async Task<JsonDocument> GetJsonAsync(string path)
    {
        var json = await _http.GetStringAsync(path);
        return JsonDocument.Parse(json);
    }
}

// Role is "system", "user" or "assistant", the same roles as the OpenAI chat API.
public sealed record OllamaMessage(string Role, string Content);
