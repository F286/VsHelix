using System;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Formatting;

namespace VsHelix
{
internal sealed class GotoMode : IInputMode
{
private readonly Keymap _keymap;
private int _pendingNumber;

internal GotoMode()
{
_keymap = new Keymap();

_keymap.Add("g", (c, v, b, o) => { GoToLine(v, b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("e", (c, v, b, o) => { GoToEndOfFile(v, b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("f", (c, v, b, o) => { GoToFile(v, b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("h", (c, v, b, o) => { GoToLineStart(v, b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("l", (c, v, b, o) => { GoToLineEnd(v, b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("s", (c, v, b, o) => { GoToFirstNonWhitespace(v, b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("t", (c, v, b, o) => { GoToTop(v); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("c", (c, v, b, o) => { GoToMiddle(v); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("b", (c, v, b, o) => { GoToBottom(v); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("d", (c, v, b, o) => { ExecuteCommand("Edit.GoToDefinition"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("y", (c, v, b, o) => { ExecuteCommand("Edit.GoToTypeDefinition"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("r", (c, v, b, o) => { ExecuteCommand("Edit.GoToAllReferences"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("i", (c, v, b, o) => { ExecuteCommand("Edit.GoToImplementation"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("a", (c, v, b, o) => { ExecuteCommand("View.NavigateBackward"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("m", (c, v, b, o) => { ExecuteCommand("View.NavigateForward"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("n", (c, v, b, o) => { ExecuteCommand("Window.NextDocumentWindow"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("p", (c, v, b, o) => { ExecuteCommand("Window.PreviousDocumentWindow"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add(".", (c, v, b, o) => { ExecuteCommand("Edit.GoToLastEditLocation"); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("j", (c, v, b, o) => { MoveDown(b); ModeManager.Instance.EnterNormal(v, b); return true; });
_keymap.Add("k", (c, v, b, o) => { MoveUp(b); ModeManager.Instance.EnterNormal(v, b); return true; });
}

public bool HandleChar(char ch, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
{
if (ch == '\b')
{
_pendingNumber /= 10;
StatusBarHelper.ShowMode(ModeManager.EditorMode.Goto, _pendingNumber > 0 ? _pendingNumber.ToString() : string.Empty);
return true;
}

if (char.IsDigit(ch) && !_keymap.HasPending)
{
_pendingNumber = (_pendingNumber * 10) + (ch - '0');
StatusBarHelper.ShowMode(ModeManager.EditorMode.Goto, _pendingNumber.ToString());
return true;
}

if (_keymap.TryGetCommand(ch, out var handler))
{
if (handler != null)
{
var result = handler(ch, view, broker, operations);
_pendingNumber = 0;
_keymap.Reset();
return result;
}
return true;
}

_pendingNumber = 0;
_keymap.Reset();
ModeManager.Instance.EnterNormal(view, broker);
return true;
}

private void GoToLine(ITextView view, IMultiSelectionBroker broker)
{
var snapshot = view.TextSnapshot;
int lineNum = _pendingNumber > 0 ? _pendingNumber - 1 : 0;
_pendingNumber = 0;
if (lineNum < 0) lineNum = 0;
if (lineNum >= snapshot.LineCount) lineNum = snapshot.LineCount - 1;
var line = snapshot.GetLineFromLineNumber(lineNum);
broker.PerformActionOnAllSelections(sel => sel.MoveTo(new VirtualSnapshotPoint(line.Start), false, PositionAffinity.Successor));
view.Caret.EnsureVisible();
}

private void GoToEndOfFile(ITextView view, IMultiSelectionBroker broker)
{
var snapshot = view.TextSnapshot;
var point = new SnapshotPoint(snapshot, snapshot.Length);
broker.PerformActionOnAllSelections(sel => sel.MoveTo(new VirtualSnapshotPoint(point), false, PositionAffinity.Predecessor));
view.Caret.EnsureVisible();
}

private void GoToFile(ITextView view, IMultiSelectionBroker broker)
{
foreach (var sel in broker.AllSelections)
{
var span = new SnapshotSpan(sel.Start.Position, sel.End.Position);
string text = span.GetText();
if (File.Exists(text))
{
VsShellUtilities.OpenDocument(ServiceProvider.GlobalProvider, text);
}
}
}

private void GoToLineStart(ITextView view, IMultiSelectionBroker broker)
{
broker.PerformActionOnAllSelections(sel =>
{
var line = sel.Selection.ActivePoint.Position.GetContainingLine();
sel.MoveTo(new VirtualSnapshotPoint(line.Start), false, PositionAffinity.Successor);
});
}

private void GoToLineEnd(ITextView view, IMultiSelectionBroker broker)
{
broker.PerformActionOnAllSelections(sel =>
{
var line = sel.Selection.ActivePoint.Position.GetContainingLine();
sel.MoveTo(new VirtualSnapshotPoint(line.End), false, PositionAffinity.Predecessor);
});
}

private void GoToFirstNonWhitespace(ITextView view, IMultiSelectionBroker broker)
{
broker.PerformActionOnAllSelections(sel =>
{
var line = sel.Selection.ActivePoint.Position.GetContainingLine();
string text = line.GetText();
int offset = 0;
while (offset < text.Length && char.IsWhiteSpace(text[offset])) offset++;
sel.MoveTo(new VirtualSnapshotPoint(new SnapshotPoint(line.Snapshot, line.Start.Position + offset)), false, PositionAffinity.Successor);
});
}

private void GoToTop(ITextView view)
{
var first = view.TextViewLines.FirstVisibleLine;
view.Caret.MoveTo(first.Start);
view.Caret.EnsureVisible();
}

private void GoToMiddle(ITextView view)
{
var lines = view.TextViewLines;
int idx = lines.Count / 2;
var line = lines[idx];
view.Caret.MoveTo(line.Start);
view.Caret.EnsureVisible();
}

private void GoToBottom(ITextView view)
{
var last = view.TextViewLines.LastVisibleLine;
view.Caret.MoveTo(last.Start);
view.Caret.EnsureVisible();
}

private static void ExecuteCommand(string command)
{
ThreadHelper.ThrowIfNotOnUIThread();
var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
if (dte == null)
return;

try
{
var cmd = dte.Commands.Item(command);
if (cmd != null && cmd.IsAvailable)
dte.ExecuteCommand(command);
}
catch (Exception)
{
// Ignore unavailable commands
}
}

private static void MoveDown(IMultiSelectionBroker broker)
{
broker.PerformActionOnAllSelections(sel => sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine));
}

private static void MoveUp(IMultiSelectionBroker broker)
{
broker.PerformActionOnAllSelections(sel => sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine));
}
}
}
