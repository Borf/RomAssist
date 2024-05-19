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

namespace RomAssistant.modules
{
    // Interation modules must be public and inherit from an IInterationModuleBase
    public class CollectThreadModule : InteractionModuleBase<SocketInteractionContext>
    {
        private InteractionHandler _handler;
        private Context context;
        private SheetsService sheetsService;

        // Constructor injection is also a valid way to access the dependencies
        public CollectThreadModule(InteractionHandler handler, Context context, SheetsService sheetsService)
        {
            _handler = handler;
            this.context = context;
            this.sheetsService = sheetsService;
        }

        [SlashCommand("gathermessages", "Gathers all messages in a thread into an excel")]
        public async Task GatherMessages([Summary(description: "The ID in the url")] string sheetId, [Summary(description: "The name of the tab to store into")] string sheetName)
        {
            await DeferAsync(ephemeral: true);
            try
            {
                string status = "";
                var sheet = sheetsService.Spreadsheets.Get(sheetId);
                var sheets = sheet.Execute().Sheets;
                var tab = sheets.FirstOrDefault(s => s.Properties.Title.ToLower() == sheetName.ToLower());
                if (tab == null)
                {
                    await ModifyOriginalResponseAsync(msg => msg.Content = "This sheet does not exist. Sheets are " + string.Join(",", sheets.Select(s => s.Properties.Title)));
                    return;
                }
                status += "Scanning messages...\n";
                await ModifyOriginalResponseAsync(msg => msg.Content = status);
                var messages = (await Context.Channel.GetMessagesAsync(100000).FlattenAsync()).Reverse();
                status += "Found " + messages.Count() + " messages, checking sheet\n";
                await ModifyOriginalResponseAsync(msg => msg.Content = status);
                var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{tab.Properties.Title}!A:Z").Execute();
                status += "Fetched sheet data, filling data now\n";
                await ModifyOriginalResponseAsync(msg => msg.Content = status);

                int rowIndex = 2;
                List<ValueRange> Data = new();
                foreach (var msg in messages)
                {
                    var index = rowIndex;
                    if (sheetData.Values != null)
                    {
                        for (int row = 0; row < sheetData.Values.Count && index == rowIndex; row++)
                        {
                            if (sheetData.Values[row] == null)
                                continue;
                            if (sheetData.Values[row].Count == 0)
                                continue;
                            if (sheetData.Values[row][0] == null)
                                continue;
                            if (sheetData.Values[row][0] == "'" + msg.Id + "")
                                index = row;
                        }
                    }
                    if (index == rowIndex)
                        rowIndex++;


                    List<string> attachments = msg.Attachments.Select(a => a.Url).ToList();
                    if (attachments.Count > 4)
                        attachments.RemoveRange(4, attachments.Count - 4);
                    while (attachments.Count < 4)
                        attachments.Add("");

                    ValueRange valueRange = new ValueRange()
                    {
                        Range = $"'{tab.Properties.Title}'!A{index}",
                        Values = new[] {
                            new object?[]
                            {
                                "'" + msg.Id + "",
                                msg.Timestamp,
                                msg.Author.Username + "#" + msg.Author.Discriminator,
                                msg.Content,
                                attachments[0],
                                attachments[1],
                                attachments[2],
                                attachments[3],
                                msg.EditedTimestamp,
                            }
                        }
                    };
                    Data.Add(valueRange);
                }

                var req = sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
                {
                    ValueInputOption = "USER_ENTERED",
                    Data = Data,
                }, sheetId);
                req.Execute();
                Data.Clear();


                status += "DONE!\n";
                await ModifyOriginalResponseAsync(msg => msg.Content = status);


            }
            catch (Exception ex)
            {
                await ModifyOriginalResponseAsync(msg => msg.Content = "The discord bot has no access to this sheet. Please add ticketbot@ticketbot-366321.iam.gserviceaccount.com to this sheet");
                Console.WriteLine(ex.ToString());
            }
        }
    }





}