using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant.db;
using RomAssistant.Models;
using RomAssistant.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;
public class UserRegistrationModule : InteractionModuleBase<SocketInteractionContext>
{
    private Context context;
    public IServiceProvider services;
    private Config _config;

    public UserRegistrationModule(Context context, IServiceProvider services, Config config)
    {
        this.context = context;
        this.services = services;
        _config = config;
    }

    [DoAdminCheck]
    [SlashCommand("createuserregistration", "creates user message")]
    public async Task CreateMessage()
    {
        await this.RespondAsync("Creating...", ephemeral: true);

        await Context.Channel.SendMessageAsync(null, components: new ComponentBuilder()
            .WithButton("Link your discord to your game account", "userregistration_startlink:false:x")
            .Build());

        await this.ModifyOriginalResponseAsync(m => m.Content = "Done");
    }

    [ComponentInteraction("userregistration_startlink:*:*")]
    public async Task StartLink(bool overrideMessage, string referer)
    {
        var user = context.Users.Find(Context.User.Id);
        if (user != null && user.CharacterId != 0)
        {
            if (!overrideMessage)
                await RespondAsync("Loading...", ephemeral: true);
        }
        if (user == null)
            await ChangeServer(overrideMessage, referer);
        else if (user.CharacterId == 0)
            await RespondWithModalAsync<CidModal>("userregistration_registercid:" + overrideMessage + ":" + referer);
        else
        {
            if (referer.StartsWith("raffle_"))
            {
                var raffle = context.Raffles.Find(int.Parse(referer[7..])) ?? throw new Exception("Raffle not found");
                if (overrideMessage)
                    await DeferAsync(ephemeral: true);
                await ModifyOriginalResponseAsync(m =>
                {
                    m.Content = $"# You are registered with:\n" +
                        $"- Server: {user.Server.FullString()}\n" +
                        $"- Character ID: {user.CharacterId}\n" +
                        $"\nThis is a one-time process, you only have to do this the first time.\n\n" +
                        $"Please click the button on https://discord.com/channels/{raffle.DiscordServerId}/{raffle.DiscordChannelid}/{raffle.DiscordMessageId} to enter your answer";
                    m.Components = new ComponentBuilder()
                    .Build();
                });
            }
            else
            {
                if (overrideMessage)
                    await DeferAsync(ephemeral: true);
                await ModifyOriginalResponseAsync(m =>
                {
                    m.Content = $"# You are registered with:\n" +
                        $"- Server: {user.Server.FullString()}\n" +
                        $"- Character ID: {user.CharacterId}\n";
                    m.Components = new ComponentBuilder()
                    .WithButton("Change Server", "userregistration_changeserver:x")
                    .WithButton("Change Character ID", "userregistration_changecid:x")
                    .Build();
                });
            }
        }
    }

