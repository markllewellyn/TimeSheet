namespace TimeSheet.Infrastructure.Services;

/// <summary>Splits a Fixed Fee project's recognized revenue across report/breakdown rows (per user, role, team or
/// staff member) in proportion to each row's hours. RevenueRecognitionService recognizes revenue linearly in hours
/// (hours / BudgetHours * FixedFeeAmount), so an hours-proportional split is the same rule applied per row rather
/// than a second way of counting the same money.</summary>
public static class RevenueAllocation
{
    /// <summary>Each share is rounded to 2dp (matching CurrencyConversionService's own rounding), and whatever
    /// rounding leaves over goes to the row with the most hours - so the shares always sum to exactly
    /// <paramref name="total"/>, never a penny off the summary figure. All zeros when there are no hours.</summary>
    public static decimal[] ByHours(decimal total, IReadOnlyList<decimal> hours)
    {
        var shares = new decimal[hours.Count];
        var totalHours = hours.Sum();
        if (hours.Count == 0 || totalHours <= 0) return shares;

        for (var i = 0; i < hours.Count; i++)
        {
            shares[i] = Math.Round(total * hours[i] / totalHours, 2);
        }

        var remainder = total - shares.Sum();
        if (remainder != 0)
        {
            var largest = 0;
            for (var i = 1; i < hours.Count; i++)
            {
                if (hours[i] > hours[largest]) largest = i;
            }
            shares[largest] += remainder;
        }
        return shares;
    }
}
