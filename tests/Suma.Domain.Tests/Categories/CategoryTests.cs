using Suma.Domain.Categories;
using Xunit;

namespace Suma.Domain.Tests.Categories;

public sealed class CategoryTests
{
    [Fact]
    public void Create_WithValidValues_CreatesActiveCategory()
    {
        var parentId = Guid.NewGuid();

        var category = new Category(
            "Groceries",
            CategoryTransactionKind.Expense,
            parentId,
            "cart",
            10,
            true);

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Groceries", category.Name);
        Assert.Equal(CategoryTransactionKind.Expense, category.TransactionKind);
        Assert.Equal(parentId, category.ParentCategoryId);
        Assert.Equal("cart", category.IconKey);
        Assert.Equal(10, category.SortOrder);
        Assert.True(category.IsSystem);
        Assert.False(category.IsArchived);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_IsRejected(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Category(name!, CategoryTransactionKind.Expense));
    }

    [Fact]
    public void Create_WithNegativeSortOrder_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Category("Groceries", CategoryTransactionKind.Expense, sortOrder: -1));
    }

    [Fact]
    public void SetParentCategory_ToOwnId_IsRejected()
    {
        var category = new Category("Groceries", CategoryTransactionKind.Expense);

        Assert.Throws<ArgumentException>(() => category.SetParentCategory(category.Id));
    }

    [Fact]
    public void Archive_ArchivesCategory()
    {
        var category = CreateCategory();

        category.Archive();

        Assert.True(category.IsArchived);
    }

    [Fact]
    public void Restore_RestoresArchivedCategory()
    {
        var category = CreateCategory();
        category.Archive();

        category.Restore();

        Assert.False(category.IsArchived);
    }

    [Fact]
    public void Rename_UsesTrimmedNonEmptyName()
    {
        var category = CreateCategory();

        category.Rename("  Dining  ");

        Assert.Equal("Dining", category.Name);
        Assert.ThrowsAny<ArgumentException>(() => category.Rename(" "));
    }

    private static Category CreateCategory() =>
        new("Groceries", CategoryTransactionKind.Expense);
}
