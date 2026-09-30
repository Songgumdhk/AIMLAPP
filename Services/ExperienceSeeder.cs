using AIMLAPP.Configuration;
using AIMLAPP.Data;
using AIMLAPP.Models;
using Microsoft.Data.SqlTypes;
using OpenAI.Embeddings;

namespace AIMLAPP.Services;

// Menu option 2. Embeds each profile description and stores it in a SQL Server VECTOR column.
// Guide: Services/02-SeedExperiences.md. Video walkthrough ("vectors and embeddings"):
// https://www.youtube.com/watch?v=lEUPgdv0gY8
public static class ExperienceSeeder
{
    private static string EmbeddingModel => AppSettings.Current.OpenAI.EmbeddingModel;

    private static readonly Dictionary<string, string> SampleExperiences = new()
    {
        // .NET / Backend
        ["Junior .NET developer with 0-2 years of experience"] =
            "C# fundamentals, OOP principles, LINQ, async/await basics, and unit testing with xUnit",

        ["Mid-level .NET developer with 3-5 years of experience"] =
            "ASP.NET Core Web API, Entity Framework Core, dependency injection, REST design, and integration testing",

        ["Senior .NET developer with 5-8 years of experience"] =
            "ASP.NET Core, EF Core performance tuning, design patterns, SOLID principles, and CI/CD pipelines",

        ["Principal .NET engineer with 8+ years of experience"] =
            "Domain-driven design, CQRS, event sourcing, microservices, and cross-team technical leadership",

        ["Software architect with 10+ years of experience"] =
            "System design, distributed systems, software architecture, scalability, and technology strategy",

        // Frontend
        ["Junior frontend developer with 0-2 years of experience"] =
            "HTML, CSS, JavaScript fundamentals, DOM manipulation, and basic React components",

        ["Mid-level React developer with 3-5 years of experience"] =
            "React hooks, state management, TypeScript, component design, and Jest testing",

        ["Senior frontend engineer with 5+ years of experience"] =
            "Advanced React patterns, performance optimization, accessibility, micro-frontends, and design systems",

        // Cloud / DevOps
        ["DevOps engineer with 3-5 years of experience"] =
            "Docker, Kubernetes, CI/CD pipelines, infrastructure as code with Terraform, and monitoring",

        ["Senior cloud engineer with 5+ years of experience on Azure"] =
            "Azure App Service, Azure Functions, AKS, networking, cost optimization, and security best practices",

        ["Site reliability engineer with 5+ years of experience"] =
            "Observability, SLIs/SLOs, incident response, chaos engineering, and capacity planning",

        // Data / AI
        ["Data engineer with 3-5 years of experience"] =
            "SQL, data modeling, ETL pipelines, Apache Spark, and data warehousing with Snowflake or BigQuery",

        ["Machine learning engineer with 3-5 years of experience"] =
            "Python, PyTorch or TensorFlow, feature engineering, model deployment, and MLOps",

        ["Senior AI engineer with 5+ years of experience"] =
            "LLM fine-tuning, RAG architectures, vector databases, prompt engineering, and model evaluation",

        // Mobile
        ["Mobile developer with 3-5 years of experience"] =
            "iOS with Swift or Android with Kotlin, mobile UI patterns, offline-first design, and app store deployment",

        // QA / Security
        ["QA automation engineer with 3-5 years of experience"] =
            "Test automation frameworks, Selenium or Playwright, API testing, and test strategy",

        ["Security engineer with 5+ years of experience"] =
            "OWASP Top 10, threat modeling, penetration testing, secure code review, and identity management"
    };

    public static async Task SeedAsync(string openAiApiKey)
    {
        Console.WriteLine("Seeding Experiences table...");

        var embeddingClient = new EmbeddingClient(EmbeddingModel, openAiApiKey);

        var experiences = await Task.WhenAll(
            SampleExperiences.Select(kvp => BuildExperienceAsync(kvp.Key, kvp.Value, embeddingClient)));

        await using var db = new AppDbContext();

        await db.Experiences.AddRangeAsync(experiences);
        var inserted = await db.SaveChangesAsync();

        Console.WriteLine($"Seed complete. {inserted} row(s) inserted.");
    }

    private static async Task<Experience> BuildExperienceAsync(
        string experienceText,
        string questions,
        EmbeddingClient embeddingClient)
    {
        Console.WriteLine($"  Embedding: \"{experienceText}\"");

        var embedding = await embeddingClient.GenerateEmbeddingAsync(experienceText);

        return new Experience
        {
            ExperienceText = experienceText,
            ExpVector = new SqlVector<float>(embedding.Value.ToFloats()),
            Questions = questions,
        };
    }
}
