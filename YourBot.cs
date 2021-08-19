using System;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace YourEasyBot
{
	/* Notes:
	 * 1. To use this example, first paste your bot token in the Project Properties > Debug > Application arguments
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

		public override async Task OnPrivateChat(Chat chat, User user, UpdateInfo update)
		{
			if (update.UpdateKind != UpdateKind.NewMessage || update.MsgCategory != MsgCategory.Text) return;
			if (update.Message.Text == "/start")
			{
				await Telegram.SendTextMessageAsync(chat, "What is your first name?");
				var firstName = await NewTextMessage(update);
				// execution continues once we received a new text message
				await Telegram.SendTextMessageAsync(chat, "What is your last name?");
				var lastName = await NewTextMessage(update);
				await Telegram.SendTextMessageAsync(chat, $"Welcome, {firstName} {lastName}!" +
					$"\n\nFor more fun, try to type /button@{BotName} in a group I'm in");
				return;
			}
		}

		public override async Task OnGroupChat(Chat chat, UpdateInfo update)
		{
			Console.WriteLine($"In group chat {chat.Name()}");
			do
			{
				switch (update.UpdateKind)
				{
					case UpdateKind.NewMessage:
						Console.WriteLine($"{update.Message.From.Name()} wrote: {update.Message.Text}");
						if (update.Message.Text == "/button@" + BotName)
							await Telegram.SendTextMessageAsync(chat, "You summoned me!", replyMarkup: new InlineKeyboardMarkup("I grant your wish"));
						break;
					case UpdateKind.EditedMessage:
						Console.WriteLine($"{update.Message.From.Name()} edited: {update.Message.Text}");
						break;
					case UpdateKind.CallbackQuery:
						Console.WriteLine($"{update.Message.From.Name()} clicked the button with data '{update.CallbackData}' on the msg: {update.Message.Text}");
						ReplyCallback(update, "Wish granted !");
						break;
				}
				// in this approach, we choose to continue execution in a loop, obtaining new updates/messages for this chat as they come
			} while (await NextEvent(update) != 0);
		}
	}
}