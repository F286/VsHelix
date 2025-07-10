using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Utilities;

namespace VxHelix3
{
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name(nameof(TypeCharHandler))]
    [Order(Before = "TypeChar")]
    [VisualStudioContribution]
    internal sealed class TypeCharHandler : ICommandHandler<TypeCharCommandArgs>
    {
        private readonly ModeService _modeService;

        [ImportingConstructor]
        internal TypeCharHandler(ModeService modeService)
        {
            _modeService = modeService;
        }

        public string DisplayName => "Helix Emulation (demo)";

        public CommandState GetCommandState(TypeCharCommandArgs args)
            => CommandState.Unspecified; // let VS decide enable/disable

        public bool ExecuteCommand(TypeCharCommandArgs args, CommandExecutionContext ctx)
            => _modeService.HandleTypeChar(args);
    }
}
