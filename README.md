# EasyBot

With this small framework based around [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot), you can easily write Console polling **Telegram bot**, in particular those that requires sequential inputs.

```
    DISCLAIMER

    My fork can (and I think will) have a lot of difference
    compared to original repo, so mind your step using my fork :)

```

Here is an example bot asking sequential questions when the user sends **/start** in private chat:
```csharp
    public class MyBot : BotBase
    {
        public MyBot(string MyBotToken) : base(MyBotToken){}

        public override async Task OnPrivateChat(UpdateContext ctx)
        {
            if (ctx.Update.UpdateKind != UpdateKind.NewMessage || ctx.Update.MsgCategory != MsgCategory.Text)
                return;
            if (ctx.Update.Message.Text == "/start")
            {
                await Bot.SendTextMessageAsync(ctx.Chat, "What is your first name?");
                var firstName = await NewTextMessage(ctx.Update);
                await Bot.SendTextMessageAsync(ctx.Chat, "What is your last name?");
                var lastName = await NewTextMessage(ctx.Update);
                await Bot.SendTextMessageAsync(ctx.Chat, $"Welcome, {firstName} {lastName}!");
            }
        }
    }
```

To host this bot you can just do the following:

```csharp
    var myBot = new MyBot("MyBotToken");
    await myBot.RunAsync();
```

For additional information please visit [original repo](https://raw.githubusercontent.com/wiz0u/YourEasyBot)
