using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace TicketBot.attributes
{
	internal class DoAdminCheck : PreconditionAttribute
	{
		public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context, ICommandInfo commandInfo, IServiceProvider services)
		{
			if (context.User is SocketGuildUser guildUser)
			{
				if (guildUser.Roles.Any(gr => gr.Id == 933634203298443275 || gr.Id == 885127557815615528))
					return Task.FromResult(PreconditionResult.FromSuccess());
				else
					return Task.FromResult(PreconditionResult.FromError("You're not an admin"));
			}
			return Task.FromResult(PreconditionResult.FromError("You're not an admin"));
		}
	}
}