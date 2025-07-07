using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace VsHelix
{
	//[Export(typeof(ICommandHandler))]
	//[Name(nameof(HelixCommandHandler))]
	//[ContentType("code")]
	//[TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
	//public class HelixCommandHandler : ICommandHandler<EditorCommandArgs> // Replace EditorCommandArgs with more specific CommandArgs class
	//{
	//	public string DisplayName => nameof(HelixCommandHandler);

	//	public bool ExecuteCommand(EditorCommandArgs args, CommandExecutionContext executionContext)
	//	{
	//		throw new NotImplementedException();
	//	}

	//	public CommandState GetCommandState(EditorCommandArgs args)
	//	{
	//		return CommandState.Available;
	//	}
	//}

	[Export(typeof(ICommandHandler))]
	[ContentType("text")]
	[TextViewRole(PredefinedTextViewRoles.Editable)]
	[Name(nameof(HelixCommandHandler))]
	[Order(Before = "TypeChar")]
        internal sealed class HelixCommandHandler : ICommandHandler<TypeCharCommandArgs>
        {
                private readonly IEditorOperationsFactoryService _editorOperationsFactory;

                [ImportingConstructor]
                internal HelixCommandHandler(IEditorOperationsFactoryService editorOperationsFactory)
                {
                        _editorOperationsFactory = editorOperationsFactory;
                }

                public string DisplayName => "Helix Emulation (demo)";

                public CommandState GetCommandState(TypeCharCommandArgs args)
                        => CommandState.Unspecified; // let VS decide enable/disable

                public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext ctx)
                {
                        var view = args.TextView;
                        var broker = view.GetMultiSelectionBroker();
                        var ops = _editorOperationsFactory.GetEditorOperations(view);

                        switch (args.TypedChar)
                        {
                                case 'w':
                                        broker.PerformActionOnAllSelections(() => ops.MoveToNextWord(true));
                                        return true;

                                case 'd':
                                        using (var edit = view.TextBuffer.CreateEdit())
                                        {
                                                broker.PerformActionOnAllSelections(() =>
                                                {
                                                        if (!view.Selection.IsEmpty)
                                                                edit.Delete(view.Selection.StreamSelectionSpan.SnapshotSpan);
                                                });
                                                edit.Apply();
                                        }

                                        broker.PerformActionOnAllSelections(() => view.Selection.Clear());
                                        return true;

                                case 'c':
                                        var starts = new List<SnapshotPoint>();
                                        using (var edit = view.TextBuffer.CreateEdit())
                                        {
                                                broker.PerformActionOnAllSelections(() =>
                                                {
                                                        if (!view.Selection.IsEmpty)
                                                        {
                                                                starts.Add(view.Selection.Start.Position);
                                                                edit.Delete(view.Selection.StreamSelectionSpan.SnapshotSpan);
                                                        }
                                                        else
                                                        {
                                                                starts.Add(view.Caret.Position.BufferPosition);
                                                        }
                                                });
                                                edit.Apply();
                                        }

                                        broker.ClearSecondarySelections();
                                        if (starts.Count > 0)
                                        {
                                                view.Caret.MoveTo(starts[0]);
                                                for (var i = 1; i < starts.Count; i++)
                                                        broker.AddSelection(new SnapshotSpan(starts[i], 0));
                                        }
                                        return true;
                        }

                        return false; // not ours, pass to next handler
                }
        }
}
