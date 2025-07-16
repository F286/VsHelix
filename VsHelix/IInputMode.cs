using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Utilities;

namespace VsHelix
{
	/// <summary>
	/// Defines the contract for a mode handler.
	/// </summary>
       internal interface IInputMode
       {
               bool HandleChar(char ch, ITextView view, IMultiSelectionBroker broker, IEditorOperations operations);
       }
}