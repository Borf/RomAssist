using CsvHelper;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant.db;
using RomAssistant.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;
public class MessageToolModule : InteractionModuleBase<SocketInteractionContext>
{
    private Context context;
    private IServiceProvider services;
    private InteractionService handler;

    public MessageToolModule(Context context, IServiceProvider services, InteractionService handler)
    {
        this.context = context;
        this.services = services;
        this.handler = handler;
    }

    [DoAdminCheck]
    [MessageCommand("Extract Message")]
    public async Task ExtractMessage(IMessage message)
    {
        await DeferAsync(ephemeral: true);
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
                foreach (var r in reactions)
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



            await ModifyOriginalResponseAsync(m =>
            {
                m.Attachments = new List<FileAttachment>()
                {
                    new FileAttachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data)), message.Id + "metadata.txt"),
                    new FileAttachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data2)), message.Id + "reactions.csv")
                };
                m.Content = "Message information:";
            });
        }
        else
            await ModifyOriginalResponseAsync(m => m.Content = "Error");
    }


    [DoAdminCheck]
    [MessageCommand("Admin Message")]
    public async Task AdminMessage(IMessage message)
    {
        var raffle = context.Raffles.FirstOrDefault(r => r.DiscordMessageId == message.Id);
        if (raffle != null)
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ModuleInvoker>().ModuleScoped<RaffleModule>(Context).HandleAdmin(message);
        }
    }
}
