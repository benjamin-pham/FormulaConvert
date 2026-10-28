using System.Text;

namespace FormulaConvert.Cli;

/// <summary>Command line front end: <c>formulaconvert omml2latex|latex2omml [file]</c>.</summary>
public static class CliApp
{
    private const string Usage =
        "Usage: formulaconvert <omml2latex|latex2omml> [file]\n" +
        "  Reads [file] (UTF-8) or standard input; writes the result to standard output.\n" +
        "  Warnings go to standard error. Exit code: 0 success, 1 error.";

    public static int Run(string[] args, TextReader stdin, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            if (args.Length is < 1 or > 2 || (args[0] != "omml2latex" && args[0] != "latex2omml"))
            {
                stderr.Write(Usage + "\n");
                return 1;
            }

            string input;
            if (args.Length == 2)
            {
                try
                {
                    input = File.ReadAllText(args[1], new UTF8Encoding(false));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    stderr.Write($"error: cannot read file '{args[1]}': {ex.Message}\n");
                    return 1;
                }
            }
            else
            {
                input = stdin.ReadToEnd();
            }

            ConvertResult result;
            if (args[0] == "latex2omml")
            {
                if (input.EndsWith("\r\n", StringComparison.Ordinal)) input = input[..^2];
                else if (input.EndsWith('\n')) input = input[..^1];
                result = FormulaConverter.LatexToOmml(input);
            }
            else
            {
                result = FormulaConverter.OmmlToLatex(input);
            }

            foreach (var d in result.Diagnostics)
                stderr.Write($"warning [{d.Code}] position {d.Position}: {d.Message}\n");
            stdout.Write(result.Output + "\n");
            return 0;
        }
        catch (FormulaParseException ex)
        {
            var pos = ex.Position >= 0 ? $" (position {ex.Position})" : "";
            stderr.Write($"error: {ex.Message}{pos}\n");
            return 1;
        }
        catch (Exception ex)
        {
            stderr.Write($"error: internal error: {ex.GetType().Name}: {ex.Message}\n");
            return 1;
        }
    }
}
