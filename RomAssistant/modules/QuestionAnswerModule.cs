using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;
using RomAssistant.db;
using RomAssistant.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace RomAssistant.modules;

/*
```mermaid
    stateDiagram-v2
    /createquestion --> answerquestion:id
    answerquestion --> changecid:id
    answerquestion --> changeserver:id
    answerquestion --> answerquestiononly:id
    answerquestion --> answerquestion_server:id

    answerquestiononly --> sendanswer(modal) :id
    changeserver --> answerquestion_server:id
    changecid --> changecid_only(modal) : id
    changecid_only(modal) --> answerquestion
    answerquestion_server --> sendanswer(modal) :id
    sendanswer(modal) --> [*]

    ```
*/
public class QuestionAnswerModule : InteractionModuleBase<SocketInteractionContext>
{
    private Context context;

    public QuestionAnswerModule(Context context)
    {
        this.context = context;
    }

    [DoAdminCheck]
    [SlashCommand("createquestion", "Creates a question for players to answer")]
    public async Task CreateQuestion(
        [Summary(description: "The question for the players")] string question,
        [Summary(description: "a unique identifier for this question (keep it short, no spaces)")] string id,
        [Summary(description: "Picture to use in the question")] Attachment? header = null,
        [Summary(description: "Whether or not users can modify their answer")] bool canModify = true
        )
    {

        var cb = new ComponentBuilder();
        cb.WithButton("Answer", "answerquestion:" + id);

        await RespondAsync("Question created", ephemeral: true);
        if(header != null)
            await Context.Interaction.Channel.SendFileAsync(await new HttpClient().GetStreamAsync(header.Url), header.Filename);
        await Context.Interaction.Channel.SendMessageAsync("# " + question, components: cb.Build());
//        await RespondAsync("# " + question, components: cb.Build(), ephemeral: true);
    }

    [ComponentInteraction("answerquestion:*")]
    public async Task AnswerQuestion(string id)
    {
        await AnswerQuestion(id, false);
    }
    public async Task AnswerQuestion(string id, bool change)
    {
        var user = context.Users.Find(Context.User.Id);
        var answer = context.Answers.FirstOrDefault(a => a.User == user && a.QuestionId == id);
        
        var msg = "";
        var cb = new ComponentBuilder()
            .WithButton("Change CID", $"changecid:{id}")
            .WithButton("Change Server", $"changeserver:{id}");
        if (answer != null && user != null)
        {
            msg =
                $"# You already answered this question\n" +
                $"- **Your answer**: {answer.UserAnswer}\n" +
                $"## Your character settings:\n";
        }
        else if (user != null && user.Server != 0 && user.CharacterId != 0)
        {
            msg = $"# Please validate your character settings:\n";
            cb.WithButton("Looks good, let me answer", $"answerquestiononly:{id}", ButtonStyle.Success);
        }
        else if (user != null && user.Server != 0 && user.CharacterId == 0)
        {
            msg = $"# Please set your CID\n";
        }


        if (!string.IsNullOrEmpty(msg))
        {
            msg += 
                $"- **Server**: {user.Server.FullString()}\n" +
                $"- **CID**: {user.CharacterId}\n";
                //if (!string.IsNullOrEmpty(user.CharacterName))
                //    msg += $"- **Character Name**: {user.CharacterName}\n";
                //if (!string.IsNullOrEmpty(user.Guild))
                //    msg += $"- **Character Guild**: {user.Guild}\n";

            if (change)
                await ModifyOriginalResponseAsync(m =>
                {
                    m.Content = msg;
                    m.Components = cb.Build();
                });
            else
                await RespondAsync(msg, components: cb.Build(), ephemeral: true);
            return;
        }
        cb = new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
            .WithCustomId($"answerquestion_server:" + id)
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithPlaceholder("Your server")
            .WithOptions(Enum.GetValues<Server>().Where(s => s.IsServer()).Select(s => new SelectMenuOptionBuilder(s.FullString(), s.ToString(), isDefault: user == null ? null : user.Server == s)).ToList())
            );

