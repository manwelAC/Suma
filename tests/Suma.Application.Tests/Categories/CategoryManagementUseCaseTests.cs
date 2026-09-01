using Suma.Application.Categories.ArchiveCategory;
using Suma.Application.Categories.CreateCategory;
using Suma.Application.Categories.GetCategories;
using Suma.Application.Categories.RestoreCategory;
using Suma.Application.Categories.UpdateCategory;
using Suma.Application.Common.Exceptions;
using Suma.Application.Tests.TestDoubles;
using Suma.Domain.Categories;
using Xunit;

namespace Suma.Application.Tests.Categories;

public sealed class CategoryManagementUseCaseTests
{
    [Fact]
    public async Task List_filters_kind_and_archive_state_and_resolves_parent_name()
    {
        var data = new FakeData();
        var parent = Add(data, "Living", CategoryTransactionKind.Expense);
        Add(data, "Groceries", CategoryTransactionKind.Expense, parent.Id);
        Add(data, "Salary", CategoryTransactionKind.Income);
        var archived = Add(data, "Old", CategoryTransactionKind.Expense);
        archived.Archive();

        var results = await new GetCategoriesUseCase(data).ExecuteAsync(
            new(CategoryTransactionKind.Expense, Archived: false), Token);

        Assert.Equal(2, results.Count);
        Assert.Equal("Living", Assert.Single(results, item => item.Name == "Groceries").ParentCategoryName);
    }

    [Theory]
    [InlineData(CategoryTransactionKind.Expense)]
    [InlineData(CategoryTransactionKind.Income)]
    public async Task Create_valid_category_adds_and_saves(CategoryTransactionKind kind)
    {
        var data = new FakeData();

        var result = await new CreateCategoryUseCase(data, data).ExecuteAsync(new("New", kind), Token);

        Assert.Equal(kind, result.Kind);
        Assert.Single(data.Categories);
        Assert.Equal(1, data.SaveCount);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("archived")]
    [InlineData("kind")]
    public async Task Create_rejects_invalid_parent_without_save(string scenario)
    {
        var data = new FakeData();
        var parent = Add(data, "Parent", scenario == "kind" ? CategoryTransactionKind.Income : CategoryTransactionKind.Expense);
        if (scenario == "archived") parent.Archive();
        var parentId = scenario == "missing" ? Guid.NewGuid() : parent.Id;

        await Assert.ThrowsAnyAsync<Exception>(() =>
            new CreateCategoryUseCase(data, data).ExecuteAsync(
                new("Child", CategoryTransactionKind.Expense, parentId), Token));

        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Update_changes_name_and_safe_parent()
    {
        var data = new FakeData();
        var parent = Add(data, "Living", CategoryTransactionKind.Expense);
        var category = Add(data, "Food", CategoryTransactionKind.Expense);

        var result = await new UpdateCategoryUseCase(data, data).ExecuteAsync(
            new(category.Id, "  Groceries ", parent.Id), Token);

        Assert.Equal("Groceries", result.Name);
        Assert.Equal(parent.Id, category.ParentCategoryId);
        Assert.Equal(1, data.SaveCount);
    }

    [Fact]
    public async Task Update_rejects_self_parent_without_mutation_or_save()
    {
        var data = new FakeData();
        var category = Add(data, "Food", CategoryTransactionKind.Expense);

        await Assert.ThrowsAsync<ApplicationValidationException>(() =>
            new UpdateCategoryUseCase(data, data).ExecuteAsync(
                new(category.Id, "Changed", category.Id), Token));

        Assert.Equal("Food", category.Name);
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Archive_rejects_active_children_without_save()
    {
        var data = new FakeData();
        var parent = Add(data, "Living", CategoryTransactionKind.Expense);
        Add(data, "Food", CategoryTransactionKind.Expense, parent.Id);

        await Assert.ThrowsAsync<ConflictException>(() =>
            new ArchiveCategoryUseCase(data, data).ExecuteAsync(parent.Id, Token));

        Assert.False(parent.IsArchived);
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Archive_and_restore_valid_category_persist()
    {
        var data = new FakeData();
        var category = Add(data, "Food", CategoryTransactionKind.Expense);

        await new ArchiveCategoryUseCase(data, data).ExecuteAsync(category.Id, Token);
        Assert.True(category.IsArchived);
        await new RestoreCategoryUseCase(data, data).ExecuteAsync(category.Id, Token);

        Assert.False(category.IsArchived);
        Assert.Equal(2, data.SaveCount);
    }

    [Fact]
    public async Task Restore_rejects_archived_parent_without_save()
    {
        var data = new FakeData();
        var parent = Add(data, "Living", CategoryTransactionKind.Expense);
        var child = Add(data, "Food", CategoryTransactionKind.Expense, parent.Id);
        parent.Archive();
        child.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            new RestoreCategoryUseCase(data, data).ExecuteAsync(child.Id, Token));

        Assert.True(child.IsArchived);
        Assert.Equal(0, data.SaveCount);
    }

    [Fact]
    public async Task Missing_category_workflows_return_not_found_without_save()
    {
        var data = new FakeData();
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateCategoryUseCase(data, data).ExecuteAsync(new(id, "Missing", null), Token));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ArchiveCategoryUseCase(data, data).ExecuteAsync(id, Token));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new RestoreCategoryUseCase(data, data).ExecuteAsync(id, Token));

        Assert.Equal(0, data.SaveCount);
    }

    private static Category Add(
        FakeData data,
        string name,
        CategoryTransactionKind kind,
        Guid? parentId = null)
    {
        var category = new Category(name, kind, parentId);
        data.Categories.Add(category.Id, category);
        return category;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
