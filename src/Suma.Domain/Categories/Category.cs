using Suma.Domain.Common;

namespace Suma.Domain.Categories;

public sealed class Category : Entity
{
    public Category(
        string name,
        CategoryTransactionKind transactionKind,
        Guid? parentCategoryId = null,
        string? iconKey = null,
        int sortOrder = 0,
        bool isSystem = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(transactionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(transactionKind),
                transactionKind,
                "Category transaction kind is not supported.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Sort order cannot be negative.");
        }

        Name = name.Trim();
        TransactionKind = transactionKind;
        IconKey = iconKey;
        SortOrder = sortOrder;
        IsSystem = isSystem;
        SetParentCategory(parentCategoryId);
    }

    public string Name { get; private set; }

    public CategoryTransactionKind TransactionKind { get; }

    public Guid? ParentCategoryId { get; private set; }

    public string? IconKey { get; }

    public int SortOrder { get; }

    public bool IsSystem { get; }

    public bool IsArchived { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void SetParentCategory(Guid? parentCategoryId)
    {
        if (parentCategoryId == Id)
        {
            throw new ArgumentException("Category cannot be its own parent.", nameof(parentCategoryId));
        }

        ParentCategoryId = parentCategoryId;
    }

    public void Archive() => IsArchived = true;

    public void Restore() => IsArchived = false;
}
