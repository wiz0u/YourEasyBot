using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace YourEasyBot
{
	class UpdateInfo : IGetNext
	{
		public enum Kind { None, NewMessage, EditedMessage, CallbackQuery, OtherUpdate }
		public Kind UpdateKind;
		public Message Message;
		public string CallbackData;
		public Update Update;

		private readonly TaskInfo _taskInfo;
		internal UpdateInfo(TaskInfo taskInfo) => _taskInfo = taskInfo;
		async Task<UpdateInfo> IGetNext.NextUpdate(CancellationToken cancel)
		{
			await _taskInfo.Semaphore.WaitAsync(cancel);
			UpdateInfo newUpdate;
			lock (_taskInfo.Updates)
				newUpdate = _taskInfo.Updates.Dequeue();
			return newUpdate;
		}
	}
}
