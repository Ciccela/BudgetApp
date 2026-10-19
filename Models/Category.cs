using System.ComponentModel.DataAnnotations;

namespace BudgetApp.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Namn är obligatoriskt.")]
        [StringLength(60)]
        [Display(Name = "Namn")]
        public string Name { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
        public ICollection<CategoryBudget> Budgets { get; set; } = new List<CategoryBudget>();
    }
}