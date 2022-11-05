using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal interface IMessageStore { }

    internal sealed class MessageStore<TBody> : IMessageStore
    {
        private ConcurrentDictionary<string, Message<TBody>> _state { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Message<TBody> message)
        {
            _state[message.Key!] = message;
        }

        public IEnumerable<Message<TBody>> GetState()
        {
            foreach (var (_, message) in _state)
            {
                yield return message;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryDelete(string key, out Message<TBody> message)
        {
            return _state.TryRemove(key, out message);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Reset()
        {
            _state.Clear();
        }
    }
}