using Telegram.Bot.Types;

namespace Wizou.EasyBot;

/// <summary>
///     Extension Methods
/// </summary>
public static class TelegramExtensions
{
	/// <summary>
	///     Gets the user's name
	/// </summary>
	/// <param name="user"></param>
	/// <returns>
	///     <see cref="User.Username" /> if it is not <see langword="null" />; <see cref="User.FirstName" /> +
	///     <see cref="User.LastName" /> Otherwise
	/// </returns>
	public static string GetName(this User user)
		=> !string.IsNullOrEmpty(user.Username) ? "@" + user.Username : (user.FirstName + " " + user.LastName).TrimEnd();
}

class TaskInfo
{
	internal readonly SemaphoreSlim _semaphore = new(0);
	internal readonly Queue<UpdateInfo> _updates = new();
	internal Task? _task;
}

interface IUpdateGetter
{
	Task<UpdateInfo> NextUpdate(CancellationToken cancel);
}