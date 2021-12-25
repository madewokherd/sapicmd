using System;
using System.Collections.Generic;
using System.Net;
using System.Speech.Synthesis;

namespace sapicmd
{
    internal class Program
    {
        static string ReadFileContents(string filename)
        {
            WebClient wc = new WebClient();

            return wc.DownloadString(filename);
        }

        static int Main(string[] args)
        {
            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
            List<object> prompt_items = new List<object>();

            synthesizer.SetOutputToDefaultAudioDevice();

            // Argument processing
            for (int i=0; i < args.Length; i++)
            {
                string item = args[i];
                string lower = item.ToLowerInvariant();

                if (!item.StartsWith("-") && item != "/?")
                {
                    // Just read this as text
                    prompt_items.Add(item);
                }
                else if (lower == "-text")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing text to read after -text");
                        return 1;
                    }
                    prompt_items.Add(args[i]);
                }
                else if (lower == "-textfile")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing filename or url after -textFile");
                        return 1;
                    }
                    prompt_items.Add(ReadFileContents(args[i]));
                }
                else if (lower == "-listvoices")
                {
                    foreach (var voice in synthesizer.GetInstalledVoices())
                    {
                        var info = voice.VoiceInfo;
                        if (!voice.Enabled)
                        {
                            Console.WriteLine("{0} (disabled)", info.Id);
                        }
                        Console.WriteLine("{0}", info.Id);
                        Console.WriteLine(" Name: {0}", info.Name);
                        Console.WriteLine(" Culture: {0}", info.Culture.DisplayName);
                        Console.WriteLine(" Age: {0}", info.Age);
                        Console.WriteLine(" Gender: {0}", info.Gender);
                        Console.WriteLine(" Description: {0}", info.Description);
                    }
                    return 0;
                }
                else if (lower == "-help" || lower == "-h" || lower == "/?")
                {
                    Usage();
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine("Unrecognized argument: {0}", item);
                    Usage();
                    return 1;
                }
            }

            if (prompt_items.Count == 0)
            {
                Console.Error.WriteLine("Nothing to do");
                Usage();
                return 1;
            }

            PromptBuilder builder = new PromptBuilder();

            foreach (var item in prompt_items)
            {
                if (item is string text)
                {
                    builder.AppendText(text);
                }
                else
                {
                    throw new ApplicationException(String.Format("Unexpected error, don't know what to do with {0}", item));
                }
            }

            synthesizer.Speak(builder);

            return 0;
        }

        private static void Usage()
        {
            Console.WriteLine("Usage: sapicmd [INSTRUCTION [INSTRUCTION ...]]");
            Console.WriteLine();
            Console.WriteLine("Instructions may be any of the following:");
            Console.WriteLine();
            Console.WriteLine("-text TEXT");
            Console.WriteLine("    Read the given text.");
            Console.WriteLine("    Text can also be given without the -text switch as long as it does not start with '-'.");
            Console.WriteLine("-textFile FILENAME");
            Console.WriteLine("-textFile URL");
            Console.WriteLine("    Read the contents of the given file as text.");
            Console.WriteLine("-listVoices");
            Console.WriteLine("    Print a list of installed voices and exit.");
            Console.WriteLine("-help");
            Console.WriteLine("    Print this help text and exit.");
        }
    }
}
