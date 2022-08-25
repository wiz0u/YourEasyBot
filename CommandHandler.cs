namespace Wizou.EasyBot;

/// <summary>
///     Represents an object that handles commands
/// </summary>
public class CommandHandler
{
	Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	///     Constructor that sets the commands prefix
	/// </summary>
	/// <param name="prefix">Command's prefix</param>
	public CommandHandler(char prefix)
	{
		Prefix = prefix;
	}

	/// <summary>
	///     List of available commands
	/// </summary>
	public IEnumerable<string> AvailableCommands => _commands.Keys;

	/// <summary>
	///     Commands prefix
	/// </summary>
	public char Prefix { get; }

	/// <summary>
	///     Event that is fired when a unknown command is received
	/// </summary>
	public event Command.HandlerDelegate OnUnknownCommand = (_, _) => Task.CompletedTask;

	/// <summary>
	///     Event that is fired when a command is called in wrong scope
	/// </summary>
	public event Func<UpdateContext, Command, bool, Task> OnWrongScopeCommand = (_, _, _) => Task.CompletedTask;

	/// <summary>
	///     Event that is fired before command execution<br />
	///     Note:<br />You can set for your commands <see cref="Command.NeedsPreExecutionHandling" /> property to indicate
	///     weather you need to handle the command on this event or not
	/// </summary>
	public event Func<UpdateContext, Command, string[], Task> OnCommandExecution = (_, _, _) => Task.CompletedTask;

	/// <summary>
	///     Shows if command with name <paramref name="cmdName" /> is available
	/// </summary>
	/// <param name="cmdName">Name of the command</param>
	/// <returns> <see langword="True" /> if command is defined; Otherwise <see langword="False" /></returns>
	public bool IsAvailableCommand(string cmdName)
		=> _commands.ContainsKey(cmdName);

	/// <summary>
	///     Gets the description of the command with name <paramref name="cmdName" />
	/// </summary>
	/// <param name="cmdName">Name of the command</param>
	/// <returns>Command Description if found; Otherwise <see langword="null" /></returns>
	/// <exception cref="ArgumentException">If command with name <paramref name="cmdName" /> does not exist</exception>
	public string? GetDescription(string cmdName)
	{
		return IsAvailableCommand(cmdName)
			? _commands[cmdName].Description
			: throw new ArgumentException($"No such command : {cmdName}", nameof(cmdName));
	}

	/// <summary>
	///     Sets the initial command set for the bot
	/// </summary>
	/// <param name="commands"></param>
	/// <exception cref="InvalidOperationException">When commands are already set</exception>
	public void SetCommands(params Command[] commands)
	{
		if (_commands.Any())
			throw new InvalidOperationException("Commands are already set");
		_commands = commands.Select(c =>
		{
			c.Name = EnsureSinglePrefixInName(c.Name);
			return c;
		}).ToDictionary(c => c.Name, c => c);
	}

	internal async Task HandleCommand(UpdateContext ctx, bool isPrivateChat, string botName)
	{
		var commandRaw = ctx.Update.Message.Text!.Split(' ');
		var commandArgs = commandRaw.Skip(1).ToArray();
		var commandName = commandRaw[0].Contains($"@{botName}")
			? commandRaw[0].Replace($"@{botName}", string.Empty)
			: commandRaw[0];

		if (!_commands.TryGetValue(commandName, out var command))
		{
			await OnUnknownCommand(ctx, commandArgs);
			return;
		}

		await OnCommandExecution(ctx, command, commandArgs);

		if (isPrivateChat ? command.AllowedInPrivateChats : command.AllowedInGroupChats)
			await GetHandler(commandName, isPrivateChat).Invoke(ctx, commandArgs);
		else
			await OnWrongScopeCommand.Invoke(ctx, command, isPrivateChat);
	}

	Command.HandlerDelegate GetHandler(string name, bool privateChat)
		=> (privateChat ? _commands[name].PrivateChatHandler : _commands[name].GroupChatHandler) ??
		   throw new ArgumentException("Command not found", nameof(name));

	/// <summary>
	///     Indicates if a command is allowed in private chats
	/// </summary>
	/// <param name="name">command name</param>
	/// <returns><see langword="True" /> if allowed in private chats; <see langword="False" /> otherwise</returns>
	public bool IsPrivateChatCommand(string name)
		=> !IsAvailableCommand(name) || _commands[name].AllowedInPrivateChats;

	/// <summary>
	///     Indicates if a command is allowed in Group chats
	/// </summary>
	/// <param name="name">command name</param>
	/// <returns><see langword="True" /> if allowed in Group chats; <see langword="False" /> otherwise</returns>
	public bool IsGroupChatCommand(string name)
		=> !IsAvailableCommand(name) || _commands[name].AllowedInGroupChats;

	string EnsureSinglePrefixInName(string name)
		=> $"{Prefix}{name.TrimStart(Prefix)}";
}