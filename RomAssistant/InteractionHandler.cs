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
			// Process when the client is ready, so we can register our commands.
			client.Ready += ReadyAsync;
			handler.Log += LogAsync;

			// Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
			await handler.AddModulesAsync(Assembly.GetEntryAssembly(), services);

			// Process the InteractionCreated payloads to execute Interactions commands
			client.InteractionCreated += HandleInteraction;
			client.MessageReceived += HandleMessage;
		}

		class CsvEntry
		{
			public string DiscordId { get; set; }
			public string DiscordName { get; set; }
			public ulong CharacterId { get; set; }
			public string CharacterName { get; set; }
			public string Region { get; set; }
		}
		private async Task HandleMessage(SocketMessage message)
		{
			if (message.Channel.Id != UserIdModule.quizChannelId)
				return;
			if (message.Content.ToLower() == UserIdModule.currentAnswer.ToLower() && UserIdModule.answerCount > 0)
			{
				var db = services.GetRequiredService<Context>();
				var user = await db.Users.FindAsync(message.Author.Id);
				if (user == null)
				{
					await message.Author.SendMessageAsync("Sorry you have not registered your character ID and your answer won't be taken. Please use /registerid on the discord server (not this chat) to register your character ID and try again with the next question!");
					return;
					user = new User() { Id = message.Author.Id, DiscordName = message.Author.Username + "#" + message.Author.Discriminator };
					db.Users.Add(user);
					await db.SaveChangesAsync();
				}

				if (db.QuizAnswers.Any(qa => qa.User == user))
					return;
				UserIdModule.answerCount--;
				db.QuizAnswers.Add(new QuizAnswer()
				{
					Answer = UserIdModule.currentAnswer,
					Answered = message.Content,
					User = user
				});
				await db.SaveChangesAsync();

				await UserIdModule.quizInteraction.ModifyOriginalResponseAsync(m => m.Content = UserIdModule.answerCount + " answer left");

				var entries = new List<CsvEntry>();
				if(UserIdModule.answerCount <= 0)
				{
					var winnerMsg = "That's it for this question! the winners are\n\n";
					var winners = db.QuizAnswers.Include(q => q.User).Where(q => q.Answered == UserIdModule.currentAnswer).ToList();
					winners.ForEach(a => winnerMsg += "<@" + a.User.Id + ">\n");
					winners.ForEach(a => entries.Add(new CsvEntry() {
						 CharacterId = a.User.CharacterId,
						 DiscordId = "'" + a.User.Id,
						 DiscordName = a.User.DiscordName,
						 Region = a.User?.Region.ToString() ?? "",
						 CharacterName = a.User?.CharacterName ?? "",
					}));
					await message.Channel.SendMessageAsync(winnerMsg);

					using (var ms = new MemoryStream())
					{
						using (var writer = new StreamWriter(ms))
						using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
						{
							HasHeaderRecord = true,
							ShouldQuote = a => { return a.Row.Index == 1 || a.Row.Index == 3; }
						}))
						{
							csv.WriteRecords(entries);
						}

						var host = await message.Channel.GetUserAsync(UserIdModule.quizHost);
						await host.SendFileAsync(new MemoryStream(ms.GetBuffer()), "results.csv", "Here is your file for answer " + UserIdModule.currentAnswer);
					}

					UserIdModule.quizChannelId = 0;
				}


				


			}
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
			await handler.RegisterCommandsToGuildAsync(248700646302285824, true); //community
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