using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Extensibility.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
		internal sealed class SearchMode : IInputMode
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
		foreach (var span in _domain)
		{
		string text = span.GetText();
		foreach (Match m in regex.Matches(text))
		{
		var start = span.Start + m.Index;
		var matchSpan = new SnapshotSpan(start, m.Length);
		matches.Add(matchSpan);
		}
		}
		
		if (matches.Count == 0)
		{
		UpdateStatus();
		return;
		}
		
		matches.Sort((a, b) => a.Start.Position.CompareTo(b.Start.Position));
		
		SnapshotSpan primary = matches[0];
		foreach (var m in matches)
		{
		if (m.Start >= _start)
		{
		primary = m;
		break;
		}
		}
		
		_view.Selection.Select(primary, false);
		foreach (var m in matches)
		{
		if (m != primary)
		{
		_broker.AddSelection(new Selection(m));
		}
		}
		
		UpdateStatus();
		}
		}
		}
