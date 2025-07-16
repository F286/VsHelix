// Updated SearchMode.cs (including the tagger classes as before)
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace VsHelix
{
	// New class: Define the highlight format (exported for the editor to recognize)
	[Export(typeof(EditorFormatDefinition))]
	[Name("MarkerFormatDefinition/SearchHighlightFormatDefinition")]
	[UserVisible(true)]
	internal class SearchHighlightFormatDefinition : MarkerFormatDefinition
	{
		public SearchHighlightFormatDefinition()
		{
			this.BackgroundColor = System.Windows.Media.Colors.LightYellow;
			this.DisplayName = "Search Highlight";
			this.ZOrder = 2; // Adjust z-order to appear behind selections if needed
		}
	}

	// New class: The tag type for search highlights
	internal class SearchHighlightTag : TextMarkerTag
	{
		public SearchHighlightTag() : base("MarkerFormatDefinition/SearchHighlightFormatDefinition") { }
	}

	// New class: The tagger that provides highlight tags based on provided spans
	internal class SearchHighlightTagger : ITagger<SearchHighlightTag>
	{
		private readonly ITextView _view;
		private readonly ITextBuffer _buffer;
		private NormalizedSnapshotSpanCollection _highlightSpans;
		private readonly object _lock = new object();

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public SearchHighlightTagger(ITextView view, ITextBuffer buffer)
		{
			_view = view;
			_buffer = buffer;
			_highlightSpans = new NormalizedSnapshotSpanCollection();
		}

		public IEnumerable<ITagSpan<SearchHighlightTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0 || _highlightSpans == null || _highlightSpans.Count == 0)
				yield break;

			var currentHighlights = _highlightSpans;

			// Translate to the requested snapshot if necessary
			if (currentHighlights[0].Snapshot != spans[0].Snapshot)
			{
				currentHighlights = new NormalizedSnapshotSpanCollection(
					currentHighlights.Select(s => s.TranslateTo(spans[0].Snapshot, SpanTrackingMode.EdgeExclusive)));
			}

			// Find overlapping spans
			var intersecting = NormalizedSnapshotSpanCollection.Overlap(currentHighlights, spans);
			foreach (var intersectSpan in intersecting)
			{
				yield return new TagSpan<SearchHighlightTag>(intersectSpan, new SearchHighlightTag());
			}
		}

		public void UpdateHighlights(IEnumerable<SnapshotSpan> newHighlights)
		{
			var snapshot = _buffer.CurrentSnapshot;
			var newNorm = new NormalizedSnapshotSpanCollection(newHighlights.Where(s => s.Length > 0));

			lock (_lock)
			{
				if (NormalizedSnapshotSpanCollection.Equals(_highlightSpans, newNorm))
					return;

				var oldSpans = _highlightSpans;
				_highlightSpans = newNorm;

				// Raise TagsChanged for the entire buffer or the union of old and new spans
				SnapshotSpan changedSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
				if (oldSpans != null && oldSpans.Count > 0 && newNorm.Count > 0)
				{
					var union = NormalizedSnapshotSpanCollection.Union(oldSpans, newNorm);
					if (union.Count > 0)
					{
						changedSpan = new SnapshotSpan(union.First().Start, union.Last().End);
					}
				}
				else if (oldSpans != null && oldSpans.Count > 0)
				{
					changedSpan = new SnapshotSpan(oldSpans.First().Start, oldSpans.Last().End);
				}
				else if (newNorm.Count > 0)
				{
					changedSpan = new SnapshotSpan(newNorm.First().Start, newNorm.Last().End);
				}

				TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(changedSpan));
			}
		}
	}

	// New class: Provider for the tagger (exported via MEF)
	[Export(typeof(IViewTaggerProvider))]
	[ContentType("text")]
	[TagType(typeof(SearchHighlightTag))]
	internal class SearchHighlightTaggerProvider : IViewTaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
		{
			if (textView == null || buffer == null)
				return null;

			// Create the tagger and store it in view properties for easy access
			var tagger = new SearchHighlightTagger(textView, buffer);
			textView.Properties["SearchHighlightTagger"] = tagger;
			return tagger as ITagger<T>;
		}
	}

	// Updated SearchMode class
	public sealed class SearchMode : IInputMode
	{
		private readonly bool _selectAllMatches;
		private readonly ITextView _view;
		private readonly IMultiSelectionBroker _broker;
		private readonly ITextBuffer _buffer;
		private readonly List<SnapshotSpan> _domain;
		private readonly SnapshotPoint _start;
		private string _query = string.Empty;
		private readonly ITextSearchService2 _searchService;
               private readonly SearchHighlightTagger _highlighter;

               private delegate bool CommandHandler(TypeCharCommandArgs args);
               private readonly Dictionary<char, CommandHandler> _commandMap;

               public SearchMode(bool selectAll, ITextView view, IMultiSelectionBroker broker, List<SnapshotSpan> domain, ITextSearchService2 searchService)
               {
                       _selectAllMatches = selectAll;
                       _view = view;
                       _broker = broker;
                       _buffer = view.TextBuffer;
                       _domain = domain;
                       _start = view.Caret.Position.BufferPosition;
                       _searchService = searchService;

                       // Get the highlighter tagger from view properties
                       _highlighter = _view.Properties.GetProperty<SearchHighlightTagger>("SearchHighlightTagger");

                       _commandMap = new Dictionary<char, CommandHandler>
                       {
                               ['n'] = args => { CycleMatch(true); return true; },
                               ['N'] = args => { CycleMatch(false); return true; }
                       };

                       UpdateStatus();
               }



               public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
               {
                       if (_commandMap.TryGetValue(args.TypedChar, out var handler))
                               return handler(args);

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


			// Clear highlights
			_highlighter.UpdateHighlights(Enumerable.Empty<SnapshotSpan>());

			if (_selectAllMatches)
			{
				SelectionManager.Instance.ClearSelections();
			}
			else
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
				_highlighter.UpdateHighlights(Enumerable.Empty<SnapshotSpan>());
				UpdateStatus();
				return;
			}

			var findOptions = FindOptions.UseRegularExpressions | FindOptions.MatchCase;

			var matches = new List<SnapshotSpan>();
			SnapshotSpan? firstAfterStart = null;

			foreach (var domainSpan in _domain)
			{
				try
				{
					var found = _searchService.FindAll(domainSpan, _query, findOptions);
					foreach (var span in found)
					{
						if (span.Length > 0)
						{
							matches.Add(span);
							if (!firstAfterStart.HasValue && span.Start >= _start)
							{
								firstAfterStart = span;
							}
						}
					}
				}
				catch (ArgumentException)
				{
					// Invalid regex pattern
					_highlighter.UpdateHighlights(Enumerable.Empty<SnapshotSpan>());
					UpdateStatus();
					return;
				}
			}

			// Always update highlights with all matches
			_highlighter.UpdateHighlights(matches);

			if (matches.Count == 0)
			{
				UpdateStatus();
				return;
			}

			var primary = firstAfterStart ?? matches[0];
			_view.Selection.Select(primary, true);  // Reversed=true to place caret at start

			if (_selectAllMatches)
			{
				List<Microsoft.VisualStudio.Text.Selection> selections = new List<Microsoft.VisualStudio.Text.Selection>();
				foreach (var m in matches)
				{
					_broker.AddSelection(new Microsoft.VisualStudio.Text.Selection(new VirtualSnapshotSpan(m), true));  // Reversed=true	
				}
				if (selections.Count > 0)
				{
					_broker.SetSelectionRange(selections, selections.First());
				}
			}

		UpdateStatus();
		}
		
		private void CycleMatch(bool forward)
		{
		if (string.IsNullOrEmpty(_query))
		return;
		
		var matches = GetCurrentMatches();
		if (matches.Count == 0)
		return;
		
		var ordered = matches.OrderBy(s => s.Start.Position).ToList();
		var currentPos = _view.Caret.Position.BufferPosition.Position;
		
		SnapshotSpan? target = null;
		if (forward)
		{
		target = ordered.FirstOrDefault(s => s.Start.Position > currentPos);
		if (!target.HasValue)
		target = ordered.First();
		}
		else
		{
		target = ordered.LastOrDefault(s => s.Start.Position < currentPos);
		if (!target.HasValue)
		target = ordered.Last();
		}
		
		if (target.HasValue)
		{
		_broker.ClearSecondarySelections();
		_view.Selection.Select(target.Value, true);
		_view.DisplayTextLineContainingBufferPosition(target.Value.Start, 0, ViewRelativePosition.Top);
		}
		}
		
				// Helper to retrieve current matches (assuming UpdateMatches can be called to cache if needed; for simplicity, recalculate or add caching if performance issue)
				private List<SnapshotSpan> GetCurrentMatches()
				{
			// For now, recalculate; optimize if needed
			var findOptions = FindOptions.UseRegularExpressions | FindOptions.MatchCase;
			var matches = new List<SnapshotSpan>();
			foreach (var domainSpan in _domain)
			{
				try
				{
					var found = _searchService.FindAll(domainSpan, _query, findOptions);
					matches.AddRange(found.Where(s => s.Length > 0));
				}
				catch (ArgumentException)
				{
					return new List<SnapshotSpan>();
				}
			}
			return matches;
		}
	}
}
