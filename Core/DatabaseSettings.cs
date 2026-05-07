namespace SANJET.Core
{
    /// <summary>
    /// 保存應用程式執行期間使用的 SQLite 資料庫路徑與連接字串。
    /// </summary>
    public sealed record DatabaseSettings(string Path, string ConnectionString);
}

