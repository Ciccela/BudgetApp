using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BudgetApp.Models
{
    public class Transaction
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Beskrivning är obligatorisk.")]
        [StringLength(100, ErrorMessage = "Beskrivning får max vara 100 tecken.")]
        [Display(Name = "Beskrivning")]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 999999999, ErrorMessage = "Belopp måste vara större än 0.")]
        [Display(Name = "Belopp")]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Datum")]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "Inkomst")]
        public bool IsIncome { get; set; }

        [Display(Name = "Kategori")]
        [Range(1, int.MaxValue, ErrorMessage = "Välj en kategori.")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Display(Name = "Återkommande")]
        public bool IsRecurring { get; set; } = false;

        [Range(1, 28, ErrorMessage = "Dag i månaden måste vara mellan 1 och 28.")]
        [Display(Name = "Dag i månaden")]
        public int RecurringDayOfMonth { get; set; } = 1;

        public string UserId { get; set; } = string.Empty;
    }
}