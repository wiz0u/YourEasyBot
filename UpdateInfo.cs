using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Wizou.EasyBot;

/// <summary>
/// Shows the exact kind of update
/// </summary>
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
    VideoChat
}

/// <summary>
/// Update Information
/// </summary>
public class UpdateInfo : IUpdateGetter
{
    readonly TaskInfo _taskInfo;
    /// <summary>
    /// CallBack Data
    /// </summary>
    public string CallbackData = null!;

    /// <inheritdoc cref="Telegram.Bot.Types.Message"/>
    public Message Message = null!;
    /// <inheritdoc cref="Telegram.Bot.Types.Update"/>
    public Update Update = null!;
    /// <inheritdoc cref="Wizou.EasyBot.UpdateKind"/>
    public UpdateKind UpdateKind;

    internal UpdateInfo(TaskInfo taskInfo) => _taskInfo = taskInfo;

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

        MessageType.VideoChatScheduled or MessageType.VideoChatStarted or MessageType.VideoChatEnded
            or MessageType.VideoChatParticipantsInvited
            => MsgCategory.VideoChat,

        _ => MsgCategory.Other
    };

    async Task<UpdateInfo> IUpdateGetter.NextUpdate(CancellationToken cancel)
    {
        await _taskInfo.semaphore.WaitAsync(cancel);
        UpdateInfo newUpdate;
        lock (_taskInfo.updates)
        {
            newUpdate = _taskInfo.updates.Dequeue();
        }

        return newUpdate;
    }
}