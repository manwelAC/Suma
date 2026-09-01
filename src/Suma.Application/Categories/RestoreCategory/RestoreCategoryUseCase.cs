using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Categories.RestoreCategory;

public sealed class RestoreCategoryUseCase(ICategoryStore categories, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");

        if (category.ParentCategoryId.HasValue)
        {
            var parent = await categories.GetByIdAsync(category.ParentCategoryId.Value, cancellationToken);
            if (parent is null || parent.IsArchived)
            {
                throw new ConflictException("Restore the parent category first.");
            }

            if (parent.TransactionKind != category.TransactionKind)
            {
                throw new ConflictException("Parent category must use the same category kind.");
            }
        }

        category.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
