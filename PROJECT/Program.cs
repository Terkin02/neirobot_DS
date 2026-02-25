using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

class Program
{
    private DiscordSocketClient _client;
    private IConfiguration _config;
    private HttpClient _http;

    static async Task Main()
        => await new Program().RunAsync();

    public async Task RunAsync()
    { 
        _config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged |
                             GatewayIntents.MessageContent
        });

        _http = new HttpClient();

        _client.Log += LogAsync;
        _client.MessageReceived += OnMessageReceived;

        var token = _config["Discord:Token"];

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        await Task.Delay(-1);
    }

    private Task LogAsync(LogMessage msg)
    {
        Console.WriteLine(msg);
        return Task.CompletedTask;
        
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        // Бот отвечает, если его упомянули или написали в ЛС
        if (message.Channel is IDMChannel ||
            message.MentionedUsers.Any(u => u.Id == _client.CurrentUser.Id))
        {
            await message.Channel.TriggerTypingAsync();

            var userText = message.Content;
            userText.Replace($"<@{_client.CurrentUser.Id}>", "");
            userText.Trim();

            var reply = await AskAI(userText);

            await message.Channel.SendMessageAsync(reply);
        }
    }

    private async Task<string> AskAI(string text)
    {
        var endpoint = _config["AI:Endpoint"];
        var apiKey = _config["AI:ApiKey"];
        var model = _config["AI:Model"];

        var request = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "" },
                new { role = "user", content = text }
            }
        };

        var json = JsonSerializer.Serialize(request);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest);
        var responseText = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseText);

        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? "Ошибка ответа AI.";
    }
}