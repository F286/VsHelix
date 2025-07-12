using Microsoft.VisualBasic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using System.Linq;

namespace VxHelix3
{
    /// <summary>
    /// Handles search commands such as '/' and 'n'.
    /// </summary>
    internal sealed class SearchMode : IInputMode
    {
        private readonly ITextSearchService _searchService;
        private readonly IIncrementalSearchFactoryService _incSearchFactory;
        private string? _lastSearch;

        internal SearchMode(ITextSearchService searchService, IIncrementalSearchFactoryService incSearchFactory)
        {
            _searchService = searchService;
            _incSearchFactory = incSearchFactory;
        }

        public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations)
        {
            switch (args.TypedChar)
            {
                case '/':
                    DoSearch(view, broker);
                    return true;
                case 'n':
                    RepeatSearch(view, broker);
                    return true;
            }

            return false;
        }

        private void DoSearch(ITextView view, IMultiSelectionBroker broker)
        {
            var start = broker.PrimarySelection.ActivePoint.Position.Position;
            var term = Interaction.InputBox("Enter search regex:", "Search", _lastSearch ?? string.Empty);
            if (string.IsNullOrEmpty(term))
            {
                return;
            }

            var inc = _incSearchFactory.GetIncrementalSearch(view);
            inc.Clear();
            inc.SearchString = term;
            inc.Start();
            inc.Dismiss();

            var data = new FindData(term, view.TextSnapshot)
            {
                FindOptions = FindOptions.UseRegularExpressions | FindOptions.Wrap | FindOptions.DoNotUpdateUI
            };
            var span = _searchService.FindNext(start, true, data);
            if (span.HasValue)
            {
                _lastSearch = term;
                broker.ClearSecondarySelections();
                broker.TextView.Selection.Select(span.Value, false);
            }
        }

        private void RepeatSearch(ITextView view, IMultiSelectionBroker broker)
        {
            if (string.IsNullOrEmpty(_lastSearch))
            {
                return;
            }

            var inc = _incSearchFactory.GetIncrementalSearch(view);
            inc.SearchString = _lastSearch;
            inc.SelectNextResult();
            inc.Dismiss();

            var start = broker.PrimarySelection.ActivePoint.Position.Position;
            var data = new FindData(_lastSearch, view.TextSnapshot)
            {
                FindOptions = FindOptions.UseRegularExpressions | FindOptions.Wrap | FindOptions.DoNotUpdateUI
            };
            var next = _searchService.FindNext(start, true, data);
            if (next.HasValue)
            {
                broker.ClearSecondarySelections();
                broker.TextView.Selection.Select(next.Value, false);
            }
        }
    }
}