    [ComponentInteraction("userregistration_changeserver:*")]
    public async Task ChangeServer(string referer)
    {
        await ChangeServer(false, referer);
    }
    public async Task ChangeServer(bool overrideMessage, string referer)
    {
        if (overrideMessage)
            await DeferAsync(ephemeral: true);
        else
            await RespondAsync("Loading...", ephemeral: true);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "# What server are you playing on?";
            if (referer.StartsWith("raffle_"))
                m.Content = "# Please register your ingame account\n" +
                "Before entering the raffle, we would like you to register your ingame account. This registration will be saved, so you will only have to do this once. " +
                "You can always change your character ID later.\n" +
                "## First of all, select the server you are playing on";


            m.Components = new ComponentBuilder()
                .WithButton("SEA: Eternal Love", "userregistration_registerserver:SEAEL:"+referer, emote: Emote.Parse("<:serverELSEA:1115300738709594123>"), row: 0)
                .WithButton("SEA: Midnight Party", "userregistration_registerserver:SEAMP:"+referer, emote: Emote.Parse("<:serverMPSEA:1115300733907111977>"), row: 0)
                .WithButton("SEA: Memory of Faith", "userregistration_registerserver:SEAMOF:"+referer, emote: Emote.Parse("<:serverMOFSEA:1115300730497138720>"), row: 0)
                .WithButton("SEA: Valhalla Glory", "userregistration_registerserver:SEAVG:"+referer, emote: Emote.Parse("<:serverVGSEA:1115300724667072582>"), row: 0)
                .WithButton("SEA: Port City", "userregistration_registerserver:SEAPC:" + referer, emote: Emote.Parse("<:serverEU:1115300750067769456>"), row: 0)
                .WithButton("Global: Eternal Love", "userregistration_registerserver:NAEL:" + referer, emote: Emote.Parse("<:serverELGlobal:1115300746754277549>"), row: 1)
                .WithButton("Global: DP", "userregistration_registerserver:NADP:" + referer, emote: Emote.Parse("<:serverDPGlobal:1115300743340114010>"), row: 1)
                .WithButton("EU: Eternal Love", "userregistration_registerserver:EUEL:" + referer, emote: Emote.Parse("<:serverEU:1115300750067769456>"), row: 2)
                .Build();
        });
    }

    [ComponentInteraction("userregistration_changecid:*")]
    public async Task ChangeCid(string referer)
    {
        await RespondWithModalAsync<CidModal>("userregistration_registercid:true:"+referer);
    }


    [ComponentInteraction("userregistration_registerserver:*:*")]
    public async Task RegisterServer(Server server, string referer)
    {
        var user = context.Users.Find(Context.User.Id);
        if(user == null)
            context.Users.Add(user = new User() { DiscordId = Context.User.Id });
        user.Server = server;
        user.CharacterId = 0;
        user.CharacterName = string.Empty;
        user.AccountId = 0;
        user.Guild = string.Empty;
        user.LastCheckTime = null;
        user.LastUpdateTime = null;
        await context.SaveChangesAsync();
        await StartLink(true, referer);
    }

    [ModalInteraction("userregistration_registercid:*:*")]
    public async Task RegisterCid(bool overrideMessage, string referer, CidModal data)
    {
        var user = context.Users.Find(Context.User.Id);
        if (user == null)
        {
            if (!overrideMessage)
                await RespondAsync("Loading...", ephemeral: true);
            else
                await DeferAsync(ephemeral: true);
            await ModifyOriginalResponseAsync(m => m.Content = "# Register your server first");
        }
        else if (ulong.TryParse(data.Cid, out ulong cid))
        {
            if (user.CharacterId == 0 && user.LastUpdateTime == null)
                await Context.Guild.GetTextChannel(_config.RegLogChannel).SendMessageAsync($"## User <@!{Context.User.Id}> finished linking their character.\nCharacter ID {cid} on {user.Server.FullString()}");
            else
                await Context.Guild.GetTextChannel(_config.RegLogChannel).SendMessageAsync($"## User <@!{Context.User.Id}> changed their character ID.\nCharacter ID changed from {user.CharacterId} to {cid} on {user.Server.FullString()}");
            user.CharacterId = cid;
            user.CharacterName = string.Empty;
            user.AccountId = 0;
            user.Guild = string.Empty;
            user.LastCheckTime = null;
            user.LastUpdateTime = null;
            await context.SaveChangesAsync();
           

            await StartLink(overrideMessage, referer);
        }
        else
        {
            if (!overrideMessage)
                await RespondAsync("Loading...", ephemeral: true);
            else
                await DeferAsync(ephemeral: true);
            await ModifyOriginalResponseAsync(m => m.Content = $"# Please enter a correct CID.\n{data.Cid} is not a valid Character ID. You can find your Character ID by clicking on your profile photo and copying the ID there");
        }
    }

}


public class CidModal : IModal
{
    public string Title => "Please enter your character id";

    [ModalTextInput("cid", TextInputStyle.Short, placeholder: "43xxxxxxxx", 10, 10)]
    [InputLabel("Your Character ID (cid)")]
    public string Cid { get; set; } = string.Empty;
}
