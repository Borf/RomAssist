using Discord.Interactions;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using RomAssistant.db;
using RomAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace RomAssistant.modules;

public class RaffleSheetModule : InteractionModuleBase<SocketInteractionContext>
{
    private SheetsService sheetsService;
    public RaffleSheetModule(SheetsService sheetsService)
    {
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
                if (row[12].ToString() != "#N/A")
                    if (!ulong.TryParse(row[12].ToString(), out cid))
                        Console.WriteLine("couldn't parse " + row[12].ToString());
                string ign = row[13].ToString() ?? "";
                string server = row[14].ToString() ?? "";
                if (row.Count > 15 && !string.IsNullOrEmpty(row[15].ToString()))
                    if (!FirstCorrect.ContainsKey(row[15].ToString() ?? ""))
                        FirstCorrect[row[15].ToString() ?? ""] = messageId;

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
                if(entry.Entries.Count < 6)
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
                .Where(messageId => !string.IsNullOrEmpty(sheetData.Values.FirstOrDefault(row => row != null && row.Count > 15 &&
                        row[0] != null &&
                        ulong.TryParse(row[0].ToString(), out var msgId) && msgId == messageId)?[15]?.ToString()))
                .ToList();
        }
        Console.WriteLine($"Gathered {raffleEntries.Count} people, with {raffleEntries.Sum(re => re.Entries.Count)} total");

        int roll = 1;
        var currentRound = raffleEntries.Raffle();
        //first, make sure the people who answered correctly first get a win
        foreach (var fc in FirstCorrect)
        {
            var entry = raffleEntries.FirstOrDefault(r => r.Entries.Contains(fc.Value));
            if (entry != null && entry.WinnerEntries.Count == 0)
            {
                entry.WinnerRolls.Add(roll);
                entry.WinnerEntries.Add(fc.Key);
                entry.Entries.RemoveAt(0);
                currentRound.Remove(entry);
                roll++;
            }
        }


