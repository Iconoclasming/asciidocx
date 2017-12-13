using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Asciidocx
{
    // Usage:
    // asciidocx.exe hello-asciidoc.txt -to docx hello-asciidoc.docx
    // asciidocx.exe hello-asciidoc.txt hello-asciidoc.docx
    // asciidocx.exe hello-asciidoc.txt -to docx
    // asciidocx.exe hello-asciidoc.txt -to pdf
    // Return codes:
    // 0 -> normal exit
    // 1 -> error occured
    // -1 -> help requested, no actual work done
    public static class Program
    {
        private const string Usage = "Usage:\n\tinput_file [-to (docx|pdf|html|markdown)] [output_file]";
        private static readonly string[] AsciidocBackends = {"docbook45", "xhtml11", "html4", "html5", "docbook", "html"};
        private static readonly string[] PandocOutputFormats = {"markdown_strict", "docx"};

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
                var formats = string.Empty;
                foreach (var asciidocBackend in AsciidocBackends)
                {
                    formats += asciidocBackend + ", ";
                }
                foreach (var pandocOutputFormat in PandocOutputFormats)
                {
                    formats += pandocOutputFormat + ", ";
                }
                formats = formats.TrimEnd(',', ' ');
                Console.Out.WriteLine($"Supported output formats:\n\t{formats}");
                return -1;
            }

            var input = string.Empty;
            var outputFormat = string.Empty;
            var output = string.Empty;

            var argsRegex = new Regex(@"(^)(?<input>[^\s]+)(\s)+((-to )(?<format>[a-zA-Z_]+))?(\s?)+(?<output>[^-][^\s]+)?(\n|\r|\r\n)");
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
                Console.Error.WriteLine("error while reading input.\n" + Usage);
                return 1;
            }
            if (string.IsNullOrWhiteSpace(outputFormat) && string.IsNullOrWhiteSpace(output))
            {
                Console.Error.WriteLine("error: either format or output file name with extension must be specified.");
                return 1;
            }

            var inputExtension = Path.GetExtension(input);
            if (string.IsNullOrWhiteSpace(inputExtension))
            {
                Console.Error.WriteLine("error: extension for input file must be specified");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(outputFormat))
            {
                outputFormat = Path.GetExtension(output).TrimStart('.');
                if (outputFormat.ToLower() == "md")
                {
                    outputFormat = "markdown_strict";
                }
                if (outputFormat.ToLower() == "xml")
                {
                    outputFormat = "docbook";
                }
#if DEBUG
                Console.Out.WriteLine($"recognized format for output file \"{output}\" is \"{outputFormat}\"");
#endif
            }
            else if (string.IsNullOrWhiteSpace(output))
            {
                string newExtension;
                switch (outputFormat.ToLower())
                {
                    case "docbook":
                    case "docbook45":
                        newExtension = ".xml";
                        break;
                    case "xhtml11":
                    case "html4":
                    case "html5":
                    case "html":
                        newExtension = ".html";
                        break;
                    case "markdown_strict":
                        newExtension = ".md";
                        break;
                    default:
                        newExtension = $".{outputFormat}";
                        break;
                }
                output = input.Replace(inputExtension, newExtension);
#if DEBUG
                Console.Out.WriteLine($"constructed output file name for input file \"{input}\" and output" +
                                      $" format \"{outputFormat}\": {output}");
#endif
            }

            if (string.IsNullOrWhiteSpace(outputFormat))
            {
                Console.Error.WriteLine("error: output format was not specified");
                return 1;
            }

            var asciidocPath = Path.GetFullPath(ConfigurationManager.AppSettings["asciidoc_path"]);
            if (AsciidocBackends.Contains(outputFormat.ToLower()))
            {
                return ConvertWithAsciidoc(input, outputFormat, output, asciidocPath);
            }
            if (PandocOutputFormats.Contains(outputFormat.ToLower()))
            {
                var asciidocBackend = ConfigurationManager.AppSettings["asciidoc_backend"];
                var pandocPath = Path.GetFullPath(ConfigurationManager.AppSettings["pandoc_path"]);
                var pandocInputFormat = ConfigurationManager.AppSettings["pandoc_input_format"];
                var pandocExtraArguments = ConfigurationManager.AppSettings["pandoc_extra_arguments"];
                return ConvertWithAsciidocAndPandoc(input, asciidocBackend, pandocInputFormat, outputFormat,
                    output, asciidocPath, pandocPath, pandocExtraArguments);
            }
            Console.Error.WriteLine($"error: output format \"{outputFormat}\" is not recognized." +
                                    " Type \"help\" without quotes to see supported formats.");
            return 1;
        }

        private static int ConvertWithAsciidoc(string inputFile, string asciidocBackend, string outputFile,
            string asciidocPath)
        {
            var asciidocArguments = $"-b {asciidocBackend} -o \"{outputFile}\" \"{inputFile}\"";
            var asciidocProcessStartInfo = new ProcessStartInfo
            {
                FileName = asciidocPath,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                Arguments = asciidocArguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
#if DEBUG
            Console.Out.WriteLine($"starting {asciidocProcessStartInfo.FileName} with" +
                                  $" arguments: {asciidocProcessStartInfo.Arguments}");
#endif
            Process asciidocProcess;
            try
            {
                asciidocProcess = Process.Start(asciidocProcessStartInfo);
                if (asciidocProcess == null)
                {
                    throw new InvalidOperationException("error: failed to start asciidoc process");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            asciidocProcess.WaitForExit();
            return 0;
        }

        private static int ConvertWithAsciidocAndPandoc(string inputFile, string asciidocBackend,
            string pandocInputFormat, string pandocOutputFormat, string outputFile, string asciidocPath,
            string pandocPath, string pandocExtraArguments)
        {
            var inputDirectory = Path.GetDirectoryName(Path.GetFullPath(inputFile));
            if (string.IsNullOrWhiteSpace(inputDirectory))
            {
                Console.Out.WriteLine($"error: directory for file {inputFile} was null");
                return 1;
            }

            var tmpAsciidocOutput = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var ret = ConvertWithAsciidoc(inputFile, asciidocBackend, tmpAsciidocOutput, asciidocPath);
            if (ret != 0)
            {
                if (File.Exists(tmpAsciidocOutput)) File.Delete(tmpAsciidocOutput);
                return ret;
            }

            var pandocArguments = $"-f {pandocInputFormat} -t {pandocOutputFormat} {pandocExtraArguments}" +
                                  $" -o \"{Path.GetFullPath(outputFile)}\" \"{tmpAsciidocOutput}\"";
            var pandocProcessStartInfo = new ProcessStartInfo
            {
                FileName = pandocPath,
                WorkingDirectory = inputDirectory,
                Arguments = pandocArguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
#if DEBUG
            Console.Out.WriteLine($"starting {pandocProcessStartInfo.FileName} with" +
                                  $" arguments: {pandocProcessStartInfo.Arguments}");
#endif
            Process pandocProcess;
            try
            {
                pandocProcess = Process.Start(pandocProcessStartInfo);
                if (pandocProcess == null)
                {
                    throw new InvalidOperationException("error: failed to start pandoc process");
                }
            }
            catch (Exception ex)
            {
                if (File.Exists(tmpAsciidocOutput)) File.Delete(tmpAsciidocOutput);
                Console.Error.WriteLine(ex);
                return 1;
            }

            pandocProcess.WaitForExit();
            if (File.Exists(tmpAsciidocOutput)) File.Delete(tmpAsciidocOutput);

            return 0;
        }
    }
}

/* 
 * Regex validation data (check lines separately):
../hello-asciidoc.txt -to docx ../hello-asciidoc.docx
../hello-asciidoc.txt ../hello-asciidoc.docx
../hello-asciidoc.txt -to docx
../hello-asciidoc.txt -to pdf

../hello-asciidoc.txt -to
../hello-asciidoc.txt ../hello-asciidoc.docx -to
../hello-asciidoc.txt ../hello-asciidoc.docx -to pdf
../hello-asciidoc.txt -d pdf
../hello-asciidoc.txt -d pdf ../hello-asciidoc.docx 
../hello-asciidoc.txt -d pdf ../hello-asciidoc.docx -d pdf
../hello-asciidoc.txt -to pdf ../hello-asciidoc.docx -to pdf
 */
