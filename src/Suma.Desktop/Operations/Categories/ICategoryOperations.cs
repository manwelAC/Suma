using Suma.Application.Categories;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.UpdateCategory;

namespace Suma.Desktop.Operations.Categories;

public interface ICategoryOperations
{
    Task<IReadOnlyList<CategoryResult>> GetAsync(GetCategoriesRequest request, CancellationToken cancellationToken = default);

    Task<CategoryResult> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryResult> UpdateAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task RestoreAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
