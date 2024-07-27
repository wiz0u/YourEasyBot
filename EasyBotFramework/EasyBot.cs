using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace YourEasyBot
{
	public class EasyBot	// A fun way to code Telegram Bots, by Wizou
	{
		public readonly TelegramBotClient Telegram;
		public User Me { get; private set; }
		public string BotName => Me.Username;

		private int _lastUpdateId = -1;
		private readonly CancellationTokenSource _cancel = new();
		private readonly Dictionary<long, TaskInfo> _tasks = new();

		public virtual Task OnPrivateChat(Chat chat, User user, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnGroupChat(Chat chat, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnChannel(Chat channel, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnOtherEvents(UpdateInfo update) => Task.CompletedTask;

		public EasyBot(string botToken)
		{
			Telegram = new(botToken);
			Me = Task.Run(() => Telegram.GetMeAsync()).Result;
		}

		public void Run() => RunAsync().Wait();
		public async Task RunAsync()
		{
			Console.WriteLine("Press Escape to stop the bot");
			while (true)
			{
				var updates = await Telegram.GetUpdatesAsync(_lastUpdateId + 1, timeout: 2);
				foreach (var update in updates)
					HandleUpdate(update);
				if (Console.KeyAvailable)
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
			if (update.Id <= _lastUpdateId) return;
			_lastUpdateId = update.Id;
			switch (update)
			{
				case { Message: { } m }: HandleUpdate(update, UpdateKind.NewMessage, m); break;
				case { EditedMessage: { } em }: HandleUpdate(update, UpdateKind.EditedMessage, em); break;
				case { ChannelPost: { } cp }: HandleUpdate(update, UpdateKind.NewMessage, cp); break;
				case { EditedChannelPost: { } ecp }: HandleUpdate(update, UpdateKind.EditedMessage, ecp); break;
				case { BusinessMessage: { } bm }: HandleUpdate(update, UpdateKind.NewMessage, bm); break;
				case { EditedBusinessMessage: { } ebm }: HandleUpdate(update, UpdateKind.EditedMessage, ebm); break;
				case { CallbackQuery: { } cq }: HandleUpdate(update, UpdateKind.CallbackQuery, cq.Message); break;
				case { MyChatMember: { } mcm }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: mcm.Chat); break;
				case { ChatMember: { } cm }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: cm.Chat); break;
				case { ChatJoinRequest: { } cjr }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: cjr.Chat); break;
				case { MessageReaction: { } mr }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: mr.Chat); break;
				case { MessageReactionCount: { } mrc }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: mrc.Chat); break;
				case { ChatBoost: { } cb }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: cb.Chat); break;
				case { RemovedChatBoost: { } rcb }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: rcb.Chat); break;
				case { DeletedBusinessMessages: { } dbm }: HandleUpdate(update, UpdateKind.OtherUpdate, chat: dbm.Chat); break;
				default: HandleUpdate(update, UpdateKind.OtherUpdate); break;
			}
		}

		private void HandleUpdate(Update update, UpdateKind updateKind, Message message = null, Chat chat = null)
		{
			TaskInfo taskInfo;
			chat ??= message?.Chat;
			long chatId = chat?.Id ?? 0;
			lock (_tasks)
				if (!_tasks.TryGetValue(chatId, out taskInfo))
					_tasks[chatId] = taskInfo = new TaskInfo();
			var updateInfo = new UpdateInfo(taskInfo) { UpdateKind = updateKind, Update = update, Message = message };
			if (update is { CallbackQuery: { } cq })
				updateInfo.CallbackData = cq.Data;
			lock (taskInfo)
				if (taskInfo.Task != null)
				{
					taskInfo.Updates.Enqueue(updateInfo);
					taskInfo.Semaphore.Release();
					return;
				}
			RunTask(taskInfo, updateInfo, chat);
		}

		private void RunTask(TaskInfo taskInfo, UpdateInfo updateInfo, Chat chat)
		{
			Func<Task> taskStarter = (chat?.Type) switch
			{
				ChatType.Private => () => OnPrivateChat(chat, updateInfo.Message?.From, updateInfo),
				ChatType.Group or ChatType.Supergroup => () => OnGroupChat(chat, updateInfo),
				ChatType.Channel => () => OnChannel(chat, updateInfo),
				_ => () => OnOtherEvents(updateInfo),
			};
			taskInfo.Task = Task.Run(taskStarter).ContinueWith(async t =>
			{
				lock (taskInfo)
					if (taskInfo.Semaphore.CurrentCount == 0)
					{
						taskInfo.Task = null;
						return;
					}
				var newUpdate = await ((IGetNext)updateInfo).NextUpdate(_cancel.Token);
				RunTask(taskInfo, newUpdate, chat);
			});
		}

		public async Task<UpdateKind> NextEvent(UpdateInfo update, CancellationToken ct = default)
		{
			using var bothCT = CancellationTokenSource.CreateLinkedTokenSource(ct, _cancel.Token);
			var newUpdate = await ((IGetNext)update).NextUpdate(bothCT.Token);
			update.Message = newUpdate.Message;
			update.CallbackData = newUpdate.CallbackData;
			update.Update = newUpdate.Update;
			return update.UpdateKind = newUpdate.UpdateKind;
		}

		public async Task<string> ButtonClicked(UpdateInfo update, Message msg = null, CancellationToken ct = default)
		{
			while (true)
			{
				switch (await NextEvent(update, ct))
				{
					case UpdateKind.CallbackQuery:
						if (msg != null && update.Message.MessageId != msg.MessageId)
							_ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, null, cancellationToken: ct);
						else
							return update.CallbackData;
						continue;
					case UpdateKind.OtherUpdate
					when update.Update.MyChatMember is { NewChatMember.Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked }:
						throw new LeftTheChatException(); // abort the calling method
				}
			}
		}

		public async Task<MsgCategory> NewMessage(UpdateInfo update, CancellationToken ct = default)
		{
			while (true)
			{
				switch (await NextEvent(update, ct))
				{
					case UpdateKind.NewMessage
					when update.MsgCategory is MsgCategory.Text or MsgCategory.MediaOrDoc or MsgCategory.StickerOrDice:
						return update.MsgCategory; // NewMessage only returns for messages from these 3 categories
					case UpdateKind.CallbackQuery:
						_ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, null, cancellationToken: ct);
						continue;
					case UpdateKind.OtherUpdate
					when update.Update.MyChatMember is { NewChatMember.Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked }:
						throw new LeftTheChatException(); // abort the calling method
				}
			}
		}

		public async Task<string> NewTextMessage(UpdateInfo update, CancellationToken ct = default)
		{
			while (await NewMessage(update, ct) != MsgCategory.Text) { }
			return update.Message.Text;
		}

		public void ReplyCallback(UpdateInfo update, string text = null, bool showAlert = false, string url = null)
		{
			if (update.Update is not { CallbackQuery: { } })
				throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
			_ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, text, showAlert, url);
		}
	}

	public class LeftTheChatException : Exception
	{
		public LeftTheChatException() : base("The chat was left") { }
	}
}
