using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace YourEasyBot
{
	internal class EasyBot	// A fun way to code Telegram Bots, by Wizou
	{
		public TelegramBotClient Telegram;
		public User Me;
		public string BotName => Me.Username;

		private readonly CancellationTokenSource _cancel = new();
		private readonly Dictionary<long, TaskInfo> _tasks = new();

		public EasyBot(string botToken)
		{
			Telegram = new(botToken);
		}

		public virtual Task OnPrivateChat(Chat chat, User user, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnGroupChat(Chat chat, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnChannelEvent(Chat channel, UpdateInfo update) => Task.CompletedTask;
		public virtual Task OnOtherEvents(UpdateInfo update) => Task.CompletedTask;

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
						case UpdateType.Message: HandleUpdate(update, update.Message.Chat, update.Message.From); break;
						case UpdateType.EditedMessage: HandleUpdate(update, update.EditedMessage.Chat, update.EditedMessage.From); break;
						case UpdateType.ChannelPost: HandleUpdate(update, update.ChannelPost.Chat, null); break;
						case UpdateType.EditedChannelPost: HandleUpdate(update, update.EditedChannelPost.Chat, null); break;
						case UpdateType.CallbackQuery: HandleUpdate(update, update.CallbackQuery.Message.Chat, null); break;
						default: HandleUpdate(update, null, null); break;
					}
				}
				if (Console.KeyAvailable)
					if (Console.ReadKey().Key == ConsoleKey.Escape)
						break;
			}
			_cancel.Cancel();
		}

		private void HandleUpdate(Update update, Chat chat, User user)
		{
			TaskInfo taskInfo;
			long chatId = chat?.Id ?? 0;
			lock (_tasks)
				if (!_tasks.TryGetValue(chatId, out taskInfo))
					_tasks[chatId] = taskInfo = new TaskInfo();
			var updateInfo = new UpdateInfo(taskInfo) { UpdateKind = UpdateInfo.Kind.OtherUpdate, Update = update };
			switch (update.Type)
			{
				case UpdateType.Message:
					updateInfo.UpdateKind = UpdateInfo.Kind.NewMessage;
					updateInfo.Message = update.Message;
					break;
				case UpdateType.EditedMessage:
					updateInfo.UpdateKind = UpdateInfo.Kind.EditedMessage;
					updateInfo.Message = update.EditedMessage;
					break;
				case UpdateType.ChannelPost:
					updateInfo.UpdateKind = UpdateInfo.Kind.NewMessage;
					updateInfo.Message = update.ChannelPost;
					break;
				case UpdateType.EditedChannelPost:
					updateInfo.UpdateKind = UpdateInfo.Kind.EditedMessage;
					updateInfo.Message = update.EditedChannelPost;
					break;
				case UpdateType.CallbackQuery:
					updateInfo.UpdateKind = UpdateInfo.Kind.CallbackQuery;
					updateInfo.Message = update.CallbackQuery.Message;
					updateInfo.CallbackData = update.CallbackQuery.Data;
					break;
			}
			if (taskInfo.Task != null)
			{
				lock (taskInfo.Updates)
					taskInfo.Updates.Enqueue(updateInfo);
				taskInfo.Semaphore.Release();
				return;
			}
			Func<Task> taskStarter = (chat?.Type) switch
			{
				ChatType.Private => () => OnPrivateChat(chat, user, updateInfo),
				ChatType.Group or ChatType.Supergroup => () => OnGroupChat(chat, updateInfo),
				ChatType.Channel => () => OnChannelEvent(chat, updateInfo),
				_ => () => OnOtherEvents(updateInfo),
			};
			taskInfo.Task = Task.Run(taskStarter).ContinueWith(t => taskInfo.Task = null);
		}

		public async Task<UpdateInfo.Kind> WaitNext(UpdateInfo update)
		{
			var newUpdate = await ((IGetNext)update).NextUpdate(_cancel.Token);
			update.Message = newUpdate.Message;
			update.CallbackData = newUpdate.CallbackData;
			update.Update = newUpdate.Update;
			return update.UpdateKind = newUpdate.UpdateKind;
		}

		public async Task WaitNewMessage(UpdateInfo update)
		{
			while (await WaitNext(update) != UpdateInfo.Kind.NewMessage) { }
		}

		public void ReplyCallback(UpdateInfo update, string text = null)
		{
			if (update.Update.Type != UpdateType.CallbackQuery)
				throw new InvalidOperationException("This method can be called only for CallbackQuery updates");
			_ = Telegram.AnswerCallbackQueryAsync(update.Update.CallbackQuery.Id, text);
		}
	}
}
