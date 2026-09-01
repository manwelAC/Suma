using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Categories;

namespace Suma.Application.Categories.CreateCategory;

public sealed record CreateCategoryRequest(
    string Name,
    CategoryTransactionKind Kind,
    Guid? ParentCategoryId = null);

public sealed class CreateCategoryUseCase(ICategoryStore categories, IUnitOfWork unitOfWork)
{
    public async Task<CategoryResult> ExecuteAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApplicationValidationException("Category name is required.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            throw new ApplicationValidationException("Category kind is not supported.");
        }

        var parent = await CategoryRules.GetValidParentAsync(
            categories,
            request.ParentCategoryId,
            request.Kind,
            categoryId: null,
            cancellationToken);
        var category = new Category(request.Name, request.Kind, parent?.Id);
        await categories.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CategoryResult(
            category.Id,
            category.Name,
            category.TransactionKind,
            category.ParentCategoryId,
            parent?.Name,
            category.IsSystem,
            category.IsArchived);
    }
}