        List<ValueRange> Data = new();
        while (roll < rolls+1 && currentRound.Count > 0)
        {
            var entry = currentRound.First();
            ulong messageId = entry.Entries.First();
            if (!string.IsNullOrEmpty(sheetData.Values.FirstOrDefault(row => row != null && row.Count > 15 && row[0] != null && ulong.TryParse(row[0].ToString(), out var msgId) && msgId == messageId)?[15]?.ToString()))
            {
                //                    entry.WinnerEntries.Add(messageId);
                entry.WinnerEntries.Add(sheetData.Values.FirstOrDefault(row => row != null && row.Count > 15 && row[0] != null && ulong.TryParse(row[0].ToString(), out var msgId) && msgId == messageId)?[15]?.ToString() ?? "");
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
                                            entry.WinnerRolls.Count > 3 ? entry.WinnerRolls[3].ToString() : null,
                                            entry.WinnerRolls.Count > 3 ? entry.WinnerEntries[3].ToString() : null,
                                            entry.WinnerRolls.Count > 4 ? entry.WinnerRolls[4].ToString() : null,
                                            entry.WinnerRolls.Count > 4 ? entry.WinnerEntries[4].ToString() : null,
                                            entry.WinnerRolls.Count > 5 ? entry.WinnerRolls[5].ToString() : null,
                                            entry.WinnerRolls.Count > 5 ? entry.WinnerEntries[5].ToString() : null,
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


    [DoAdminCheck]
    [SlashCommand("collectcid", "Collects the accountid, name, and guild for cids")]
    public async Task CollectCid(
        [Summary(description: "The ID in the url")] string sheetId,
        [Summary(description: "Source sheet")] string sourceSheetName,
        [Summary(description: "Column (A,B,C,D,...)")] string column,
        [Summary(description: "Server (can be EUEL,NAEL,NADP,SEAEL,SEAMP,SEAMOF,SEAVG,SEAPC or column)")] string server,
        [Summary(description: "Where do you want to write to")] string targetSheetName = "userinfo"
        )
    {
        await DeferAsync(ephemeral: true);
        var sheet = sheetsService.Spreadsheets.Get(sheetId).Execute();
        var sheets = sheet.Sheets;
        if (!sheets.Any(s => s.Properties.Title == sourceSheetName))
        {
            await ModifyOriginalResponseAsync(m => m.Content = "Tab " + sourceSheetName + " not found");
            return;
        }
        List<RowData> Rows = new();

        if (!sheets.Any(s => s.Properties.Title == targetSheetName))
        {
            sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
            {
                Requests = new List<Request>() { new() { AddSheet = new AddSheetRequest() { Properties = new SheetProperties() { Title = targetSheetName } } } }
            }, sheetId).Execute();


            Rows.Add(new RowData()
            {
                Values = new List<CellData>()
                {
                        new() { UserEnteredValue = new() {  StringValue = "Cid"} },
                        new() { UserEnteredValue = new() {  StringValue = "Server" } },
                        new() { UserEnteredValue = new() {  StringValue = "Name" } },
                        new() { UserEnteredValue = new() {  StringValue = "AccountId" } },
                        new() { UserEnteredValue = new() {  StringValue = "Guild" } },
                        new() { UserEnteredValue = new() {  StringValue = "Server ID" } },
                        new() { UserEnteredValue = new() {  StringValue = "Scan Time" } },
                    }
            });
        }

        var targetSheet = sheetsService.Spreadsheets.Get(sheetId).Execute().Sheets.First(s => s.Properties.Title == targetSheetName);



        await ModifyOriginalResponseAsync(m => m.Content = "Getting current data");

        var currentData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{targetSheetName}!A:A").Execute();

        List<ulong> parsedCids = currentData?.Values?.Where(r => r != null && r.Count > 0 && ulong.TryParse((string)r[0], out ulong bla)).Select(v => ulong.Parse((string)v[0])).ToList() ?? new();


        await ModifyOriginalResponseAsync(m => m.Content = "Getting CIDs");

        var cids = sheetsService.Spreadsheets.Values.Get(sheetId, $"{sourceSheetName}!"+column+":"+column).Execute();
        await ModifyOriginalResponseAsync(m => m.Content = cids?.Values?.Count + " rows found");

        var allCids = cids.Values.Where(v => v?.Count > 0 && v[0] != null).Select(v => (string)v[0]);

        int index = 0;
        foreach (var cidStr in allCids)
        {
            index++;
            if (ulong.TryParse(cidStr, out ulong cid))
            {
                if (parsedCids.Contains(cid))
                {
                    Console.WriteLine("Skipping CID " + cid);
                    continue;
                }
                Console.WriteLine("Finding CID " + cid);
                try
                {
                    var res = await new HttpClient().GetStringAsync("https://romapi.borf.nl/characternoserver/" + cid);

                    if (!res.StartsWith("{"))
                    {
                        Console.WriteLine("CID " + cid + " got error: " + res);
                        continue;
                    }

                    var charData = JsonSerializer.Deserialize<JsonObject>(res)!;
                    Console.WriteLine("Found character " + charData["Name"].GetValue<string>());
                    try
                    {
                        await ModifyOriginalResponseAsync(m => m.Content = "Scanned " + index + " / " + allCids.Count());
                    }
                    catch (Exception) { }

                    Rows.Add(new RowData()
                    {
                        Values = new List<CellData>()
                        {
                            new() { UserEnteredValue = new() {  NumberValue = cid } },
                            new() { UserEnteredValue = new() {  StringValue = ((Server) (charData["Server"].GetValue<int>())).ToString() } },
                            new() { UserEnteredValue = new() {  StringValue = charData["Name"].GetValue<string>() } },
                            new() { UserEnteredValue = new() {  NumberValue = charData["AccountId"].GetValue<ulong>() } },
                            new() { UserEnteredValue = new() {  StringValue = charData["GuildName"].GetValue<string>() } },
                            new() { UserEnteredValue = new() {  NumberValue = charData["Serverid"].GetValue<int>() } },
                            new() { UserEnteredValue = new() {  StringValue = DateTimeOffset.FromUnixTimeSeconds(charData["LastScanTimeStamp"].GetValue<long>()).ToString() } },
                        }
                    });
                }catch(Exception e)
                {
                    Console.WriteLine(e.ToString());
                }

                if (Rows.Count > 5)
                {
                    sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
                    {
                        Requests = new List<Request>() { new() { AppendCells = new AppendCellsRequest() { SheetId = targetSheet.Properties.SheetId, Fields = "*", Rows = Rows } } }
                    }, sheetId).Execute();
                    Rows.Clear();
                }
                parsedCids.Add(cid);
            }
            else
                Console.WriteLine($"Could not parse CID '{cidStr}'");
        }

        sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest()
        {
            Requests = new List<Request>() { new() { AppendCells = new AppendCellsRequest() { SheetId = targetSheet.Properties.SheetId, Fields = "*", Rows = Rows } } }
        }, sheetId).Execute();
        Rows.Clear();

        Console.WriteLine("Done!");

        try
        {
            await ModifyOriginalResponseAsync(m => m.Content = "Done!");
        }
        catch (Exception) { }



    }
}
