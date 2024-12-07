using Discord;
using Discord.Interactions;
using RomAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;
[DoAdminCheck]
public class AdminModule : InteractionModuleBase<SocketInteractionContext>
{
    private Config _config;

    public AdminModule(Config config)
    {
        _config = config;
    }

    [SlashCommand("admin", "Shows admin buttons")]
    public async Task Admin()
    {
        await RespondAsync("# Admin Panel", ephemeral: true,
            components: new ComponentBuilder()
                .WithButton("Registration Log Channel", "setreglogchannel")
            .Build()
            );
    }


    [ComponentInteraction("setreglogchannel")]
    public async Task SetRegLogChannel()
    {
        var channel = Context.Guild.GetTextChannel(_config.RegLogChannel);
        await RespondAsync("Please pick the log channel for the registration log", ephemeral: true, components: new ComponentBuilder()
            .WithSelectMenu(
                new SelectMenuBuilder()
                    .WithType(ComponentType.ChannelSelect)
                    .WithCustomId("dosetreglogchannel")
                    .WithMinValues(1)
                    .WithMaxValues(1)
                    .WithPlaceholder(channel != null ? ("#"+channel.Name) : "Select channel")
            ).Build()
            );
    }
    [ComponentInteraction("dosetreglogchannel")]
    public async Task DoSetRegLogChannel(string[] channels)
    {
        await DeferAsync();
        _config.RegLogChannel = ulong.Parse(channels[0]);
        await DeleteOriginalResponseAsync();
    }

}
