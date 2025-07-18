using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
	internal static class SelectionUtils
	{
		internal static int CalculateExpandedOffset(string text, int tabSize)
		{
			int expandedLength = 0;
			foreach (char c in text)
			{
				if (c == '\t')
				{
					int spacesForTab = tabSize - (expandedLength % tabSize);
					expandedLength += spacesForTab;
				}
				else
				{
					expandedLength++;
				}
			}
			return expandedLength;
		}

		internal static VirtualSnapshotPoint CreatePointAtVisualOffset(ITextSnapshotLine line, int visualOffset, int tabSize)
		{
			string lineText = line.GetText();
			int currentVisualOffset = 0;
			int charOffset = 0;

			while (charOffset < lineText.Length && currentVisualOffset < visualOffset)
			{
				if (lineText[charOffset] == '\t')
				{
					int spacesForTab = tabSize - (currentVisualOffset % tabSize);
					currentVisualOffset += spacesForTab;
				}
				else
				{
					currentVisualOffset++;
				}

				if (currentVisualOffset <= visualOffset)
				{
					charOffset++;
				}
			}

			int virtualSpaces = visualOffset - currentVisualOffset;
			if (virtualSpaces < 0) virtualSpaces = 0;

			var basePoint = new SnapshotPoint(line.Snapshot, line.Start.Position + charOffset);
			return new VirtualSnapshotPoint(basePoint, virtualSpaces);
		}

		internal static void ExtendSelectionLinewise(ISelectionTransformer transformer, ITextView view)
		{
			var snapshot = view.TextSnapshot;
			var currentSelection = transformer.Selection;
			var startLine = currentSelection.Start.Position.GetContainingLine();
			var endLine = currentSelection.End.Position.GetContainingLine();

			bool isAlreadyLinewise = currentSelection.Start.Position == startLine.Start &&
			  currentSelection.End.Position == endLine.End;

			VirtualSnapshotPoint newStart, newEnd;
			if (!isAlreadyLinewise)
			{
				newStart = new VirtualSnapshotPoint(startLine.Start);
				newEnd = new VirtualSnapshotPoint(endLine.End);
			}
			else
			{
				if (endLine.LineNumber + 1 < snapshot.LineCount)
				{
					var nextLine = snapshot.GetLineFromLineNumber(endLine.LineNumber + 1);
					newStart = new VirtualSnapshotPoint(startLine.Start);
					newEnd = new VirtualSnapshotPoint(nextLine.End);
				}
				else
				{
					newStart = new VirtualSnapshotPoint(startLine.Start);
					newEnd = new VirtualSnapshotPoint(endLine.End);
				}
			}

			var newSpan = new VirtualSnapshotSpan(newStart, newEnd);
			transformer.MoveTo(newSpan.Start, false, PositionAffinity.Successor);
			transformer.MoveTo(newSpan.End, true, PositionAffinity.Successor);
		}

		internal static bool IsLinewiseSelection(Selection sel, ITextSnapshot snapshot)
		{
			if (sel.IsEmpty || sel.Start.IsInVirtualSpace || sel.End.IsInVirtualSpace)
				return false;

			var startLine = sel.Start.Position.GetContainingLine();
			var endLine = sel.End.Position.GetContainingLine();
			return sel.Start.Position == startLine.Start && sel.End.Position == endLine.End;
		}

		internal static void DeleteSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var selections = broker.AllSelections.ToList();
			if (!selections.Any())
				return;

			using (var edit = view.TextBuffer.CreateEdit())
			{
				foreach (var sel in selections.OrderByDescending(s => s.Start.Position))
				{
					if (sel.IsEmpty) continue;

					var spanToDelete = new SnapshotSpan(sel.Start.Position, sel.End.Position);
					if (IsLinewiseSelection(sel, view.TextSnapshot))
					{
						var endLine = sel.End.Position.GetContainingLine();
						if (endLine.End.Position < endLine.EndIncludingLineBreak.Position)
						{
							spanToDelete = new SnapshotSpan(sel.Start.Position, endLine.EndIncludingLineBreak);
						}
					}
					edit.Delete(spanToDelete);
				}
				edit.Apply();
			}

			broker.PerformActionOnAllSelections(transformer =>
			{
				var start = transformer.Selection.Start;
				transformer.MoveTo(start, false, PositionAffinity.Successor);
			});
		}

		internal static void YankSelections(ITextView view, IMultiSelectionBroker broker)
		{
			var snapshot = view.TextSnapshot;
			var selections = broker.AllSelections.ToList();
			if (selections.Count == 0)
				return;

			var yankRegister = new List<YankItem>();
			var concatenatedText = new StringBuilder();

			foreach (var sel in selections)
			{
				var span = new SnapshotSpan(sel.Start.Position, sel.End.Position);
				string text = span.GetText();
				bool isLinewise = IsLinewiseSelection(sel, snapshot);

				if (isLinewise)
				{
					text += Environment.NewLine;
				}

				yankRegister.Add(new YankItem(text, isLinewise));
				concatenatedText.Append(text);
			}

			string clipboardText = concatenatedText.ToString();
			if (yankRegister.Any(item => item.IsLinewise) && !clipboardText.EndsWith(Environment.NewLine))
			{
				clipboardText += Environment.NewLine;
			}

			var dataObject = new DataObject();
			dataObject.SetText(clipboardText);
			string json = JsonSerializer.Serialize(yankRegister);
			dataObject.SetData("MyVsHelixYankFormat", json);

			try
			{
				Clipboard.SetDataObject(dataObject, true);
			}
			catch
			{
			}
		}
	}
}
