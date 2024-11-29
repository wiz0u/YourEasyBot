using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace YourEasyBot
{
	public enum UpdateKind { None, NewMessage, EditedMessage, CallbackQuery, OtherUpdate }
	public enum MsgCategory { Other, Text, MediaOrDoc, StickerOrDice, Sharing, ChatStatus, VideoChat }

	public class UpdateInfo : IGetNext
	{
		public UpdateKind UpdateKind;
		public Message Message;
		public string CallbackData;
		public Update Update;

		public MsgCategory MsgCategory => (Message?.Type) switch
		{
			MessageType.Text => MsgCategory.Text,
			MessageType.Photo or MessageType.Audio or MessageType.Video or MessageType.Voice or MessageType.Document or MessageType.VideoNote
			  => MsgCategory.MediaOrDoc,
			MessageType.Sticker or MessageType.Dice
			  => MsgCategory.StickerOrDice,
			MessageType.Location or MessageType.Contact or MessageType.Venue or MessageType.Game or MessageType.Invoice or
			MessageType.SuccessfulPayment or MessageType.ConnectedWebsite
			  => MsgCategory.Sharing,
			MessageType.NewChatMembers or MessageType.LeftChatMember or MessageType.NewChatTitle or MessageType.NewChatPhoto or
			MessageType.PinnedMessage or MessageType.DeleteChatPhoto or MessageType.GroupChatCreated or MessageType.SupergroupChatCreated or
			MessageType.ChannelChatCreated or MessageType.MigrateToChatId or MessageType.MigrateFromChatId
			  => MsgCategory.ChatStatus,
			MessageType.VideoChatScheduled or MessageType.VideoChatStarted or MessageType.VideoChatEnded or MessageType.VideoChatParticipantsInvited
			  => MsgCategory.VideoChat,
			_ => MsgCategory.Other,
		};

		private readonly TaskInfo _taskInfo;
		internal UpdateInfo(TaskInfo taskInfo) => _taskInfo = taskInfo;
		async Task<UpdateInfo> IGetNext.NextUpdate(CancellationToken cancel)
		{
			await _taskInfo.Semaphore.WaitAsync(cancel);
			UpdateInfo newUpdate;
			lock (_taskInfo)
				newUpdate = _taskInfo.Updates.Dequeue();
			return newUpdate;
		}
	}
}
