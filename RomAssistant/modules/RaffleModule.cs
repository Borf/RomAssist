using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
using RomAssistant.db;
using RomAssistant.Models;
using RomAssistant.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace RomAssistant.modules;
public class RaffleModule : InteractionModuleBase<SocketInteractionContext>
{
    public Context context;
    public IServiceProvider services;

    public RaffleModule(Context context, IServiceProvider services)
    {
        this.context = context;
        this.services = services;
    }

    [SlashCommand("newraffle", "Starts a new raffle")]
    public async Task NewRaffle()
    {
        await DeferAsync(ephemeral: true);
        var message = await Context.Channel.SendMessageAsync("# To answer, please click the button below", components:
            new ComponentBuilder()
            .WithButton("Enter your answer", "raffle_enter", ButtonStyle.Primary)
            .Build()
            );

        var raffle = new Raffle()
        {
            DiscordChannelid = Context.Channel.Id,
            DiscordMessageId = message.Id,
            DiscordServerId = Context.Guild.Id,
            Type = RaffleType.Answer,
            Counts = $"{Server.EU}:150|{Server.NA}:120|{Server.SEA}:500",
        };
        context.Raffles.Add(raffle);
        await context.SaveChangesAsync();
        await ModifyOriginalResponseAsync(m => m.Content = "Message is created");
        await UpdateMessage(raffle.Id);
    }

    private async Task UpdateMessage(int id)
    {
        var raffle = context.Raffles.Find(id) ?? throw new Exception("Raffle not found");
        var channel = Context.Guild.GetTextChannel(raffle.DiscordChannelid);
        var msg = await channel.GetMessageAsync(raffle.DiscordMessageId);

        string message = $"# To answer, please click the button below\n" +
            $"\n" +
            $"## Number of winners:\n";
        foreach(var count in raffle.RaffleCount)
            message += $"- {count.Key}: {count.Value}\n";
        message += $"\n" +
                   $"Number of Entries\n";

        var counts = context.RaffleAnswers.Where(r => r.RaffleId == raffle.Id).Include(r => r.User).ToList().CountBy(r => r.User.Server.BaseServer());
        foreach (var server in new[] { Server.EU, Server.NA, Server.SEA })
        {
            var count = counts.FirstOrDefault(kv => kv.Key == server).Value;
            message += $"- {server}: {count} {(count == 1 ? "entry" : "entries")}\n";
        }

        await channel.ModifyMessageAsync(raffle.DiscordMessageId, m => { 
            m.Content = message;
            m.Components = new ComponentBuilder()
                .WithButton("Enter your answer", "raffle_enter:" + raffle.Id, ButtonStyle.Primary)
                .Build();

        });
    }

    [ComponentInteraction("raffle_enter:*")]
    public async Task Enter(int raffleId)
    {
        var raffle = context.Raffles.Find(raffleId);
        var user = context.Users.Find(Context.User.Id);
        if(user == null || user.CharacterId == 0)
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ModuleInvoker>().ModuleScoped<UserRegistrationModule>(Context).StartLink(false, "raffle_" + raffleId);
            return;
        }
        if (context.RaffleAnswers.Any(r => r.RaffleId == raffleId && r.DiscordUserId == Context.User.Id))
        {
            await RespondAsync("You have already answered. The first answer counts!", ephemeral: true);
            return;
        }

        await RespondWithModalAsync<AnswerModal>("raffle_doenter:" + raffleId);
    }

    [ModalInteraction("raffle_doenter:*")]
    public async Task DoEnter(int raffleId, AnswerModal answer)
    {
        var raffle = context.Raffles.Find(raffleId);
        var user = context.Users.Find(Context.User.Id);
        if (user == null)
        {
            await RespondAsync("You have not registered your ingame character yet. Please register at <link>", ephemeral: true);
            return;
        }

        if(context.RaffleAnswers.Any(r => r.RaffleId == raffleId && r.DiscordUserId == Context.User.Id))
        {
            await RespondAsync("You have already answered. The first answer counts!", ephemeral: true);
            return;
        }

        context.RaffleAnswers.Add(new RaffleAnswer()
        {
            Answer = answer.Answer,
            DiscordUserId = Context.User.Id,
            RaffleId = raffleId,
            Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
        });
        await context.SaveChangesAsync();

        await RespondAsync("Thank you for answering!", ephemeral: true);
        await UpdateMessage(raffleId);
    }


    public async Task HandleAdmin(IMessage message)
    {
        var cb = new ComponentBuilder();

        var raffle = context.Raffles.First(r => r.DiscordMessageId == message.Id);       
        foreach(var server in new []{ Server.EU, Server.NA, Server.SEA })
        {
            cb.WithButton("Change winner counts for " + server, "raffle_admin_changecount:" + raffle.Id + ":" + server);
        }
        cb.WithButton("Change answer", "raffle_admin_changeregex:" + raffle.Id);
        if(raffle.Opened)
            cb.WithButton("Close", "raffle_admin_setopened:" + raffle.Id + "0", ButtonStyle.Danger);
        else
            cb.WithButton("Open", "raffle_admin_setopened:" + raffle.Id + "1", ButtonStyle.Danger);

        await RespondAsync("# Admin options for raffle: ", ephemeral: true, components: cb.Build());
    }

    [ComponentInteraction("raffle_admin_changecount:*:*")]
    public async Task ChangeCount(int raffleId, Server server)
    {
        var raffle = context.Raffles.Find(raffleId) ?? throw new Exception("Raffle not found");
        AmountModal m = new AmountModal();
        m.Amount = raffle.RaffleCount[server.ToString()].ToString();
        await RespondWithModalAsync<AmountModal>($"raffle_admin_dochangecount:{raffleId}:{server}", m);
    }

    [ModalInteraction("raffle_admin_dochangecount:*:*")]
    public async Task DoChangeCount(int raffleId, Server server, AmountModal amount)
    {
        var raffle = context.Raffles.Find(raffleId) ?? throw new Exception("Raffle not found");
        if (int.TryParse(amount.Amount, out int count))
        {
            await DeferAsync(ephemeral: true);
            raffle.RaffleCount[server.ToString()] = count;
            await context.SaveChangesAsync();
            await UpdateMessage(raffleId);
        }
        else
            await RespondAsync($"Error: {amount.Amount} is not a number", ephemeral: true);
    }


    public class AmountModal : IModal
    {
        public string Title => "Please enter the amount";

        [ModalTextInput("amount", TextInputStyle.Short, placeholder: "100", 1, 10)]
        [InputLabel("The amount")]
        public string Amount { get; set; } = "100";
    }

    public class AnswerModal : IModal
    {
        public string Title => "What's the answer?";

        [ModalTextInput("answer", TextInputStyle.Short, placeholder: "Your answer", 1, 100)]
        [InputLabel("Your answer")]
        public string Answer { get; set; } = string.Empty;
    }

}
