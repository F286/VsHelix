using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
	public sealed class SearchMode : IInputMode
	{
		private readonly bool _selectAll;
		private readonly ITextView _view;
		private readonly IMultiSelectionBroker _broker;
		private readonly List<SnapshotSpan> _domain;
		private readonly SnapshotPoint _start;
		private string _query = string.Empty;

		public SearchMode(bool selectAll, ITextView view, IMultiSelectionBroker broker, List<SnapshotSpan> domain)
		{
			_selectAll = selectAll;
			_view = view;
			_broker = broker;
			_domain = domain;
			_start = view.Caret.Position.BufferPosition;
			UpdateStatus();
		}

		public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
		{
			char ch = args.TypedChar;
			if (!char.IsControl(ch))
			{
				_query += ch;
				UpdateMatches();
			}
			return true;
		}

		public void HandleBackspace()
		{
			if (_query.Length > 0)
			{
				_query = _query.Substring(0, _query.Length - 1);
				UpdateMatches();
			}
		}

		public void Finish()
		{
			if (!_selectAll)
			{
				_broker.ClearSecondarySelections();
			}
			ModeManager.Instance.EnterNormal(_view, _broker);
		}

		private void UpdateStatus()
		{
			StatusBarHelper.ShowMode(ModeManager.EditorMode.Search, _query);
		}
		private void UpdateMatches()
		{
			_broker.ClearSecondarySelections();

			if (string.IsNullOrEmpty(_query))
			{
				_view.Caret.MoveTo(_start);
				_view.Selection.Clear();
				UpdateStatus();
				return;
			}

			Regex regex;
			try
			{
				regex = new Regex(_query);
			}
			catch
			{
				UpdateStatus();
				return;
			}

			var matches = new List<SnapshotSpan>();
			SnapshotSpan? firstAfterStart = null;

			foreach (var domainSpan in _domain)
			{
				var snapshot = domainSpan.Snapshot;
				var startLineNum = domainSpan.Start.GetContainingLine().LineNumber;
				var endLineNum = domainSpan.End.GetContainingLine().LineNumber;

				for (int lineNum = startLineNum; lineNum <= endLineNum; lineNum++)
				{
					var line = snapshot.GetLineFromLineNumber(lineNum);
					int lineStart = (lineNum == startLineNum) ? Math.Max(0, domainSpan.Start.Position - line.Start.Position) : 0;
					int lineEnd = (lineNum == endLineNum) ? domainSpan.End.Position - line.Start.Position : line.Length;

					if (lineStart >= lineEnd) continue; // empty line portion

					string lineText = line.GetText().Substring(lineStart, lineEnd - lineStart);

					foreach (Match m in regex.Matches(lineText))
					{
						var matchStartPos = line.Start + lineStart + m.Index;
						var matchSpan = new SnapshotSpan(matchStartPos, m.Length);
						matches.Add(matchSpan);

						if (!firstAfterStart.HasValue && matchSpan.Start >= _start)
						{
							firstAfterStart = matchSpan;
						}
					}
				}
			}

			if (matches.Count == 0)
			{
				UpdateStatus();
				return;
			}

			// No sort needed, as matches are collected in document order (lines are sequential)

			SnapshotSpan primary = firstAfterStart ?? matches[0]; // wrap to first if none after start

			_view.Selection.Select(primary, false);

			if (_selectAll)
			{
				foreach (var m in matches)
				{
					if (m != primary)
					{
						_broker.AddSelection(new Microsoft.VisualStudio.Text.Selection(m));
					}
				}
			}

			UpdateStatus();
		}
	}
}
