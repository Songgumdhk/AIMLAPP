using AIMLAPP.Data;
using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using OpenAI.Embeddings;
using System.ComponentModel;

namespace AIMLAPP.Mcp;

[McpServerToolType]
public static class ExperienceMcpTools
{
    [McpServerTool(Name = "find_matching_experience"),
     Description("Given a free-form description of a candidate's experience/skills/years, " +
                 "find the closest matching experience profile in the database using cosine " +
                 "similarity on OpenAI embedding vectors, and return the matched " +
                 "profile along with its associated interview questions.")]
    public static async Task<object> FindMatchingExperienceAsync(
        [Description("Free-form candidate description, e.g. 'senior .NET dev with 6 years'.")]
        string experience,
        AppDbContext db,
        EmbeddingClient embeddingClient,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(experience))
            return new { error = "experience must be a non-empty string." };

        var embedding = await embeddingClient.GenerateEmbeddingAsync(experience, cancellationToken: cancellationToken);
        var userVector = new SqlVector<float>(embedding.Value.ToFloats());

        var match = await db.Experiences
            .AsNoTracking()
            .Select(e => new
            {
                e.Id,
                e.ExperienceText,
                e.Questions,
                Distance = EF.Functions.VectorDistance("cosine", e.ExpVector!.Value, userVector)
            })
            .OrderBy(x => x.Distance)
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null)
            return new { error = "No experiences found in the database." };

        return new
        {
            match.Id,
            experienceText = match.ExperienceText,
            match.Questions,
            cosineDistance = match.Distance
        };
    }

    [McpServerTool(Name = "list_experiences", ReadOnly = true),
     Description("List all seeded experience profiles with their id, text, and questions.")]
    public static async Task<object> ListExperiencesAsync(
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var rows = await db.Experiences
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                experienceText = e.ExperienceText,
                e.Questions
            })
            .ToListAsync(cancellationToken);

        return new { count = rows.Count, items = rows };
    }

    [McpServerTool(Name = "get_experience_by_id", ReadOnly = true),
     Description("Get a single experience profile by its numeric Id.")]
    public static async Task<object> GetExperienceByIdAsync(
        [Description("The Id of the experience row to fetch.")] int id,
        AppDbContext db,
        CancellationToken cancellationToken = default)
    {
        var row = await db.Experiences
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new
            {
                e.Id,
                experienceText = e.ExperienceText,
                e.Questions
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return new { error = $"No experience found with Id={id}." };

        return row;
    }
}
