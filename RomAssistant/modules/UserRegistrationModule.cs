using Discord;
using Discord.Interactions;
using RomAssistant.db;
using RomAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;
public class UserRegistrationModule : InteractionModuleBase<SocketInteractionContext>
{
    private Context context;

    public UserRegistrationModule(Context context)
    {
        this.context = context;
    }

    [DoAdminCheck]
    [SlashCommand("createuserregistration", "creates user message")]
    public async Task CreateMessage()
    {
        await this.RespondAsync("Creating...", ephemeral: true);

        await Context.Channel.SendMessageAsync(null, components: new ComponentBuilder()
            .WithButton("Link your discord to your game account", "userregistration_startlink:false")
            .Build());

        await this.ModifyOriginalResponseAsync(m => m.Content = "Done");
    }

    [ComponentInteraction("userregistration_startlink:*")]
    public async Task StartLink(bool overrideMessage)
    {
        var user = context.Users.Find(Context.User.Id);
        if (user != null && user.CharacterId != 0)
        {
            if (!overrideMessage)
                await RespondAsync("Loading...", ephemeral: true);
        }
        if (user == null)
            await ChangeServer(overrideMessage);
        else if (user.CharacterId == 0)
            await RespondWithModalAsync<CidModal>("userregistration_registercid:" + overrideMessage);
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
                .WithButton("Change Server", "userregistration_changeserver")
                .WithButton("Change Character ID", "userregistration_changecid")
                .Build();
            });
        }
    }

    [ComponentInteraction("userregistration_changeserver")]
    public async Task ChangeServer()
    {
        await ChangeServer(false);
    }
    public async Task ChangeServer(bool overrideMessage)
    {
        if (overrideMessage)
            await DeferAsync(ephemeral: true);
        else
            await RespondAsync("Loading...", ephemeral: true);
        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "# What server are you playing on?";
            m.Components = new ComponentBuilder()
                .WithButton("SEA: Eternal Love", "userregistration_registerserver:SEAEL", emote: Emote.Parse("<:serverELSEA:1115300738709594123>"), row: 0)
                .WithButton("SEA: Midnight Party", "userregistration_registerserver:SEAMP", emote: Emote.Parse("<:serverMPSEA:1115300733907111977>"), row: 0)
                .WithButton("SEA: Memory of Faith", "userregistration_registerserver:SEAMOF", emote: Emote.Parse("<:serverMOFSEA:1115300730497138720>"), row: 0)
                .WithButton("SEA: Valhalla Glory", "userregistration_registerserver:SEAVG", emote: Emote.Parse("<:serverVGSEA:1115300724667072582>"), row: 0)
                .WithButton("SEA: Port City", "userregistration_registerserver:SEAPC", emote: Emote.Parse("<:serverEU:1115300750067769456>"), row: 0)
                .WithButton("Global: Eternal Love", "userregistration_registerserver:NAEL", emote: Emote.Parse("<:serverELGlobal:1115300746754277549>"), row: 1)
                .WithButton("Global: DP", "userregistration_registerserver:NADP", emote: Emote.Parse("<:serverDPGlobal:1115300743340114010>"), row: 1)
                .WithButton("EU: Eternal Love", "userregistration_registerserver:EUEL", emote: Emote.Parse("<:serverEU:1115300750067769456>"), row: 2)
                .Build();
        });
    }

    [ComponentInteraction("userregistration_changecid")]
    public async Task ChangeCid()
    {
        await RespondWithModalAsync<CidModal>("userregistration_registercid:true");
    }


    [ComponentInteraction("userregistration_registerserver:*")]
    public async Task RegisterServer(Server server)
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
        await StartLink(true);
    }

    [ModalInteraction("userregistration_registercid:*")]
    public async Task RegisterCid(bool overrideMessage, CidModal data)
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
            user.CharacterId = cid;
            user.CharacterName = string.Empty;
            user.AccountId = 0;
            user.Guild = string.Empty;
            user.LastCheckTime = null;
            user.LastUpdateTime = null;
            await context.SaveChangesAsync();
            await StartLink(true);
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
