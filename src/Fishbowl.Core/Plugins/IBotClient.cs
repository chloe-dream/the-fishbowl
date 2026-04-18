namespace Fishbowl.Core.Plugins;

/// <summary>
/// A chat-platform client (Discord, Telegram, WhatsApp, ...).
/// One instance per platform connection.
/// </summary>
public interface IBotClient
{
    string Name { get; }
    Task SendAsync(string userId, string message, CancellationToken ct);
    IAsyncEnumerable<IncomingMessage> ReceiveAsync(CancellationToken ct);
}
