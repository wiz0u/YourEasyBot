using System.Collections;

namespace Wizou.EasyBot;

public class CommandHandler
{
    public IEnumerable<string> AvailableCommands => Commands.Keys;
    public bool IsAvailableCommand(string command) => Commands.ContainsKey(command);

    public string? GetDescription(string command) => Commands[command].Description ?? null;
    public CommandHandlerFunc UnknownCommandHandler { get; set; } = (_, _) => Task.CompletedTask;

    public Func<UpdateContext, Command, string[], Task> CommandPrehandler { get; set; } =
        (_, _, _) => Task.CompletedTask;

    public Func<UpdateContext, bool, Task> WrongScopeCommandHandler { get; set; } = (_, _) => Task.CompletedTask;

    // ReSharper disable once InconsistentNaming
    readonly Dictionary<string, Command> Commands = new(StringComparer.OrdinalIgnoreCase);
    public char Prefix { get; init; }

    public void Add(params Command[] commands)
    {
        foreach (var c in commands) Commands.Add(c.Name, c);
    }

    public async Task HandleCommand(UpdateContext ctx, bool isPrivateChat, string botName)
    {
        var msg = ctx.Update.Message.Text![1..];
        if (msg.Contains("@" + botName) && msg.IndexOf('@') <
            (msg.Contains(' ') ? msg.IndexOf(' ') : int.MaxValue))
        {
            ctx.Update.Message.Text =
                msg.Remove(ctx.Update.Message.Text!.IndexOf('@'), botName.Length + 1);
        }

        var commandRaw = ctx.Update.Message.Text!.Split(' ');
        if (!Commands.TryGetValue(commandRaw[0], out var command))
        {
            await UnknownCommandHandler(ctx, commandRaw.Skip(1).ToArray());
            return;
        }

        if (command.NeedsPrehandling)
            await CommandPrehandler(ctx, Commands[commandRaw[0]], commandRaw.Skip(1).ToArray());
        if (isPrivateChat ? command.AllowedInPrivateChats : command.AllowedInGroupChats)
            await GetHandler(commandRaw[0], isPrivateChat)!(ctx, commandRaw.Skip(1).ToArray());
        else
            await WrongScopeCommandHandler.Invoke(ctx, isPrivateChat);
    }

    CommandHandlerFunc? GetHandler(string name, bool privateChat) =>
        IsAvailableCommand(name)
            ? privateChat ? Commands[name].PrivateChatHandler : Commands[name].GroupChatHandler
            : null;

    bool IsPrivateChat(string name)
        => !IsAvailableCommand(name) || Commands[name].AllowedInPrivateChats;

    bool IsGroupChat(string name)
        => !IsAvailableCommand(name) || Commands[name].AllowedInGroupChats;
}