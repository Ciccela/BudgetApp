using BudgetApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Data
{
    public class BudgetContext : IdentityDbContext
    {
        public BudgetContext(DbContextOptions<BudgetContext> options) : base(options) { }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<CategoryBudget> CategoryBudgets => Set<CategoryBudget>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<CategoryBudget>()
                .HasIndex(x => new { x.UserId, x.CategoryId, x.Year, x.Month })
                .IsUnique();

            builder.Entity<CategoryBudget>()
                .HasOne(x => x.Category)
                .WithMany(c => c.Budgets)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}