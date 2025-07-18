using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;  // For JSON serialization
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{

	/// <summary>
	/// Handles key input when in Normal mode. 
	/// Refactored to use a key-to-command map (Helix-style) for maintainability.
	/// </summary>
	internal sealed class NormalMode : IInputMode
	{
		internal static NormalMode? Instance { get; private set; }

		// Map from typed character to its command handler.
		private readonly Keymap _keymap;

		// Stores a pending numeric prefix when the user types digits.
		private int _pendingCount = 0;

		private bool _pendingSurround;
		private bool _pendingDeletePair;

		private enum ReplaceState { None, FirstChar, SecondChar }
		private ReplaceState _pendingReplace = ReplaceState.None;
		private char _replaceFromChar;

		private enum ObjectSelectType { Around, Inside }
		private ObjectSelectType? _pendingObjectSelect;

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

		public NormalMode()
		{
			Instance = this;
			_keymap = new Keymap();

			// ** Movement commands keymap (single-key movements) **
			var movementCommands = new Dictionary<char, Action<ISelectionTransformer>>
			{
				// Basic cursor movements

				// Move left
				['h'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
						}
					}
				},
				// Move right
				['l'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToNextCaretPosition);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToNextCaretPosition);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
						}
					}
				},
				['j'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToNextLine);
						}
					}
				},
				['k'] = sel =>
				{
					if (sel.Selection.IsEmpty)
					{
						sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
					}
					else
					{
						if (sel.Selection.IsReversed)
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
						}
						else
						{
							sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousCaretPosition);
							sel.PerformAction(PredefinedSelectionTransformations.MoveToPreviousLine);
						}
					}
				},


				// Word-wise movements (clear selection then extend)
				['w'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToNextSubWord);
				},  // Next sub-word
				['W'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToNextWord);
				},  // Next Word
				['b'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToPreviousSubWord);
				},  // Previous sub-word
				['B'] = sel =>
				{
					sel.PerformAction(PredefinedSelectionTransformations.ClearSelection);
					sel.PerformAction(PredefinedSelectionTransformations.SelectToPreviousWord);
				}   // Previous Word
			};

			// Register all movement commands to the command map.
			foreach (var kvp in movementCommands)
			{
				_keymap.Add(kvp.Key.ToString(), (c, view, broker, ops) =>
				{
					broker.PerformActionOnAllSelections(kvp.Value);
					return true;
				});
			}

			// ** Other normal-mode commands **
			_keymap.Add("v", (c, view, broker, ops) =>
			{
				ModeManager.Instance.EnterVisual(view, broker);
				return true;
			});
			_keymap.Add("i", (c, view, broker, ops) =>
			{
				// Enter Insert mode at the start of each selection.
				SelectionManager.Instance.SaveSelections(broker);
				broker.PerformActionOnAllSelections(sel => MoveCaretToSelectionStart(sel));
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			});
			_keymap.Add("a", (c, view, broker, ops) =>
			{
				// Enter Insert mode at the end of each selection (append).
				SelectionManager.Instance.SaveSelections(broker);
				broker.PerformActionOnAllSelections(sel => MoveCaretToSelectionEnd(sel));
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			});
			_keymap.Add("o", (c, view, broker, ops) =>
			{
				// Open a new line *below* each selection and enter Insert mode.
				AddLine(view, broker, ops, above: false);
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			});
			_keymap.Add("O", (c, view, broker, ops) =>
			{
				// Open a new line *above* each selection and enter Insert mode.
				AddLine(view, broker, ops, above: true);
				ModeManager.Instance.EnterInsert(view, broker);
				return true;
			});
			_keymap.Add("g", (c, view, broker, ops) =>
			{
				ModeManager.Instance.EnterGoto(view, broker);
				return true;
			});
			_keymap.Add("m", null); // prefix for match commands
			_keymap.Add("mm", (c, view, broker, ops) =>
			{
				broker.PerformActionOnAllSelections(sel => GoToMatchingBracket(sel));
				return true;
			});
			_keymap.Add("ms", (c, view, broker, ops) =>
			{
				_pendingSurround = true;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal, "surround with char");
				return true;
			});
			_keymap.Add("mr", (c, view, broker, ops) =>
			{
				_pendingReplace = ReplaceState.FirstChar;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal, "replace pair from");
				return true;
			});
			_keymap.Add("md", (c, view, broker, ops) =>
			{
				_pendingDeletePair = true;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal, "delete pair");
				return true;
			});
			_keymap.Add("ma", (c, view, broker, ops) =>
			{
				_pendingObjectSelect = ObjectSelectType.Around;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal, "select around");
				return true;
			});
			_keymap.Add("mi", (c, view, broker, ops) =>
			{
				_pendingObjectSelect = ObjectSelectType.Inside;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal, "select inside");
				return true;
			});
			_keymap.Add("/", (c, view, broker, ops) =>
			{
				SelectionManager.Instance.SaveSelections(broker);
				var spans = GetSearchDomain(view, broker);
				ModeManager.Instance.EnterSearch(view, broker, false, spans);
				return true;
			});
			_keymap.Add("s", (c, view, broker, ops) =>
			{
				SelectionManager.Instance.SaveSelections(broker);
				var spans = GetSearchDomain(view, broker);
				ModeManager.Instance.EnterSearch(view, broker, true, spans);
				return true;
			});
			_keymap.Add("x", (c, view, broker, ops) =>
			{
				// Extend selection to full lines, or extend linewise selection to the next line.
				broker.PerformActionOnAllSelections(sel => ExtendSelectionLinewise(sel, view));
				return true;
			});
			_keymap.Add("y", (c, view, broker, ops) =>
			{
				// Yank (copy) current selections.
				YankSelections(view, broker);
				return true;
			});
			_keymap.Add("d", (c, view, broker, ops) =>
			{
				// Delete selection (and yank unless Alt is held).
				return ExecuteDeleteCommand(view, broker, switchToInsert: false);
			});
			_keymap.Add("c", (c, view, broker, ops) =>
			{
				// Change (delete then enter Insert mode, and yank unless Alt is held).
				return ExecuteDeleteCommand(view, broker, switchToInsert: true);
			});
			_keymap.Add("u", (c, view, broker, ops) =>
			{
				// Undo last edit.
				var undoManager = view.TextBuffer.Properties.GetProperty<ITextBufferUndoManager>(typeof(ITextBufferUndoManager));
				var undoHistory = undoManager.TextBufferUndoHistory;
				if (undoHistory.CanUndo)
				{
					undoHistory.Undo(1);
				}
				return true;
			});
			_keymap.Add("U", (c, view, broker, ops) =>
			{
				// Redo last undone edit.
				var undoManager = view.TextBuffer.Properties.GetProperty<ITextBufferUndoManager>(typeof(ITextBufferUndoManager));
				var undoHistory = undoManager.TextBufferUndoHistory;
				if (undoHistory.CanRedo)
				{
					undoHistory.Redo(1);
				}
				return true;
			});
			_keymap.Add("p", (c, view, broker, ops) =>
			{
				// Paste from yank register/clipboard.
				Paste(view, broker);
				return true;
			});
			_keymap.Add("C", (c, view, broker, ops) =>
			{
				// Add a new caret below the last selection (multi-cursor).
				AddCaretBelowLastSelection(view, broker);
				return true;
			});
			_keymap.Add("K", (c, view, broker, ops) =>
			{
				// Add a new caret above the first selection (multi-cursor).
				AddCaretAboveFirstSelection(view, broker);
				return true;
			});
			_keymap.Add(",", (c, view, broker, ops) =>
			{
				// Clear all secondary selections, keeping only the primary.
				broker.ClearSecondarySelections();
				return true;
			});
		}

		/// <summary>
		/// Handles a typed character command in Normal mode by dispatching to the appropriate action.
		/// </summary>
		public bool HandleChar(char c, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
		{
			if (_pendingSurround)
			{
				_pendingSurround = false;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal);
				SurroundSelections(c, view, broker);
				return true;
			}
			if (_pendingDeletePair)
			{
				_pendingDeletePair = false;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal);
				DeleteSurround(c, view, broker);
				return true;
			}
			if (_pendingReplace != ReplaceState.None)
			{
				if (_pendingReplace == ReplaceState.FirstChar)
				{
					_replaceFromChar = c;
					_pendingReplace = ReplaceState.SecondChar;
					StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal, "replace pair to");
					return true;
				}
				else
				{
					_pendingReplace = ReplaceState.None;
					StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal);
					ReplaceSurround(_replaceFromChar, c, view, broker);
					return true;
				}
			}
			if (_pendingObjectSelect.HasValue)
			{
				var around = _pendingObjectSelect == ObjectSelectType.Around;
				_pendingObjectSelect = null;
				StatusBarHelper.ShowMode(ModeManager.EditorMode.Normal);
				SelectTextObject(c, around, view, broker);
				return true;
			}

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
						if (ModeManager.Instance.Current != ModeManager.EditorMode.Normal)
							break;

						result &= handler(c, view, broker, operations);
					}

					// Ensure the primary caret is visible after executing the command.
					view.Caret.EnsureVisible();

					return result;
				}
				return true; // awaiting more keys
			}

			_pendingCount = 0;
			_keymap.Reset();

			// Unrecognized key in Normal mode: consume without inserting.
			return true;
		}

		/// <summary>
		/// Executes a delete/change command: yanks selections (unless Alt is pressed), deletes them, and optionally enters Insert mode.
		/// </summary>
		/// <param name="switchToInsert">If true, enters Insert mode after deletion (for 'c' command).</param>
		private bool ExecuteDeleteCommand(ITextView view, IMultiSelectionBroker broker, bool switchToInsert)
		{
			bool altDown = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
			if (!altDown)
			{
				// Yank (copy) selections to register, unless Alt is held.
				YankSelections(view, broker);
			}

			DeleteSelection(view, broker);

			if (switchToInsert)
			{
				ModeManager.Instance.EnterInsert(view, broker);
			}
			return true;
		}

		private void MoveCaretToSelectionStart(ISelectionTransformer selection)
		{
			var startPoint = selection.Selection.Start;
			// Collapse selection to its start.
			selection.MoveTo(startPoint, false, PositionAffinity.Successor);
		}

		/// <summary>
		/// Moves the caret to the end of the selection, collapsing the selection.
		/// </summary>
		private void MoveCaretToSelectionEnd(ISelectionTransformer selection)
		{
			var endPoint = selection.Selection.End;
			// Collapse selection to its end.
			selection.MoveTo(endPoint, false, PositionAffinity.Successor);
		}

		/// <summary>
		/// Extends a selection to whole lines. If the selection is already linewise, extend it to include the next line.
		/// </summary>
		private void ExtendSelectionLinewise(ISelectionTransformer transformer, ITextView view)
				=> SelectionUtils.ExtendSelectionLinewise(transformer, view);

		/// <summary>
		/// Inserts a new blank line above or below each selection (preserving indentation) and positions the caret on that new line.
		/// </summary>
		/// <param name="above">True to insert above each selection; false to insert below.</param>
		private void AddLine(ITextView view, IMultiSelectionBroker broker, IEditorOperations operations, bool above)
		{
			var snapshot = view.TextBuffer.CurrentSnapshot;
			var insertionPoints = new List<ITrackingPoint>();
			var indents = new List<string>();

			// Determine insertion points and indent strings for each selection.
			broker.PerformActionOnAllSelections(selection =>
			{
				var line = selection.Selection.ActivePoint.Position.GetContainingLine();
				var lineText = line.GetText();
				// Capture the leading whitespace (indentation) of the current line.
				var indent = lineText.Substring(0, lineText.Length - lineText.TrimStart().Length);
				indents.Add(indent);

				// Calculate position: start of line (for above) or end of line (for below).
				int pos = above ? line.Start.Position : line.End.Position;
				insertionPoints.Add(snapshot.CreateTrackingPoint(pos, PointTrackingMode.Negative));
			});

			// Insert newline(s) with proper indent at each calculated insertion point.
			using (var edit = view.TextBuffer.CreateEdit())
			{
				for (int i = 0; i < insertionPoints.Count; i++)
				{
					var tp = insertionPoints[i];
					var indent = indents[i];
					string textToInsert = above
						? indent + Environment.NewLine       // insert a new line above, with same indent
						: Environment.NewLine + indent;      // insert a new line below, with same indent
					edit.Insert(tp.GetPosition(snapshot), textToInsert);
				}
				edit.Apply();
			}

			// After insertion, move each selection’s caret to the beginning of the inserted line.
			var newSnapshot = view.TextBuffer.CurrentSnapshot;
			int index = 0;
			broker.PerformActionOnAllSelections(selection =>
			{
				// Calculate the caret position on the new line (after indent).
				int pos = insertionPoints[index].GetPosition(newSnapshot);
				string indent = indents[index++];
				int offset = above ? indent.Length : Environment.NewLine.Length + indent.Length;
				var caretPosition = new SnapshotPoint(newSnapshot, pos + offset);
				selection.MoveTo(new VirtualSnapshotPoint(caretPosition), false, PositionAffinity.Successor);
			});
		}

		/// <summary>
		/// Adds a new caret below the last selection, aligning it vertically with the original selection’s start/end.
		/// </summary>
		private void AddCaretBelowLastSelection(ITextView view, IMultiSelectionBroker broker)
		{
			// Find the bottom-most selection (last in document order).
			var bottomSelection = broker.AllSelections
										.OrderByDescending(s => s.End.Position.GetContainingLine().LineNumber)
										.ThenByDescending(s => s.End.Position.Position)
										.FirstOrDefault();
			if (bottomSelection == null)
				return;

			ITextSnapshot snapshot = view.TextSnapshot;
			var startPoint = bottomSelection.Start;
			var endPoint = bottomSelection.End;
			var startLine = startPoint.Position.GetContainingLine();
			var endLine = endPoint.Position.GetContainingLine();

			if (startLine.LineNumber != endLine.LineNumber)
			{
				// For simplicity, only duplicate carets for single-line selections.
				return;
			}

			// Get original line text and compute expanded (tab-expanded) text length for alignment.
			string originalLineText = startLine.GetText();
			string expandedText = originalLineText.Replace("\t", new string(' ', view.Options.GetTabSize()));

			// Calculate visual offsets of selection start and end within the line (accounting for tabs and virtual spaces).
			int startOffset = CalculateExpandedOffset(originalLineText.Substring(0, startPoint.Position - startLine.Start), view.Options.GetTabSize());
			int endOffset = CalculateExpandedOffset(originalLineText.Substring(0, endPoint.Position - startLine.Start), view.Options.GetTabSize());
			// Include virtual space in the offset calculations.
			startOffset += startPoint.VirtualSpaces;
			endOffset += endPoint.VirtualSpaces;

			// Find a line below that has enough length to accommodate the selection at these offsets.
			int nextLineNumber = endLine.LineNumber + 1;
			ITextSnapshotLine targetLine = null;
			int requiredOffset = Math.Max(startOffset, endOffset);
			while (nextLineNumber < snapshot.LineCount)
			{
				var candidateLine = snapshot.GetLineFromLineNumber(nextLineNumber);
				int candidateLength = CalculateExpandedOffset(candidateLine.GetText(), view.Options.GetTabSize());
				if (candidateLength >= requiredOffset)
				{
					targetLine = candidateLine;
					break;
				}
				nextLineNumber++;
			}
			if (targetLine == null)
			{
				// No suitable line found below; cannot add a caret aligned to the selection.
				return;
			}

			// Create points on the target line corresponding to the original selection's start/end visual offsets.
			var newStartPoint = CreatePointAtVisualOffset(targetLine, startOffset, view.Options.GetTabSize());
			var newEndPoint = CreatePointAtVisualOffset(targetLine, endOffset, view.Options.GetTabSize());

			// Form a new selection on the target line with the same orientation (direction) as the original.
			var newSelection = bottomSelection.IsReversed
				? new Microsoft.VisualStudio.Text.Selection(newEndPoint, newStartPoint)   // reversed selection
				: new Microsoft.VisualStudio.Text.Selection(newStartPoint, newEndPoint);  // forward selection

			broker.AddSelection(newSelection);
		}

		/// <summary>
		/// Adds a new caret above the first selection, preserving column and range.
		/// </summary>
		private void AddCaretAboveFirstSelection(ITextView view, IMultiSelectionBroker broker)
		{
			var topSelection = broker.AllSelections
										   .OrderBy(s => s.Start.Position.GetContainingLine().LineNumber)
										   .ThenBy(s => s.Start.Position.Position)
										   .FirstOrDefault();
			if (topSelection == null)
				return;

			var snapshot = view.TextSnapshot;
			var startLine = topSelection.Start.Position.GetContainingLine();
			var endLine = topSelection.End.Position.GetContainingLine();
			if (startLine.LineNumber != endLine.LineNumber)
				return;
			if (startLine.LineNumber == 0)
				return;

			int tabSize = view.Options.GetTabSize();
			string lineText = startLine.GetText();
			int startOffset = CalculateExpandedOffset(lineText.Substring(0, topSelection.Start.Position - startLine.Start), tabSize) + topSelection.Start.VirtualSpaces;
			int endOffset = CalculateExpandedOffset(lineText.Substring(0, topSelection.End.Position - startLine.Start), tabSize) + topSelection.End.VirtualSpaces;
			int requiredOffset = Math.Max(startOffset, endOffset);

			var prevLine = snapshot.GetLineFromLineNumber(startLine.LineNumber - 1);
			int prevLen = CalculateExpandedOffset(prevLine.GetText(), tabSize);
			if (prevLen < requiredOffset)
				return;

			var newStartPoint = CreatePointAtVisualOffset(prevLine, startOffset, tabSize);
			var newEndPoint = CreatePointAtVisualOffset(prevLine, endOffset, tabSize);
			var newSel = topSelection.IsReversed
					? new Microsoft.VisualStudio.Text.Selection(newEndPoint, newStartPoint)
					: new Microsoft.VisualStudio.Text.Selection(newStartPoint, newEndPoint);
			broker.AddSelection(newSel);
		}

		private System.Collections.Generic.List<SnapshotSpan> GetSearchDomain(ITextView view, IMultiSelectionBroker broker)
		{
			var spans = new System.Collections.Generic.List<SnapshotSpan>();
			foreach (var sel in broker.AllSelections)
			{
				if (!sel.IsEmpty)
				{
					spans.Add(new SnapshotSpan(sel.Start.Position, sel.End.Position));
				}
			}
			if (spans.Count == 0)
			{
				spans.Add(new SnapshotSpan(view.TextSnapshot, 0, view.TextSnapshot.Length));
			}
			return spans;
		}

		/// <summary>
		/// Calculates the expanded length of a text segment after replacing tabs with spaces (for alignment calculations).
		/// </summary>
		private int CalculateExpandedOffset(string text, int tabSize)
				=> SelectionUtils.CalculateExpandedOffset(text, tabSize);

		/// <summary>
		/// Creates a virtual snapshot point at a given visual offset into a line (accounts for tabs and returns a point with virtual spaces if needed).
		/// </summary>
		private VirtualSnapshotPoint CreatePointAtVisualOffset(ITextSnapshotLine line, int visualOffset, int tabSize)
				=> SelectionUtils.CreatePointAtVisualOffset(line, visualOffset, tabSize);

		/// <summary>
		/// Deletes the content of all non-empty selections from the buffer.
		/// </summary>
		private void DeleteSelection(ITextView view, IMultiSelectionBroker broker)
				=> SelectionUtils.DeleteSelections(view, broker);

		/// <summary>
		/// Checks if a selection spans whole lines (from the start of a line to the end of that line’s content).
		/// </summary>
		private bool IsLinewiseSelection(Microsoft.VisualStudio.Text.Selection sel, ITextSnapshot snapshot)
				=> SelectionUtils.IsLinewiseSelection(sel, snapshot);

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

		private void SurroundSelections(char ch, ITextView view, IMultiSelectionBroker broker)
		{
			if (!TryGetBracket(ch, out var open, out var close))
				return;

			var sels = broker.AllSelections.OrderByDescending(s => s.Start.Position).ToList();
			using (var edit = view.TextBuffer.CreateEdit())
			{
				foreach (var sel in sels)
				{
					edit.Insert(sel.End.Position, close.ToString());
					edit.Insert(sel.Start.Position, open.ToString());
				}
				edit.Apply();
			}

			var snapshot = view.TextBuffer.CurrentSnapshot;
			var newSelections = new List<Selection>();
			foreach (var sel in sels)
			{
				var start = new VirtualSnapshotPoint(snapshot, sel.Start.Position + 1);
				var end = new VirtualSnapshotPoint(snapshot, sel.End.Position + 1);
				newSelections.Add(new Selection(new VirtualSnapshotSpan(start, end), sel.IsReversed));
			}
			ApplySelections(newSelections, view, broker);
		}

		private void ReplaceSurround(char fromCh, char toCh, ITextView view, IMultiSelectionBroker broker)
		{
			if (!TryGetBracket(fromCh, out var fromOpen, out var fromClose) || !TryGetBracket(toCh, out var toOpen, out var toClose))
				return;

			var snapshot = view.TextBuffer.CurrentSnapshot;
			var toReplace = new Dictionary<int, string>();
			foreach (var sel in broker.AllSelections)
			{
				for (int p = sel.Start.Position - 1; p >= 0; p--)
				{
					if (snapshot[p] == fromOpen)
					{
						int match = FindMatch(snapshot, p, fromOpen, fromClose, 1, 0);
						if (match >= 0 && match >= sel.End.Position)
						{
							toReplace[p] = toOpen.ToString();
							toReplace[match] = toClose.ToString();
							break;
						}
					}
				}
			}

			if (toReplace.Count == 0)
				return;

			var ordered = toReplace.OrderByDescending(kv => kv.Key).ToList();
			using (var edit = view.TextBuffer.CreateEdit())
			{
				foreach (var kv in ordered)
				{
					edit.Replace(kv.Key, 1, kv.Value);
				}
				edit.Apply();
			}
		}

		private void DeleteSurround(char ch, ITextView view, IMultiSelectionBroker broker)
		{
			if (!TryGetBracket(ch, out var open, out var close))
				return;

			var snapshot = view.TextBuffer.CurrentSnapshot;
			var toDelete = new List<int>();
			foreach (var sel in broker.AllSelections)
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
			using (var edit = view.TextBuffer.CreateEdit())
			{
				foreach (int pos in toDelete)
				{
					edit.Delete(pos, 1);
				}
				edit.Apply();
			}
		}

		private void SelectTextObject(char ch, bool around, ITextView view, IMultiSelectionBroker broker)
		{
			if (!TryGetBracket(ch, out var open, out var close))
				return;

			var snapshot = view.TextBuffer.CurrentSnapshot;
			var newSelections = new List<Selection>();
			foreach (var sel in broker.AllSelections)
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
			ApplySelections(newSelections, view, broker);
		}

		private void ApplySelections(IReadOnlyList<Selection> selections, ITextView view, IMultiSelectionBroker broker)
		{
			if (selections.Count == 0)
				return;

			broker.ClearSecondarySelections();
			view.Selection.Select(new SnapshotSpan(selections[0].Start.Position, selections[0].End.Position), selections[0].IsReversed);
			foreach (var sel in selections.Skip(1))
				broker.AddSelection(sel);
		}

		/// <summary>
		/// Yanks (copies) the current selections to both an internal register and the system clipboard.
		/// </summary>
		private void YankSelections(ITextView view, IMultiSelectionBroker broker)
				=> SelectionUtils.YankSelections(view, broker);

		/// <summary>
		/// Pastes text from the internal yank register if available; otherwise falls back to system clipboard text.
		/// </summary>
		private void Paste(ITextView view, IMultiSelectionBroker broker)
		{
			var currentSelections = broker.AllSelections.ToList();
			if (currentSelections.Count == 0)
				return;

			List<YankItem> pasteItems = null;

			// Try to retrieve our custom yank format from the clipboard, else use plain text.
			IDataObject dataObject = Clipboard.GetDataObject();
			if (dataObject != null)
			{
				if (dataObject.GetDataPresent("MyVsHelixYankFormat"))
				{
					// Our yank register format is present – use it.
					string json = (string)dataObject.GetData("MyVsHelixYankFormat");
					pasteItems = JsonSerializer.Deserialize<List<YankItem>>(json);
				}
				else if (dataObject.GetDataPresent(DataFormats.Text))
				{
					// Fallback: treat entire clipboard text as one yank item.
					string text = (string)dataObject.GetData(DataFormats.Text);
					bool isLinewise = text.EndsWith(Environment.NewLine);
					pasteItems = new List<YankItem> { new YankItem(text, isLinewise) };
				}
			}

			if (pasteItems == null || pasteItems.Count == 0)
				return;

			// Insert the pasted content at each selection. If more selections than yank items, cycle through yank items.
			using (var edit = view.TextBuffer.CreateEdit())
			{
				for (int i = 0; i < currentSelections.Count; i++)
				{
					var sel = currentSelections[i];
					var item = pasteItems[i % pasteItems.Count];

					SnapshotPoint insertionPoint;
					if (item.IsLinewise)
					{
						// Linewise paste: insert at end-of-line (so content goes on a new line below).
						var line = sel.End.Position.GetContainingLine();
						insertionPoint = line.EndIncludingLineBreak;
					}
					else
					{
						// Characterwise paste: insert at the selection end.
						insertionPoint = sel.End.Position;
					}

					edit.Insert(insertionPoint.Position, item.Text);
				}
				edit.Apply();
			}

			// (Optional enhancement: move each caret to the end of its pasted text, if desired.)
		}

		internal void Reset()
		{
			_keymap.Reset();
			_pendingCount = 0;
			_pendingSurround = false;
			_pendingDeletePair = false;
			_pendingReplace = ReplaceState.None;
			_pendingObjectSelect = null;
		}
	}
}
