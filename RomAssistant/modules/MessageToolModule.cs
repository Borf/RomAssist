﻿using CsvHelper;
using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;
public class MessageToolModule : InteractionModuleBase<SocketInteractionContext>
{
    [DoAdminCheck]
    [MessageCommand("Extract Message")]
    public async Task ExtractMessage(IMessage message)
    {
        // make a safety cast to check if the message is ISystem- or IUserMessage
        if (message is IUserMessage userMessage)
        {

            var msg2 = await message.Channel.GetMessageAsync(message.Id);

            string data = "";
            data += $"Id: {msg2.Id}\n";
            data += $"Author: {msg2.Author.Id}\n";
            data += $"Content: {msg2.Content}\n";
            data += $"Embeds: {msg2.Embeds.Count}\n";
            data += $"Attachments: {msg2.Attachments.Count}\n";
            data += $"Reactions: {msg2.Reactions.Count}\n";

            var records = new List<object>();
            foreach (var reaction in msg2.Reactions)
            {
                var reactions = await msg2.GetReactionUsersAsync(reaction.Key, 9999).FlattenAsync();
                foreach(var r in reactions)
                {
                    records.Add(new
                    {
                        Emoji = reaction.Key.Name,
                        r.Id,
                        r.Username
                    });
                }
            }

            string data2 = "";
            using (var writer = new StringWriter())
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
                csv.Flush();
                writer.Flush();
                data2 = writer.ToString();
            }
            


            await RespondWithFilesAsync(
                new List<FileAttachment>()
                {
                    new FileAttachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data)), message.Id + "metadata.txt"),
                    new FileAttachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data2)), message.Id + "reactions.csv")
                },
                "Message information:", ephemeral: true);
        }
        else
            await RespondAsync(text: ":x: You cant pin system messages!", ephemeral: true);
    }
}