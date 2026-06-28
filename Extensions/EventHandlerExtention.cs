using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace IMV.Extensions;

public static class EventHandlerExtensions
{
    public static void InvokeEx(this EventHandler? handler, object? sender, EventArgs? args = null, [CallerMemberName] string? caller = null)
    {
        var senderName = sender?.GetType().ToString() ?? "null";
        if (handler == null)
        {
            Debug.WriteLine($"{nameof(InvokeEx)} was called, but the handler was null. (sender: {senderName}, caller: {caller})");
            return;
        }

        if (args == null)
            args = EventArgs.Empty;

        foreach (EventHandler h in handler.GetInvocationList())
        {
            Debug.WriteLine($"{h.Method.Name} Invoked. (sender: {senderName}, caller: {caller})");
            h(sender, args);
        }
    }

    public static void InvokeEx<TEventArgs>(this EventHandler<TEventArgs>? handler, object? sender, TEventArgs args, [CallerMemberName] string? caller = null) where TEventArgs : EventArgs
    {
        var senderName = sender?.GetType().ToString() ?? "null";
        if (handler == null)
        {
            Debug.WriteLine($"{nameof(InvokeEx)} was called, but the handler was null. (sender: {senderName}, caller: {caller})");
            return;
        }

        foreach (EventHandler<TEventArgs> h in handler.GetInvocationList())
        {
            Debug.WriteLine($"{h.Method.Name} Invoked. (sender: {senderName}, caller: {caller})");
            h(sender, args);
        }
    }
}