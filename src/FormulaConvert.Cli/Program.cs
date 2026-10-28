using System.Text;
using FormulaConvert.Cli;

var utf8 = new UTF8Encoding(false);
using var stdin = new StreamReader(Console.OpenStandardInput(), utf8);
using var stdout = new StreamWriter(Console.OpenStandardOutput(), utf8);
using var stderr = new StreamWriter(Console.OpenStandardError(), utf8);
var code = CliApp.Run(args, stdin, stdout, stderr);
stdout.Flush();
stderr.Flush();
return code;
