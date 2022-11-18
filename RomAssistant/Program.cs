using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant;
using RomAssistant.db;

if (!Directory.Exists("data"))
	Directory.CreateDirectory("data");

var configuration = new ConfigurationBuilder()
	.AddJsonFile("data/appsettings.json", optional: true)
	.AddUserSecrets<Program>()
	.Build();


var serviceBuilder = new ServiceCollection()
	.AddSingleton(configuration)
	.AddSingleton(new DiscordSocketConfig()
	{
		GatewayIntents = Discord.GatewayIntents.AllUnprivileged
			& ~Discord.GatewayIntents.GuildScheduledEvents
			& ~Discord.GatewayIntents.GuildInvites,
		//		AlwaysDownloadUsers = true,
	})
	.AddDbContext<Context>()
	.AddSingleton<DiscordSocketClient>()
	.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
	.AddSingleton<InteractionHandler>()
	;

var services = serviceBuilder.BuildServiceProvider();

{
	using (var scope = services.CreateScope())
	using (var context = scope.ServiceProvider.GetRequiredService<Context>())
	{
		if (context.Database.EnsureCreated())
		{
		}
	}
}

{
	var client = services.GetRequiredService<DiscordSocketClient>();

	client.Log += LogAsync;

	// Here we can initialize the service that will register and execute our commands
	await services.GetRequiredService<InteractionHandler>()
		.InitializeAsync();

	// Bot token can be provided from the Configuration object we set up earlier
	await client.LoginAsync(TokenType.Bot, configuration["token"]);
	await client.StartAsync();

	await client.SetGameAsync("to the moderators", null, ActivityType.Listening);
}



Console.WriteLine("Waiting");
await Task.Delay(-1);



Task LogAsync(LogMessage message)
{ 
	Console.WriteLine(message.ToString());
	return Task.CompletedTask;
}