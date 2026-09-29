using Microsoft.EntityFrameworkCore;

namespace RecipeJoe.Api;

internal sealed class RecipeJoeDbContext(DbContextOptions<RecipeJoeDbContext> options) : DbContext(options);
