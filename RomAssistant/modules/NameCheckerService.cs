using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant;
using RomAssistant.db;
using RomAssistant.modules;
using RomAssistant.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace KafraAdmin.Services;

public class NameCheckerService : IBackgroundService
{
    private IServiceProvider serviceProvider;
    public NameCheckerService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }


    static TimeSpan rescanTimeout = TimeSpan.FromHours(1);

    protected override async Task Run()
    {
        await Task.Delay(TimeSpan.FromSeconds(5));
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<Context>();

                var toCheck = context.Users.Where(user => user.CharacterId != 0).ToList().Where(user => (string.IsNullOrEmpty(user.CharacterName) || user.LastCheckTime == null || (DateTimeOffset.Now - user.LastCheckTime.Value) > rescanTimeout)).ToList();
                Program.Log(Module.NameChecker, $"Checking {toCheck.Count} players");

                foreach (var user in toCheck)
                {
                    string apiUrl = "http://romapi.borf.nl/" + user.Server.ToString() + "/character/get/" + user.CharacterId;
                    try
                    {
                        string reason = "";
                        if (user.LastCheckTime == null)
                            reason = "has not been checked yet";
                        else if ((DateTimeOffset.Now - user.LastCheckTime.Value) > rescanTimeout)
                            reason = "has not been scanned for " + (DateTimeOffset.Now - user.LastCheckTime.Value);
                        else if (string.IsNullOrEmpty(user.CharacterName))
                            reason = "no ingame name found yet";
                        else
                            reason = "Unknown reason, lastchecktime is " + user.LastCheckTime;

                        Program.Log(Module.NameChecker, $"Checking player <@{user.DiscordId}>, server {user.Server}, CID {user.CharacterId}, IGN: {user.CharacterName}, {reason}");
                        string info = await new HttpClient().GetStringAsync(apiUrl);
                        JsonObject charInfo = JsonSerializer.Deserialize<JsonObject>(info) ?? throw new Exception("Could not get character json");
                        if (string.IsNullOrEmpty(info) || charInfo == null || !charInfo.ContainsKey("Name") || string.IsNullOrEmpty(charInfo?["Name"]?.GetValue<string>()))
                        {
                            Program.Log(Module.NameChecker, "Error, could not find user with cid " + user.CharacterId);
                            if (user.LastCheckTime == null) //first check
                            {
                            }
                            user.AccountId = 0;
                            user.LastCheckTime = DateTimeOffset.Now;
                            await context.SaveChangesAsync();
                            continue;
                        }

                        if (charInfo.ContainsKey("Name"))
                        {
                            user.CharacterName = charInfo?["Name"]?.ToString() ?? "";
                            user.Guild = charInfo?["GuildName"]?.ToString() ?? "";
                            if (long.TryParse(charInfo?["LastScanTimeStamp"]?.ToString(), out long lastScanTimeStamp))
                                user.LastUpdateTime = DateTimeOffset.FromUnixTimeSeconds(lastScanTimeStamp);
                            user.LastCheckTime = DateTimeOffset.Now.AddMinutes(Random.Shared.NextDouble() * 10);
                            await context.SaveChangesAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.Log(Module.NameChecker, $"Error: {ex.Message} while checking name for <@{user.DiscordId}>, server {user.Server}, CID {user.CharacterId}, IGN: {user.CharacterName}\n{apiUrl}");
                    }
                }
                await context.SaveChangesAsync();
            }catch(Exception ex)
            {
                Program.Log(Module.NameChecker, $"Error: {ex.Message}");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), token);
        }
    }
}
