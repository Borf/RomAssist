using CsvHelper;
using CsvHelper.Configuration;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using RomAssistant.db;
using System;
using System.Globalization;
using System.Threading.Tasks;
using TicketBot.attributes;

namespace RomAssistant
{
	// Interation modules must be public and inherit from an IInterationModuleBase
	public class UserIdModule : InteractionModuleBase<SocketInteractionContext>
	{
		private InteractionHandler _handler;
		private Context context;

		// Constructor injection is also a valid way to access the dependencies
		public UserIdModule(InteractionHandler handler, Context context)
		{
			_handler = handler;
			this.context = context;
		}

		[SlashCommand("registerid", "Registers your ingame account ID")]
		public async Task RegisterId()
		{
			var user = context.Users.Find(Context.User.Id);
			var cb = new ComponentBuilder()
				.WithSelectMenu(new SelectMenuBuilder()
					.WithCustomId("server")
					.WithMinValues(1)
					.WithMaxValues(1)
					.AddOption("Europe - Eternal Love", "EU_EL", isDefault: user?.Region == Regions.EU_EL)
					.AddOption("Global - Eternal Love", "NA_EL", isDefault: user?.Region == Regions.NA_EL)
					.AddOption("Global - DP", "NA_DP", isDefault: user?.Region == Regions.NA_DP)
					.AddOption("SEA - Eternal Love", "SEA_EL", isDefault: user?.Region == Regions.SEA_EL)
					.AddOption("SEA - Midnight Party", "SEA_MP", isDefault: user?.Region == Regions.SEA_MP)
					.AddOption("SEA - Memory of Faith", "SEA_MOF", isDefault: user?.Region == Regions.SEA_MOF)
					.AddOption("SEA - Valhalla Glory", "SEA_VG", isDefault: user?.Region == Regions.SEA_VG))
				;
			await RespondAsync("Please pick your server", components: cb.Build(), ephemeral: true);
		}

		[ModalInteraction("registeridpost")]
		public async Task RegisterIdPost(CharacterIdModal data)
		{
			var user = context.Users.Find(Context.User.Id);
			if (user == null)
			{
				await RespondAsync("Make sure you register your server first", ephemeral: true);
				return;
			}
			if (user.Region == Regions.None)
			{
				await RespondAsync("Make sure you register your server first", ephemeral: true);
				return;
			}
			ulong characterid;
			if(!ulong.TryParse(data.characterid, out characterid))
			{
				await RespondAsync("Your characterID is not a number! please try again", ephemeral: true);
				return;
			}
			if (context.Users.Any(u => u.Id != Context.User.Id && u.CharacterId == characterid && u.Region == user.Region))
			{
				await RespondAsync("The characterID " + characterid + " is already registered to another discord account. Please contact the event administrator", ephemeral: true);
				return;
			}

			user.CharacterId = characterid;
			user.CharacterName = data.charactername;

			await context.SaveChangesAsync();
			await RespondAsync("Thank you for registering your character ID. You can always edit your characterid by using /registerid again", ephemeral: true);
		}

		[ComponentInteraction("server")]
		public async Task SetServer(string server)
		{
			var user = context.Users.Find(Context.User.Id);
			if (user == null)
			{
				user = new User() { Id = Context.User.Id, DiscordName = Context.User.Username + "#" + Context.User.Discriminator };
				context.Users.Add(user);
			}
			user.Region = Enum.Parse<Regions>(server);
			await context.SaveChangesAsync();

			string? charid = null;
			string? charname = null;

			charid = user?.CharacterId.ToString();
			if (charid?.Length < 10)
				charid = null;
			if (charname?.Length < 1)
				charname = null;

			var mb = new ModalBuilder()
							.WithCustomId("registeridpost")
							.WithTitle("Register your ingame character")
							.AddTextInput("Your character ID", "characterid", TextInputStyle.Short, "123456789", 10, 11, true, charid)
							.AddTextInput("Your character name", "charactername", TextInputStyle.Short, "", 1, 20, true, charname)
							;
			await RespondWithModalAsync(mb.Build());


		}


		class CsvEntry
		{
			public string Emote { get; set; } = "";
			public string DiscordId { get; set; } = "";
			public string DiscordName { get; set; } = "";
			public ulong CharacterId { get; set; }
			public string CharacterName { get; set; } = "";
			public string Region { get; set; } = "";
		}

