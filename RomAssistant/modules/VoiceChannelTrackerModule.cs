using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using RomAssistant.db;
using RomAssistant.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;
public class VoiceChannelTrackerModule : InteractionModuleBase<SocketInteractionContext>
{
    VoiceChannelTrackerService voiceTracker;
    Context context;

    public VoiceChannelTrackerModule(VoiceChannelTrackerService voiceTracker, Context context)
    {
        this.voiceTracker = voiceTracker;
        this.context = context;
    }

    [SlashCommand("trackevent", "Tracks event")]
    public async Task TrackEvent(string title)
    {
        var type = Context.Channel.GetChannelType();
        if (Context.Channel.GetChannelType() != ChannelType.Stage)
        {
            await RespondAsync("This only works on stage channels", ephemeral: true);
            return;
        }
        if(voiceTracker.activeChannels.ContainsKey(Context.Channel.Id))
        {
            await RespondAsync("Channel is already tracked", ephemeral: true);
            return;
        }

        var session = new VoiceTrackerSession()
        {
            Channel = Context.Channel.Id,
            Title = title,
            Active = true,
        };
        context.VoiceTrackerSessions.Add(session);
        await context.SaveChangesAsync();
        voiceTracker.activeChannels[Context.Channel.Id] = session.Id;

        var channel = Context.Channel as SocketStageChannel;
        var currentUsers = channel.ConnectedUsers;
        Console.WriteLine("Current users: " + string.Join(", ", currentUsers.Select(u => u.Username)));
        foreach(var user in currentUsers)
        {
            context.VoiceTrackerMembers.Add(new VoiceTrackerMember()
            {
                LastJoinTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                SessionId = session.Id,
                Id = user.Id,
            });
        }
        await context.SaveChangesAsync();

        await RespondAsync("Channel is actively logged now", ephemeral: true);
    }

    [SlashCommand("stoptrackevent", "Stops tracking and sends results")]
    public async Task StopTrackEvent()
    {
        if (Context.Channel.GetChannelType() != ChannelType.Stage)
        {
            await RespondAsync("This only works on stage channels", ephemeral: true);
            return;
        }
        voiceTracker.StopTracking(Context.Channel.Id);
        await RespondAsync("Stopped tracking", ephemeral: true);
    }

    [SlashCommand("triggerword", "Sets a trigger word")]
    public async Task TriggerWord(string word = "", int time = 5)
    {
        word = word.ToLower();
        if (Context.Channel.GetChannelType() != ChannelType.Stage)
        {
            await RespondAsync("This only works on stage channels", ephemeral: true);
            return;
        }
        if (!voiceTracker.activeChannels.ContainsKey(Context.Channel.Id))
        {
            await RespondAsync("Channel is not tracked yet", ephemeral: true);
            return;
        }
        var session = context.VoiceTrackerSessions.First(s => s.Active && s.Channel == Context.Channel.Id);
        session.TriggerWord = word;
        context.VoiceTrackerSessions.Update(session);
        await context.SaveChangesAsync();

        if (string.IsNullOrEmpty(word))
        {
            voiceTracker.triggerWords.Remove(Context.Channel.Id);
            await RespondAsync("Removed trigger word", ephemeral: true);
        }
        else
        {
            voiceTracker.triggerWords[Context.Channel.Id] = (Word: word, EndTime: DateTimeOffset.Now + TimeSpan.FromMinutes(time));
            await RespondAsync("Set trigger word to " + word, ephemeral: true);
        }
    }


    [SlashCommand("lastevent", "Results from last event")]
    public async Task LastEvent()
    {
        if (Context.Channel.GetChannelType() != ChannelType.Stage)
        {
            await RespondAsync("This only works on stage channels", ephemeral: true);
            return;
        }
        
        var session = context.VoiceTrackerSessions.Include(c => c.Members).Where(c => c.Channel == Context.Channel.Id).OrderByDescending(c => c.StartTime).FirstOrDefault();
        if(session == null) {
            await RespondAsync("No session", ephemeral: true);
            return;
        }

        string msg = "Tracked time:\n";
        foreach (var m in session.Members)
        {
            var user = Context.Guild.GetUser(m.Id);
            msg += $"<@{m.Id}>,";
            msg += $"{m.Id},";
            msg += $"{user?.Username},";
            msg += $"{user?.Discriminator},";
            msg += $"{user?.Nickname},";
            msg += $"{m.AccumulatedTime},";
            foreach(var trackedMessage in context.VoiceTrackerMessages.Where(mm => mm.SessionId == session.Id && mm.DiscordMemberId == m.Id))
            {
                msg += $"{DateTimeOffset.FromUnixTimeSeconds(trackedMessage.Time)},\"{trackedMessage.Message.Replace("\"", "\\\"")}\",";
            }
            msg += "\n";
        }

        if (msg.Length < 1500)
            await RespondAsync(msg, ephemeral: true);
        else
        {
            await RespondWithFileAsync(new FileAttachment(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(msg)), "results.txt"), "Results", ephemeral: true);
        }

    }
}
