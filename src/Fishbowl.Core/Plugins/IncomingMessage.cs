namespace Fishbowl.Core.Plugins;

public record IncomingMessage(string UserId, string Text, DateTime ReceivedAt);
