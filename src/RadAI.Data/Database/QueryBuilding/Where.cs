using static RadAI.Data.Database.Enums;

namespace RadAI.Data.Database;

public class Where
{
    public string Field { get; set; }
    public WhereLogic? Logic { get; set; } = null;
    public WhereAction Action { get; set; }
    public string[] Parameters { get; set; }

    // For String Actions
    public bool CaseSensitive { get; set; }
    public string EscapeCharacter { get; set; }
}
