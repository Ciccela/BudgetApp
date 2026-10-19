using BudgetApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BudgetApp.Data
{
    public static class DbInitializer
    {
        public static async Task SeedAsync(BudgetContext context, UserManager<IdentityUser> userManager)
        {
            await context.Database.MigrateAsync();

            // Om inga users finns, skapa en demo-user
            var email = "demo@budgetapp.local";
            var demoUser = await userManager.FindByEmailAsync(email);
            if (demoUser == null)
            {
                demoUser = new IdentityUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(demoUser, "Demo123!");
                if (!result.Succeeded) return;
            }

            // Seeda endast demo-user om hen saknar kategorier
            bool hasCategories = await context.Categories.AnyAsync(c => c.UserId == demoUser.Id);
            if (!hasCategories)
            {
                var categories = new List<Category>
                {
                    new() { Name = "Mat", UserId = demoUser.Id },
                    new() { Name = "Boende", UserId = demoUser.Id },
                    new() { Name = "Transport", UserId = demoUser.Id },
                    new() { Name = "Nöje", UserId = demoUser.Id },
                    new() { Name = "Lön", UserId = demoUser.Id },
                    new() { Name = "Övrigt", UserId = demoUser.Id }
                };

                context.Categories.AddRange(categories);
                await context.SaveChangesAsync();
            }
        }
    }
}