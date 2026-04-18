using Spectre.Console;

namespace Fishbowl.Host;

public static class StartupBranding
{
    public static void PrintBanner()
    {
        try
        {
            // Simple check to avoid clearing if not a real terminal
            if (!Console.IsOutputRedirected)
            {
                AnsiConsole.Clear();
            }

            AnsiConsole.Write(new Rule("[gold1]THE FISHBOWL[/]").RuleStyle("grey"));
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[bold gold1]  > [/] [bold white]THE FISHBOWL[/]");
            AnsiConsole.MarkupLine("[bold grey]    v1.0.0-alpha[/] - [italic]Your memory lives here. You don't.[/]");
            AnsiConsole.WriteLine();

            var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Grey);
            table.AddColumn(new TableColumn("[u]Service[/]").Centered());
            table.AddColumn(new TableColumn("[u]Status[/]").Centered());
            table.AddColumn(new TableColumn("[u]Endpoint[/]").LeftAligned());

            table.AddRow("Web UI", "[green]Running[/]", "[link=https://localhost:7180]https://localhost:7180[/]");
            table.AddRow("REST API", "[green]Ready[/]", "https://localhost:7180/api/notes");
            table.AddRow("Security", "[gold1]Google Auth[/]", "Enabled (Enforced SSL)");
            table.AddRow("Sovereignty", "[green]Encrypted[/]", "Client-side Vault (Key required)");

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]>[/] Press [bold white]Ctrl+C[/] to stop the bowl.");
            AnsiConsole.WriteLine();
        }
        catch
        {
            // Fail silent if console styling fails (e.g. in non-interactive environments)
            Console.WriteLine("The Fishbowl starting at https://localhost:7180");
        }
    }
}
