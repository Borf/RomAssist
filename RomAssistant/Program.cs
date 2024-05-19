using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using IMPTShuffleBot.services;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using RomAssistant;
using RomAssistant.db;
using RomAssistant.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Channels;

if (!Directory.Exists("data"))
	Directory.CreateDirectory("data");

var configuration = new ConfigurationBuilder()
	.AddJsonFile("data/appsettings.json", optional: true)
	.AddUserSecrets<Program>()
	.Build();


GoogleCredential credential;
using (var stream = new FileStream("data/client_secrets.json", FileMode.Open, FileAccess.Read))
{
	credential = GoogleCredential.FromStream(stream);
	if (credential.IsCreateScopedRequired)
		credential = credential.CreateScoped(new string[] { SheetsService.Scope.Drive });
}
var sheetService = new SheetsService(new BaseClientService.Initializer()
{
	HttpClientInitializer = credential,
	ApplicationName = "TicketBot"
});

var serviceBuilder = new ServiceCollection()
    .AddSingleton(configuration)
    .AddSingleton<SheetsService>(sheetService)
    .AddSingleton(new DiscordSocketConfig()
    {
        GatewayIntents = (Discord.GatewayIntents.AllUnprivileged
            | Discord.GatewayIntents.MessageContent
            | Discord.GatewayIntents.GuildMembers)
            & ~Discord.GatewayIntents.GuildScheduledEvents
            & ~Discord.GatewayIntents.GuildInvites,
        AlwaysDownloadUsers = true,
    })
    .AddDbContext<Context>()
    //    .AddBackgroundService<NameCheckerService>()
    .AddSingleton<DiscordSocketClient>()
    .AddBackgroundService<VoiceChannelTrackerService>()
	.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
	.AddSingleton<InteractionHandler>()
    .AddSingleton<BugTracker>()
    .AddSingleton<FeedbackTracker>()
	.AddSingleton<IServiceProvider>(sp => sp);

;

var services = serviceBuilder.BuildServiceProvider();

{
	using (var scope = services.CreateScope())
	using (var context = scope.ServiceProvider.GetRequiredService<Context>())
	{
		//await context.Database.EnsureDeletedAsync();
		if (context.Database.EnsureCreated())
		{
		}
	}
}
//await new RaffleSheetModule(null, services.GetRequiredService<SheetsService>()).RaffleSheet("1189KOxFXr8kOlDzL_GWaqVcvUTgi-5wcD_vFh50l0Eo", "EU Messages", "EU Draw", 100);
//await new RaffleSheetModule(null, services.GetRequiredService<SheetsService>()).RaffleSheet("1189KOxFXr8kOlDzL_GWaqVcvUTgi-5wcD_vFh50l0Eo", "NA Messages", "NA Draw", 200);
//await new RaffleSheetModule(null, services.GetRequiredService<SheetsService>()).RaffleSheet("1189KOxFXr8kOlDzL_GWaqVcvUTgi-5wcD_vFh50l0Eo", "SEA Messages", "SEA Draw", 300);

