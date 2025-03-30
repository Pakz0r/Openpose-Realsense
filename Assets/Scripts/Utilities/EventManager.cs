using System;
using System.Collections.Generic;

public static class EventManager
{
    private static Dictionary<string, Action<object>> eventDictionary = new();

    public static void Subscribe(string eventName, Action<object> listener)
    {
        if (!eventDictionary.ContainsKey(eventName))
            eventDictionary[eventName] = delegate { };

        eventDictionary[eventName] += listener;
    }

    public static void Unsubscribe(string eventName, Action<object> listener)
    {
        if (eventDictionary.ContainsKey(eventName))
            eventDictionary[eventName] -= listener;
    }

    public static void TriggerEvent(string eventName, object param = null)
    {
        if (eventDictionary.ContainsKey(eventName))
            eventDictionary[eventName].Invoke(param);
    }
}
