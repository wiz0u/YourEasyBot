using System.Collections;

namespace Wizou.EasyBot;

public class CommandHandler
{
    public IEnumerable<string> AvailableCommands => Commands.Keys;
    public bool IsAvailableCommand(string command) => Commands.ContainsKey(command);

    public string? GetDescription(string command) => Commands[command].Description ?? null;
    public CommandHandlerFunc UnknownCommandHandler { get; set; } = (_, _) => Task.CompletedTask;
    public Func<UpdateContext, bool, Task> WrongScopeCommandHandler { get; set; } = (_, _) => Task.CompletedTask;

    // ReSharper disable once InconsistentNaming
    readonly Dictionary<string, Command> Commands = new(StringComparer.OrdinalIgnoreCase);
    public char Prefix { get; set; }

    public void Add(params Command[] commands)
    {
        foreach (var c in commands) Commands.Add(c.Name, c);
    }

    public async Task HandleCommand(UpdateContext ctx, bool isPrivateChat)
    {
        var command = ctx.Update.Message.Text!.Split(' ');
        if (isPrivateChat ? IsPrivateChat(command[0]) : IsGroupChat(command[0]))
            await (this[command[0], isPrivateChat] ?? UnknownCommandHandler)
                .Invoke(ctx, command.Skip(1).ToArray());
        else
            await WrongScopeCommandHandler.Invoke(ctx, isPrivateChat);
    }

    CommandHandlerFunc? this[string name, bool privateChat] =>
        IsAvailableCommand(name)
            ? privateChat ? Commands[name].PrivateChatHandler : Commands[name].GroupChatHandler
            : null;

    bool IsPrivateChat(string name)
        => !IsAvailableCommand(name) || Commands[name].AllowedInPrivateChats;

    bool IsGroupChat(string name)
        => !IsAvailableCommand(name) || Commands[name].AllowedInGroupChats;
}