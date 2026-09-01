using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;

namespace Suma.Application.Categories.ArchiveCategory;

public sealed class ArchiveCategoryUseCase(ICategoryStore categories, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(categoryId, cancellationToken)
            ?? throw new NotFoundException("Category was not found.");
        CategoryRules.RequireEditable(category);
        if (await categories.HasActiveChildrenAsync(category.Id, cancellationToken))
        {
            throw new ConflictException("Archive or re-parent active child categories first.");
        }

        category.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
