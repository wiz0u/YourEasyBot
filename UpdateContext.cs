using Telegram.Bot.Types;

namespace Wizou.EasyBot;

public class UpdateContext
{
    public Chat Chat { get; set; }
    public User User { get; set; }
    public UpdateInfo Update { get; set; }

    public UpdateContext(Chat chat, User user, UpdateInfo update)
    {
        Chat = chat;
        User = user;
        Update = update;
    }
}