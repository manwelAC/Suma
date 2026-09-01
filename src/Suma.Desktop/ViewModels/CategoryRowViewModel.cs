using Microsoft.UI.Xaml;
using Suma.Domain.Categories;

namespace Suma.Desktop.ViewModels;

public sealed record CategoryRowViewModel(
    Guid Id,
    string Name,
    CategoryTransactionKind Kind,
    Guid? ParentCategoryId,
    string ParentDisplay,
    bool IsSystem,
    bool IsArchived)
{
    public string KindDisplay => Kind.ToString();

    public string SystemDisplay => IsSystem ? "System category" : "Personal category";

    public Visibility EditVisibility => !IsArchived && !IsSystem ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ArchiveVisibility => !IsArchived && !IsSystem ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RestoreVisibility => IsArchived ? Visibility.Visible : Visibility.Collapsed;
}

public sealed record CategoryParentOption(Guid? Id, string Name)
{
    public override string ToString() => Name;
}
