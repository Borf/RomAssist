using Discord.Interactions;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using RomAssistant.db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules
{
    public class RaffleSheetModule : InteractionModuleBase<SocketInteractionContext>
    {
        private InteractionHandler _handler;
        private SheetsService sheetsService;
        public RaffleSheetModule(InteractionHandler handler, SheetsService sheetsService)
        {
            _handler = handler;
            this.sheetsService = sheetsService;
        }

        [DoAdminCheck]
        [SlashCommand("rafflesheet", "Raffles excel sheet")]
        public async Task RaffleSheet([Summary(description: "The ID in the url")] string sheetId, [Summary(description: "The name of the tab with the users")] string sheetName, [Summary(description: "The name of the tab with the users")] string targetSheetName, int rolls = 100)
        {
            if(Context != null) await DeferAsync(ephemeral: true);
            var sheet = sheetsService.Spreadsheets.Get(sheetId).Execute();
            var sheets = sheet.Sheets;
            if (!sheets.Any(s => s.Properties.Title == sheetName))
            {
                await ModifyOriginalResponseAsync(m => m.Content = "Tab " + sheetName + " not found");
                return;
            }
            if (Context != null) await ModifyOriginalResponseAsync(m => m.Content = "Clearing draw sheet");
            if (sheets.Any(s => s.Properties.Title == targetSheetName))
            {
                sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
                {
                    Requests = new List<Request>() { new() { DeleteSheet = new DeleteSheetRequest() { SheetId = sheets.First(s => s.Properties.Title == targetSheetName).Properties.SheetId } } }
                }, sheetId).Execute();
            }
            sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
            {
                Requests = new List<Request>() { new() { AddSheet = new AddSheetRequest() { Properties = new SheetProperties() { Title = targetSheetName } } } }
            }, sheetId).Execute();

            if (Context != null) await ModifyOriginalResponseAsync(m => m.Content = "Getting raffle data");
            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{sheetName}!A:Z").Execute();

            Dictionary<string, ulong> FirstCorrect = new();

            var raffleEntries = new List<RaffleEntry>();
            foreach (var row in sheetData.Values)
            {
                if (row == null)
                    continue;
                if (row.Count == 0)
                    continue;
                if (row[0] == null)
                    continue;
                if (!ulong.TryParse(row[0].ToString(), out ulong tmp))
                    continue;
                try
                {
                    ulong messageId = ulong.Parse(row[0].ToString() ?? "");
                    DateTime dateTime = (DateTime)row[1];
                    string user = row[3].ToString() ?? "";
                    if (user == "autumnhime")
                        continue;
                    string userId = row[2].ToString() ?? "";
                    string msg = row[4].ToString() ?? "";
                    ulong cid = 0;
                    string attachment1 = row[5].ToString() ?? "";
                    if (row[11].ToString() != "#N/A")
                        if (!ulong.TryParse(row[11].ToString(), out cid))
                            Console.WriteLine("couldn't parse " + row[11].ToString());
                    string ign = row[12].ToString() ?? "";
                    string server = row[13].ToString() ?? "";
                    if (row.Count > 14 && !string.IsNullOrEmpty(row[14].ToString()))
                        if (!FirstCorrect.ContainsKey(row[14].ToString() ?? ""))
                            FirstCorrect[row[14].ToString() ?? ""] = messageId;

                    var entry = raffleEntries.FirstOrDefault(e => e.UserName == user);
                    if (entry == null)
                    {
                        entry = new RaffleEntry()
                        {
                            UserName = user,
                            Userid = userId,
                            cid = cid,
                            ign = ign,
                            server = server
                        };
                        raffleEntries.Add(entry);
                    }
                    if (entry.cid == 0)
                        entry.cid = cid;
                    if (string.IsNullOrEmpty(entry.ign) || entry.ign == "#N/A")
                        entry.ign = ign;
                    if (string.IsNullOrEmpty(entry.server) || entry.server == "#N/A")
                        entry.server = server;

                    if (!string.IsNullOrEmpty(attachment1) && entry.Images.Count < 3)
                        entry.Images.Add(attachment1);
                    if(entry.Entries.Count < 3)
                        entry.Entries.Add(messageId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
            Console.WriteLine($"Gathered {raffleEntries.Count} for {sheetName} out of {sheetData.Values.Count} rows. Writing to {targetSheetName}");

            //remove invalids
            foreach(var entry in raffleEntries)
            {

                entry.Entries = entry.Entries
                    .Where(messageId => !string.IsNullOrEmpty(sheetData.Values.FirstOrDefault(row => row != null && row.Count > 14 &&
                            row[0] != null &&
                            ulong.TryParse(row[0].ToString(), out var msgId) && msgId == messageId)?[14]?.ToString()))
                    .ToList();
            }

            int roll = 1;
            var currentRound = raffleEntries.Raffle();

            foreach (var fc in FirstCorrect)
            {
                var entry = raffleEntries.FirstOrDefault(r => r.Entries.Contains(fc.Value));
                if (entry != null && entry.WinnerEntries.Count == 0)
                {
                    entry.WinnerRolls.Add(roll);
                    entry.WinnerEntries.Add(fc.Key);
                    currentRound.Remove(entry);
                    roll++;
                }
            }


            List<ValueRange> Data = new();
            while (roll < rolls+1 && currentRound.Count > 0)
            {
                var entry = currentRound.First();
                ulong messageId = entry.Entries.First();
                if (!string.IsNullOrEmpty(sheetData.Values.FirstOrDefault(row => row != null && row.Count > 14 && row[0] != null && ulong.TryParse(row[0].ToString(), out var msgId) && msgId == messageId)?[14]?.ToString()))
                {
                    //                    entry.WinnerEntries.Add(messageId);
                    entry.WinnerEntries.Add(sheetData.Values.FirstOrDefault(row => row != null && row.Count > 14 && row[0] != null && ulong.TryParse(row[0].ToString(), out var msgId) && msgId == messageId)?[14]?.ToString() ?? "");
                    entry.WinnerRolls.Add(roll);
                    roll++;
                }

                entry.Entries.RemoveAt(0);
                currentRound.RemoveAt(0);
                if (currentRound.Count == 0)
                    currentRound = raffleEntries.Raffle();

            }

            int newRowIndex = 2;
            foreach (var entry in raffleEntries)
            {
                if (entry.WinnerRolls.Count > 0)
                {
                    ValueRange valueRange = new ValueRange()
                    {
                        Range = $"'{targetSheetName}'!A{newRowIndex}",
                        Values = new[] {
                                            new object?[]
                                            {
                                                entry.Userid + "",
                                                entry.UserName,
                                                entry.cid,
                                                entry.ign,
                                                entry.server,
                                                entry.WinnerEntries.Count,
                                                entry.WinnerRolls.Count > 0 ? entry.WinnerRolls[0].ToString() : null,
                                                entry.WinnerRolls.Count > 0 ? entry.WinnerEntries[0].ToString() : null,
                                                entry.WinnerRolls.Count > 1 ? entry.WinnerRolls[1].ToString() : null,
                                                entry.WinnerRolls.Count > 1 ? entry.WinnerEntries[1].ToString() : null,
                                                entry.WinnerRolls.Count > 2 ? entry.WinnerRolls[2].ToString() : null,
                                                entry.WinnerRolls.Count > 2 ? entry.WinnerEntries[2].ToString() : null,
                                                entry.UserName,
                                            }
                                        }
                    };
                    Data.Add(valueRange);
                    newRowIndex++;
                }
                else
                {
                    Console.WriteLine(entry.UserName + " is a loser. This person had " + entry.Entries.Count + " entries");
                }
            }
            Console.WriteLine((newRowIndex-2) + " winners");

            sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
            {
                //ValueInputOption = "USER_ENTERED",
                ValueInputOption = "RAW",
                Data = Data,
            }, sheetId).Execute();


            Data.Clear();
            for(int i = newRowIndex; i < rolls+2; i++)
            {
                ValueRange valueRange = new ValueRange()
                {
                    Range = $"'{targetSheetName}'!A{newRowIndex}",
                    Values = new[] {
                                            new object?[]
                                            {
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                null,
                                                "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ",
                                            }
                                        }
                };
                Data.Add(valueRange);
                newRowIndex++;
            }

            sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
            {
                //ValueInputOption = "USER_ENTERED",
                ValueInputOption = "RAW",
                Data = Data,
            }, sheetId).Execute();


            if (Context != null) await ModifyOriginalResponseAsync(m => m.Content = "Done");

        }
    }
}
