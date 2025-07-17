using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
internal sealed class VisualMode : IInputMode
{
internal static VisualMode? Instance { get; private set; }

private readonly Keymap _keymap;
private int _pendingCount = 0;

public VisualMode()
{
Instance = this;
_keymap = new Keymap();

var movementCommands = new Dictionary<char, System.Action<ITextView, ISelectionTransformer>>
{
['h'] = (view, sel) =>
{
var active = sel.Selection.ActivePoint;
var line = active.Position.GetContainingLine();
if (active.Position > line.Start)
{
var prev = active.Position - 1;
sel.MoveTo(new VirtualSnapshotPoint(view.TextSnapshot, prev), true, PositionAffinity.Predecessor);
}
},
['l'] = (view, sel) =>
{
var active = sel.Selection.ActivePoint;
var line = active.Position.GetContainingLine();
if (active.Position < line.End)
{
var next = active.Position + 1;
sel.MoveTo(new VirtualSnapshotPoint(view.TextSnapshot, next), true, PositionAffinity.Successor);
}
},
['j'] = (view, sel) =>
{
var active = sel.Selection.ActivePoint;
var line = active.Position.GetContainingLine();
var snapshot = view.TextSnapshot;
if (line.LineNumber + 1 < snapshot.LineCount)
{
var nextLine = snapshot.GetLineFromLineNumber(line.LineNumber + 1);
int tabSize = view.Options.GetTabSize();
int posInLine = active.Position - line.Start.Position;
string textToPos = line.GetText().Substring(0, posInLine);
int expanded = SelectionUtils.CalculateExpandedOffset(textToPos, tabSize);
expanded += active.VirtualSpaces;
var target = SelectionUtils.CreatePointAtVisualOffset(nextLine, expanded, tabSize);
sel.MoveTo(target, true);
}
},
['k'] = (view, sel) =>
{
var active = sel.Selection.ActivePoint;
var line = active.Position.GetContainingLine();
var snapshot = view.TextSnapshot;
if (line.LineNumber > 0)
{
var prevLine = snapshot.GetLineFromLineNumber(line.LineNumber - 1);
int tabSize = view.Options.GetTabSize();
int posInLine = active.Position - line.Start.Position;
string textToPos = line.GetText().Substring(0, posInLine);
int expanded = SelectionUtils.CalculateExpandedOffset(textToPos, tabSize);
expanded += active.VirtualSpaces;
var target = SelectionUtils.CreatePointAtVisualOffset(prevLine, expanded, tabSize);
sel.MoveTo(target, true);
}
},
['w'] = (view, sel) => sel.PerformAction(PredefinedSelectionTransformations.SelectToNextSubWord),
['W'] = (view, sel) => sel.PerformAction(PredefinedSelectionTransformations.SelectToNextWord),
['b'] = (view, sel) => sel.PerformAction(PredefinedSelectionTransformations.SelectToPreviousSubWord),
['B'] = (view, sel) => sel.PerformAction(PredefinedSelectionTransformations.SelectToPreviousWord)
};

foreach (var kvp in movementCommands)
{
_keymap.Add(kvp.Key.ToString(), (c, view, broker, ops) =>
{
broker.PerformActionOnAllSelections(sel => kvp.Value(view, sel));
return true;
});
}

_keymap.Add("y", (c, view, broker, ops) =>
{
SelectionUtils.YankSelections(view, broker);
ModeManager.Instance.EnterNormal(view, broker);
return true;
});
_keymap.Add("d", (c, view, broker, ops) =>
{
ExecuteDeleteCommand(view, broker, false);
ModeManager.Instance.EnterNormal(view, broker);
return true;
});
_keymap.Add("c", (c, view, broker, ops) =>
{
ExecuteDeleteCommand(view, broker, true);
return true;
});

_keymap.Add("x", (c, view, broker, ops) =>
{
broker.PerformActionOnAllSelections(sel => SelectionUtils.ExtendSelectionLinewise(sel, view));
return true;
});

_keymap.Add("v", (c, view, broker, ops) =>
{
ModeManager.Instance.EnterNormal(view, broker);
return true;
});
}

public bool HandleChar(char c, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
{
if (char.IsDigit(c) && !_keymap.HasPending)
{
int digit = c - '0';
_pendingCount = (_pendingCount * 10) + digit;
return true;
}

if (_keymap.TryGetCommand(c, out var handler))
{
if (handler != null)
{
int count = _pendingCount > 0 ? _pendingCount : 1;
_pendingCount = 0;

bool result = true;
for (int i = 0; i < count; i++)
{
if (ModeManager.Instance.Current != ModeManager.EditorMode.Visual)
break;

result &= handler(c, view, broker, operations);
}
return result;
}
return true;
}

_pendingCount = 0;
_keymap.Reset();
return true;
}

private bool ExecuteDeleteCommand(ITextView view, IMultiSelectionBroker broker, bool switchToInsert)
{
bool altDown = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0;
if (!altDown)
{
SelectionUtils.YankSelections(view, broker);
}

SelectionUtils.DeleteSelections(view, broker);

if (switchToInsert)
{
ModeManager.Instance.EnterInsert(view, broker);
}
return true;
}

internal void Reset()
{
_keymap.Reset();
_pendingCount = 0;
}
}
}
