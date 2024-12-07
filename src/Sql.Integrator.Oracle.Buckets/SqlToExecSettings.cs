namespace Sql.Integrator.Oracle.Buckets;

public class SqlToExecSettings
{
    public string ConnectionString { get; set; }
    public string StoredProcedureName { get; set; }
    public Dictionary<string, string> SqlParametersMapping { get; set; }
    
}