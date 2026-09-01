using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Suma.Domain.Categories;

namespace Suma.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable(
            "categories",
            table => table.HasCheckConstraint("ck_categories_sort_order", "sort_order >= 0"));
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(category => category.ParentCategoryId)
            .HasColumnName("parent_category_id");
        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(category => category.TransactionKind)
            .HasColumnName("transaction_kind")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(category => category.IconKey)
            .HasColumnName("icon_key")
            .HasMaxLength(100);
        builder.Property(category => category.SortOrder)
            .HasColumnName("sort_order");
        builder.Property(category => category.IsSystem)
            .HasColumnName("is_system");
        builder.Property(category => category.IsArchived)
            .HasColumnName("is_archived");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(category => category.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
