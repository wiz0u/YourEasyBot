using Telegram.Bot.Types;

namespace Wizou.EasyBot;

/// <summary>
///     Context of the update.
/// </summary>
public class UpdateContext
{
    /// <summary></summary>
    /// <param name="chat">
    ///     <see cref="Chat" />
    /// </param>
    /// <param name="user">
    ///     <see cref="User" />
    /// </param>
    /// <param name="update">
    ///     <see cref="Update" />
    /// </param>
    public UpdateContext(Chat chat, User user, UpdateInfo update)
	{
		Chat = chat;
		User = user;
		Update = update;
	}

    /// <summary>
    ///     Chat where the event occured
    /// </summary>
    public Chat Chat { get; }

    /// <summary>
    ///     User who triggered the event
    /// </summary>
    public User User { get; }

    /// <summary>
    ///     Information about update
    /// </summary>
    public UpdateInfo Update { get; }
}