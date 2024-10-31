using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using RomAssistant.db;
using RomAssistant.modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.Services;
public class ModuleInvoker
{
    private IServiceProvider services;

    public ModuleInvoker(IServiceProvider services)
    {
        this.services = services;
    }

    public T ModuleScoped<T>(SocketInteractionContext context, params object[] properties)
    {
        var name = typeof(T).Name;

        //TODO: cache this please
        var assembly = Assembly.GetEntryAssembly()!;
        var type = assembly.GetTypes().First(type => type.Name == name);

        var parameters = type.GetConstructors().Where(c => !c.IsStatic).First().GetParameters();
        var args = parameters.Select(p => services.GetService(p.ParameterType)).ToArray();
        T handler = (T)Activator.CreateInstance(type, args);

        if (type.GetMember("SetContext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod) != null)
        {
            //var context = properties.FirstOrDefault(p => p.GetType().IsAssignableTo(typeof(SocketInteractionContext)));
            type.InvokeMember("SetContext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.InvokeMethod, null, handler, new object?[] { context });
        }

        if (type.GetField("context") != null)
            type.GetField("context").SetValue(handler, services.GetRequiredService<Context>());

        return handler;
    }

}
