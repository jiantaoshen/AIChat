// This singleton keyed gate serializes chat turns that target the same persisted conversation without blocking unrelated conversations.
namespace AiAvatar.Backend.Services.Concurrency;

public sealed class ConversationTurnGate
{
    private readonly object _sync = new();
    private readonly Dictionary<TurnGateKey, GateEntry> _entries = [];

    public async ValueTask<IDisposable> AcquireAsync(
        Guid? conversationId,
        Guid turnId,
        CancellationToken cancellationToken)
    {
        var key = conversationId is Guid persistedConversationId
            ? TurnGateKey.ForConversation(persistedConversationId)
            : TurnGateKey.ForNewTurn(turnId);

        GateEntry entry;
        lock (_sync)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new GateEntry();
                _entries.Add(key, entry);
            }

            entry.ReferenceCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new Releaser(this, key, entry);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }
    }

    private void Release(TurnGateKey key, GateEntry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(TurnGateKey key, GateEntry entry)
    {
        var dispose = false;

        lock (_sync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0 &&
                _entries.TryGetValue(key, out var current) &&
                ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                dispose = true;
            }
        }

        if (dispose)
        {
            entry.Semaphore.Dispose();
        }
    }

    private sealed class GateEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(initialCount: 1, maxCount: 1);

        public int ReferenceCount { get; set; }
    }

    private readonly record struct TurnGateKey(TurnGateKeyKind Kind, Guid Id)
    {
        public static TurnGateKey ForConversation(Guid conversationId) =>
            new(TurnGateKeyKind.Conversation, conversationId);

        public static TurnGateKey ForNewTurn(Guid turnId) =>
            new(TurnGateKeyKind.NewTurn, turnId);
    }

    private enum TurnGateKeyKind
    {
        Conversation,
        NewTurn,
    }

    private sealed class Releaser(
        ConversationTurnGate owner,
        TurnGateKey key,
        GateEntry entry) : IDisposable
    {
        private ConversationTurnGate? _owner = owner;

        public void Dispose()
        {
            var currentOwner = Interlocked.Exchange(ref _owner, null);
            currentOwner?.Release(key, entry);
        }
    }
}
