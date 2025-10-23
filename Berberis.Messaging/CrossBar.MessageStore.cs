using System.Runtime.CompilerServices;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal interface IMessageStore { }

    internal sealed class MessageStore<TBody> : IMessageStore
    {
        private Dictionary<string, Message<TBody>> _state { get; } = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(Message<TBody> message)
        {
            lock (_state)
            {
                _state[message.Key!] = message;
            }
        }

        public IEnumerable<Message<TBody>> GetState()
        {
            List<Message<TBody>> state;

            lock (_state)
            {
                state = new List<Message<TBody>>(_state.Values);
            }

            return state;
        }

        public bool TryGet(string key, out Message<TBody> message)
        {
            lock (_state)
            {
                return _state.TryGetValue(key, out message);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryDelete(string key)
        {
            bool removed = false;
            lock (_state)
            {
                removed = _state.Remove(key);
            }

            return removed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Reset()
        {
            lock (_state)
            {
                _state.Clear();
            }
        }
    }
}
