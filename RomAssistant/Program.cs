using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Webhook;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using RomAssistant;
using RomAssistant.db;
using RomAssistant.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;

public class Program
{
    private static async Task Main(string[] args)
    {
        if (!Directory.Exists("data"))
            Directory.CreateDirectory("data");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("data/appsettings.json", optional: true)
            .AddUserSecrets<Program>()
            .Build();


        GoogleCredential credential;
        using (var stream = new FileStream("data/client_secrets.json", FileMode.Open, FileAccess.Read))
        {
            credential = GoogleCredential.FromStream(stream);
            if (credential.IsCreateScopedRequired)
                credential = credential.CreateScoped(new string[] { SheetsService.Scope.Drive });
        }
        var sheetService = new SheetsService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "TicketBot"
        });

        var serviceBuilder = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton(sheetService)
            .AddSingleton(new DiscordSocketConfig()
            {
                GatewayIntents = (GatewayIntents.AllUnprivileged
                    | GatewayIntents.MessageContent
                    | GatewayIntents.GuildMembers
                    | GatewayIntents.GuildMessageReactions)
                    & ~GatewayIntents.GuildScheduledEvents
                    & ~GatewayIntents.GuildInvites,
                AlwaysDownloadUsers = true,
            })
            .AddDbContext<Context>()
            //    .AddBackgroundService<NameCheckerService>()
            .AddSingleton<DiscordSocketClient>()
            .AddBackgroundService<VoiceChannelTrackerService>()
            .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
            .AddSingleton<InteractionHandler>()
            .AddScoped<ModuleInvoker>()
            .AddBackgroundService<NameCheckerService>()

        ;

        var services = serviceBuilder.BuildServiceProvider();

        {
            using (var scope = services.CreateScope())
            using (var context = scope.ServiceProvider.GetRequiredService<Context>())
            {
                //await context.Database.EnsureDeletedAsync();
                if (context.Database.EnsureCreated())
                {
                }
            }
        }

        {
            var client = services.GetRequiredService<DiscordSocketClient>();

            client.Log += LogAsync;

            // Here we can initialize the service that will register and execute our commands
            await services.GetRequiredService<InteractionHandler>().InitializeAsync();
            await client.LoginAsync(TokenType.Bot, configuration["token"]);
            await client.StartAsync();
            await client.SetGameAsync("to the moderators", null, ActivityType.Listening);
        }


        CancellationTokenSource mainTokenSource = new();

        var backgroundServices = services.GetServices<IBackgroundService>();
        foreach (var backgroundService in backgroundServices)
            backgroundService.Start(mainTokenSource.Token);



        Console.WriteLine("Waiting");
        await Task.Delay(-1);
    }

    public static Task LogAsync(LogMessage message) { Log(Module.Discord, $"{message.Source:10} {(message.Exception != null ? message.Exception.ToString() : message.Message)}"); return Task.CompletedTask; }
    public static void Log(Module source, string message) { Console.WriteLine($"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] {source.ToString(),-20}{message}"); }

    public static async Task LogToDiscord(Module source, string message)
    {
        DiscordWebhookClient wh = new DiscordWebhookClient("https://discord.com/api/webhooks/1174815786301390898/TS7K4RrKF35BNcp0fhaSTCsB-IMZP6jZsBhHpKgcNDlIZfpQ5fXU_2qWgZXDs6UhBE4E");
        await wh.SendMessageAsync($"`{source.ToString().PadRight(20)}` {message}");
    }
}

[DebuggerDisplay("RaffleEntry, UserName = {UserName}, Entries={Entries.Count}, WinnerRolls={WinnerRolls.Count}")]
public class RaffleEntry
{
    public string UserName { get; set; } = string.Empty;
    public List<ulong> Entries { get; set; } = new List<ulong>();
    public List<string> Images { get; set; } = new List<string>();
    public ulong cid { get; set; }
    public string ign { get; set; } = string.Empty;
    public string server { get; set; } = string.Empty;
    public List<int> WinnerRolls { get; set; } = new();
    public List<string> WinnerEntries{ get; set; } = new();
}