{
	var client = services.GetRequiredService<DiscordSocketClient>();
    

    
	client.Log += LogAsync;

	// Here we can initialize the service that will register and execute our commands
	await services.GetRequiredService<InteractionHandler>().InitializeAsync();
	await client.LoginAsync(TokenType.Bot, configuration["token"]);
	await client.StartAsync();
	await client.SetGameAsync("to the moderators", null, ActivityType.Listening);

    //client.ThreadCreated += async (SocketThreadChannel channel) =>
    //{
    //    await services.GetRequiredService<BugTracker>().ThreadCreated(channel);
    //    await services.GetRequiredService<FeedbackTracker>().ThreadCreated(channel);
    //};
    //client.ThreadDeleted += async (Cacheable<SocketThreadChannel, ulong> channel) =>
    //{
    //    //await services.GetRequiredService<BugTracker>().ThreadDeleted(channel.Value);
    //    //await services.GetRequiredService<FeedbackTracker>().ThreadDeleted(channel.Value);
    //};
    //client.ThreadUpdated += async (Cacheable<SocketThreadChannel, ulong> channel, SocketThreadChannel channel2) =>
    //{
    //    await services.GetRequiredService<BugTracker>().ThreadUpdated(channel.Value);
    //    await services.GetRequiredService<FeedbackTracker>().ThreadUpdated(channel.Value);
    //};

    client.Ready += async () =>
	{
        try
        {
            //var bugTracker = services.GetRequiredService<BugTracker>();
            //await bugTracker.Start(client, sheetService);

            //var feedbackTracker = services.GetRequiredService<FeedbackTracker>();
            //await feedbackTracker.Start(client, sheetService);
        }
        catch (Exception ex) { Console.WriteLine(ex); }


        if(false) //rename people to their proper name
        {
            Console.WriteLine("renaming people...waiting 5 seconds to load users");
            await Task.Delay(5000);
            Console.WriteLine("renaming people");
            var sheetsService = services.GetRequiredService<SheetsService>();

            string sheetId = "1rs2zoyE8Oamru4NOGb7HJCDRkTWiDi3Nb3xv4-ec4kg";
            var guild = client.GetGuild(885126454545891398);
            var sheet = sheetsService.Spreadsheets.Get(sheetId);
            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"Users!A:Z").Execute();

            foreach(var user in sheetData.Values)
            {
                if (user == sheetData.Values[0])
                    continue;
                if (user.Count < 26)
                    continue;
                if(user[25].ToString() == "rename")
                {
                    string newname = user[21] + " - " + user[22];
                    Console.WriteLine($"Renaming {user[0]} {user[3]} to {newname}");


                    var guser = guild.GetUser(ulong.Parse(user[0].ToString()));
                    if (guser != null)
                    {
                        await guser.ModifyAsync(u =>
                        {
                            u.Nickname = newname;
                        });
                    }
                    else
                        Console.WriteLine("Error renaming!");
                }

            }



        }


        if (false) //update Users sheet
        {
            Console.WriteLine("Updating GL excel");
            var sheetsService = services.GetRequiredService<SheetsService>();
            string sheetId = "1rs2zoyE8Oamru4NOGb7HJCDRkTWiDi3Nb3xv4-ec4kg";
            var guild = client.GetGuild(885126454545891398);
            var sheet = sheetsService.Spreadsheets.Get(sheetId);
            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"Users!A:Z").Execute();

            Console.WriteLine("Getting users");
            var users = await guild.GetUsersAsync().FlattenAsync();
            int rowCount = 2;
            for(int i = 1; i < (sheetData?.Values?.Count ?? 0); i++)
                if (!string.IsNullOrEmpty(sheetData.Values[i][0].ToString()))
                    rowCount = i + 2;
            List<ValueRange> Data = new();
            Console.WriteLine("Updating users");
            foreach (var user in users.Where(u => !u.IsBot).OrderBy(u => u.Id))
            {
                bool found = false;
                for (int i = 0; i < sheetData.Values.Count; i++)
                {
                    if (sheetData.Values[i] == null || sheetData.Values[i].Count == 0)
                        continue;
                    if (sheetData.Values[i][0].ToString() == "" + user.Id)
                    { 
                        found = true;
                        break;
                    }
                }
                if (found)
                    continue;
                ValueRange valueRange = new ValueRange()
                {
                    Range = $"'Users'!A{rowCount}",
                    Values = new[] {
                        new object?[]
                        {
                            "'" + user.Id + ""
                        }
                    }
                };
                Data.Add(valueRange);
                rowCount++;
            }
            var req = sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
            {
                ValueInputOption = "USER_ENTERED",
                Data = Data,
            }, sheetId);
            req.Execute();
            Data.Clear();
            sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"Users!A:Z").Execute();

            Console.WriteLine("Updated users, getting intro messages");
            var introduceChannel = guild.GetTextChannel(887010818959495258);
            var messages = (await introduceChannel.GetMessagesAsync(100000).FlattenAsync()).Reverse();
            Console.WriteLine("Got " + messages.Count() + " messages");
            foreach (var user in users)
            {
                if(Data.Count > 100)
                {
                    Console.Write(".");
                    sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
                    {
                        ValueInputOption = "USER_ENTERED",
                        Data = Data,
                    }, sheetId).Execute();
                    Data.Clear();
                }

                for (int i = 0; i < sheetData.Values.Count; i++)
                {
                    if (sheetData.Values[i] == null || sheetData.Values[i].Count == 0)
                        continue;
                    if (sheetData.Values[i][0].ToString() == "" + user.Id)
                    {
                        var introMessageCount = messages.Count(c => c.Author.Id == user.Id);

                        string introMessageIGN = "";
                        string introMessageGuild = "";
                        string introMessageServer = "";
                        string introMessageRole = "";
                        string introMessage = "";

                        var userMessages = messages.Where(m => m.Author.Id == user.Id).Reverse();
                        foreach(var m in userMessages.Take(50))
                        {
                            {
                                var match = Regex.Match(m.Content, "IGN[: ]+(.*)", RegexOptions.IgnoreCase);
                                if (match.Success && string.IsNullOrEmpty(introMessageIGN))
                                    introMessageIGN = match.Groups[1].Value;
                            }
                            {
                                var match = Regex.Match(m.Content, "Guild[: ]+(.*)", RegexOptions.IgnoreCase);
                                if (match.Success && string.IsNullOrEmpty(introMessageGuild))
                                    introMessageGuild = match.Groups[1].Value;
                            }
                            {
                                var match = Regex.Match(m.Content, "Server[: ]+(.*)", RegexOptions.IgnoreCase);
                                if (match.Success && string.IsNullOrEmpty(introMessageServer))
                                    introMessageServer = match.Groups[1].Value;
                            }
                            {
                                var match = Regex.Match(m.Content, "Role[: ]+(.*)", RegexOptions.IgnoreCase);
                                if (match.Success && string.IsNullOrEmpty(introMessageRole))
                                    introMessageRole = match.Groups[1].Value;
                            }
                        }

                        if (userMessages.Count() < 10)
                            introMessage = string.Join("\n\n---\n\n", userMessages.Select(m => m.CreatedAt + "\n" + m.Content));

                        Data.Add(new ValueRange()
                        {
                            Range = $"'Users'!B{i+1}",
                            Values = new[] {
                                new object?[]
                                {
                                    user.Username,
                                    user.Discriminator,
                                    user.Nickname ?? "",
                                    user.CreatedAt,
                                    user.JoinedAt,
                                    "=IMAGE(\"" + user.GetAvatarUrl() + "\")",
                                    string.Join(",", user.RoleIds.Select(ri => guild.Roles.First(r => r.Id == ri).Name).Where(r => r != "@everyone")),
                                    introMessageCount + "",
                                    introMessage,
                                    introMessageIGN.Trim(),
                                    introMessageGuild.Trim(),
                                    introMessageServer.Trim(),
                                    introMessageRole,
                                }
                            }
                        });

                        continue;
                    }
                }
            }
            sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
            {
                ValueInputOption = "USER_ENTERED",
                Data = Data,
            }, sheetId).Execute();
            Data.Clear();

            Console.WriteLine("Done updating excel");

        }


        if (false) //update userinfo sheet
        {
            Console.WriteLine("Updating GL excel userinfo");
            var sheetsService = services.GetRequiredService<SheetsService>();
            string sheetId = "1rs2zoyE8Oamru4NOGb7HJCDRkTWiDi3Nb3xv4-ec4kg";
            var guild = client.GetGuild(885126454545891398);
            var sheet = sheetsService.Spreadsheets.Get(sheetId);
            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"Users!A:Z").Execute();
            var userData = sheetsService.Spreadsheets.Values.Get(sheetId, $"UserInfo!A:Z").Execute();

            List<ValueRange> Data = new();
            Console.WriteLine("Updating users");

            int rowCount = 2;
            for (int i = 1; i < (userData?.Values?.Count ?? 0); i++)
                if (userData.Values[i].Count > 0 && !string.IsNullOrEmpty(userData.Values[i][0].ToString()))
                    rowCount = i + 2;

            HashSet<(string, string)> added = new();

            var addUserInfo = async (string server, string name, List<ValueRange> Data) =>
            {
                if (added.Contains((server, name)))
                    return;
                bool found = false;
                for (int i = 0; i < (userData.Values?.Count ?? 0); i++)
                {
                    if (userData.Values[i] == null || userData.Values[i].Count == 0)
                        continue;
                    if (userData.Values[i][0].ToString() == server && userData.Values[i]?[1]?.ToString() == name)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Console.WriteLine($"Looking up {server} - {name}");
                    JsonObject? gameData = null;
                    try
                    {
                        gameData = JsonSerializer.Deserialize<JsonObject>(await new HttpClient().GetStringAsync($"http://romapi.borf.nl/{server}/character/get/{name}"));
                    }
                    catch (Exception ex)
                    {

                    }

                    ValueRange valueRange = new ValueRange()
                    {
                        Range = $"'UserInfo'!A{rowCount}",
                        Values = new[] {
                                new object?[]
                                {
                                    server,
                                    name,
                                    server + "_" + name,
                                    gameData?["Id"]?.ToString(),
                                    gameData?["AccountId"]?.ToString(),
                                    gameData?["GuildName"]?.ToString(),
                                    gameData?["Name"]?.ToString(),
                                }
                            }
                    };
                    Data.Add(valueRange);
                    added.Add((server, name));
                    rowCount++;
                }
            };


            foreach (var user in sheetData.Values)
            {
                if (user == sheetData.Values[0])
                    continue;
                //server = 15
                //nick1 = 17
                //nick2 = 18

                if (user.Count < 15 || string.IsNullOrEmpty(user[15].ToString()))
                    continue;
                if (user.Count > 17 && !string.IsNullOrEmpty(user[17].ToString()))
                    await addUserInfo(user[15].ToString(), user[17].ToString(), Data);


                if (user.Count > 18 && user[17].ToString() != user[18].ToString() && !string.IsNullOrEmpty(user[18].ToString()))
                    await addUserInfo(user[15].ToString(), user[18].ToString(), Data);
                if (Data.Count > 5)
                {
                    var req = sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
                    {
                        ValueInputOption = "USER_ENTERED",
                        Data = Data,
                    }, sheetId);
                    req.Execute();
                    Data.Clear();
                }


            }
            var req2 = sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
            {
                ValueInputOption = "USER_ENTERED",
                Data = Data,
            }, sheetId);
            req2.Execute();
            Console.WriteLine("Done updating userinfo");
        }


        if (false) // gathermessages command
    {
        var sheetsService = services.GetRequiredService<SheetsService>();
        string sheetId = "1DL8IiLuALmqX273dt2cZ2hDSKfb_sVzjGOXOtJoKwcI";
        var guild = client.GetGuild(248700646302285824);

            //var sheetName = "EU";
            //var channel = guild.GetThreadChannel(1110606875944300564);
            //var sheetName = "Global";
            //var channel = guild.GetThreadChannel(1110606800371318944);
            var sheetName = "SEA";
            var channel = guild.GetThreadChannel(1110606724756410388);

            try
        {
            string status = "";
            var sheet = sheetsService.Spreadsheets.Get(sheetId);
            var sheets = sheet.Execute().Sheets;
            var tab = sheets.FirstOrDefault(s => s.Properties.Title.ToLower() == sheetName.ToLower());
            if (tab == null)
            {
                Console.WriteLine("This sheet does not exist. Sheets are " + string.Join(",", sheets.Select(s => s.Properties.Title)));
                return;
            }
            Console.WriteLine("Scanning messages...");
            var messages = (await channel.GetMessagesAsync(100000).FlattenAsync()).Reverse();
            Console.WriteLine("Found " + messages.Count() + " messages, checking sheet");
            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{tab.Properties.Title}!A:Z").Execute();

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




            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        //if (true) // raffling!
        //{
        //    var sheetsService = services.GetRequiredService<SheetsService>();
        //    string sheetId = "1DL8IiLuALmqX273dt2cZ2hDSKfb_sVzjGOXOtJoKwcI";
        //    var guild = client.GetGuild(248700646302285824);


        //    var sheet = sheetsService.Spreadsheets.Get(sheetId);
        //    var sheets = sheet.Execute().Sheets;
        //    var tabs = new[] { ("EU", 100), ("SEA", 300), ("Global", 200) };
        //    try
        //    {
        //        foreach (var tabName in tabs)
        //        {
        //            var tab = sheets.FirstOrDefault(s => s.Properties.Title == tabName.Item1);
        //            if (tab == null)
        //            {
        //                Console.WriteLine("This tab does not exist. Sheets are " + string.Join(",", sheets.Select(s => s.Properties.Title)));
        //                return;
        //            }
        //            var targetTabName = $"Draw {tabName.Item1}";

        //            var sheetData = sheetsService.Spreadsheets.Values.Get(sheetId, $"{tab.Properties.Title}!A:Z").Execute();

        //            var raffleEntries = new List<RaffleEntry>();


        //            foreach (var row in sheetData.Values)
        //            {
        //                if (row == null)
        //                    continue;
        //                if (row.Count == 0)
        //                    continue;

        //                try
        //                {
        //                    ulong messageId = ulong.Parse(row[0].ToString());
        //                    DateTime dateTime = (DateTime)row[1];
        //                    string user = row[2].ToString();
        //                    if (user == "autumnhime#5210")
        //                        continue;
        //                    string msg = row[3].ToString();
        //                    ulong cid = 0;
        //                    string attachment1 = row[4].ToString();
        //                    if (row[9].ToString() != "#N/A")
        //                        if (!ulong.TryParse(row[9].ToString(), out cid))
        //                            Console.WriteLine("couldn't parse " + row[9].ToString());
        //                    string ign = row[10].ToString();
        //                    string server = row[11].ToString();

        //                    var entry = raffleEntries.FirstOrDefault(e => e.UserName == user);
        //                    if (entry == null)
        //                    {
        //                        entry = new RaffleEntry()
        //                        {
        //                            UserName = user,
        //                            cid = cid,
        //                            ign = ign,
        //                            server = server
        //                        };
        //                        raffleEntries.Add(entry);
        //                    }
        //                    if (entry.cid == 0)
        //                        entry.cid = cid;
        //                    if (string.IsNullOrEmpty(entry.ign))
        //                        entry.ign = ign;
        //                    if (string.IsNullOrEmpty(entry.server))
        //                        entry.server = server;
        //                    if (!string.IsNullOrEmpty(attachment1))
        //                        entry.Images.Add(attachment1);
        //                    entry.Entries.Add(messageId);
        //                }
        //                catch (Exception ex)
        //                {
        //                    //Console.WriteLine(ex.ToString());
        //                }
        //            }
        //            Console.WriteLine($"Gathered {raffleEntries.Count} for {tabName.Item1} out of {sheetData.Values.Count} rows");

        //            var currentRound = raffleEntries.Raffle();
        //            int rolls = tabName.Item2;
        //            int newRowIndex = 2;
        //            List<ValueRange> Data = new();
        //            int roll = 1;
        //            while (roll < rolls)
        //            {
        //                var entry = currentRound.First();
        //                currentRound.RemoveAt(0);

        //                if (currentRound.Count == 0)
        //                {
        //                    currentRound = raffleEntries.Raffle();
        //                }
        //                entry.WinnerRolls.Add(roll);
        //                roll++;

        //            }


        //            foreach (var entry in raffleEntries)
        //            {
        //                if (entry.WinnerRolls.Count > 0)
        //                {
        //                    ValueRange valueRange = new ValueRange()
        //                    {
        //                        Range = $"'{targetTabName}'!A{newRowIndex}",
        //                        Values = new[] {
        //                                        new object?[]
        //                                        {
        //                                            entry.UserName,
        //                                            entry.cid,
        //                                            entry.ign,
        //                                            entry.server,
        //                                            entry.Entries.Count,
        //                                            entry.WinnerRolls[0],
        //                                            entry.WinnerRolls.Count > 1 ? entry.WinnerRolls[1].ToString() : null,
        //                                            entry.WinnerRolls.Count > 2 ? entry.WinnerRolls[2].ToString() : null,
        //                                        }
        //                                    }
        //                    };
        //                    foreach (var e in entry.Images)
        //                        valueRange.Values[0] = valueRange.Values[0].ToList().Append(e).ToArray();
        //                    foreach (var e in entry.Images)
        //                        valueRange.Values[0] = valueRange.Values[0].ToList().Append("=IMAGE(\"" + e + "\")").ToArray();

        //                    Data.Add(valueRange);
        //                    newRowIndex++;

        //                }
        //            }

        //            sheetsService.Spreadsheets.Values.Clear(new ClearValuesRequest(), sheetId, $"'{targetTabName}'!A1:AG1000").Execute();


        //            Data = Data.OrderBy(row => row.Values[0][0].ToString()).ToList();


        //            sheetsService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
        //            {
        //                ValueInputOption = "USER_ENTERED",
        //                Data = Data,
        //            }, sheetId).Execute();


        //        }



        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine(ex);
        //        }



        //    }






        return;
/*
        var result = sheetService.Spreadsheets.Values.Get("1bH-29rNEopsKiZZbSAxmMiZh7XAEAdgfh2xcaeQ-sq4", "Members").Execute();
        var rowIndex = 2;
//        if(result.Values != null)
//            rowIndex = result.Values.Count + 1;

        var Data = new List<ValueRange>();

        await Console.Out.WriteLineAsync("Getting members");
        var guild = client.GetGuild(885126454545891398);
        var members = await guild.GetUsersAsync().FlattenAsync();
        await Console.Out.WriteLineAsync("Building data");


        foreach (var member in members)
        {
            string name = member.DisplayName;
            string playerGuild = "";
            string message = "OK";
            if (name.Contains(" - "))
            {
                name = name.Substring(name.IndexOf(" - ") + 3);
                playerGuild = member.DisplayName.Substring(0, member.DisplayName.IndexOf(" - "));
            }
            else
                message = "No - in name";

            string server = "";
            if (member.RoleIds.ToList().Contains(886995559418826823))
                server += "EU";
            if (member.RoleIds.ToList().Contains(886995485875904572))
                server += "NA-DP";
            if (member.RoleIds.ToList().Contains(886995111341338705))
                server += "NA-EL";

            string nameIngame = "";
            string guildIngame = "";
            string ingameId = "";

            if (server == "EU")
            {
                Console.WriteLine("Searching for " + name);

                HttpClient client = new HttpClient();

                string searchName = name;
                if(searchName.Contains("_"))
                    searchName = searchName.Substring(0, searchName.IndexOf("_"));

                if (searchName != "")
                {
                    var res = await client.PostAsync("http://romdiscord.borf.nl/api/searchchars/" + searchName, new StringContent(""));
                    var data = JsonSerializer.Deserialize<JsonObject>(res.Content.ReadAsStream());

                    if (data?["datas"] == null)
                        message = "Error: character not found at all";
                    else
                    {
                        foreach (var character in data["datas"].AsArray())
                        {
                            string charName = character["name"].GetValue<string>().TrimEnd(new char[] { '\u0002', '\u200b' });
                            if (charName == name)
                            {
                                nameIngame = charName;
                                ingameId = character["guid"].GetValue<string>();
                                if (character["guildname"] != null)
                                    guildIngame = character["guildname"].GetValue<string>().TrimEnd(new char[] { '\u0002', '\u200b' });
                                else
                                    message = "Character is not in a guild";

                                if (guildIngame != playerGuild)
                                    message = "Guild names don't match";
                            }
                        }
                    }
                }
                else
                    message = "Name starts with _, can not search";
            }

            ValueRange valueRange = new ValueRange()
            {
                Range = "Members!A" + rowIndex,
                Values = new[] {
                    new[]
                    {
                        "'" + member.Id + "",
                        member.Username + "#" + member.Discriminator,
                        member.Nickname,
                        member.DisplayName,
                        string.Join(",",member.RoleIds.Select(r => guild.GetRole(r).Name)),
                        server,
                        playerGuild,
                        name,
                        message,
                        guildIngame,
                        nameIngame,
                        ingameId,
                    }
                }
            };
            Data.Add(valueRange);
            rowIndex++;
        }

        var req = sheetService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
        {
            ValueInputOption = "USER_ENTERED",
            Data = Data,
        }, "1bH-29rNEopsKiZZbSAxmMiZh7XAEAdgfh2xcaeQ-sq4");
        req.Execute();
        Data.Clear();
*/











        ///image downloader

        //if (!Directory.Exists("img"))
        //	Directory.CreateDirectory("img");
        //var channel = client.GetGuild(248700646302285824).GetTextChannel(272407469144145920);
        //Console.WriteLine("Getting messages");
        //var messages = await channel.GetMessagesAsync(10000000).FlattenAsync();

        //using var log = new FileStream("img/log.txt", FileMode.Append);
        //foreach(var m in messages)
        //{
        //	if(m.Attachments.Count > 0)
        //	{
        //		string username = string.Join("_", m.Author.Username.Split(Path.GetInvalidFileNameChars())).Trim();
        //		username = username.Replace(".", "_");
        //		if (!Directory.Exists(Path.Combine("img", username)))
        //			Directory.CreateDirectory(Path.Combine("img", username));
        //		var httpClient = new HttpClient();
        //		foreach(var a in m.Attachments)
        //		{
        //			var filename = Path.Combine("img", username, m.Id + "_" + a.Filename);
        //			if (!File.Exists(filename))
        //			{
        //				Console.WriteLine("Downloading");
        //				var res = await httpClient.GetAsync(a.Url);
        //				using var fs = new FileStream(filename, FileMode.Create);
        //				res.Content.ReadAsStream().CopyTo(fs);
        //				log.Write(System.Text.Encoding.UTF8.GetBytes(filename + ",\t" + "https://discordapp.com/channels/248700646302285824/272407469144145920/" + m.Id + "\n"));
        //				log.Flush();
        //			}
        //		}
        //	}
        //}
        //log.Close();
        //Console.WriteLine(channel);
    };
}


CancellationTokenSource mainTokenSource = new();

var backgroundServices = services.GetServices<IBackgroundService>();
foreach (var backgroundService in backgroundServices)
    backgroundService.Start(mainTokenSource.Token);





Console.WriteLine("Waiting");
await Task.Delay(-1);



Task LogAsync(LogMessage message)
{ 
	Console.WriteLine(message.ToString());
	return Task.CompletedTask;
}


[DebuggerDisplay("RaffleEntry, UserName = {UserName}, Entries={Entries.Count}, WinnerRolls={WinnerRolls.Count}")]
public class RaffleEntry
{
    public string UserName { get; set; }
    public List<ulong> Entries { get; set; } = new List<ulong>();
    public List<string> Images { get; set; } = new List<string>();
    public ulong cid { get; set; }
    public string ign { get; set; }
    public string server { get; set; }
    public List<int> WinnerRolls { get; set; } = new();
    public List<string> WinnerEntries{ get; set; } = new();
}