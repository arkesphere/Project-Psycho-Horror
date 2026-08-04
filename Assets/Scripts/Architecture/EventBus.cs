using System;
using System.Collections.Generic;

namespace SurvivalHorror
{
    /// <summary>
    /// Process-local publish/subscribe hub for cross-system game events.
    /// Subscribe in OnEnable and unsubscribe in OnDisable to avoid stale listeners.
    /// Keep direct references for owned, high-frequency, or physics-driven work.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new();

        public static void Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Type type = typeof(TEvent);
            if (Handlers.TryGetValue(type, out Delegate existing))
                Handlers[type] = Delegate.Combine(existing, handler);
            else
                Handlers.Add(type, handler);
        }

        public static void Unsubscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) return;

            Type type = typeof(TEvent);
            if (!Handlers.TryGetValue(type, out Delegate existing)) return;

            Delegate updated = Delegate.Remove(existing, handler);
            if (updated == null) Handlers.Remove(type);
            else Handlers[type] = updated;
        }

        public static void Publish<TEvent>(TEvent gameEvent)
        {
            if (Handlers.TryGetValue(typeof(TEvent), out Delegate existing) &&
                existing is Action<TEvent> listeners)
            {
                listeners.Invoke(gameEvent);
            }
        }

        public static void ClearAll() => Handlers.Clear();
    }
}