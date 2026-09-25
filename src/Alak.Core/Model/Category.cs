namespace Alak.Core.Model;

/// <summary>
/// The nine primary categories (spec §6.1). Each notification has exactly one;
/// the Codes wallet is a derived collection, not a category.
/// </summary>
public enum Category
{
    Offers,
    Work,
    People,
    Money,
    Orders,
    Security,
    Social,
    Updates,
    Other,
}

public static class CategoryExtensions
{
    private static readonly Dictionary<Category, string> s_ids = new()
    {
        [Category.Offers] = "offers",
        [Category.Work] = "work",
        [Category.People] = "people",
        [Category.Money] = "money",
        [Category.Orders] = "orders",
        [Category.Security] = "security",
        [Category.Social] = "social",
        [Category.Updates] = "updates",
        [Category.Other] = "other",
    };

    /// <summary>
    /// Conflict priority (safety first, spec §6.1 / RUL-018):
    /// security > money > work > orders > people > offers > social > updates > other.
    /// Lower number = higher priority. Breaks ties only; never outranks strong contrary evidence.
    /// </summary>
    public static int Priority(this Category c) => c switch
    {
        Category.Security => 0,
        Category.Money => 1,
        Category.Work => 2,
        Category.Orders => 3,
        Category.People => 4,
        Category.Offers => 5,
        Category.Social => 6,
        Category.Updates => 7,
        Category.Other => 8,
        _ => 8,
    };

    /// <summary>The stable lower-kebab id used in storage and packs (spec §6.1).</summary>
    public static string ToKebabId(this Category c)
        => s_ids.TryGetValue(c, out var id) ? id : "other";

    public static bool TryParse(string? id, out Category category)
    {
        category = Category.Other;
        if (string.IsNullOrEmpty(id)) return false;
        foreach (var (c, kebab) in s_ids)
            if (string.Equals(kebab, id, StringComparison.OrdinalIgnoreCase))
            {
                category = c;
                return true;
            }
        return false;
    }
}
