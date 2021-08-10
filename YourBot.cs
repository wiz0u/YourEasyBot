using System;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace YourEasyBot
{
	/* Notes:
	 * 1. To use, first paste your bot token in the Project Properties > Debug > Application arguments
	 * 2. This software is provided "as-is", without any warranty or liability (license MIT)
	 * 3. This project can help you write little bots with sequential logic methods, awaiting consecutive user messages
	 * 3.14 However this was written as fun project, it would not be reasonable to use it in serious company code
	 */

	internal class YourBot : EasyBot
	{
		static void Main(string[] args)
		{
			var bot = new YourBot(args[0]);
			bot.Run();
		}

		public YourBot(string botToken) : base(botToken) { }

		// There are 4 overridable methods on EasyBot, place your code in here.
		// OnPrivateChat: called when a private chat (duh!) is initiated with a given user
		// OnGroupChat:   called when there are new updates/messages concerning a group chat the bot is in
		// OnChannelPost: called if your bot is on a channel
		// OnOtherEvents: called for any other Update

		// if two or more users are speaking to your bot, 2 calls of this method might be executing in parallel
		public override async Task OnPrivateChat(Chat chat, User user, UpdateInfo update)
		{
			Console.WriteLine($"In private chat with {user.Name()}");
			if (update.UpdateKind != UpdateInfo.Kind.NewMessage) return;
			Console.WriteLine($"He wrote: {update.Message.Text}");
			await Telegram.SendTextMessageAsync(chat, "OK, what else?");
			// in this method, we want to proceed sequentially:
			await WaitNewMessage(update);
			// execution continue here when there is a new message from the same user
			Console.WriteLine($"And then: {update.Message.Text}");
			await Telegram.SendTextMessageAsync(chat, "That too, yeah");
			await WaitNewMessage(update);
			Console.WriteLine($"And finally: {update.Message.Text}");
			await Telegram.SendTextMessageAsync(chat, $"Alright! Thanks!\n\nFor more fun, try to type /button@{BotName} in chat I'm in");
			// we exit the function. it will be called again if there are new updates/messages concerning this or another user
		}

		public override async Task OnGroupChat(Chat chat, UpdateInfo update)
		{
			Console.WriteLine($"In group chat {chat.Name()}");
			do
			{
				switch (update.UpdateKind)
				{
					case UpdateInfo.Kind.NewMessage:
						Console.WriteLine($"{update.Message.From.Name()} wrote: {update.Message.Text}");
						if (update.Message.Text == "/button@" + BotName)
							await Telegram.SendTextMessageAsync(chat, "You summoned me!", replyMarkup: new InlineKeyboardMarkup("I grant your wish"));
						break;
					case UpdateInfo.Kind.EditedMessage:
						Console.WriteLine($"{update.Message.From.Name()} edited: {update.Message.Text}");
						break;
					case UpdateInfo.Kind.CallbackQuery:
						Console.WriteLine($"{update.Message.From.Name()} clicked the button with data '{update.CallbackData}' on the msg: {update.Message.Text}");
						ReplyCallback(update, "Wish granted !");
						break;
				}
				// in this approach, we choose to continue execution in a loop, obtaining new updates/messages for this chat as they come
			} while (await WaitNext(update) != 0);
		}
	}
}