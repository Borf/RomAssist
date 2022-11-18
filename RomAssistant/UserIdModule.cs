using CsvHelper;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
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
			var mb = new ModalBuilder()
				.WithCustomId("registeridpost")
				.WithTitle("Register your ingame character")
				.AddTextInput("Your character ID", "characterid", TextInputStyle.Short, "123456789", 10, 11, true, user?.CharacterId.ToString())
				.AddTextInput("Your character name", "charactername", TextInputStyle.Short, "", 1, 20, true, user?.CharacterName)
				;
			await RespondWithModalAsync(mb.Build());
		}

		[ModalInteraction("registeridpost")]
		public async Task RegisterIdPost(CharacterIdModal data)
		{
			var user = context.Users.Find(Context.User.Id);
			var cb = new ComponentBuilder()
				.WithSelectMenu(new SelectMenuBuilder()
					.WithCustomId("server")
					.WithMinValues(1)
					.WithMaxValues(1)
					.AddOption("Europe - Eternal Love",	"EU_EL",	isDefault : user?.Region == Regions.EU_EL )
					.AddOption("Global - Eternal Love",	"NA_EL",	isDefault : user?.Region == Regions.NA_EL)
					.AddOption("Global - DP",			"NA_DP",	isDefault : user?.Region == Regions.NA_DP)
					.AddOption("SEA - Eternal Love",	"SEA_EL",	isDefault : user?.Region == Regions.SEA_EL)
					.AddOption("SEA - mp",				"SEA_MP",	isDefault : user?.Region == Regions.SEA_MP)
					.AddOption("SEA - mof",				"SEA_MOF",	isDefault : user?.Region == Regions.SEA_MOF)
					.AddOption("SEA - Valhalla Glory",	"SEA_VG",	isDefault : user?.Region == Regions.SEA_VG))
				;
			await RespondAsync("Please pick your server", components: cb.Build(), ephemeral : true);

			if(user == null)
			{
				user = new User() { Id = Context.User.Id };
				context.Users.Add(user);
			}
			ulong characterid;
			ulong.TryParse(data.characterid, out characterid);

			user.CharacterId = characterid;
			user.CharacterName = data.charactername;

			await context.SaveChangesAsync();
		}

		[ComponentInteraction("server")]
		public async Task SetServer(string server)
		{
			await RespondAsync("Thank you for registering", ephemeral: true);
			var user = context.Users.Find(Context.User.Id);
			if (user == null)
			{
				user = new User() { Id = Context.User.Id };
				context.Users.Add(user);
			}
			user.Region = Enum.Parse<Regions>(server);
			await context.SaveChangesAsync();
		}


		class CsvEntry
		{
			public string Emote { get; set; }
			public ulong DiscordId { get; set; }
			public ulong CharacterId { get; set; }
			public string CharacterName { get; set; }
			public string Region { get; set; }
		}

		//[DoAdminCheck]
		[MessageCommand("Get Reaction Character IDs")]
		public async Task GetCharacterId(IMessage message)
		{
			await DeferAsync();

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
				var users = await userMessage.GetReactionUsersAsync(Emoji.Parse("👍"), 999999).FlattenAsync();
				foreach (var user in users)
				{
					var u = await context.Users.FindAsync(user.Id);
					data.Add(new CsvEntry() 
					{ 
						Emote = reaction.Key.Name, 
						DiscordId = user.Id, 
						CharacterId = u?.CharacterId ?? 0, 
						CharacterName = u?.CharacterName ?? "",
						Region = u?.Region.ToString() ?? "" 
					});
				}
			}
			using (var ms = new MemoryStream())
			{
				using (var writer = new StreamWriter(ms))
				using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
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