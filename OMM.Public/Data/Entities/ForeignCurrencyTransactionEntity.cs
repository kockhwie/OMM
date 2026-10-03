using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OMM.Public.Models;

namespace OMM.Public.Data.Entities;

[Table("foreign_currency_transaction")]
public class ForeignCurrencyTransactionEntity : UserOwnedEntity
{
    [Required]
    [Column("mine_id")]
    public Guid MineId { get; set; }

    [ForeignKey(nameof(MineId))]
    public MineEntity? Mine { get; set; }

    [Column("transaction_type")]
    public ForeignCurrencyTransactionType TransactionType { get; set; } = ForeignCurrencyTransactionType.Purchase;

    [Column("transaction_date")]
    public DateOnly TransactionDate { get; set; }

    [Column("foreign_amount", TypeName = "decimal(24,8)")]
    public decimal ForeignAmount { get; set; }

    [Column("myr_amount", TypeName = "decimal(18,2)")]
    public decimal MyrAmount { get; set; }

    /// <summary>Foreign currency units received or sold per one MYR.</summary>
    [Column("exchange_rate", TypeName = "decimal(18,8)")]
    public decimal ExchangeRate { get; set; }

    [Column("fees_myr", TypeName = "decimal(18,2)")]
    public decimal FeesMyr { get; set; }

    [MaxLength(300)]
    [Column("notes")]
    public string? Notes { get; set; }
}