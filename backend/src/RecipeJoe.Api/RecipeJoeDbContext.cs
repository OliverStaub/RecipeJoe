using Microsoft.EntityFrameworkCore;
using RecipeJoe.Api.Images;
using RecipeJoe.Api.Recipes;

namespace RecipeJoe.Api;

internal sealed class RecipeJoeDbContext(DbContextOptions<RecipeJoeDbContext> options) : DbContext(options)
{
    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeImage> RecipeImages => Set<RecipeImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Recipe>(recipe =>
        {
            recipe.Property(r => r.Title).IsRequired();
            recipe.Property(r => r.SourceUrl).IsRequired();

            recipe.OwnsMany(
                r => r.IngredientLines,
                lines =>
                {
                    lines.ToTable("IngredientLines");
                    lines.WithOwner().HasForeignKey("RecipeId");
                    lines.HasKey("RecipeId", nameof(IngredientLine.Position));
                    lines.Property(l => l.Position).ValueGeneratedNever();
                    lines.Property(l => l.Text).IsRequired();
                }
            );

            recipe.OwnsMany(
                r => r.Steps,
                steps =>
                {
                    steps.ToTable("Steps");
                    steps.WithOwner().HasForeignKey("RecipeId");
                    steps.HasKey("RecipeId", nameof(Step.Position));
                    steps.Property(s => s.Position).ValueGeneratedNever();
                    steps.Property(s => s.Text).IsRequired();
                }
            );

            recipe.Navigation(r => r.IngredientLines).AutoInclude();
            recipe.Navigation(r => r.Steps).AutoInclude();
        });

        modelBuilder.Entity<RecipeImage>(image =>
        {
            image.ToTable("RecipeImages");
            image.HasKey(i => i.RecipeId);
            image.Property(i => i.RecipeId).ValueGeneratedNever();
            image.Property(i => i.ContentType).IsRequired();
            image.Property(i => i.Bytes).IsRequired();
            image.HasOne<Recipe>().WithOne(r => r.Image).HasForeignKey<RecipeImage>(i => i.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
