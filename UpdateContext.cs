using Telegram.Bot.Types;

namespace Wizou.EasyBot;

public class UpdateContext
{
    public Chat Chat { get; }
    public User User { get; }
    public UpdateInfo Update { get; }

    public UpdateContext(Chat chat, User user, UpdateInfo update)
    {
        Chat = chat;
        User = user;
        Update = update;
    }
}