        await RespondAsync("# What is your server?", components: cb.Build(), ephemeral: true);
    }

    [ComponentInteraction("answerquestiononly:*")]
    public async Task AnswerQuestionOnly(string id)
    {
        ModalBuilder mb = BuildModal(id, null);
        await RespondWithModalAsync(mb.Build());
        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "# Please enter your answer";
            m.Components = null;
        });
    }
    
    [ComponentInteraction("changecid:*")]
    public async Task ChangeCid(string id)
    {
        var user = context.Users.Find(Context.User.Id);
        if (user != null)
        {
            var mb = new ModalBuilder()
                        .WithCustomId($"changecid_only:{id}")
                        .WithTitle("Please change your cid")
                        ;
            var cid = new TextInputBuilder()
                .WithLabel("Your CID")
                .WithCustomId("cid")
                .WithPlaceholder("43xxxxxxxx")
                .WithRequired(true)
                .WithStyle(TextInputStyle.Short)
                .WithMinLength(10)
                .WithMaxLength(10);
            if (user.CharacterId.ToString().Length == 10)
                cid.WithValue(user.CharacterId.ToString());
            mb.AddTextInput(cid);
            await RespondWithModalAsync(mb.Build());
        }
    }
    
    [ComponentInteraction("changeserver:*")]
    public async Task ChangeServer(string id)
    {
        await DeferAsync(true);
        var user = context.Users.Find(Context.User.Id);
        var cb = new ComponentBuilder()
            .WithSelectMenu(new SelectMenuBuilder()
            .WithCustomId($"answerquestion_server:" + id)
            .WithMinValues(1)
            .WithMaxValues(1)
            .WithPlaceholder("Your server")
            .WithOptions(Enum.GetValues<Server>().Where(s => s.IsServer()).Select(s => new SelectMenuOptionBuilder(s.FullString(), s.ToString(), isDefault: user == null ? null : user.Server == s)).ToList())
            );
        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = "# Please change your server";
            m.Components = cb.Build();
        });
    }


    [ComponentInteraction("answerquestion_server:*")]
    public async Task AnswerQuestionServer(string id, string[] server)
    {
        var user = context.Users.Find(Context.User.Id);
        if (user == null)
        {
            context.Users.Add(user = new User()
            {
                Id = Context.User.Id,
                DiscordName = Context.User.Username
            });
        }
        user.Server = Enum.Parse<Server>(server[0]);
        user.CharacterName = "";
        user.Guild = "";
        user.AccountId = 0;
        await context.SaveChangesAsync();

        var answer = context.Answers.FirstOrDefault(a => a.User == user && a.QuestionId == id);
        if (answer == null)
        {
            ModalBuilder mb = BuildModal(id, user);
            await RespondWithModalAsync(mb.Build());
            await ModifyOriginalResponseAsync(m =>
            {
                m.Content = "# Please enter your answer";
                m.Components = null;
            });
        }
        else
        {
            await DeferAsync(true);
            await AnswerQuestion(id, true);
        }
    }

    private static ModalBuilder BuildModal(string id, User? user)
    {
        var mb = new ModalBuilder()
                    .WithCustomId($"sendanswer:{id}")
                    .WithTitle("Please submit your answer")
                    ;
        mb.AddTextInput(new TextInputBuilder()
            .WithLabel("Your answer")
            .WithCustomId("answer")
            .WithPlaceholder("Your answer can not be changed once submitted")
            .WithRequired(true)
            .WithStyle(TextInputStyle.Short)
            .WithMinLength(1)
            .WithMaxLength(32)
        );

        if (user != null && user.CharacterId == 0)
        {
            var cid = new TextInputBuilder()
                .WithLabel("Your CID")
                .WithCustomId("cid")
                .WithPlaceholder("43xxxxxxxx")
                .WithRequired(true)
                .WithStyle(TextInputStyle.Short)
                .WithMinLength(10)
                .WithMaxLength(10);
            mb.AddTextInput(cid);
        }

        return mb;
    }

    public class AnswerModal : IModal
    {
        public string Title => string.Empty;

        [ModalTextInput("answer")]
        public string Answer { get; set; } = string.Empty;
        [ModalTextInput("cid")]
        public string Cid { get; set; } = string.Empty;
    }

    [ModalInteraction("changecid_only:*")]
    public async Task ChangeCidOnly(string id, AnswerModal modal)
    {
        await DeferAsync(true);
        var user = context.Users.Find(Context.User.Id);
        if (user != null)
        {
            if (ulong.TryParse(modal.Cid, out var cid))
            {
                user.CharacterId = cid;
                user.CharacterName = "";
                user.Guild = "";
                user.AccountId = 0;
                await context.SaveChangesAsync();
                await AnswerQuestion(id, true);
            }
            else
                await ModifyOriginalResponseAsync(m => m.Content = $"You entered '{modal.Cid}' for a character ID, but this is not a valid number");

        }
        else
            await ModifyOriginalResponseAsync(m => m.Content = "Something went wrong, please contact <@!184343836070248449>");
    }


    [ModalInteraction("sendanswer:*")]
    public async Task SendAnswer(string id, AnswerModal modal)
    {
        await DeferAsync(true);
        var user = context.Users.Find(Context.User.Id);
        if(user == null)
        {//this should never happen
            context.Users.Add(user = new User()
            {
                Id = Context.User.Id,
                DiscordName = Context.User.Username
            });
        }
        if (!string.IsNullOrEmpty(modal.Cid))
        {
            if (ulong.TryParse(modal.Cid, out var cid))
                user.CharacterId = ulong.Parse(modal.Cid);
            else
            {
                await ModifyOriginalResponseAsync(m =>
                {
                    m.Content = $"## Your CID is not a number and could not be registered. Please answer again";
                });
                return;
            }
        }
        await context.SaveChangesAsync();
        context.Answers.Add(new Answer()
        {
            QuestionId = id,
            Time = DateTime.Now,
            User = user,
            UserAnswer = modal.Answer,
        });
        await context.SaveChangesAsync();



        await ModifyOriginalResponseAsync(m =>
        {
            m.Content = $"## Your answer has been registered.\n\n### You\n- Server: {user.Server}\n- CID: {user.CharacterId}\n- Your answer: {modal.Answer}\n\nYou can now Dismiss this message";
        });
    }



}
