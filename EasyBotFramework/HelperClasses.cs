using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace YourEasyBot
{
	public static class Helpers
	{
		public static string Name(this User user) => !string.IsNullOrEmpty(user.Username) ? "@" + user.Username : (user.FirstName + " " + user.LastName).TrimEnd();
		public static string Name(this Chat chat) => !string.IsNullOrEmpty(chat.Username) ? "@" + chat.Username : chat.Title;
	}

	internal class TaskInfo
	{
		internal readonly SemaphoreSlim Semaphore = new(0);
		internal readonly Queue<UpdateInfo> Updates = new();
		internal Task Task;
	}

	internal interface IGetNext
	{
		Task<UpdateInfo> NextUpdate(CancellationToken cancel);
	}
}
