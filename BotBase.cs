#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8604 // Possible null reference argument.
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.Threading.Tasks.Task;
using File = System.IO.File;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeProtected.Global

namespace Wizou.EasyBot;

public class BotBase // A fun way to code Telegram Bots, by Wizou
{
    readonly CancellationTokenSource cancel = new();
    readonly Dictionary<long, TaskInfo> tasks = new();
    public readonly TelegramBotClient Bot;
    public string UnknownCommandResponse { get; set; } = "I don't know that command";

    public BotBase(string botToken)
    {
        CommandHandler = new()
        {
            UnknownCommandHandler = async (ctx, _) => await Bot.SendTextMessageAsync(ctx.Chat, UnknownCommandResponse),
            Prefix = '/',
            WrongScopeCommandHandler = async (ctx, isPrivateChat) => await Bot.SendTextMessageAsync(ctx.Chat,
                $"Sorry, I can't do that in {(isPrivateChat ? "Private" : "Group")} chat")
        };
        Bot = new(botToken);
    }

    public User Me { get; private set; } = null!;
    public string BotName => Me.Username;
    public CommandHandler CommandHandler { get; }

    public virtual Task OnPrivateChat(UpdateContext updateContext)
    {
        return CompletedTask;
    }

    public virtual Task OnGroupChat(UpdateContext updateContext)
    {
        return CompletedTask;
    }

    public virtual Task OnChannel(Chat channel, UpdateInfo update)
    {
        return CompletedTask;
    }

    public virtual Task OnOtherEvents(UpdateInfo update)
    {
        return CompletedTask;
    }

    public void Run()
    {
        RunAsync().Wait();
    }

