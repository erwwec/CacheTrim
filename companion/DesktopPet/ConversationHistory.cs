using System;
using System.Collections.Generic;

namespace DesktopPet;

internal sealed record ConversationMessage(string Role, string Text);

// Optional, in-memory only, bounded plain-text history; tool results are never stored.
internal sealed class ConversationHistory
{
    internal const int MaxTurns = 4;
    private readonly List<ConversationMessage> _messages = new();
    internal IReadOnlyList<ConversationMessage> Snapshot() => _messages.ToArray();
    internal void Clear() => _messages.Clear();

    internal void AddTurn(string user, string assistant)
    {
        if (string.IsNullOrWhiteSpace(user) || user.Length > 1000 ||
            string.IsNullOrWhiteSpace(assistant) || assistant.Length > 800)
            throw new ArgumentException("Недопустимый текст диалога.");
        _messages.Add(new ConversationMessage("user", user));
        _messages.Add(new ConversationMessage("assistant", assistant));
        while (_messages.Count > MaxTurns * 2) _messages.RemoveRange(0, 2);
    }
}