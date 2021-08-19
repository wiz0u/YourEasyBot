# EasyBot

With this small framework based around [TelegramBots](https://github.com/TelegramBots/Telegram.Bot), you can easily write Console polling **Telegram bot**, in particular those that requires sequential inputs.

Here is an example bot asking sequential questions when the user sends **/start** in private chat:

    public class MyBot : EasyBot
    {
        public MyBot() : base("PASTE YOUR BOT TOKEN HERE") { }

        public override async Task OnPrivateChat(Chat chat, User user, UpdateInfo update)
        {
            if (update.UpdateKind != UpdateKind.NewMessage || update.MsgCategory != MsgCategory.Text) return;
            if (update.Message.Text == "/start")
            {
                await Telegram.SendTextMessageAsync(chat, "What is your first name?");
                var firstName = await NewTextMessage(update);
                await Telegram.SendTextMessageAsync(chat, "What is your last name?");
                var lastName = await NewTextMessage(update);
                await Telegram.SendTextMessageAsync(chat, $"Welcome, {firstName} {lastName}!");
                return;
            }
        }
    }

# Available methods

There are 4 overridable methods on EasyBot, where you can place your code:
* **OnPrivateChat**: called when something happens on a private chat *(initiated with a given user)*
* **OnGroupChat**: called when something happens on a group chat the bot is in *(the bot must be added to the group by an admin)*
* **OnChannel**: called when something happens on a channel
* **OnOtherEvents**: called when something else happens that doesn't fit the above categories

These methods must be marked as `async` and typically include a **Chat** or **User** parameter telling you where the event is happening, as well as an **UpdateInfo** update parameter giving you more detail about the event:
* **UpdateKind**: tells you if the event is about:
  * a new message
  * a message that was edited
  * a callback query was triggered *(a user clicking an inline button)*
  * or another type of update *(typically, the bot or other users joining/leaving the chat)*
* **Message**: the associated message if any.
* **MsgCategory**: the category of the message *(Text, MediaOrDoc, StickerOrDice, Sharing, ChatStatus, VoiceChat or Other)*
* **CallbackData**: the callback query string associated with the inline button
* **Update**: the full Telegram Update object in case you need to access additional information

It is therefore recommended that the first thing you do in your overriden method is to check what UpdateKind it was called upon.

⚠️ Please note that a "new message" is not necessarily a text message written in the chat.
<br/>It can also be a media, document, sticker, etc.. that was posted, **or it can be an event** concerning the chat *(like creation, change of characteristics, pinned message, voice chat status, etc..)*. So make sure to check the value of **MsgCategory**, **Message.Type**, or that **Message.Text** is not null before accessing it.

Once your method is running, EasyBot ensure that execution is sequential for this particular channel/group/private chat.
However, two or more versions of your method might be running in parallel if different channels/groups/users are involved.

In your method, you can call `await Telegram.XYZAsync()` to send messages or do any other Telegram actions you want.

After you've done handling this update event, you can either:
* return from your method.
  <br/>The method will be called again for future events concerning this chat.
* or continue execution of your method and call:
  * `await WaitNext(update)` to obtain the next sequential update event concerning the current chat
  * `await WaitNewMessage(update)` to obtain the next new message in the current chat
    <br/>*(this method return only real messages in categories Text, MediaOrDoc and StickerOrDice and ignore all other events; it will raise an exception to abort your method if the chat is left)*
  * `await WaitNewTextMessage(update)` to obtain the next new text message in the current chat
    *(same as above but keeps only Text messages)*
  * `ReplyCallback(update, "you clicked!")` to acknowledge the callback query and optionally display a text to the user

If you have questions about this EasyBot framework, you can contact me in [TelegramBots support group](https://t.me/joinchat/B35YY0QbLfd034CFnvCtCA).
