using Suma.Domain.Categories;

namespace Suma.Application.Categories;

public sealed record CategoryResult(
    Guid Id,
    string Name,
    CategoryTransactionKind Kind,
    Guid? ParentCategoryId,
    string? ParentCategoryName,
    bool IsSystem,
    bool IsArchived);
