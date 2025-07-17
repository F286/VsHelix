using System.Collections.Generic;
using System.Linq;
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
		private readonly Keymap _keymap;

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
			['\''] = new BracketPair('\'', '\''),
			['`'] = new BracketPair('`', '`')
		};

		public MatchMode(ITextView view, IMultiSelectionBroker broker)
		{
			_view = view;
			_broker = broker;
			StatusBarHelper.ShowMode(ModeManager.EditorMode.Match);
			_keymap = new Keymap();
			_keymap.Add("m", (c, v, b, o) => { _broker.PerformActionOnAllSelections(sel => GoToMatchingBracket(sel)); return Exit(); });
			_keymap.Add("s", (c, v, b, o) => { _state = MatchState.Surround; return true; });
			_keymap.Add("r", (c, v, b, o) => { _state = MatchState.ReplaceFirst; return true; });
			_keymap.Add("d", (c, v, b, o) => { _state = MatchState.Delete; return true; });
			_keymap.Add("a", (c, v, b, o) => { _state = MatchState.Around; return true; });
			_keymap.Add("i", (c, v, b, o) => { _state = MatchState.Inside; return true; });
			_keymap.Add("\u001b", (c, v, b, o) => Exit());
			_keymap.Add("\r", (c, v, b, o) => Exit());
		}

		public bool HandleChar(char ch, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
		{
			if (char.IsControl(ch))
				return true;

			if (_state == MatchState.AwaitCommand)
			{
				if (_keymap.TryGetCommand(ch, out var handler))
				{
					if (handler != null)
						return handler(ch, view, broker, operations);
					return true;
				}
				return Exit();
			}

			return _state switch
			{
				MatchState.Surround => ExitAfter(() => SurroundSelections(ch)),
				MatchState.ReplaceFirst => SetReplaceFrom(ch),
				MatchState.ReplaceSecond => ExitAfter(() => ReplaceSurround(_replaceFrom, ch)),
				MatchState.Delete => ExitAfter(() => DeleteSurround(ch)),
				MatchState.Around => ExitAfter(() => SelectTextObject(ch, true)),
				MatchState.Inside => ExitAfter(() => SelectTextObject(ch, false)),
				_ => Exit()
			};
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

		private static bool TryGetBracket(char ch, out char open, out char close)
		{
			open = default;
			close = default;
			if (_pairs.TryGetValue(ch, out var pair))
			{
				open = ch;
				close = pair.Close;
				return true;
			}
			foreach (var kv in _pairs)
			{
				if (kv.Value.Close == ch)
				{
					open = kv.Value.Open;
					close = ch;
					return true;
				}
			}
			return false;
		}

		private void GoToMatchingBracket(ISelectionTransformer transformer)
		{
			var pos = transformer.Selection.ActivePoint.Position;
			var snapshot = pos.Snapshot;

			// Check character before cursor
			if (pos.Position > 0 && TryGetBracket(snapshot[pos.Position - 1], out var openBefore, out var closeBefore))
			{
				var isOpen = snapshot[pos.Position - 1] == openBefore;
				var dir = isOpen ? 1 : -1;
				if (!isOpen)
				{
					var temp = openBefore;
					openBefore = closeBefore;
					closeBefore = temp;
				}
				int match = FindMatch(snapshot, pos.Position - 1, openBefore, closeBefore, dir, 0);
				if (match >= 0)
					transformer.MoveTo(new VirtualSnapshotPoint(new SnapshotPoint(snapshot, match)), false, PositionAffinity.Successor);
				return;
			}

			// Check character at cursor
			if (pos.Position < snapshot.Length && TryGetBracket(snapshot[pos.Position], out var openAfter, out var closeAfter))
			{
				var isOpen = snapshot[pos.Position] == openAfter;
				var dir = isOpen ? 1 : -1;
				if (!isOpen)
				{
					var temp = openAfter;
					openAfter = closeAfter;
					closeAfter = temp;
				}
				int match = FindMatch(snapshot, pos.Position, openAfter, closeAfter, dir, 0);
				if (match >= 0)
					transformer.MoveTo(new VirtualSnapshotPoint(new SnapshotPoint(snapshot, match)), false, PositionAffinity.Successor);
			}
		}

		private static int FindMatch(ITextSnapshot snapshot, int start, char open, char close, int dir, int depth)
		{
			if (open == close)
			{
				int i = start + dir;
				while (i >= 0 && i < snapshot.Length)
				{
					if (snapshot[i] == close)
						return i;
					i += dir;
				}
				return -1;
			}
			else
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
		}

		private void SurroundSelections(char ch)
		{
			if (!TryGetBracket(ch, out var open, out var close))
				return;

			var sels = _broker.AllSelections.OrderByDescending(s => s.Start.Position).ToList();
			using (var edit = _view.TextBuffer.CreateEdit())
			{
				foreach (var sel in sels)
				{
					edit.Insert(sel.End.Position, close.ToString());
					edit.Insert(sel.Start.Position, open.ToString());
				}
				edit.Apply();
			}

			var snapshot = _view.TextBuffer.CurrentSnapshot;
			var newSelections = new List<Selection>();
			foreach (var sel in sels)
			{
				var start = new VirtualSnapshotPoint(snapshot, sel.Start.Position + 1);
				var end = new VirtualSnapshotPoint(snapshot, sel.End.Position + 1);
				newSelections.Add(new Selection(new VirtualSnapshotSpan(start, end), sel.IsReversed));
			}
			ApplySelections(newSelections);
		}

		private void ReplaceSurround(char fromCh, char toCh)
		{
			if (!TryGetBracket(fromCh, out var fromOpen, out var fromClose) || !TryGetBracket(toCh, out var toOpen, out var toClose))
				return;

			var snapshot = _view.TextBuffer.CurrentSnapshot;
			var toReplace = new List<(int pos, string newText)>();
			foreach (var sel in _broker.AllSelections)
			{
				bool found = false;
				for (int p = sel.Start.Position - 1; p >= 0; p--)
				{
					if (snapshot[p] == fromOpen)
					{
						int match = FindMatch(snapshot, p, fromOpen, fromClose, 1, 0);
						if (match >= 0 && match >= sel.End.Position)
						{
							toReplace.Add((p, toOpen.ToString()));
							toReplace.Add((match, toClose.ToString()));
							found = true;
							break;
						}
					}
				}
			}

			if (toReplace.Count == 0)
				return;

			var ordered = toReplace.OrderByDescending(t => t.pos).ToList();
			using (var edit = _view.TextBuffer.CreateEdit())
			{
				foreach (var t in ordered)
				{
					edit.Replace(t.pos, 1, t.newText);
				}
				edit.Apply();
			}
		}

		private void DeleteSurround(char ch)
		{
			if (!TryGetBracket(ch, out var open, out var close))
				return;

			var snapshot = _view.TextBuffer.CurrentSnapshot;
			var toDelete = new List<int>();
			foreach (var sel in _broker.AllSelections)
			{
				for (int p = sel.Start.Position - 1; p >= 0; p--)
				{
					if (snapshot[p] == open)
					{
						int match = FindMatch(snapshot, p, open, close, 1, 0);
						if (match >= 0 && match >= sel.End.Position)
						{
							toDelete.Add(match);
							toDelete.Add(p);
							break;
						}
					}
				}
			}

			if (toDelete.Count == 0)
				return;

			toDelete = toDelete.Distinct().OrderByDescending(x => x).ToList();
			using (var edit = _view.TextBuffer.CreateEdit())
			{
				foreach (int pos in toDelete)
				{
					edit.Delete(pos, 1);
				}
				edit.Apply();
			}
		}

		private void SelectTextObject(char ch, bool around)
		{
			if (!TryGetBracket(ch, out var open, out var close))
				return;

			var snapshot = _view.TextBuffer.CurrentSnapshot;
			var newSelections = new List<Selection>();
			foreach (var sel in _broker.AllSelections)
			{
				bool found = false;
				for (int p = sel.Start.Position - 1; p >= 0; p--)
				{
					if (snapshot[p] == open)
					{
						int match = FindMatch(snapshot, p, open, close, 1, 0);
						if (match >= 0 && match >= sel.End.Position)
						{
							int startPos = around ? p : p + 1;
							int endPos = around ? match + 1 : match;
							var start = new VirtualSnapshotPoint(snapshot, startPos);
							var end = new VirtualSnapshotPoint(snapshot, endPos);
							newSelections.Add(new Selection(new VirtualSnapshotSpan(start, end), sel.IsReversed));
							found = true;
							break;
						}
					}
				}
				if (!found)
				{
					newSelections.Add(sel);
				}
			}
			ApplySelections(newSelections);
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