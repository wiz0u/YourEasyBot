using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using static System.Threading.Tasks.Task;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeProtected.Global

namespace Wizou.EasyBot;

/// <summary>
///     Derive from this class to create your simple bot
/// </summary>
public class BotBase
{
	private int _lastUpdateId = -1;
	readonly CancellationTokenSource _cancel = new();
	readonly Dictionary<long, TaskInfo> _tasks = new();

	/// <summary>
	///     <see cref="TelegramBotClient" /> for sending Messages etc.
	/// </summary>
	public readonly TelegramBotClient Bot;

	/// <summary>
	///     Public base constructor that takes Bot Token for <see cref="TelegramBotClient" />
	/// </summary>
	/// <param name="logger">A logger for your needs</param>
	/// <param name="botToken">Telegram Bot Token</param>
	// ReSharper disable once SuggestBaseTypeForParameterInConstructor
	public BotBase(string botToken, ILogger<BotBase>? logger = null)
	{
		Bot = new(botToken);
		Me = Task.Run(() => Bot.GetMeAsync()).Result;
		Logger = logger;
		CommandHandler = new('/');
		CommandHandler.OnWrongScopeCommand += async (ctx, command, isPrivateChat) =>
											  {
												  var scope = $"{(isPrivateChat ? "Private" : "Group")} chat";
												  await Bot.SendTextMessageAsync(ctx.Chat, $"Sorry, I can't do {command.Name} in {scope}");
												  Logger?.LogWarning("{UserId} invoked command '{CommandName}' in wrong scope: {Scope} Context: {ctx}",
																	 ctx.User.Id, command.Name, scope, JsonConvert.SerializeObject(ctx));
											  };
		CommandHandler.OnUnknownCommand += async (ctx, _) => await Bot.SendTextMessageAsync(ctx.Chat, UnknownCommandResponse);
	}

	/// <summary>
	///     An <see cref="ILogger" /> for your needs
	/// </summary>
	public ILogger? Logger { get; }

	/// <summary>
	///     Response for unknown commands
	/// </summary>
	public string UnknownCommandResponse { get; set; } = "I don't know that command";

	/// <summary>
	///     Basic information about Bot
	/// </summary>
	public User Me { get; private set; } = null!;

	/// <summary>
	///     Your Bot's <see cref="User.Username" />
	/// </summary>
	public string BotName => Me.Username!;

	/// <summary>
	///     A simple Command Handler
	/// </summary>
	public CommandHandler CommandHandler { get; }

	/// <summary>
	///     When overriden Handles the updates in Private chats
	/// </summary>
	/// <param name="updateContext"></param>
	/// <returns></returns>
	public virtual Task OnPrivateChat(UpdateContext updateContext)
		=> CompletedTask;

	/// <summary>
	///     When overriden Handles the updates in Group chats
	/// </summary>
	/// <param name="updateContext">Update event context</param>
	/// <returns></returns>
	public virtual Task OnGroupChat(UpdateContext updateContext)
		=> CompletedTask;

	/// <summary>
	///     When overriden Handles the updates in Channels
	/// </summary>
	/// <param name="channel">Object that represents a channel</param>
	/// <param name="update">Update event information</param>
	/// <returns></returns>
	public virtual Task OnChannel(Chat channel, UpdateInfo update)
		=> CompletedTask;

	/// <summary>
	/// </summary>
	/// <param name="update">Update event information</param>
	/// <returns></returns>
	public virtual Task OnOtherEvents(UpdateInfo update)
		=> CompletedTask;

	/// <summary>
	///     Synchronous version of <see cref="RunAsync" />
	/// </summary>
	public void Run()
		=> RunAsync().Wait();

	/// <summary>
	///     Delegate that handles the exceptions in update loop
	/// </summary>
	public event Func<Exception, Task> OnException = _ => CompletedTask;

	/// <summary>
	///     Starts the bot
	/// </summary>
	/// <returns></returns>
	public async Task RunAsync()
	{
		Logger?.LogInformation("Press Escape to stop the bot");
		while (true)
		{
			Update[] updates;
			try
			{
				updates = await Bot.GetUpdatesAsync(_lastUpdateId + 1, timeout: 5);
			}
			catch (Exception e)
			{
				await OnException.Invoke(e);
				continue;
			}

			foreach (var update in updates)
				HandleUpdate(update);

			if (!Console.KeyAvailable)
				continue;
			if (Console.ReadKey().Key == ConsoleKey.Escape)
				break;
		}

		_cancel.Cancel();
	}

	public async Task<string> CheckWebhook(string url)
	{
		var webhookInfo = await Telegram.GetWebhookInfoAsync();
		string result = $"{BotName} is running";
		if (webhookInfo.Url != url)
		{
			await Telegram.SetWebhookAsync(url);
			result += " and now registered as Webhook";
		}
		return $"{result}\n\nLast webhook error: {webhookInfo.LastErrorDate} {webhookInfo.LastErrorMessage}";
	}

	/// <summary>Use this method in your WebHook controller</summary>
	public void HandleUpdate(Update update)
	{
		if (update.Id < _lastUpdateId)
			return;
		_lastUpdateId = update.Id;
		switch (update.Type)
		{
			case UpdateType.Message:
				HandleUpdate(update, UpdateKind.NewMessage, update.Message!);
				break;
			case UpdateType.EditedMessage:
				HandleUpdate(update, UpdateKind.EditedMessage, update.EditedMessage!);
				break;
			case UpdateType.ChannelPost:
				HandleUpdate(update, UpdateKind.NewMessage, update.ChannelPost!);
				break;
			case UpdateType.EditedChannelPost:
				HandleUpdate(update, UpdateKind.EditedMessage, update.EditedChannelPost!);
				break;
			case UpdateType.CallbackQuery:
				HandleUpdate(update, UpdateKind.CallbackQuery, update.CallbackQuery!.Message!);
				break;
			case UpdateType.MyChatMember:
				HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.MyChatMember!.Chat);
				break;
			case UpdateType.ChatMember:
				HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.ChatMember!.Chat);
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

