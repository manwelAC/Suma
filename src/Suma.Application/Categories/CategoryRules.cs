using Suma.Application.Abstractions.Persistence;
using Suma.Application.Common.Exceptions;
using Suma.Domain.Categories;

namespace Suma.Application.Categories;

internal static class CategoryRules
{
    public static async Task<Category?> GetValidParentAsync(
        ICategoryStore categories,
        Guid? parentCategoryId,
        CategoryTransactionKind kind,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (!parentCategoryId.HasValue)
        {
            return null;
        }

        if (parentCategoryId == categoryId)
        {
            throw new ApplicationValidationException("Category cannot be its own parent.");
        }

        var parent = await categories.GetByIdAsync(parentCategoryId.Value, cancellationToken)
            ?? throw new NotFoundException("Parent category was not found.");
        if (parent.IsArchived)
        {
            throw new ConflictException("Parent category must be active.");
        }

        if (parent.TransactionKind != kind)
        {
            throw new ConflictException("Parent category must use the same category kind.");
        }

        if (categoryId.HasValue)
        {
            await EnsureNoCycleAsync(categories, parent, categoryId.Value, cancellationToken);
        }

        return parent;
    }

    public static void RequireEditable(Category category)
    {
        if (category.IsSystem)
        {
            throw new ConflictException("System categories cannot be edited or archived.");
        }
    }

    private static async Task EnsureNoCycleAsync(
        ICategoryStore categories,
        Category proposedParent,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var all = (await categories.GetAllAsync(cancellationToken)).ToDictionary(category => category.Id);
        var visited = new HashSet<Guid>();
        Category? current = proposedParent;
        while (current is not null && visited.Add(current.Id))
        {
            if (current.Id == categoryId)
            {
                throw new ConflictException("A category cannot be moved beneath one of its children.");
            }

            current = current.ParentCategoryId.HasValue
                ? all.GetValueOrDefault(current.ParentCategoryId.Value)
                : null;
        }
    }
}
