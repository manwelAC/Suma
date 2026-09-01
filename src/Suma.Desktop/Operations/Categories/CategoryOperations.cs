using Microsoft.Extensions.DependencyInjection;
using Suma.Application.Categories;
using Suma.Application.Categories.ArchiveCategory;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.RestoreCategory;
using Suma.Application.Categories.UpdateCategory;

namespace Suma.Desktop.Operations.Categories;

public sealed class CategoryOperations(IServiceScopeFactory scopeFactory) : ICategoryOperations
{
    public async Task<IReadOnlyList<CategoryResult>> GetAsync(
        GetCategoriesRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GetCategoriesUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task<CategoryResult> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CreateCategoryUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task<CategoryResult> UpdateAsync(
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UpdateCategoryUseCase>()
            .ExecuteAsync(request, cancellationToken);
    }

    public async Task ArchiveAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ArchiveCategoryUseCase>()
            .ExecuteAsync(categoryId, cancellationToken);
    }

    public async Task RestoreAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<RestoreCategoryUseCase>()
            .ExecuteAsync(categoryId, cancellationToken);
    }
}
