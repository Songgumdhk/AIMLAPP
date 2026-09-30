namespace AIMLAPP.ConsoleUi;

// Plain ASCII only, so it renders the same in every terminal and code page.
// Never call this in --mcp mode: stdout belongs to the MCP JSON-RPC stream there.
public static class AppBanner
{
    private const string Logo = """
           _                             ____               _    ___
          | |    ___  __ _ _ __ _ __    / ___| ___ _ __    / \  |_ _|
          | |   / _ \/ _` | '__| '_ \  | |  _ / _ \ '_ \  / _ \  | |
          | |__|  __/ (_| | |  | | | | | |_| |  __/ | | |/ ___ \ | |
          |_____\___|\__,_|_|  |_| |_|  \____|\___|_| |_/_/   \_\___|
        """;

    private static readonly string Rule = new('=', 64);
    private static readonly string ThinRule = new('-', 64);

    public static void PrintMenu()
    {
        Console.WriteLine(Rule);
        WriteColored(Logo, ConsoleColor.Cyan);
        Console.WriteLine();
        WriteColored("   AI Engineering in .NET  |  C#  |  OpenAI  |  SQL Vectors  |  MCP", ConsoleColor.Yellow);
        WriteColored("   From your first tool call to production and local AI.", ConsoleColor.DarkGray);
        Console.WriteLine(Rule);

        Console.WriteLine();
        WriteColored(" [ SAMPLE APP ]  interview questions from SQL Server vectors", ConsoleColor.Green);
        WriteColored("   Video walkthrough: https://www.youtube.com/watch?v=lEUPgdv0gY8", ConsoleColor.DarkGray);
        Console.WriteLine(ThinRule);
        Console.WriteLine("   1) Chat with agent");
        Console.WriteLine("   2) Seed Experiences table");
        Console.WriteLine("   3) Run interview (match experience -> ask questions)");
        Console.WriteLine("   4) Run MCP server (stdio)");

        Console.WriteLine();
        WriteColored(" [ LEARNING PATH ]  read the matching guide in Learning/", ConsoleColor.Green);
        Console.WriteLine(ThinRule);
        Console.WriteLine("   5) Chapter 1 - Function Calling");
        Console.WriteLine("   6) Chapter 2 - Semantic Kernel");
        Console.WriteLine("   7) Chapter 3 - Advanced RAG");
        Console.WriteLine("   8) Chapter 4 - Agentic Workflows");
        Console.WriteLine("   9) Chapter 5 - AI Evaluation");
        Console.WriteLine("  10) Chapter 6 - Observability");
        Console.WriteLine("  11) Chapter 7 - Production AI");
        Console.WriteLine("  12) Chapter 8 - Local AI            (no API key needed)");
        Console.WriteLine(Rule);
        Console.Write("Selection: ");
    }

    private static void WriteColored(string text, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = previous;
    }
}
