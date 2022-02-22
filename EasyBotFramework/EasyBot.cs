using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
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

		private readonly CancellationTokenSource _cancel = new();
		private readonly Dictionary<long, TaskInfo> _tasks = new();

		public virtual Task OnPrivateChat(Chat chat, User user, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnGroupChat(Chat chat, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnChannel(Chat channel, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnOtherEvents(UpdateInfo update) => Task.CompletedTask;

		public EasyBot(string botToken) => Telegram = new(botToken);
		public void Run() => RunAsync().Wait();
		public async Task RunAsync()
		{
			Me = await Telegram.GetMeAsync();
			int messageOffset = 0;
			Console.WriteLine("Press Escape to stop the bot");
			while (true)
			{
				var updates = await Telegram.GetUpdatesAsync(messageOffset, timeout: 2);
				foreach (var update in updates)
				{
					if (update.Id < messageOffset) continue;
					messageOffset = update.Id + 1;
					switch (update.Type)
					{
						case UpdateType.Message: HandleUpdate(update, UpdateKind.NewMessage, update.Message); break;
						case UpdateType.EditedMessage: HandleUpdate(update, UpdateKind.EditedMessage, update.EditedMessage); break;
						case UpdateType.ChannelPost: HandleUpdate(update, UpdateKind.NewMessage, update.ChannelPost); break;
						case UpdateType.EditedChannelPost: HandleUpdate(update, UpdateKind.EditedMessage, update.EditedChannelPost); break;
						case UpdateType.CallbackQuery: HandleUpdate(update, UpdateKind.CallbackQuery, update.CallbackQuery.Message); break;
						case UpdateType.MyChatMember: HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.MyChatMember.Chat); break;
						case UpdateType.ChatMember: HandleUpdate(update, UpdateKind.OtherUpdate, chat: update.ChatMember.Chat); break;
						default: HandleUpdate(update, UpdateKind.OtherUpdate); break;
					}
				}
				if (Console.KeyAvailable)
					if (Console.ReadKey().Key == ConsoleKey.Escape)
						break;
			}
			_cancel.Cancel();
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
			if (update.Type is UpdateType.CallbackQuery)
				updateInfo.CallbackData = update.CallbackQuery.Data;
			if (taskInfo.Task != null)
			{
				lock (taskInfo.Updates)
					taskInfo.Updates.Enqueue(updateInfo);
				taskInfo.Semaphore.Release();
				return;
			}
			Func<Task> taskStarter = (chat?.Type) switch
			{
				ChatType.Private => () => OnPrivateChat(chat, message?.From, updateInfo),
				ChatType.Group or ChatType.Supergroup => () => OnGroupChat(chat, updateInfo),
				ChatType.Channel => () => OnChannel(chat, updateInfo),
				_ => () => OnOtherEvents(updateInfo),
			};
			taskInfo.Task = Task.Run(taskStarter).ContinueWith(t => taskInfo.Task = null);
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
						when update.Update.MyChatMember is ChatMemberUpdated
						{ NewChatMember: { Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked } }:
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
						when update.Update.MyChatMember is ChatMemberUpdated
						{ NewChatMember: { Status: ChatMemberStatus.Left or ChatMemberStatus.Kicked } }:
							throw new LeftTheChatException(); // abort the calling method
				}
			}
		}

		public async Task<string> NewTextMessage(UpdateInfo update, CancellationToken ct = default)
		{
			while (await NewMessage(update, ct) != MsgCategory.Text) { }
			return update.Message.Text;
		}

		public void ReplyCallback(UpdateInfo update, string text = null)
		{
			if (update.Update.Type != UpdateType.CallbackQuery)
				throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
			_ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, text);
		}
	}

	public class LeftTheChatException : Exception
	{
		public LeftTheChatException() : base("The chat was left") { }
	}
}