		//[DoAdminCheck]
		[MessageCommand("Get Reaction Character IDs")]
		public async Task GetCharacterId(IMessage message)
		{
			await DeferAsync(ephemeral: true);

			var data = new List<CsvEntry>();

			var userMessage = message as SocketUserMessage;
			if (userMessage == null)
			{
				await RespondAsync(text: ":x: You can't add system messages to a ticket!", ephemeral: true);
				return;
			}

			var msg = await message.Channel.GetMessageAsync(message.Id);

			foreach(var reaction in msg.Reactions)
			{
				var users = await userMessage.GetReactionUsersAsync(reaction.Key, 999999).FlattenAsync();
				foreach (var user in users)
				{
					var u = await context.Users.FindAsync(user.Id);
					data.Add(new CsvEntry() 
					{ 
						Emote = reaction.Key.Name, 
						DiscordId = "'" + user.Id.ToString(),
						DiscordName = u.DiscordName,
						CharacterId = u?.CharacterId ?? 0, 
						CharacterName = u?.CharacterName ?? "",
						Region = u?.Region.ToString() ?? "" 
					});
				}
			}
			using (var ms = new MemoryStream())
			{
				using (var writer = new StreamWriter(ms))
				using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
				{
					HasHeaderRecord = true,
					ShouldQuote = a => { return a.Row.Index == 1 || a.Row.Index == 3; }
				}))
				{
					csv.WriteRecords(data);
				}

				await ModifyOriginalResponseAsync(r =>
				{
					r.Content = "Here is your file!";
					r.Attachments = new List<FileAttachment>()
					{
						new FileAttachment(new MemoryStream(ms.GetBuffer()), "reactions.csv", "The reactions with userids")
					};
				});
			}
		}


		public static string currentAnswer = "";
		public static int answerCount = 0;
		public static ulong quizHost = 0;
		public static ulong quizChannelId = 0;
		public static SocketInteraction quizInteraction;
		[SlashCommand("quizanswer", "Registers a quiz answer")]
		public async Task quizAnswer(int totalAnswers, string answer)
		{
			quizInteraction = Context.Interaction;
			quizHost = Context.User.Id;
			quizChannelId = Context.Channel.Id;
			currentAnswer = answer;
			answerCount = totalAnswers;
			await RespondAsync("Answer is set! ask your question", ephemeral : true);
		}

		[SlashCommand("resetanswers", "resets quiz responses")]
		public async Task quizAnswer()
		{
			await RespondAsync("Cleared!", ephemeral: true);
			context.QuizAnswers.RemoveRange(context.QuizAnswers);
			await context.SaveChangesAsync();
		}


		class CsvEntry2
		{
			public string Answer { get; set; } = "";
			public string DiscordId { get; set; } = "";
			public string DiscordName { get; set; } = "";
			public ulong CharacterId { get; set; }
			public string CharacterName { get; set; } = "";
			public string Region { get; set; } = "";
			public string Answered { get; set; } = "";
		}

		[SlashCommand("quizresults", "sends a file with the quiz results")]
		public async Task quizResults()
		{
			await DeferAsync(true);
			var entries = new List<CsvEntry2>();
			var winners = context.QuizAnswers.Include(q => q.User).ToList();
			winners.ForEach(a => entries.Add(new CsvEntry2()
			{
				Answer = a.Answer,
				CharacterId = a.User.CharacterId,
				DiscordId = "'" + a.User.Id,
				DiscordName = a.User.DiscordName,
				Region = a.User?.Region.ToString() ?? "",
				CharacterName = a.User?.CharacterName ?? "",
				Answered = a.Answered,
			}));
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

				await ModifyOriginalResponseAsync(r =>
				{
					r.Content = "Here is your file!";
					r.Attachments = new List<FileAttachment>()
					{
						new FileAttachment(new MemoryStream(ms.GetBuffer()), "reactions.csv", "The reactions with userids")
					};
				});
			}
		}

		public class CharacterIdModal : IModal
		{
			public string Title => "Ticket Descriptions";
			// Strings with the ModalTextInput attribute will automatically become components.
			[ModalTextInput("characterid", minLength: 10, maxLength: 11)]
			public string characterid { get; set; } = string.Empty;

			// Additional paremeters can be specified to further customize the input.
			[ModalTextInput("charactername", TextInputStyle.Paragraph, "Please enter a longer description")]
			public string charactername { get; set; } = string.Empty;

		}


	}
}