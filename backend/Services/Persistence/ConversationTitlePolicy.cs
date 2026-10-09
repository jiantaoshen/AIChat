namespace AiAvatar.Backend.Services.Persistence;

internal static class ConversationTitlePolicy
{
    private const int MaxConversationTitleLength = 80;

    public static string FromFirstUserMessage(string firstUserMessage)
    {
        var title = firstUserMessage
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ')
            .Trim();

        while (title.Contains("  ", StringComparison.Ordinal))
        {
            title = title.Replace("  ", " ", StringComparison.Ordinal);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return "New conversation";
        }

        return title.Length <= MaxConversationTitleLength
            ? title
            : $"{title[..(MaxConversationTitleLength - 1)]}…";
    }
}
