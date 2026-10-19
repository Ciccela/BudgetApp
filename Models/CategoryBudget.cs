using System.ComponentModel.DataAnnotations;

namespace BudgetApp.Models
{
    public class CategoryBudget
    {
        public int Id { get; set; }

        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Range(2000, 2100)]
        public int Year { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal Amount { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
    }
}