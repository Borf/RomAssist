using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant.db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Services;
public class VoiceChannelTrackerService : IBackgroundService
{
    DiscordSocketClient Discord;
    private IServiceProvider serviceProvider;

    public Dictionary<ulong, int> activeChannels { get; private set; } = new();
    public Dictionary<ulong, string> triggerWords { get; private set; } = new();

    public VoiceChannelTrackerService(DiscordSocketClient discord, IServiceProvider serviceProvider)
    {
        Discord = discord;
        this.serviceProvider = serviceProvider;
    }

    protected override Task Run()
    {
        {
            using var scope = serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            activeChannels = context.VoiceTrackerSessions.Where(v => v.Active).ToDictionary(v => v.Channel, v => v.Id);
        }

        Discord.UserVoiceStateUpdated += VoiceChanged;
        Discord.MessageReceived += MessageReceived;

        while (!token.IsCancellationRequested)
        {
            //not sure what to do here, I guess it's not a background service but a normal singleton

        }
        return Task.CompletedTask;
    }

    private async Task MessageReceived(SocketMessage message)
    {
        if(message.Channel.GetChannelType() == ChannelType.Stage && activeChannels.ContainsKey(message.Channel.Id) && triggerWords.ContainsKey(message.Channel.Id))
        {
            var channel = message.Channel as IStageChannel;
            Console.WriteLine("Got message in " + message.Channel.Name + ": " + message.Content);
            var sessionId = activeChannels[message.Channel.Id];
            using var scope = serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            if (message.Content.ToLower().Contains(triggerWords[message.Channel.Id]))
            {
                var msg = new VoiceTrackerMessage()
                {
                    DiscordMemberId = message.Author.Id,
                    SessionId = sessionId,
                    Message = message.Content
                };
                context.VoiceTrackerMessages.Add(msg);
                await context.SaveChangesAsync();
            }

        }
    }

    private async Task VoiceChanged(SocketUser user, SocketVoiceState stateOld, SocketVoiceState stateNew)
    {
        Console.WriteLine("User " + user.Username + " went from " + stateOld.VoiceChannel?.Name + " to " + stateNew.VoiceChannel?.Name);
        using var scope = serviceProvider.CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<Context>();

        //leaving a channel
        if (stateOld.VoiceChannel != null && activeChannels.ContainsKey(stateOld.VoiceChannel.Id))
        {
            var sessionId = activeChannels[stateOld.VoiceChannel.Id];
            var member = context.VoiceTrackerMembers.FirstOrDefault(m => m.SessionId == sessionId && m.Id == user.Id);
            var session = context.VoiceTrackerSessions.Find(sessionId)!;
            if (member == null)
            {
                member = new VoiceTrackerMember()
                {
                    Id = user.Id,
                    SessionId = sessionId,
                    LastJoinTime = session.StartTime,
                };
                context.VoiceTrackerMembers.Add(member);
                await context.SaveChangesAsync();
            }

            if (member.LastJoinTime == 0)
                Console.WriteLine("User left the voice channel, did not have a join time");
            Console.WriteLine("User " + user.Username + " left event, added " + (DateTimeOffset.Now.ToUnixTimeSeconds() - member.LastJoinTime) + " seconds");

            member.AccumulatedTime += DateTimeOffset.Now.ToUnixTimeSeconds() - member.LastJoinTime;
            member.LastJoinTime = 0;
            context.VoiceTrackerMembers.Update(member);
            await context.SaveChangesAsync();
        }


        if (stateNew.VoiceChannel != null && activeChannels.ContainsKey(stateNew.VoiceChannel.Id))
        {
            var sessionId = activeChannels[stateNew.VoiceChannel.Id];
            var member = context.VoiceTrackerMembers.FirstOrDefault(m => m.SessionId == sessionId && m.Id == user.Id);
            if(member == null)
            {
                member = new VoiceTrackerMember()
                {
                    Id = user.Id,
                    SessionId = sessionId
                };
                context.VoiceTrackerMembers.Add(member);
                await context.SaveChangesAsync();
            }

            if (member.LastJoinTime != 0)
                Console.WriteLine("User joined the voice channel, but already had a lastjointime! unsure if should add time to accumulatedtime");
            member.LastJoinTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            Console.WriteLine("User " + user.Username + " joined event");

            context.VoiceTrackerMembers.Update(member);
            await context.SaveChangesAsync();
        }

    }

    public async void StopTracking(ulong id)
    {
        {
            var sessionId = activeChannels[id];
            using var scope = serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            var session = context.VoiceTrackerSessions.Find(sessionId)!;
            session.Active = false;
            context.VoiceTrackerSessions.Update(session);
            await context.SaveChangesAsync();

            var members = context.VoiceTrackerMembers.Where(m => m.SessionId == sessionId && m.LastJoinTime != 0);
            foreach (var member in members)
            {
                member.AccumulatedTime += DateTimeOffset.Now.ToUnixTimeSeconds() - member.LastJoinTime;
                member.LastJoinTime = 0;
                context.VoiceTrackerMembers.Update(member);
            }
            await context.SaveChangesAsync();
        }
        this.activeChannels.Remove(id);
    }
}
