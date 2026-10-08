using System.Text.Json;
using AiAvatar.Backend.Models;
using AiAvatar.Backend.Options;
using AiAvatar.Backend.Services.Ollama;

namespace AiAvatar.Backend.Tests;

public sealed class OllamaRequestFactoryTests
{
    [Fact]
    public async Task CreateChatRequest_DoesNotApplyASecondConversationWindow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var factory = new OllamaRequestFactory(
            Microsoft.Extensions.Options.Options.Create(new OllamaOptions()),
            Microsoft.Extensions.Options.Options.Create(new CharacterOptions()));
        var messages = Enumerable.Range(1, 13)
            .Select(index => new ChatMessage(
                index % 2 == 1 ? "user" : "assistant",
                $"message {index}"))
            .ToArray();

        using var request = factory.CreateChatRequest(messages);
        await using var content = await request.Content!.ReadAsStreamAsync(cancellationToken);
        using var payload = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
        var serializedMessages = payload.RootElement.GetProperty("messages");

        // One system message plus every already-bounded conversation message supplied by the store.
        Assert.Equal(messages.Length + 1, serializedMessages.GetArrayLength());
        Assert.Equal("message 1", serializedMessages[1].GetProperty("content").GetString());
        Assert.Equal("message 13", serializedMessages[serializedMessages.GetArrayLength() - 1].GetProperty("content").GetString());
    }
}