	void HandleUpdate(Update update, UpdateKind updateKind, Message? message = null!, Chat? chat = null!)
	{
		TaskInfo taskInfo;
		// ReSharper disable once ConstantNullCoalescingCondition
		// ReSharper disable ConstantConditionalAccessQualifier
		chat ??= message?.Chat;
		var chatId = chat?.Id ?? 0;
		lock (_tasks)
		{
			if (!_tasks.TryGetValue(chatId, out taskInfo!))
				_tasks[chatId] = taskInfo = new();
		}

		var updateInfo = new UpdateInfo(taskInfo) { UpdateKind = updateKind, Update = update, Message = message! };
		if (update.Type is UpdateType.CallbackQuery)
			updateInfo.CallbackData = update.CallbackQuery!.Data!;
		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (taskInfo._task != null)
		{
			lock (taskInfo._updates)
			{
				taskInfo._updates.Enqueue(updateInfo);
			}

			taskInfo._semaphore.Release();
			return;
		}

		Func<Task> taskStarter = chat?.Type switch
		{
			ChatType.Private or ChatType.Group or ChatType.Supergroup when updateKind is UpdateKind.NewMessage && message!.Type is MessageType.Text &&
																		   message.Text!.StartsWith(CommandHandler.Prefix) => async ()
				=> await CommandHandler.HandleCommand(new(chat, message?.From!, updateInfo), chat.Type is ChatType.Private, BotName),
			ChatType.Private => () => OnPrivateChat(new(chat, message?.From!, updateInfo)),
			ChatType.Group or ChatType.Supergroup => () => OnGroupChat(new(chat, message?.From!, updateInfo)),
			ChatType.Channel => () => OnChannel(chat, updateInfo),
			_ => () => OnOtherEvents(updateInfo)
		};

		taskInfo._task = Task.Run(taskStarter).ContinueWith(_ => taskInfo._task = null!);
	}

	/// <summary>
	///     Returns the next update kind, and changes current update info to new one
	/// </summary>
	/// <param name="update"></param>
	/// <param name="ct"></param>
	/// <returns></returns>
	public async Task<UpdateKind> NextEvent(UpdateInfo update, CancellationToken ct = default)
	{
		using var bothCt = CancellationTokenSource.CreateLinkedTokenSource(ct, _cancel.Token);
		var newUpdate = await ((IUpdateGetter)update).NextUpdate(bothCt.Token);
		update.Message = newUpdate.Message;
		update.CallbackData = newUpdate.CallbackData;
		update.Update = newUpdate.Update;
		return update.UpdateKind = newUpdate.UpdateKind;
	}

	/// <summary>
	///     Returns the next button click update ignoring others
	/// </summary>
	/// <param name="update">Update Information</param>
	/// <param name="buttonedMessage">Message with attached buttons</param>
	/// <param name="ct">Cancellation Token</param>
	/// <returns>Clicked buttons Callback Data. <br /> If the task is canceled - null</returns>
	/// <exception cref="LeftTheChatException">If user left the chat while wait</exception>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public async Task<string?> NewButtonClick(UpdateInfo update, Message? buttonedMessage = null, CancellationToken ct = default)
	{
		while (true)
		{
			if (ct.IsCancellationRequested)
				return null;
			switch (await NextEvent(update, ct))
			{
				case UpdateKind.CallbackQuery:
					if (buttonedMessage is null || update.Message.MessageId == buttonedMessage.MessageId)
						return update.CallbackData;
					break;
				case UpdateKind.OtherUpdate when update.Update.MyChatMember is { NewChatMember.Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked }:
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

	/// <summary>
	///     Returns the next message category, and changes current update info to new one
	/// </summary>
	/// <param name="update"></param>
	/// <param name="ct"></param>
	/// <returns></returns>
	/// <exception cref="LeftTheChatException"></exception>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public async Task<MsgCategory> NewMessage(UpdateInfo update, CancellationToken ct = default)
	{
		while (true)
			switch (await NextEvent(update, ct))
			{
				case UpdateKind.NewMessage when update.MsgCategory is MsgCategory.Text or MsgCategory.MediaOrDoc or MsgCategory.StickerOrDice:
					return update.MsgCategory; // NewMessage only returns for messages from these 3 categories
				case UpdateKind.OtherUpdate when update.Update.MyChatMember is
				{
					NewChatMember.Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked
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

	/// <summary>
	///     Returns the next text message ignoring other kind of updates
	/// </summary>
	/// <param name="update"></param>
	/// <param name="ct"></param>
	/// <returns></returns>
	public async Task<string?> NewTextMessage(UpdateInfo update, CancellationToken ct = default)
	{
		while (await NewMessage(update, ct) != MsgCategory.Text)
			await Delay(1, CancellationToken.None);
		return update.Message.Text;
	}

	/// <summary>
	///     Replyes to the callback query
	/// </summary>
	/// <param name="update"></param>
	/// <param name="text"></param>
	/// <exception cref="InvalidOperationException"></exception>
	public void ReplyCallback(UpdateInfo update, string text = null!)
	{
		if (update.Update.Type != UpdateType.CallbackQuery)
			throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
		_ = Bot.AnswerCallbackQueryAsync(update.Update.CallbackQuery!.Id, text);
	}
}

/// <summary>
///     Thrown when user lefts the chat
/// </summary>
public class LeftTheChatException : Exception
{
	/// <summary></summary>
	public LeftTheChatException() : base("The chat was left") { }
}