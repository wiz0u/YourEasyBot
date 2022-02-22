using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Wizou.EasyBot;

public enum UpdateKind
{
    None,
    NewMessage,
    EditedMessage,
    CallbackQuery,
    OtherUpdate
}

public enum MsgCategory
{
    Other,
    Text,
    MediaOrDoc,
    StickerOrDice,
    Sharing,
    ChatStatusChange,
    VoiceChat
}

public class UpdateInfo : IUpdateGetter
{
    readonly TaskInfo taskInfo;
    public string CallbackData;
    public Message Message;
    public Update Update;
    public UpdateKind UpdateKind;

    internal UpdateInfo(TaskInfo taskInfo)
    {
        this.taskInfo = taskInfo;
    }

    public MsgCategory MsgCategory => Message?.Type switch
    {
        MessageType.Text => MsgCategory.Text,

        MessageType.Photo or MessageType.Audio or MessageType.Video or MessageType.Voice or MessageType.Document
            or MessageType.VideoNote
            => MsgCategory.MediaOrDoc,

        MessageType.Sticker or MessageType.Dice
            => MsgCategory.StickerOrDice,

        MessageType.Location or MessageType.Contact or MessageType.Venue or MessageType.Game or MessageType.Invoice or
            MessageType.SuccessfulPayment or MessageType.WebsiteConnected
            => MsgCategory.Sharing,

        MessageType.ChatMembersAdded or MessageType.ChatMemberLeft or MessageType.ChatTitleChanged
            or MessageType.ChatPhotoChanged or
            MessageType.MessagePinned or MessageType.ChatPhotoDeleted or MessageType.GroupCreated
            or MessageType.SupergroupCreated or
            MessageType.ChannelCreated or MessageType.MigratedToSupergroup or MessageType.MigratedFromGroup
            => MsgCategory.ChatStatusChange,

        MessageType.VoiceChatScheduled or MessageType.VoiceChatStarted or MessageType.VoiceChatEnded
            or MessageType.VoiceChatParticipantsInvited
            => MsgCategory.VoiceChat,

        _ => MsgCategory.Other
    };

    async Task<UpdateInfo> IUpdateGetter.NextUpdate(CancellationToken cancel)
    {
        await taskInfo.semaphore.WaitAsync(cancel);
        UpdateInfo newUpdate;
        lock (taskInfo.updates)
        {
            newUpdate = taskInfo.updates.Dequeue();
        }

        return newUpdate;
    }
}