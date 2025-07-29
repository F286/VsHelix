using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
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
						int tabSize = (int)view.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
						int posInLine = active.Position - line.Start.Position;
						string textToPos = line.GetText().Substring(0, posInLine);
						int expanded = SelectionUtils.CalculateExpandedOffset(textToPos, tabSize);
						expanded += active.VirtualSpaces;
						var target = SelectionUtils.CreatePointAtVisualOffset(nextLine, expanded, tabSize);
						sel.MoveTo(target, true, PositionAffinity.Successor);
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
						int tabSize = (int)view.Options.GetOptionValue(DefaultOptions.TabSizeOptionId);
						int posInLine = active.Position - line.Start.Position;
						string textToPos = line.GetText().Substring(0, posInLine);
						int expanded = SelectionUtils.CalculateExpandedOffset(textToPos, tabSize);
						expanded += active.VirtualSpaces;
						var target = SelectionUtils.CreatePointAtVisualOffset(prevLine, expanded, tabSize);
						sel.MoveTo(target, true, PositionAffinity.Successor);
					}
				},
                               ['w'] = (view, sel) => SelectionUtils.MoveToNextWordStart(sel, true),
                               ['W'] = (view, sel) => SelectionUtils.MoveToNextLongWordStart(sel, true),
                               ['b'] = (view, sel) => SelectionUtils.MoveToPreviousWordStart(sel, true),
                               ['B'] = (view, sel) => SelectionUtils.MoveToPreviousLongWordStart(sel, true),
                               ['e'] = (view, sel) => SelectionUtils.MoveToNextWordEnd(sel, true),
                               ['E'] = (view, sel) => SelectionUtils.MoveToNextLongWordEnd(sel, true)
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
			_keymap.Add("p", (c, view, broker, ops) =>
			{
				PasteReplaceSelections(view, broker);
				ModeManager.Instance.EnterNormal(view, broker);
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

			_keymap.Add("C", (c, view, broker, ops) =>
			{
				AddCaretBelowSelections(view, broker);
				return true;
			});
			_keymap.Add("K", (c, view, broker, ops) =>
			{
				AddCaretAboveSelections(view, broker);
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

		private void PasteReplaceSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var selections = broker.AllSelections.ToList();
			if (selections.Count == 0) return;

			List<YankItem> pasteItems = null;
			IDataObject data = Clipboard.GetDataObject();
			if (data != null)
			{
				if (data.GetDataPresent("MyVsHelixYankFormat"))
				{
					string json = (string)data.GetData("MyVsHelixYankFormat");
					pasteItems = JsonSerializer.Deserialize<List<YankItem>>(json);
				}
				else if (data.GetDataPresent(DataFormats.Text))
				{
					string text = (string)data.GetData(DataFormats.Text);
					bool linewise = text.EndsWith(Environment.NewLine);
					pasteItems = new List<YankItem> { new YankItem(text, linewise) };
				}
			}
			if (pasteItems == null || pasteItems.Count == 0) return;

			using (var edit = view.TextBuffer.CreateEdit())
			{
				foreach (var sel in selections.OrderByDescending(s => s.Start.Position.Position))
				{
					var span = new SnapshotSpan(sel.Start.Position, sel.End.Position);
					if (SelectionUtils.IsLinewiseSelection(sel, view.TextSnapshot))
					{
						var endLine = sel.End.Position.GetContainingLine();
						if (endLine.End.Position < endLine.EndIncludingLineBreak.Position)
							span = new SnapshotSpan(sel.Start.Position, endLine.EndIncludingLineBreak);
					}
					YankItem item = pasteItems[(selections.IndexOf(sel)) % pasteItems.Count];
					edit.Replace(span, item.Text);
				}
				edit.Apply();
			}
		}

		private void AddCaretBelowSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var snapshot = view.TextSnapshot;
			int tabSize = view.Options.GetTabSize();
			var current = broker.AllSelections.ToList();

			foreach (var sel in current.OrderByDescending(s => s.End.Position.GetContainingLine().LineNumber))
			{
				var startLine = sel.Start.Position.GetContainingLine();
				var endLine = sel.End.Position.GetContainingLine();
				if (startLine.LineNumber != endLine.LineNumber) continue;
				if (endLine.LineNumber + 1 >= snapshot.LineCount) continue;
				var nextLine = snapshot.GetLineFromLineNumber(endLine.LineNumber + 1);
				string lineText = startLine.GetText();
				int startOffset = SelectionUtils.CalculateExpandedOffset(lineText.Substring(0, sel.Start.Position - startLine.Start), tabSize) + sel.Start.VirtualSpaces;
				int endOffset = SelectionUtils.CalculateExpandedOffset(lineText.Substring(0, sel.End.Position - startLine.Start), tabSize) + sel.End.VirtualSpaces;
				int required = Math.Max(startOffset, endOffset);
				int nextLen = SelectionUtils.CalculateExpandedOffset(nextLine.GetText(), tabSize);
				if (nextLen < required) continue;
				var newStart = SelectionUtils.CreatePointAtVisualOffset(nextLine, startOffset, tabSize);
				var newEnd = SelectionUtils.CreatePointAtVisualOffset(nextLine, endOffset, tabSize);
				Selection newSel = sel.IsReversed ? new Selection(newEnd, newStart) : new Selection(newStart, newEnd);
				broker.AddSelection(newSel);
			}
		}

		private void AddCaretAboveSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var snapshot = view.TextSnapshot;
			int tabSize = view.Options.GetTabSize();
			var current = broker.AllSelections.ToList();

			foreach (var sel in current.OrderBy(s => s.Start.Position.GetContainingLine().LineNumber))
			{
				var startLine = sel.Start.Position.GetContainingLine();
				var endLine = sel.End.Position.GetContainingLine();
				if (startLine.LineNumber != endLine.LineNumber) continue;
				if (startLine.LineNumber == 0) continue;
				var prevLine = snapshot.GetLineFromLineNumber(startLine.LineNumber - 1);
				string lineText = startLine.GetText();
				int startOffset = SelectionUtils.CalculateExpandedOffset(lineText.Substring(0, sel.Start.Position - startLine.Start), tabSize) + sel.Start.VirtualSpaces;
				int endOffset = SelectionUtils.CalculateExpandedOffset(lineText.Substring(0, sel.End.Position - startLine.Start), tabSize) + sel.End.VirtualSpaces;
				int required = Math.Max(startOffset, endOffset);
				int prevLen = SelectionUtils.CalculateExpandedOffset(prevLine.GetText(), tabSize);
				if (prevLen < required) continue;
				var newStart = SelectionUtils.CreatePointAtVisualOffset(prevLine, startOffset, tabSize);
				var newEnd = SelectionUtils.CreatePointAtVisualOffset(prevLine, endOffset, tabSize);
				Selection newSel = sel.IsReversed ? new Selection(newEnd, newStart) : new Selection(newStart, newEnd);
				broker.AddSelection(newSel);
			}
		}

		internal void Reset()
		{
			_keymap.Reset();
			_pendingCount = 0;
		}
	}
}
