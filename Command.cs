global using CommandHandlerFunc = System.Func<Wizou.EasyBot.UpdateContext, string[], System.Threading.Tasks.Task>;

// ReSharper disable MemberCanBePrivate.Global

namespace Wizou.EasyBot;

public class Command
{
    public string Name { get; }
    public string? Description { get; }
    public bool AllowedInGroupChats { get; }
    public bool AllowedInPrivateChats { get; }
    public CommandHandlerFunc? PrivateChatHandler { get; }
    public CommandHandlerFunc? GroupChatHandler { get; }

    public Command(string name, CommandHandlerFunc? privateHandler = null, CommandHandlerFunc? groupHandler = null,
        bool allowedInGroupChats = false,
        bool allowedInPrivateChats = true, string? description = null)
    {
        if (!allowedInGroupChats && !allowedInPrivateChats)
            throw new ArgumentException(
                "Command must have at least one working scope (private chat or/and group chat)");
        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace))
            throw new ArgumentException(
                "Command name cannot be null, empty or contain whitespaces");
        GroupChatHandler = (groupHandler ?? privateHandler) ??
                           throw new ArgumentException("Both private and group chat handlers cannot be null");
        Name = name;
        Description = description;
        AllowedInGroupChats = allowedInGroupChats;
        AllowedInPrivateChats = allowedInPrivateChats;
        PrivateChatHandler = privateHandler;
    }
}