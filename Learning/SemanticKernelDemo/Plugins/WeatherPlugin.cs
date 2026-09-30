using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AIMLAPP.Learning.SemanticKernelDemo.Plugins;

// A PLUGIN — Chapter 1's WeatherTool (schema + code) collapsed into one method.
// Compare to Chapter 1: the JSON schema string is gone. SK generates:
//   { "type":"object", "properties": { "city": {"type":"string",...} } }
// directly from this method signature + attributes.
public class WeatherPlugin
{
    // [KernelFunction] exposes the method as a tool; the string sets the tool name.
    // [Description] becomes the tool's "description" in the schema. It is still
    // what the LLM reads to decide WHEN to call this, so write it just as carefully.
    [KernelFunction("get_weather")]
    [Description("Get the current weather and temperature for a specific city. " +
                 "Use this whenever the user asks about weather, temperature, rain, " +
                 "or forecast. Can be called multiple times in parallel.")]
    // Parameter [Description]s fill in each property's description. The default
    // value on `unit` makes it optional in the schema (not in "required").
    // SK also converts the model's JSON arguments to typed C# values for you.
    public string GetWeather(
        [Description("City name, e.g. 'Sydney' or 'New York'.")] string city,
        [Description("Temperature unit: 'celsius' or 'fahrenheit'.")] string unit = "celsius")
    {
        // Same deliberate failure as Chapter 1 (Exercise #2).
        // SK will catch this exception, feed the message back to the LLM as
        // the tool result, and let the LLM adapt gracefully. No try/catch
        // needed here.
        if (city.Equals("Atlantis", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"City '{city}' not found in weather service.");
        }

        var tempC = 15 + city.Length;
        var temp = unit == "fahrenheit" ? tempC * 9 / 5 + 32 : tempC;
        var symbol = unit == "fahrenheit" ? "F" : "C";
        return $"{city}: {temp}°{symbol}, partly cloudy";
    }
}
