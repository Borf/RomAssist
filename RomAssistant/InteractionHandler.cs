using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace RomAssistant
{
	public class InteractionHandler
	{
		private readonly DiscordSocketClient client;
		private readonly InteractionService handler;
		private readonly IServiceProvider services;

		public InteractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
		{
			this.client = client;
			this.handler = handler;
			this.services = services;
		}

		public async Task InitializeAsync()
		{
			// Process when the client is ready, so we can register our commands.
			client.Ready += ReadyAsync;
			handler.Log += LogAsync;

			// Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
			await handler.AddModulesAsync(Assembly.GetEntryAssembly(), services);

			// Process the InteractionCreated payloads to execute Interactions commands
			client.InteractionCreated += HandleInteraction;
		}


		private Task LogAsync(LogMessage message)
		{
			Console.WriteLine(message.ToString());
			return Task.CompletedTask;
		}
		private async Task ReadyAsync()
		{
			//await _handler.RegisterCommandsToGuildAsync(724054882717532171, true);
			await handler.RegisterCommandsToGuildAsync(724054882717532171, true); //borf test
			//				await _handler.RegisterCommandsGloballyAsync(true);
		}

		private async Task HandleInteraction(SocketInteraction interaction)
		{
			try
			{
//				Console.WriteLine("Got interaction " + interaction.Data.ToString());
				// Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
				var context = new SocketInteractionContext(client, interaction);

				// Execute the incoming command.
				var result = await handler.ExecuteCommandAsync(context, services);

				if (!result.IsSuccess)
				{
					Console.WriteLine("Error running interaction");
					Console.WriteLine(result.ToString());
					Console.WriteLine(result.Error.ToString());
					switch (result.Error)
					{
						case InteractionCommandError.UnmetPrecondition:
							// implement
							break;
						default:
							break;
					}
				}
			}
			catch
			{
				// If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
				// response, or at least let the user know that something went wrong during the command execution.
				if (interaction.Type is InteractionType.ApplicationCommand)
					await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
			}
		}
	}
}