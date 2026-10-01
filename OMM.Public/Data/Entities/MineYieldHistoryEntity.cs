namespace OMM.Public.Data.Entities;

public class MineYieldHistoryEntity : UserOwnedEntity
{
    public Guid MineId { get; set; }

    public MineEntity? Mine { get; set; }

    /// <summary>Calendar year of the declaration (e.g. 2024, 2025) or placement year.</summary>
    public int EffectiveYear { get; set; }

    /// <summary>Exact declaration or placement date.</summary>
    public DateOnly RecordDate { get; set; }

    /// <summary>Declared rate % (e.g. 5.50 for 5.5% EPF dividend, or 3.85 for 3.85% FD rate).</summary>
    public decimal RatePct { get; set; }

    /// <summary>Actual dividend/interest amount in MYR (optional, e.g. RM 2,400).</summary>
    public decimal? DeclaredAmount { get; set; }

    /// <summary>Total balance or principal at the time of declaration/placement.</summary>
    public decimal? BalanceAtTime { get; set; }

    /// <summary>Optional label or notes (e.g. "Simpanan Konvensional", "12-mo Promotional FD").</summary>
    public string? Notes { get; set; }
}
