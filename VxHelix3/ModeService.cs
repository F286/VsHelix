using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VxHelix3
{
    /// <summary>
    /// Central service for tracking the editor mode and delegating
    /// typed input to the appropriate mode handler.
    /// </summary>
    [VisualStudioContribution]
    public sealed class ModeService
    {
        internal const string ModeKey = nameof(ModeService) + "_mode";

        private readonly IEditorOperationsFactoryService _editorOperationsFactory;
        private readonly IInputMode _insertMode = new InsertMode();
        private readonly IInputMode _normalMode = new NormalMode();

        [ImportingConstructor]
        public ModeService(IEditorOperationsFactoryService editorOperationsFactory)
        {
            _editorOperationsFactory = editorOperationsFactory;
        }

        internal HelixMode GetMode(ITextView view)
        {
            if (!view.Properties.TryGetProperty(ModeKey, out HelixMode mode))
            {
                mode = HelixMode.Insert;
                view.Properties[ModeKey] = mode;
            }

            return mode;
        }

        internal void SetMode(ITextView view, HelixMode mode)
            => view.Properties[ModeKey] = mode;

        /// <summary>
        /// Handles a typed character for the given text view.
        /// </summary>
        public bool HandleTypeChar(TypeCharCommandArgs args)
        {
            var view = args.TextView;
            var broker = view.GetMultiSelectionBroker();
            var ops = _editorOperationsFactory.GetEditorOperations(view);

            var mode = GetMode(view);
            IInputMode handler = mode == HelixMode.Insert ? _insertMode : _normalMode;

            if (handler.Handle(args, view, broker, ops, out HelixMode next))
            {
                SetMode(view, next);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes an escape key press for the given view.
        /// </summary>
        public bool HandleEscape(ITextView view)
        {
            var mode = GetMode(view);
            if (mode == HelixMode.Insert)
            {
                SetMode(view, HelixMode.Normal);
                return true;
            }

            return false;
        }
    }
}
