using Telegram.Bot.Types;

namespace Wizou.EasyBot;

public static class TelegramExtensions
{
    public static string GetName(this User user)
    {
        return !string.IsNullOrEmpty(user.Username)
            ? "@" + user.Username
            : (user.FirstName + " " + user.LastName).TrimEnd();
    }

    public static string GetName(this Chat chat)
    {
        return !string.IsNullOrEmpty(chat.Username) ? "@" + chat.Username : chat.Title;
    }
}

internal class TaskInfo
{
    internal readonly SemaphoreSlim semaphore = new(0);
    internal readonly Queue<UpdateInfo> updates = new();
    internal Task task;
}

internal interface IUpdateGetter
{
    Task<UpdateInfo> NextUpdate(CancellationToken cancel);
}

