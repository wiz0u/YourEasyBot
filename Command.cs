//global using CommandHandlerFunc = System.Func<Wizou.EasyBot.UpdateContext, string[], System.Threading.Tasks.Task>;

// ReSharper disable MemberCanBePrivate.Global

namespace Wizou.EasyBot;

/// <summary>
///     Represents a telegram bot command
/// </summary>
public class Command
{
	/// <summary>
	///     Delegate for general command handlers
	/// </summary>
	public delegate Task HandlerDelegate(UpdateContext ctx, string[] args);

	/// <summary>
	/// </summary>
	/// <param name="name">Command name</param>
	/// <param name="privateHandler">Delegate to handle command in private chat</param>
	/// <param name="groupHandler">Delegate to handle command in group chat</param>
	/// <param name="needsPreExecutionHandling">
	///     Boolean to show if command needs to be handled on
	///     <see cref="CommandHandler.OnCommandExecution" /> event (optional)
	/// </param>
	/// <param name="description">Command description</param>
	/// <exception cref="ArgumentException">
	///     When <paramref name="groupHandler" /> and <paramref name="privateHandler" /> are
	///     both null
	/// </exception>
	/// <exception cref="ArgumentException">When command name is null, empty, or contains whitespaces </exception>
	public Command(string name, HandlerDelegate? privateHandler = null, HandlerDelegate? groupHandler = null, bool needsPreExecutionHandling = false, string? description = null)
	{
		var allowedInGroupChats = groupHandler is not null;
		var allowedInPrivateChats = privateHandler is not null;
		if (!allowedInGroupChats && !allowedInPrivateChats)
			throw new ArgumentException("Command must have at least one working scope (private chat or/and group chat)");
		if (string.IsNullOrWhiteSpace(name) || name.Any(char.IsWhiteSpace))
			throw new ArgumentException("Command name cannot be null, empty, or contain whitespaces");
		GroupChatHandler = (groupHandler ?? privateHandler) ?? throw new ArgumentException("Both private and group chat handlers cannot be null");
		Name = name;
		Description = description;
		AllowedInGroupChats = allowedInGroupChats;
		AllowedInPrivateChats = allowedInPrivateChats;
		PrivateChatHandler = privateHandler;
		NeedsPreExecutionHandling = needsPreExecutionHandling;
	}

	/// <summary>
	///     Gets command name
	/// </summary>
	public string Name { get; internal set; }

	/// <summary>
	///     Gets command description
	/// </summary>
	public string? Description { get; }

	/// <summary>
	///     Indicates if the command is allowed in Group chats
	/// </summary>
	public bool AllowedInGroupChats { get; }

	/// <summary>
	///     Indicates if the command is allowed in Private chats
	/// </summary>
	public bool AllowedInPrivateChats { get; }

	/// <summary>
	///     A handler for a command if it is called in a Private chat
	/// </summary>
	public HandlerDelegate? PrivateChatHandler { get; }

	/// <summary>
	///     A handler for a command if it is called in a Group chat
	/// </summary>
	public HandlerDelegate? GroupChatHandler { get; }

	/// <summary>
	///     Optional property to show if command needs to be handled on <see cref="CommandHandler.OnCommandExecution" /> event
	/// </summary>
	public bool NeedsPreExecutionHandling { get; init; }
}