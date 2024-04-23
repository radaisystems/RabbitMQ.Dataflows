using static RadAI.Data.Database.Enums;

namespace RadAI.Data.Database;

public class Join
{
    public JoinType Type { get; set; }

    public Table ToTable { get; set; }

    public string Field { get; set; }
    public string OnField { get; set; }
}
