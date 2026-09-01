using Suma.Domain.Categories;

namespace Suma.Desktop.ViewModels;

public sealed record CategoryEditorInput(
    string Name,
    CategoryTransactionKind Kind,
    Guid? ParentCategoryId);
