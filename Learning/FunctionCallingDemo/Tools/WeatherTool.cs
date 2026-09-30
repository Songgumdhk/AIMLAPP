using System.Text.Json;

namespace AIMLAPP.Learning.FunctionCallingDemo.Tools;

// A TOOL = schema (what the LLM sees) + implementation (what your code runs).
// Fake weather lookup. Happy path + deliberate Atlantis failure (Exercise #2).
public static class WeatherTool
{
    public const string Name = "get_weather";

    // The schema is plain JSON Schema (§3). The LLM never sees the C# below,
    // only this name, description and parameter list.
    public static OpenAI.Chat.ChatTool Schema => OpenAI.Chat.ChatTool.CreateFunctionTool(
        functionName: Name,
        // WHY so wordy: the model picks tools by reading the description, not the
        // name. Say WHEN to use it, and invite parallel calls explicitly.
        functionDescription:
            "Get the current weather and temperature for a specific city. " +
            "Use this whenever the user asks about weather, temperature, rain, or forecast. " +
            "Can be called multiple times in parallel for different cities.",
        // "enum" restricts unit to two valid values. "required" lists only city,
        // which tells the model unit is optional.
        functionParameters: BinaryData.FromString("""
        {
            "type": "object",
            "properties": {
                "city": {
                    "type": "string",
                    "description": "The city name, e.g. 'Sydney' or 'New York'."
                },
                "unit": {
                    "type": "string",
                    "enum": ["celsius", "fahrenheit"],
                    "description": "Temperature unit. Defaults to celsius."
                }
            },
            "required": ["city"]
        }
        """));

    public static Task<string> ExecuteAsync(JsonElement args)
    {
        // "unit" is optional in the schema, so the model may leave it out (§5).
        var city = args.GetProperty("city").GetString() ?? "unknown";
        var unit = args.TryGetProperty("unit", out var unitEl)
            ? unitEl.GetString() ?? "celsius"
            : "celsius";

        // EXERCISE #2 — Simulated failure. Try: "What's the weather in Atlantis?"
        // Throwing is OK here only because ToolDispatcher catches it and returns
        // the message to the LLM as the tool result (§8).
        if (city.Equals("Atlantis", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"City '{city}' not found in weather service.");
        }

        var tempC = 15 + city.Length;
        var temp = unit == "fahrenheit" ? tempC * 9 / 5 + 32 : tempC;
        var symbol = unit == "fahrenheit" ? "F" : "C";

        return Task.FromResult($"{city}: {temp}°{symbol}, partly cloudy");
    }
}
