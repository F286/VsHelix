using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;

namespace VsHelix
{
    /// <summary>
    /// Handles key input when in Insert mode.
    /// </summary>
    internal sealed class InsertMode : IMode
    {
        public bool Handle(TypeCharCommandArgs args, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations, out HelixMode nextMode)
        {
            nextMode = HelixMode.Insert;
            if (args.TypedChar == 0x1b) // Esc
            {
                nextMode = HelixMode.Normal;
                return true;
            }

            return false;
        }
    }
}
