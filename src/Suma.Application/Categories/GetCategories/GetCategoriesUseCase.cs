using Suma.Application.Abstractions.Persistence;
using Suma.Domain.Categories;

namespace Suma.Application.Categories.GetCategories;

public sealed record GetCategoriesRequest(CategoryTransactionKind Kind, bool Archived);

public sealed class GetCategoriesUseCase(ICategoryStore categories)
{
    public async Task<IReadOnlyList<CategoryResult>> ExecuteAsync(
        GetCategoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        var all = await categories.GetAllAsync(cancellationToken);
        var names = all.ToDictionary(category => category.Id, category => category.Name);

        return all
            .Where(category => category.TransactionKind == request.Kind && category.IsArchived == request.Archived)
            .Select(category => new CategoryResult(
                category.Id,
                category.Name,
                category.TransactionKind,
                category.ParentCategoryId,
                category.ParentCategoryId.HasValue ? names.GetValueOrDefault(category.ParentCategoryId.Value) : null,
                category.IsSystem,
                category.IsArchived))
            .ToArray();
    }
}
