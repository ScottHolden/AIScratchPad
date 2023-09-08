using Azure.AI.OpenAI;
using System.Diagnostics;
using System.Drawing.Imaging;
using ZorkGPT;

var azOpenAIEndpoint = "";
var azOpenAIKey = "";
var azOpenAIDeployment = "gpt-35-turbo-16k";
var compactAtXTokens = 1000;
var ocrLibPath = @"ocrlib.json";
var zorkStartInfo = new ProcessStartInfo
{
    FileName = @"C:\Program Files (x86)\GOG Galaxy\Games\Zork\DOSBOX\DOSBox.exe",
    WorkingDirectory = @"C:\Program Files (x86)\GOG Galaxy\Games\Zork\DOSBOX\",
    Arguments = "-conf \"..\\dosboxZork_single.conf\" -noconsole"
};

var dosbox = Process.Start(zorkStartInfo) ?? throw new Exception("potato");
await Task.Delay(3000);

var size = User32.GetWindowSize(dosbox);
using var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);

OCRLib ocr = OCRLib.Load(ocrLibPath);
ocr.Add(new byte[16], ' ');

var openAIClient = new OpenAIClient(new Uri(azOpenAIEndpoint), new Azure.AzureKeyCredential(azOpenAIKey));
string prompt = @"
You are playing a text based adventure game. You are the player, and should always act like the player.
You will be provided with the text displayed on the screen, you should respond with your action and nothing else.
Keep responses simple, use only 1 to 5 basic words, and only perform a single action at a time.
Only provide actions that a player would enter.
Never appologise, never say sorry, do not try to narrate what is happening, only respond with the action you would like to take.
Do NOT use the words: 'try', 'try to', 'alternative', 'you', 'around', 'continue', 'another', or 'more'.
You can use commands at any time (Diagnose, Inventory, Look, Examine, Wait, Enter)
To move around, just type the direction you want to go (North, South, East, West, Up, Down, In, Out).
You can interact with objects using verbs (Take, Drop, examine, look at, open, close, read, put in, look, give, talk, attack, unlock, light)
".Trim();

var chatCompletion = new ChatCompletionsOptions()
{
    Messages =
    {
        new ChatMessage(ChatRole.System, prompt)
    },
    MaxTokens = 10,
};

Console.WriteLine("Ready to start!");
Console.ReadLine();

var running = true;
while(running)
{
    // Capture window
    using (var g = Graphics.FromImage(bmp))
    {
        IntPtr hdc = g.GetHdc();
        User32.PrintWindow(dosbox.MainWindowHandle, hdc, 1);
        g.ReleaseHdc(hdc);
    }

    // Read window
    string[] rawLines = ocr.ReadScreen(bmp);
    var allLines = rawLines.Skip(1).SkipLast(2).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    int lastInput = allLines.FindLastIndex(x => x.StartsWith(">"));
    string[] newLines = lastInput > 0 ? allLines.Skip(lastInput + 1).ToArray() : allLines.ToArray();
    Console.WriteLine("[ZORK] " + string.Join("\n       ", newLines));

    // Ask Azure OpenAI
    chatCompletion.Messages.Add(new ChatMessage(ChatRole.User, string.Join(" ", newLines)));
    string? response = null;
    bool shouldCompact = false;
    while (response == null)
    {
        var resp = await openAIClient.GetChatCompletionsAsync(azOpenAIDeployment, chatCompletion);
        response = resp.Value.Choices[0].Message.Content;
        if (response == null)
        {
            Console.WriteLine("<GPT3.5> !! Empty response");
            continue;
        }
        if (response.StartsWith("you", StringComparison.OrdinalIgnoreCase))
        {
            chatCompletion.Messages.Add(new ChatMessage(ChatRole.User, "What do you do next?"));
            Console.WriteLine("<GPT3.5> !! Error, not playing as a user, retrying: " + response);
            response = null;
        }
        Console.WriteLine("[SYSTEM] Prompt size: " + resp.Value.Usage.PromptTokens);
        if (resp.Value.Usage.PromptTokens > compactAtXTokens)
        {
            shouldCompact = true;
        }
    }

    //Compact if needed
    if (shouldCompact)
    {
        Console.WriteLine("[SYSTEM] Triggering message history compact...");
        chatCompletion.MaxTokens = 300;
        chatCompletion.Messages.Add(new ChatMessage(ChatRole.User, "PLEASE SUMMARISE THE ABOVE CONVERSATION INTO DOT POINTS, KEEPING ALL IMPORTANT DETAILS"));
        string? summary = null;
        while (summary == null)
        {
            var resp = await openAIClient.GetChatCompletionsAsync(azOpenAIDeployment, chatCompletion);
            summary = resp.Value.Choices[0].Message.Content;
            if (summary == null) Console.WriteLine("[SYSTEM] Invalid response from AOAI, retrying...");
        }
        
        Console.WriteLine("[SYSTEM] Summary: \n         " + string.Join("\n         ", summary.Split(new [] { '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        chatCompletion.Messages.Clear();
        chatCompletion.Messages.Add(new ChatMessage(ChatRole.System, prompt));
        chatCompletion.Messages.Add(new ChatMessage(ChatRole.User, summary));
        chatCompletion.Messages.Add(new ChatMessage(ChatRole.User, string.Join(" ", newLines)));
        chatCompletion.MaxTokens = 10;
    }

    chatCompletion.Messages.Add(new ChatMessage(ChatRole.Assistant, response));

    // Send input back
    Console.WriteLine("[GPT3.5] " + response);
    User32.SetForegroundWindow(dosbox.MainWindowHandle);
    await Task.Delay(100);
    SendKeys.SendWait(response + "{ENTER}");
    await Task.Delay(2000);
}
dosbox.Kill(true);