    public async Task RunAsync()
    {
        Me = await Bot.GetMeAsync();
        var messageOffset = 0;
        Console.WriteLine("Press Escape to stop the bot");
        while (true)
        {
            Update[] updates;
            try
            {
                updates = await Bot.GetUpdatesAsync(messageOffset, timeout: 2);
            }
            catch (Exception e)
            {
                var path = Environment.GetEnvironmentVariable("TEMP") ?? Environment.GetEnvironmentVariable("TMP") ??
                    Environment.GetEnvironmentVariable("TMPDIR") +
                    $"/Error{DateTime.Now.Millisecond}.txt";
                Console.WriteLine("Error acquired");
                Console.WriteLine(e.Message);
                Console.WriteLine($"Serialized error is saved to \"{path}\"");
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(e));
                continue;
            }

            foreach (var update in updates)
            {
                if (update.Id < messageOffset)
                    continue;
                messageOffset = update.Id + 1;
                switch (update.Type)
                {
                    case UpdateType.Message:
                        HandleUpdate(update, UpdateKind.NewMessage, update.Message);
                        break;
                    case UpdateType.EditedMessage:
                        HandleUpdate(update, UpdateKind.EditedMessage, update.EditedMessage);
                        break;
                    case UpdateType.ChannelPost:
                        HandleUpdate(update, UpdateKind.NewMessage, update.ChannelPost);
                        break;
                    case UpdateType.EditedChannelPost:
                        HandleUpdate(update, UpdateKind.EditedMessage, update.EditedChannelPost);
                        break;
                    case UpdateType.CallbackQuery:
                        HandleUpdate(update, UpdateKind.CallbackQuery, update.CallbackQuery.Message);
                        break;
                    case UpdateType.MyChatMember:
                        HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.MyChatMember.Chat);
                        break;
                    case UpdateType.ChatMember:
                        HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.ChatMember.Chat);
                        break;
                    case UpdateType.Poll:
                    case UpdateType.Unknown:
                    case UpdateType.PollAnswer:
                    case UpdateType.InlineQuery:
                    case UpdateType.ShippingQuery:
                    case UpdateType.ChatJoinRequest:
                    case UpdateType.PreCheckoutQuery:
                    case UpdateType.ChosenInlineResult:
                    default:
                        HandleUpdate(update, UpdateKind.OtherUpdate);
                        break;
                }
            }

            if (!Console.KeyAvailable) continue;
            if (Console.ReadKey().Key == ConsoleKey.Escape)
                break;
        }

        cancel.Cancel();
    }

    void HandleUpdate(Update update, UpdateKind updateKind, Message message = null!, Chat chat = null!)
    {
        TaskInfo taskInfo;
        // ReSharper disable once ConstantNullCoalescingCondition
        // ReSharper disable ConstantConditionalAccessQualifier
        chat ??= message?.Chat!;
        var chatId = chat?.Id ?? 0;
        lock (tasks)
        {
            if (!tasks.TryGetValue(chatId, out taskInfo!))
                tasks[chatId] = taskInfo = new();
        }

        var updateInfo = new UpdateInfo(taskInfo) {UpdateKind = updateKind, Update = update, Message = message!};
        if (update.Type is UpdateType.CallbackQuery)
            updateInfo.CallbackData = update.CallbackQuery.Data!;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (taskInfo.task != null)
        {
            lock (taskInfo.updates)
            {
                taskInfo.updates.Enqueue(updateInfo);
            }

            taskInfo.semaphore.Release();
            return;
        }

        Func<Task> taskStarter = chat?.Type switch
        {
            ChatType.Private or ChatType.Group or ChatType.Supergroup
                when updateKind is UpdateKind.NewMessage && message.Type is MessageType.Text
                     && message.Text!.StartsWith(CommandHandler.Prefix)
                => async () =>
                {
                    // message.Text = message.Text!.Replace("@" + BotName, "");
                    await CommandHandler.HandleCommand(new(chat, message?.From, updateInfo),
                        chat.Type is ChatType.Private, BotName);
                },
            ChatType.Private
                => () => OnPrivateChat(new(chat, message?.From, updateInfo)),
            ChatType.Group or ChatType.Supergroup
                => () => OnGroupChat(new(chat, message?.From, updateInfo)),
            ChatType.Channel
                => () => OnChannel(chat, updateInfo),
            _ => () => OnOtherEvents(updateInfo)
        };

        taskInfo.task = Task.Run(taskStarter).ContinueWith(_ => taskInfo.task = null!);
    }

    public async Task<UpdateKind> NextEvent(UpdateInfo update, CancellationToken ct = default)
    {
        using var bothCt = CancellationTokenSource.CreateLinkedTokenSource(ct, cancel.Token);
        var newUpdate = await ((IUpdateGetter) update).NextUpdate(bothCt.Token);
        update.Message = newUpdate.Message;
        update.CallbackData = newUpdate.CallbackData;
        update.Update = newUpdate.Update;
        return update.UpdateKind = newUpdate.UpdateKind;
    }

    public async Task<string> NewButtonClick(UpdateInfo update, Message? buttonedMessage = null,
        CancellationToken ct = default)
    {
        while (true)
        {
            switch (await NextEvent(update, ct))
            {
                case UpdateKind.CallbackQuery:
                    if (buttonedMessage is null || update.Message.MessageId == buttonedMessage.MessageId)
                        return update.CallbackData;
                    break;
                case UpdateKind.OtherUpdate
                    when update.Update.MyChatMember is
                        {NewChatMember.Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked}:
                    throw new LeftTheChatException();
                default:
                    throw new ArgumentOutOfRangeException(null);
                case UpdateKind.None:
                case UpdateKind.EditedMessage:
                case UpdateKind.NewMessage:
                    break;
            }
        }
    }


    public async Task<MsgCategory> NewMessage(UpdateInfo update, CancellationToken ct = default)
    {
        while (true)
            switch (await NextEvent(update, ct))
            {
                case UpdateKind.NewMessage
                    when update.MsgCategory is MsgCategory.Text or MsgCategory.MediaOrDoc or MsgCategory.StickerOrDice:
                    return update.MsgCategory; // NewMessage only returns for messages from these 3 categories
                case UpdateKind.OtherUpdate
                    when update.Update.MyChatMember is
                    {
                        NewChatMember: {Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked}
                    }:
                    throw new LeftTheChatException(); // abort the calling method
                case UpdateKind.None:
                case UpdateKind.EditedMessage:
                case UpdateKind.CallbackQuery:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(null);
            }
    }


    public async Task<string> NewTextMessage(UpdateInfo update, CancellationToken ct = default)
    {
        while (await NewMessage(update, ct) != MsgCategory.Text)
            await Delay(1, CancellationToken.None);
        return update.Message.Text;
    }

    public void ReplyCallback(UpdateInfo update, string text = null!)
    {
        if (update.Update.Type != UpdateType.CallbackQuery)
            throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
        _ = Bot.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, text);
    }
}

public class LeftTheChatException : Exception
{
    public LeftTheChatException() : base("The chat was left")
    {
    }
}

#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8603 // Possible null reference return.
#pragma warning restore CS8604 // Possible null reference argument.