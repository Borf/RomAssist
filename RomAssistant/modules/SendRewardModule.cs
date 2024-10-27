using CsvHelper;
using CsvHelper.Configuration;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using RomAssistant.db;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace RomAssistant
{
	// Interation modules must be public and inherit from an IInterationModuleBase
	public class SendRewardModule : InteractionModuleBase<SocketInteractionContext>
	{
		static Task? SendTask = null;

		private InteractionHandler _handler;
		private Context context;
		private SheetsService sheetsService;

		// Constructor injection is also a valid way to access the dependencies
		public SendRewardModule(InteractionHandler handler, Context context, SheetsService sheetsService)
		{
			_handler = handler;
			this.context = context;
			this.sheetsService = sheetsService;
		}

        [DoAdminCheck]
        [SlashCommand("sendrewards", "Allows you to send a reward")]
		public async Task SendRewards([Summary(description: "The ID in the url")] string sheetId = "1-r92MOoq413SzUZ740sEJAWQ1ml3-vbtfsl9I38su68")
		{
			if(SendTask != null)
			{
				await RespondAsync("Sorry, already sending messages...", ephemeral: true);
				return;
			}
			try
			{
				var sheet = sheetsService.Spreadsheets.Get(sheetId);
				var sheets = sheet.Execute().Sheets;
				var smb = new SelectMenuBuilder()
									.WithCustomId("sendrewards-sheet:" + sheetId)
									.WithMinValues(1)
									.WithMaxValues(1);
				foreach(var s in sheets)
				{
					smb.AddOption(s.Properties.Title, s.Properties.Title);
				}
				var cb = new ComponentBuilder()
					.WithSelectMenu(smb);
				await RespondAsync("What tab would you like to use?", components: cb.Build(), ephemeral: true);

			}catch(Exception)
			{
				await RespondAsync("The discord bot has no access to this sheet. Please add ticketbot@ticketbot-366321.iam.gserviceaccount.com to this sheet", ephemeral: true);
			}
		}


		[ComponentInteraction("sendrewards-sheet:*")]
		public async Task SendRewardsSheet(string sheetId, string tabTitle)
		{
			await DeferAsync(ephemeral: true);
			var values = sheetsService.Spreadsheets.Values.Get(sheetId, $"{tabTitle}!A:Z").Execute();
			if(values.Values == null)
			{
				await FollowupAsync("Error: no values found in this tab");
				return;
			}

			var buttons = new ComponentBuilder();
			buttons
				.WithButton("Yes", "sendrewards-confirm:" + sheetId + "," + tabTitle, ButtonStyle.Success)
				.WithButton("No", "cancel", ButtonStyle.Danger);
			await FollowupAsync(values.Values.Count + " values found in tab " + tabTitle + ". Are you sure you want to send?", components: buttons.Build(), ephemeral: true);
		}

		[ComponentInteraction("sendrewards-confirm:*,*")]
		public async Task SendRewardsSheetConfirm(string sheetId, string tabTitle)
		{
			await RespondAsync("Sending messages!", ephemeral: true);
			if(SendTask == null)
				SendTask = SendMessages(sheetsService, Context.Guild, sheetId, tabTitle);
		}

		private async Task SendMessages(SheetsService sheetsService, SocketGuild guild, string sheetId, string tabTitle)
		{
			Console.WriteLine("Getting values");
			var values = sheetsService.Spreadsheets.Values.Get(sheetId, $"{tabTitle}!A:Z").Execute();
			Console.WriteLine("Getting users");
			var users = await guild.GetUsersAsync().FlattenAsync();
			for (int i = 0; i < values.Values.Count; i++)
			{
				var row = values.Values[i];
				var status = "";
				if (row.Count < 1 || row[0].ToString() == "")
					continue;
				if(row.Count > 2)
					status = row[2].ToString();
				if (status == "Sent")
					continue;
				string discordName = (string)row[0];
				Console.WriteLine("Sending to " + discordName);
				//if(discordName.Contains("#"))
				{
					IGuildUser? user = null;
					if (discordName.Contains("#"))
						user = users.FirstOrDefault(u => u.Username + "#" + u.Discriminator == discordName);
					else if (ulong.TryParse(discordName, out ulong did))
						user = users.FirstOrDefault(u => u.Id == did);
					else if(user == null && !discordName.Contains("#"))
                        user = users.FirstOrDefault(u => u.Username == discordName);
                    if (user == null)
					{
						sheetsService.Spreadsheets.Values.BatchUpdate(SetStatus(tabTitle, i + 1, "Error: User not found"), sheetId).Execute();
						continue;
					}
					try
					{
						var dm = await user.CreateDMChannelAsync();
						await dm.SendMessageAsync(row[1].ToString());
						sheetsService.Spreadsheets.Values.BatchUpdate(SetStatus(tabTitle, i + 1, "Sent"), sheetId).Execute();
					}
					catch (Exception ex)
					{
						sheetsService.Spreadsheets.Values.BatchUpdate(SetStatus(tabTitle, i + 1, "Error: " + ex.Message), sheetId).Execute();
					}
				}
				await Task.Delay(500);
			}

			Console.WriteLine("Done!");

			SendTask = null;
		}

		static BatchUpdateValuesRequest SetStatus(string tabTitle, int row, string status)
		{
			return new BatchUpdateValuesRequest()
			{
				ValueInputOption = "USER_ENTERED",
				Data = new List<ValueRange>()
					{
						new ValueRange()
						{
							Range = $"{tabTitle}!C{row}",
							Values = new[] { new[] { status } }
						}
					},

			};
		}
	}
}