using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Services;
public abstract class IBackgroundService
{
    protected CancellationToken token;
    protected Task? task;
    public void Start(CancellationToken token)
    {
        this.token = token;
        task = Task.Run(Run);
    }
    public async void Stop()
    {
        if (task != null)
            await task;
    }

    protected abstract Task Run();

}


public static class BackgroundServiceServiceCollectionExtension
{
    public static IServiceCollection AddBackgroundService<T>(this IServiceCollection serviceCollection) where T : IBackgroundService
    {
        serviceCollection.AddSingleton<T>();
        serviceCollection.AddSingleton<IBackgroundService>(i => i.GetRequiredService<T>());
        return serviceCollection;
    }
}