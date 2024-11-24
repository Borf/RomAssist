using CsvHelper;
using CsvHelper.Configuration;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant.db;
using System.Globalization;
using System.Reflection;

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
			client.Ready += ReadyAsync;
			handler.Log += Program.LogAsync;
			await handler.AddModulesAsync(Assembly.GetEntryAssembly(), services);
			client.InteractionCreated += HandleInteraction;
		}
		private async Task ReadyAsync()
		{
			await handler.RegisterCommandsToGuildAsync(724054882717532171, true); //borf test
			await handler.RegisterCommandsToGuildAsync(248700646302285824, true); //community
			//await handler.RegisterCommandsGloballyAsync(true);
		}

		private async Task HandleInteraction(SocketInteraction interaction)
		{
			try
			{
                string name = "";
                if (interaction is SocketCommandBase interact)
                    name = "/" + interact.CommandName;
                else if (interaction.Data.GetType().Name == "MessageComponentInteractionData")
                    name = interaction.Data.GetType().GetProperty("CustomId")?.GetValue(interaction.Data)?.ToString() ?? "-";
                else if (interaction.Data.GetType().Name == "ModalInteractionData")
                    name = interaction.Data.GetType().GetProperty("CustomId")?.GetValue(interaction.Data)?.ToString() ?? "-";

                Program.Log(Module.Discord, "Got interaction '" + name + "' from user " + interaction.User.Username + " in channel " + interaction.Channel.Name);
                // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules.
                var context = new SocketInteractionContext(client, interaction);

				// Execute the incoming command.
				var result = await handler.ExecuteCommandAsync(context, services);

				if (result != null && !result.IsSuccess)
				{
                    Program.Log(Module.Discord, "Error running interaction");
                    Program.Log(Module.Discord, result.ToString() ?? "No result string");
                    Program.Log(Module.Discord, result.Error.ToString() ?? "No error string");
					switch (result.Error)
					{
						case InteractionCommandError.UnmetPrecondition:
							await context.Interaction.RespondAsync("You do not have access to this", ephemeral: true);
							break;
						default:
							break;
					}
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex.ToString());
				// If Slash Command execution fails it is most likely that the original interaction acknowledgement will persist. It is a good idea to delete the original
				// response, or at least let the user know that something went wrong during the command execution.
				if (interaction.Type is InteractionType.ApplicationCommand)
					await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
			}
		}
	}
}