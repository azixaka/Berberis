using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal interface IMessageStore { }

    internal sealed class MessageStore<TBody> : IMessageStore
    {
        private readonly ConcurrentDictionary<string, Message<TBody>> _state = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Message<TBody> message)
        {
            // Lock-free update using ConcurrentDictionary
            // No allocations on hot path - just stores reference
            _state[message.Key!] = message;
        }

        public IEnumerable<Message<TBody>> GetState()
        {
            // Return snapshot as array
            // ToArray() is thread-safe with ConcurrentDictionary
            // This allocates, but GetState is not a hot path
            return _state.Values.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(string key, out Message<TBody> message)
        {
            // Lock-free read, no allocations
            return _state.TryGetValue(key, out message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryDelete(string key)
        {
            // Lock-free delete, no allocations
            return _state.TryRemove(key, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Reset()
        {
            // Thread-safe clear, no allocations
            _state.Clear();
        }
    }
}