using CsvHelper;
using CsvHelper.Configuration;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using RomAssistant.db;
using RomAssistant.Models;
using RomAssistant.Services;
using System;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace RomAssistant.modules;

// Interation modules must be public and inherit from an IInterationModuleBase
public class CollectThreadModule : InteractionModuleBase<SocketInteractionContext>
{
    private InteractionHandler _handler;
    private Context context;
    private SheetsService sheetsService;
    private SheetsORM sheetsORM;

    // Constructor injection is also a valid way to access the dependencies
    public CollectThreadModule(InteractionHandler handler, Context context, SheetsService sheetsService, SheetsORM sheetsORM)
    {
        _handler = handler;
        this.context = context;
        this.sheetsService = sheetsService;
        this.sheetsORM = sheetsORM;
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
                sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
                {
                    Requests = new List<Request>() { new() { AddSheet = new AddSheetRequest() { Properties = new SheetProperties() { Title = sheetName } } } }
                }, sheetId).Execute();

                sheet = sheetsService.Spreadsheets.Get(sheetId);
                sheets = sheet.Execute().Sheets;
                tab = sheets.FirstOrDefault(s => s.Properties.Title.ToLower() == sheetName.ToLower())!;
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
                if (msg == null)
                    continue;
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
                        if (sheetData.Values[row][0].ToString().EndsWith(msg.Id + ""))
                        {
                            index = row;
                            sheetData.Values[row][0] = "PROCESSED";
                        }
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
                            msg.Author.Id + "",
                            msg.Author.Username,
                            msg.Content,
                            attachments[0],
                            attachments[1],
                            attachments[2],
                            attachments[3],
                            msg.EditedTimestamp,
                            string.Join(", ", msg.Reactions.Select(kv => kv.Key.Name +"[" + kv.Value.ReactionCount + "]"))
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

            if(sheetData.Values == null)
            {
                status += "Error, sheetData.Values is null\n";
                await ModifyOriginalResponseAsync(msg => msg.Content = status);
                return;
            }

            foreach (var r in sheetData.Values)
            {
                if (r == null || r.Count < 1 || r[0]?.ToString() == "PROCESSED")
                    continue;
                Data.Add(new ValueRange()
                {
                    Range = $"'{tab.Properties.Title}'!A{rowIndex}",
                    Values = new List<IList<Object>> { r }
                });
                rowIndex++;
                if(Data.Count > 100)
                {
                    sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
                    {
                        ValueInputOption = "USER_ENTERED",
                        Data = Data,
                    }, sheetId).Execute();
                    Data.Clear();
                }
            }
            if (Data.Count > 0)
            {
                sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
                {
                    ValueInputOption = "USER_ENTERED",
                    Data = Data,
                }, sheetId).Execute();
                Data.Clear();
            }


            status += "DONE!\n";
            await ModifyOriginalResponseAsync(msg => msg.Content = status);


        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync(msg => msg.Content = "The discord bot has no access to this sheet. Please add ticketbot@ticketbot-366321.iam.gserviceaccount.com to this sheet");
            Console.WriteLine(ex.ToString());
        }
    }


    [SlashCommand("gathermessages2", "Gathers all messages in a thread into an excel")]
    public async Task GatherMessages2([Summary(description: "The ID in the url")] string sheetId, [Summary(description: "The name of the tab to store into")] string sheetName = "Messages")
    {
        await DeferAsync(ephemeral: true);
        await ModifyOriginalResponseAsync(msg => msg.Content = "Fetching existing rows");
        var rows = sheetsORM.Get<DiscordMessage>(sheetId, sheetName, forUpdate: true);
        await ModifyOriginalResponseAsync(msg => msg.Content = "Fetching messages");
        var messages = (await Context.Channel.GetMessagesAsync(100000).FlattenAsync()).Reverse().ToList();
        await ModifyOriginalResponseAsync(msg => msg.Content = $"Got messages! {messages.Count} messages in discord, {rows.Count} in sheets");


        int added = 0;
        int updated = 0;
        int deleted = 0;

        foreach(var message in messages)
        {
            var oldRow = rows.FirstOrDefault(r => r.MessageId == message.Id);
            if (oldRow == null)
            {
                rows.Add(new DiscordMessage
                {
                    MessageId = message.Id,
                    Date = message.Timestamp,
                    ChannelId = message.Channel.Id,
                    ChannelName = message.Channel.Name,
                    AuthorId = message.Author.Id,
                    Author = message.Author.Username,
                    Content = message.Content,
                    Attachment0 = message.Attachments.ElementAtOrDefault(0)?.Url ?? string.Empty,
                    Attachment1 = message.Attachments.ElementAtOrDefault(1)?.Url ?? string.Empty,
                    Attachment2 = message.Attachments.ElementAtOrDefault(2)?.Url ?? string.Empty,
                    Attachment3 = message.Attachments.ElementAtOrDefault(3)?.Url ?? string.Empty,
                    EditedTimestamp = message.EditedTimestamp,
                    Reactions = string.Join(", ", message.Reactions.Select(kv => kv.Key.Name + "[" + kv.Value.ReactionCount + "]"))
                });
                added++;
            }
            else
            {
                string reactions = string.Join(", ", message.Reactions.Select(kv => kv.Key.Name + "[" + kv.Value.ReactionCount + "]"));
                if (oldRow.Content != message.Content || 
                    (oldRow.EditedTimestamp != null && message.EditedTimestamp == null) || 
                    (oldRow.EditedTimestamp == null && message.EditedTimestamp != null) || 
                    (oldRow.EditedTimestamp != null && message.EditedTimestamp != null && Math.Abs((oldRow.EditedTimestamp - message!.EditedTimestamp).Value.TotalSeconds) > 1) || 
                    oldRow.Reactions != reactions)
                {
                    oldRow.Content = message.Content;
                    oldRow.EditedTimestamp = message.EditedTimestamp;
                    oldRow.Reactions = reactions;
                    updated++;
                }
            }
        }
        foreach(var row in rows) //mark rows that got deleted as deleted
        {
            if(!messages.Any(m => m.Id == row.MessageId))
            {
                row.MessageDeleted = true;
                deleted++;
            }
        }
        await ModifyOriginalResponseAsync(msg => msg.Content = $"Organized changes: {added} rows added, {updated} rows updated, {deleted} rows deleted");
        sheetsORM.Update(sheetId, sheetName, rows);
        await ModifyOriginalResponseAsync(msg => msg.Content = $"Done\nSaved changes: {added} rows added, {updated} rows updated, {deleted} rows deleted");
    }

}