using Discord;
using Discord.WebSocket;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Services
{
    public class FeedbackTracker
    {
        private string sheetId = "1cz-Q_u3mx0G_TUk3Uw6uR3ANmHj4eY7PlLyGYnoVrcM";
        private string sheetName = "Feedback Forums";

        private List<(ulong Id, string Server)> forums = new()
        {
            (1091282968934371409, "gvg"),
            (1091285908885622814, "job-balancing-adventure"),
            (1092893675215925318, "job-balancing-hero"),
            (1091287467216338994, "equipment-and-enchantment"),
            (1091290342231375892, "instance"),
            (1094814314424188958, "qol-and-content")
        };
        private DiscordSocketClient client;
        private SheetsService sheetService;

        public async Task Start(DiscordSocketClient client, SheetsService sheetService)
        {
            this.client = client;
            this.sheetService = sheetService;
            await Sync();

        }

        private async Task Sync()
        {
            var sheetData = sheetService.Spreadsheets.Values.Get(sheetId, sheetName).Execute();
            var guild = client.GetGuild(885126454545891398);
            int rowIndex = 2;
            List<ValueRange> Data = new();

            foreach (var forum in forums)
            {
                var discordForum = guild.GetForumChannel(forum.Id);
                if (discordForum == null)
                {
                    Console.WriteLine("Could not get forum for " + forum.Server);
                    continue;
                }
                Console.WriteLine("Getting forum " + forum.Server);
                foreach (var discordThread in (await discordForum.GetActiveThreadsAsync()).Concat(await discordForum.GetPublicArchivedThreadsAsync()))
                //foreach (var discordThread in await discordForum.GetActiveThreadsAsync())
                {
                    if (discordThread.ParentChannelId != forum.Id)
                        continue; // wtf?

                    Console.WriteLine(discordThread.Name);
                    var sheetIndex = -1;
                    if (sheetData.Values != null)
                        for (int i = 0; i < sheetData.Values.Count && sheetIndex == -1; i++)
                            if ((string)sheetData.Values[i][0] == "'" + discordThread.Id + "'")
                                sheetIndex = i;
                    if (sheetIndex == -1)
                    {
                        sheetIndex = rowIndex;
                        rowIndex++;
                    }

                    var messages = (await discordThread.GetMessagesAsync().FlattenAsync()).Reverse();
                    List<string> attachments = messages.First().Attachments.Select(a => a.Url).ToList();
                    if (attachments.Count > 4)
                        attachments.RemoveRange(4, attachments.Count - 4);
                    while (attachments.Count < 4)
                        attachments.Add("");


                    ValueRange valueRange = new ValueRange()
                    {
                        Range = $"'{sheetName}'!A{sheetIndex}",
                        Values = new[] {
                            new[]
                            {
                                "'" + discordThread.Id + "",
                                messages.First().Timestamp.ToString(),
                                discordForum.Name,
                                discordThread.Name,
                                discordThread.MemberCount + "",
                                discordThread.MessageCount + "",
                                messages.First().Author.Username + "#" + messages.First().Author.Discriminator,
                                messages.First().Content,
                                attachments[0],
                                attachments[1],
                                attachments[2],
                                attachments[3],
                                string.Join(", ", discordThread.AppliedTags.Select(t => discordForum.Tags.First(tt => tt.Id == t).Name))
                            }
                        }
                    };
                    Data.Add(valueRange);
                }
            }


            var req = sheetService.Spreadsheets.Values.BatchUpdate(new BatchUpdateValuesRequest()
            {
                ValueInputOption = "USER_ENTERED",
                Data = Data,
            }, sheetId);
            req.Execute();
            Data.Clear();
        }

        internal async Task ThreadCreated(SocketThreadChannel channel)
        {
            await Sync(); //lazy
        }

        internal async Task ThreadDeleted(SocketThreadChannel channel)
        {
            throw new NotImplementedException();
        }

        internal async Task ThreadUpdated(SocketThreadChannel channel)
        {
            await Sync(); //lazy
        }

    }
}
