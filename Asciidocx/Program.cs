using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Asciidocx
{
    // Usage:
    // asciidocx.exe ../hello-asciidoc.txt -to docx ../hello-asciidoc.docx
    // asciidocx.exe ../hello-asciidoc.txt ../hello-asciidoc.docx
    // asciidocx.exe ../hello-asciidoc.txt -to docx
    // asciidocx.exe ../hello-asciidoc.txt -to pdf
    // Return codes:
    // 0 - normal exit
    // 1 - error occured
    // -1 - help requested, no actual work done
    public static class Program
    {
        private const string Usage = "Usage:\n\tinput_file [-to (docx|pdf|html|markdown)] [output_file]";

        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Out.WriteLine("Type \"help\" (without quotes) to display the command usage.");
                return -1;
            }
            if (args[0].ToLower() == "help")
            {
                Console.Out.WriteLine(Usage);
                return -1;
            }

            var input = string.Empty;
            var outputFormat = string.Empty;
            var output = string.Empty;

            var argsRegex = new Regex(@"(^)(?<input>[^\s]+)(\s)+((-to )(?<format>[a-zA-Z]+))?(\s?)+(?<output>[^-][^\s]+)?(\n|\r|\r\n)");
            var argsString = args.Aggregate(string.Empty, (current, s) => current + s + " ").TrimEnd(' ');
            argsString += Environment.NewLine;
            var argsMatch = argsRegex.Match(argsString);
#if DEBUG
            Console.Out.WriteLine(argsString);
#endif
            if (argsMatch.Success)
            {
                input = argsMatch.Groups["input"].Value;
                outputFormat = argsMatch.Groups["format"].Value;
                output = argsMatch.Groups["output"].Value;
#if DEBUG
                Console.Out.WriteLine($"input: {input}, format: {outputFormat}, output: {output}");
#endif
            }
            else
            {
                Console.Out.WriteLine("error while reading input.\n" + Usage);
                return 1;
            }
            if (string.IsNullOrWhiteSpace(outputFormat) && string.IsNullOrWhiteSpace(output))
            {
                Console.Out.WriteLine("error: either format or output file name with extension must be specified.");
                return 1;
            }
            if (string.IsNullOrWhiteSpace(outputFormat))
            {
                outputFormat = Path.GetExtension(output).TrimStart('.');
                if (outputFormat.ToLower() == "md")
                {
                    outputFormat = "markdown";
                }
#if DEBUG
                Console.Out.WriteLine($"recognized format for output file \"{output}\" is \"{outputFormat}\"");
#endif
            }
            else if (string.IsNullOrWhiteSpace(output))
            {
                output = input.Replace(Path.GetExtension(input), $".{outputFormat}");
#if DEBUG
                Console.Out.WriteLine($"constructed output file name for input file \"{input}\" and output" +
                                      $" format \"{outputFormat}\": {output}");
#endif
            }

            if (string.IsNullOrWhiteSpace(outputFormat))
            {
                Console.Out.WriteLine("error: output format was not specified");
                return 1;
            }
            if (outputFormat.ToLower() != "pdf" && outputFormat.ToLower() != "html"
                && outputFormat.ToLower() != "markdown" && outputFormat.ToLower() != "docx")
            {
                Console.Out.WriteLine($"error: output format \"{outputFormat}\" is not recognized");
                return 1;
            }
            
            return 0;
        }
    }
}

/* 
 * Regex validation data:
../hello-asciidoc.txt -f docx ../hello-asciidoc.docx
../hello-asciidoc.txt ../hello-asciidoc.docx
../hello-asciidoc.txt -f docx
../hello-asciidoc.txt -f pdf

../hello-asciidoc.txt -f
../hello-asciidoc.txt ../hello-asciidoc.docx -f
../hello-asciidoc.txt ../hello-asciidoc.docx -f pdf
../hello-asciidoc.txt -d pdf
../hello-asciidoc.txt -d pdf ../hello-asciidoc.docx 
../hello-asciidoc.txt -d pdf ../hello-asciidoc.docx -d pdf
../hello-asciidoc.txt -f pdf ../hello-asciidoc.docx -f pdf
 */
