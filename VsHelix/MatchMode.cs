using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
internal sealed class MatchMode : IInputMode
{
private readonly ITextView _view;
private readonly IMultiSelectionBroker _broker;

private enum MatchState { AwaitCommand, Surround, ReplaceFirst, ReplaceSecond, Delete, Around, Inside }
private MatchState _state = MatchState.AwaitCommand;
private char _replaceFrom;

private readonly struct BracketPair
{
public readonly char Open;
public readonly char Close;
public BracketPair(char open, char close)
{
Open = open;
Close = close;
}
}

private static readonly IReadOnlyDictionary<char, BracketPair> _pairs = new Dictionary<char, BracketPair>
{
['('] = new BracketPair('(', ')'),
['['] = new BracketPair('[', ']'),
['{'] = new BracketPair('{', '}'),
['<'] = new BracketPair('<', '>'),
['"'] = new BracketPair('"', '"'),
['\''] = new BracketPair('\'', '\'')
};

public MatchMode(ITextView view, IMultiSelectionBroker broker)
{
_view = view;
_broker = broker;
StatusBarHelper.ShowMode(ModeManager.EditorMode.Match);
}

public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
{
char ch = args.TypedChar;
if (char.IsControl(ch))
return true;

return _state switch
{
MatchState.AwaitCommand => HandleCommand(ch),
MatchState.Surround => ExitAfter(() => SurroundSelections(ch)),
MatchState.ReplaceFirst => SetReplaceFrom(ch),
MatchState.ReplaceSecond => ExitAfter(() => ReplaceSurround(_replaceFrom, ch)),
MatchState.Delete => ExitAfter(() => DeleteSurround(ch)),
MatchState.Around => ExitAfter(() => SelectTextObject(ch, true)),
MatchState.Inside => ExitAfter(() => SelectTextObject(ch, false)),
_ => Exit()
};
}

private bool HandleCommand(char ch)
{
switch (ch)
{
case 'm':
_broker.PerformActionOnAllSelections(sel => GoToMatchingBracket(sel));
return Exit();
case 's':
_state = MatchState.Surround;
return true;
case 'r':
_state = MatchState.ReplaceFirst;
return true;
case 'd':
_state = MatchState.Delete;
return true;
case 'a':
_state = MatchState.Around;
return true;
case 'i':
_state = MatchState.Inside;
return true;
default:
return Exit();
}
}

private bool SetReplaceFrom(char ch)
{
_replaceFrom = ch;
_state = MatchState.ReplaceSecond;
return true;
}

private bool ExitAfter(System.Action action)
{
action();
return Exit();
}

private bool Exit()
{
ModeManager.Instance.EnterNormal(_view, _broker);
return true;
}

private static bool TryGetPair(char ch, out BracketPair pair)
=> _pairs.TryGetValue(ch, out pair);

private void GoToMatchingBracket(ISelectionTransformer transformer)
{
var pos = transformer.Selection.ActivePoint.Position;
var snapshot = pos.Snapshot;

if (pos.Position > 0 && TryGetPair(snapshot[pos.Position - 1], out var before) && before.Close != snapshot[pos.Position - 1])
{
int match = FindMatch(snapshot, pos.Position - 1, before.Open, before.Close, -1, 0);
if (match >= 0)
transformer.MoveTo(new SnapshotPoint(snapshot, match), false, PositionAffinity.Successor);
return;
}

if (pos.Position < snapshot.Length && TryGetPair(snapshot[pos.Position], out var after))
{
char open = snapshot[pos.Position] == after.Open ? after.Open : after.Close;
char close = snapshot[pos.Position] == after.Open ? after.Close : after.Open;
int dir = snapshot[pos.Position] == after.Open ? 1 : -1;
int match = FindMatch(snapshot, pos.Position, open, close, dir, 0);
if (match >= 0)
transformer.MoveTo(new SnapshotPoint(snapshot, match), false, PositionAffinity.Successor);
}
}

private static int FindMatch(ITextSnapshot snapshot, int start, char open, char close, int dir, int depth)
{
int i = start;
while (true)
{
i += dir;
if (i < 0 || i >= snapshot.Length)
return -1;
char c = snapshot[i];
if (c == open)
depth++;
else if (c == close)
{
if (depth == 0)
return i;
depth--;
}
}
}

private void SurroundSelections(char ch)
{
if (!TryGetPair(ch, out var pair))
return;

var sels = _broker.AllSelections.OrderByDescending(s => s.Start.Position).ToList();
using (var edit = _view.TextBuffer.CreateEdit())
{
foreach (var sel in sels)
{
edit.Insert(sel.End.Position, pair.Close.ToString());
edit.Insert(sel.Start.Position, pair.Open.ToString());
}
edit.Apply();
}

var snapshot = _view.TextBuffer.CurrentSnapshot;
var newSelections = new List<Selection>();
foreach (var sel in sels)
{
var start = new SnapshotPoint(snapshot, sel.Start.Position);
var end = new SnapshotPoint(snapshot, sel.End.Position + 2);
newSelections.Add(new Selection(new VirtualSnapshotSpan(start, end), sel.IsReversed));
}
ApplySelections(newSelections);
}

private void ReplaceSurround(char from, char to)
{
if (!TryGetPair(from, out var fromPair) || !TryGetPair(to, out var toPair))
return;

var sels = _broker.AllSelections.OrderByDescending(s => s.Start.Position).ToList();
using (var edit = _view.TextBuffer.CreateEdit())
{
foreach (var sel in sels)
{
var snapshot = _view.TextBuffer.CurrentSnapshot;
if (sel.Start.Position == 0 || sel.End.Position >= snapshot.Length)
continue;
if (snapshot[sel.Start.Position - 1] == fromPair.Open && snapshot[sel.End.Position] == fromPair.Close)
{
edit.Replace(new Span(sel.Start.Position - 1, 1), toPair.Open.ToString());
edit.Replace(new Span(sel.End.Position, 1), toPair.Close.ToString());
}
}
edit.Apply();
}
}

private void DeleteSurround(char ch)
{
if (!TryGetPair(ch, out var pair))
return;

var sels = _broker.AllSelections.OrderByDescending(s => s.Start.Position).ToList();
using (var edit = _view.TextBuffer.CreateEdit())
{
foreach (var sel in sels)
{
var snapshot = _view.TextBuffer.CurrentSnapshot;
if (sel.Start.Position == 0 || sel.End.Position >= snapshot.Length)
continue;
if (snapshot[sel.Start.Position - 1] == pair.Open && snapshot[sel.End.Position] == pair.Close)
{
edit.Delete(new Span(sel.End.Position, 1));
edit.Delete(new Span(sel.Start.Position - 1, 1));
}
}
edit.Apply();
}
}

private void SelectTextObject(char ch, bool around)
{
if (!TryGetPair(ch, out var pair))
return;

var snapshot = _view.TextBuffer.CurrentSnapshot;
var newSelections = new List<Selection>();
foreach (var sel in _broker.AllSelections)
{
int open = FindPrev(snapshot, sel.Start.Position, pair.Open);
int close = FindNext(snapshot, sel.End.Position, pair.Close);
if (open >= 0 && close >= 0 && open < close)
{
int start = around ? open : open + 1;
int end = around ? close + 1 : close;
newSelections.Add(new Selection(new VirtualSnapshotSpan(new SnapshotPoint(snapshot, start), new SnapshotPoint(snapshot, end)), sel.IsReversed));
}
else
{
newSelections.Add(sel);
}
}
ApplySelections(newSelections);
}

private static int FindPrev(ITextSnapshot snapshot, int start, char ch)
{
for (int i = start - 1; i >= 0; i--)
if (snapshot[i] == ch)
return i;
return -1;
}

private static int FindNext(ITextSnapshot snapshot, int start, char ch)
{
for (int i = start; i < snapshot.Length; i++)
if (snapshot[i] == ch)
return i;
return -1;
}

private void ApplySelections(IReadOnlyList<Selection> selections)
{
if (selections.Count == 0)
return;

_broker.ClearSecondarySelections();
_view.Selection.Select(new SnapshotSpan(selections[0].Start.Position, selections[0].End.Position), selections[0].IsReversed);
foreach (var sel in selections.Skip(1))
_broker.AddSelection(sel);
}
}
}
