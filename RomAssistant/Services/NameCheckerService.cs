using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant.db;
using RomAssistant.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace IMPTShuffleBot.services;

public class NameCheckerService : IBackgroundService
{
    private IServiceProvider serviceProvider;
    public NameCheckerService(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }


    protected override async Task Run()
    {
        while(!token.IsCancellationRequested)
        {
            using var scope = serviceProvider.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            foreach(var player in context.Users.ToList())
            {
                if (player.CharacterId != 0 && string.IsNullOrEmpty(player.CharacterName))
                {
                    try
                    {
                        using var scope2 = serviceProvider.CreateScope();
                        using var context2 = scope2.ServiceProvider.GetRequiredService<Context>();

                        context2.Attach(player);

                        Console.WriteLine("[NameChecker] Checking player " + player.CharacterId);
                        string info = await new HttpClient().GetStringAsync("http://romapi.borf.nl/" + player.Server.ToString() + "/character/get/" + player.CharacterId);
                        if (string.IsNullOrEmpty(info))
                            continue;
                        JsonObject charInfo = JsonSerializer.Deserialize<JsonObject>(info) ?? throw new Exception("Could not get character json");
                        if (charInfo.ContainsKey("Name"))
                        {
                            player.CharacterName = charInfo?["Name"]?.ToString() ?? "";
                            player.Guild = charInfo?["GuildName"]?.ToString() ?? "";
                            player.AccountId = ulong.Parse(charInfo?["AccountId"]?.ToString() ?? "0");
                            await context2.SaveChangesAsync();
                        }
                    }catch(Exception ex)
                    {
                        Console.WriteLine("[NameChecker] " + ex.ToString());
                    }
                }
            }


            await Task.Delay(TimeSpan.FromMinutes(1), this.token);
        }
    }

}
