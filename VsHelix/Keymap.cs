using System.Collections.Generic;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;
using Microsoft.VisualStudio.Text;

namespace VsHelix
{
internal sealed class Keymap
{
internal delegate bool CommandHandler(char key, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations);

private sealed class Node
{
public Dictionary<char, Node> Next { get; } = new();
public CommandHandler? Command { get; set; }
}

private readonly Node _root = new();
private Node? _pending;

public void Add(string keys, CommandHandler handler)
{
Node node = _root;
foreach (char key in keys)
{
if (!node.Next.TryGetValue(key, out var child))
{
child = new Node();
node.Next[key] = child;
}
node = child;
}
node.Command = handler;
}

public bool TryGetCommand(char key, out CommandHandler? handler)
{
handler = null;
Node node = _pending ?? _root;
if (!node.Next.TryGetValue(key, out var next))
{
_pending = null;
return false;
}

if (next.Command != null)
{
_pending = null;
handler = next.Command;
}
else
{
_pending = next;
}

return true;
}

public void Reset()
{
_pending = null;
}

public bool HasPending => _pending != null;
}
}
