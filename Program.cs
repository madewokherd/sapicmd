using System;
using System.Collections.Generic;
using System.Net;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;

namespace sapicmd
{
    internal class Program
    {
        SpeechSynthesizer synthesizer = new SpeechSynthesizer();
        Random random = new Random();

        static string ReadFileContents(string filename)
        {
            WebClient wc = new WebClient();

            return wc.DownloadString(filename);
        }

        static bool IsControlItem(object item)
        {
            if (item is VoiceInfo ||
                item is SpecialItem.Reset ||
                item is PromptRate ||
                item is PromptEmphasis ||
                item is PromptVolume ||
                item is VolumeItem)
                return true;
            return false;
        }

        enum SsmlElementType
        {
            Voice,
            Style
        }

        class VolumeItem
        {
            public int volume;
            public VolumeItem(int volume)
            {
                this.volume = volume;
            }
        }

        class JsonItem
        {
            public string raw_json;
            public JsonItem(string raw_json)
            {
                this.raw_json = raw_json;
            }
        }

        enum SpecialItem
        {
            Reset
        }

        int ProcessCommandLine(string[] args)
        {
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
                else if (lower == "-voice")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing voice name after -voice");
                        return 1;
                    }
                    string name = args[i];
                    var voices = synthesizer.GetInstalledVoices();
                    VoiceInfo info = null;
                    foreach (var candidate in voices)
                    {
                        var cinfo = candidate.VoiceInfo;
                        if (cinfo.Id == name || cinfo.Name.ToLowerInvariant().Contains(name.ToLowerInvariant()))
                        {
                            if (!candidate.Enabled)
                            {
                                Console.Error.WriteLine("The selcted voice, {0}, is disabled", cinfo.Name);
                                return 1;
                            }
                            info = cinfo;
                            break;
                        }
                    }
                    if (info == null)
                    {
                        Console.Error.WriteLine("No voice with the name '{0}' was found.", name);
                    }
                    prompt_items.Add(info);
                }
                else if (lower == "-rate")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing number after -rate");
                        return 1;
                    }
                    int rate;
                    if (!int.TryParse(args[i], out rate) || rate < 0 || rate > 5)
                    {
                        Console.Error.WriteLine("-rate must be followed by a number from 0 to 5");
                        return 1;
                    }
                    prompt_items.Add((PromptRate)rate);
                }
                else if (lower == "-emphasis")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing number after -emphasis");
                        return 1;
                    }
                    int emphasis;
                    if (!int.TryParse(args[i], out emphasis) || emphasis < 0 || emphasis > 4)
                    {
                        Console.Error.WriteLine("-emphasis must be followed by a number from 0 to 4");
                        return 1;
                    }
                    prompt_items.Add((PromptEmphasis)emphasis);
                }
                else if (lower == "-volume")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing number after -volume");
                        return 1;
                    }
                    int volume;
                    if (!int.TryParse(args[i], out volume) || volume < 0 || volume > 100)
                    {
                        Console.Error.WriteLine("-volume must be followed by a number from 0 to 100");
                        return 1;
                    }
                    prompt_items.Add(new VolumeItem(volume));
                }
                else if (lower == "-voicevolume")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing number after -voiceVolume");
                        return 1;
                    }
                    int volume;
                    if (!int.TryParse(args[i], out volume) || volume < 0 || volume > 7)
                    {
                        Console.Error.WriteLine("-voiceVolume must be followed by a number from 0 to 7");
                        return 1;
                    }
                    prompt_items.Add((PromptVolume)volume);
                }
                else if (lower == "-reset")
                {
                    prompt_items.Add(SpecialItem.Reset);
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
                else if (lower == "-json")
                {
                    i++;
                    if (i == args.Length)
                    {
                        Console.Error.WriteLine("Missing filename or url after -json");
                        return 1;
                    }
                    prompt_items.Add(new JsonItem(ReadFileContents(args[i])));
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

            int control_items_at_end = 0;

            while (control_items_at_end < prompt_items.Count &&
                IsControlItem(prompt_items[prompt_items.Count - 1 - control_items_at_end]))
                control_items_at_end++;

            if (control_items_at_end == prompt_items.Count)
            {
                Console.Error.WriteLine("WARNING: None of the given instructions do anything without something to read.");
            }
            else if (control_items_at_end != 0)
            {
                Console.WriteLine("WARNING: Instructions that configure the voice only affect the instructions after them. Putting them at the end of the line does not make sense. These instructions will be automatically moved to the beginning.");

                List<object> end_items = prompt_items.GetRange(prompt_items.Count - control_items_at_end, control_items_at_end);
                prompt_items.RemoveRange(prompt_items.Count - control_items_at_end, control_items_at_end);
                prompt_items.InsertRange(0, end_items);
            }

            List<object> prompts = new List<object>();
            PromptBuilder builder = new PromptBuilder();
            Stack<SsmlElementType> elements = new Stack<SsmlElementType>();
            PromptStyle style = new PromptStyle();
            VoiceInfo voiceinfo = null;
            int currentVolume = 100;

            foreach (var item in prompt_items)
            {
                if (item is string text)
                {
                    builder.AppendText(text);
                }
                else if (item is JsonItem json)
                {
                    builder.AppendText(JsonToText(json.raw_json));
                }
                else if (item is VoiceInfo info)
                {
                    if (elements.Count != 0 && elements.Peek() == SsmlElementType.Voice)
                    {
                        elements.Pop();
                        builder.EndVoice();
                    }
                    voiceinfo = info;
                    builder.StartVoice(info);
                    elements.Push(SsmlElementType.Voice);
                }
                else if (item is PromptRate rate)
                {
                    if (elements.Count != 0 && elements.Peek() == SsmlElementType.Style)
                    {
                        elements.Pop();
                        builder.EndStyle();
                    }
                    style.Rate = rate;
                    builder.StartStyle(style);
                    elements.Push(SsmlElementType.Style);
                }
                else if (item is PromptEmphasis emphasis)
                {
                    if (elements.Count != 0 && elements.Peek() == SsmlElementType.Style)
                    {
                        elements.Pop();
                        builder.EndStyle();
                    }
                    style.Emphasis = emphasis;
                    builder.StartStyle(style);
                    elements.Push(SsmlElementType.Style);
                }
                else if (item is PromptVolume volume)
                {
                    if (elements.Count != 0 && elements.Peek() == SsmlElementType.Style)
                    {
                        elements.Pop();
                        builder.EndStyle();
                    }
                    style.Volume = volume;
                    builder.StartStyle(style);
                    elements.Push(SsmlElementType.Style);
                }
                else if (item is VolumeItem volume_item)
                {
                    ResetVoice(builder, elements);
                    prompts.Add(builder);
                    builder = new PromptBuilder();
                    if (voiceinfo != null)
                    {
                        builder.StartVoice(voiceinfo);
                        elements.Push(SsmlElementType.Voice);
                    }
                    builder.StartStyle(style);
                    elements.Push(SsmlElementType.Style);
                    prompts.Add(volume_item);
                    currentVolume = volume_item.volume;
                }
                else if (item is SpecialItem.Reset)
                {
                    ResetVoice(builder, elements);
                    style = new PromptStyle();
                    if (currentVolume != 100)
                    {
                        currentVolume = 100;
                    }
                    voiceinfo = null;
                }
                else
                {
                    throw new ApplicationException(String.Format("Unexpected error, don't know what to do with {0}", item));
                }
            }

            prompts.Add(builder);

            ResetVoice(builder, elements);

            foreach (var prompt in prompts)
            {
                if (prompt is PromptBuilder promptbuilder)
                {
                    synthesizer.Speak(promptbuilder);
                    synthesizer = new SpeechSynthesizer();
                }
                else if (prompt is VolumeItem volumeitem)
                {
                    synthesizer.Volume = volumeitem.volume;
                }
            }

            return 0;
        }

        private string JsonToText(string raw_json)
        {
            using (var json = JsonDocument.Parse(raw_json))
            {
                var root = json.RootElement;

                string result = "SENTENCES";

                while (true)
                {
                    bool made_substitution = false;

                    foreach (var prop in root.EnumerateObject())
                    {
                        int index = result.IndexOf(prop.Name.ToUpperInvariant(), StringComparison.InvariantCulture);
                        if (index != -1)
                        {
                            result = result.Substring(0, index) +
                                JsonElementToText(prop.Value, false) +
                                result.Substring(index + prop.Name.Length);
                            made_substitution = true;
                            break;
                        }
                    }

                    if (!made_substitution)
                        break;
                }

                return result;
            }
        }

        private string JsonElementToText(JsonElement value, bool is_sequence)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                    return value.GetString();
                case JsonValueKind.Array:
                    if (is_sequence)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var item in value.EnumerateArray())
                        {
                            sb.Append(JsonElementToText(item, false));
                            sb.Append(" ");
                        }
                        return sb.ToString();
                    }
                    int index = random.Next(value.GetArrayLength());
                    return JsonElementToText(value[index], true);
                default:
                    throw new Exception("Invalid JSON input");
            }
        }

        static int Main(string[] args)
        {
            return new Program().ProcessCommandLine(args);
        }

        private static void ResetVoice(PromptBuilder builder, Stack<SsmlElementType> elements)
        {
            while (elements.Count != 0)
            {
                switch (elements.Pop())
                {
                    case SsmlElementType.Voice:
                        builder.EndVoice();
                        break;
                    case SsmlElementType.Style:
                        builder.EndStyle();
                        break;
                }
            }
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
            Console.WriteLine("-voice NAME");
            Console.WriteLine("    Switch to a specific voice.");
            Console.WriteLine("    EXAMPLE: sapicmd -voice Zira -text \"Spoken as Zira\" -voice David -text \"Spoken as David\"");
            Console.WriteLine("-listVoices");
            Console.WriteLine("    Print a list of installed voices and exit.");
            Console.WriteLine("-rate RATE");
            Console.WriteLine("    Change the rate of speech. RATE must be a number from 0 to 5.");
            Console.WriteLine("    0 sets the rate to the default.");
            Console.WriteLine("    1 is the fastest, and 5 is the slowest.");
            Console.WriteLine("-emphasis EMPHASIS");
            Console.WriteLine("    Change the emphasis of speech. EMPHASIS must be a number from 0 to 4.");
            Console.WriteLine("    Note that this is unsupported by the default voices in Windows, and will have no effect if a default voice is used.");
            Console.WriteLine("    0 sets emphasis to the default.");
            Console.WriteLine("    1 is the strongest.");
            Console.WriteLine("-volume VOLUME");
            Console.WriteLine("    Change the volume of output. VOLUME must be a number from 0 to 100.");
            Console.WriteLine("    0 is silent, and 100 is full volume.");
            Console.WriteLine("    NOTE: This cannot be used with XML output. To output a change in volume to XML, use -voiceVolume.");
            Console.WriteLine("-voiceVolume VOLUME");
            Console.WriteLine("    Change the volume of the speech engine. VOLUME must be a number from 0 to 7.");
            Console.WriteLine("    0 represents the default. 1 is silent, and 7 is full volume.");
            Console.WriteLine("    NOTE: In most cases, it's recommended to use -volume instead.");
            Console.WriteLine("-reset");
            Console.WriteLine("    Change all voice options back to the defaults.");
            Console.WriteLine("-json FILENAME");
            Console.WriteLine("-json URL");
            Console.WriteLine("    Randomize text based on the given JSON file.");
            Console.WriteLine("-help");
            Console.WriteLine("    Print this help text and exit.");
        }
    }
}
