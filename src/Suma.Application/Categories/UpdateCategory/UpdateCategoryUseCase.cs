using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Categories.UpdateCategory;

public sealed record UpdateCategoryRequest(Guid CategoryId, string Name, Guid? ParentCategoryId);

public sealed class UpdateCategoryUseCase(ICategoryStore categories, IUnitOfWork unitOfWork)
{
    public async Task<CategoryResult> ExecuteAsync(
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ApplicationValidationException("Category name is required.");
        }

        var category = await categories.GetByIdAsync(request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        CategoryRules.RequireEditable(category);
        if (category.IsArchived)
        {
            throw new ConflictException("Archived categories must be restored before editing.");
        }

        var parent = await CategoryRules.GetValidParentAsync(
            categories,
            request.ParentCategoryId,
            category.TransactionKind,
            category.Id,
            cancellationToken);
        category.Rename(request.Name);
        category.SetParentCategory(parent?.Id);
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
