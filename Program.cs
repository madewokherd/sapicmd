﻿using System;
using System.Collections.Generic;
using System.Speech.Synthesis;

namespace sapicmd
{
    internal class Program
    {
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

                if (!item.StartsWith("-"))
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
        }
    }
}
