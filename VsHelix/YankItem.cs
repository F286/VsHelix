namespace VsHelix
{
/// <summary>
/// Record type for yanked items used for clipboard serialization.
/// </summary>
internal record YankItem(string Text, bool IsLinewise);
}
