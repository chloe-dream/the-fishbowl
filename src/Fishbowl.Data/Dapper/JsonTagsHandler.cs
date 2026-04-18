using System.Data;
using System.Text.Json;
using Dapper;

namespace Fishbowl.Data.Dapper;

/// <summary>
/// Serializes List&lt;string&gt; columns as JSON text. Used for `notes.tags`.
/// </summary>
public class JsonTagsHandler : SqlMapper.TypeHandler<List<string>>
{
    public override List<string>? Parse(object value)
    {
        if (value is null or DBNull) return new List<string>();
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return new List<string>();
        return JsonSerializer.Deserialize<List<string>>(s) ?? new List<string>();
    }

    public override void SetValue(IDbDataParameter parameter, List<string>? value)
    {
        parameter.DbType = DbType.String;
        parameter.Value = JsonSerializer.Serialize(value ?? new List<string>());
    }
}
