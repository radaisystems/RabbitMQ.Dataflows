using static RadAI.Data.Database.Enums;

namespace RadAI.Data.Database;

public class Order
{
    public string Field { get; set; }
    public OrderDirection? Direction { get; set; }
}
