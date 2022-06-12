using System.Collections.Concurrent;

namespace Berberis.Messaging;

partial class CrossBar
{
    internal interface IMessageStore { }

    internal sealed class MessageStore<TBody> : IMessageStore
    {
        private ConcurrentDictionary<string, Message<TBody>> _state { get; } = new();

        public void Update(Message<TBody> message)
        {
            _state[message.Key] = message;
        }

        public IEnumerable<Message<TBody>> GetState()
        {
            foreach (var (_, message) in _state)
            {
                yield return message;
            }
        }

        internal bool TryDelete(string key, out Message<TBody> message)
        {
            return _state.TryRemove(key, out message);
        }

        internal void Reset()
        {
            _state.Clear();
        }
    }
}