using System.Text.RegularExpressions;

namespace YourApp.Services;

public static class SongQueryParser
{
    public static string BuildQueryFromFileName(string fileNameWithoutExt)
    {
        var q = fileNameWithoutExt.Replace("-", " ").Replace("_", " ").Trim();
        q = Regex.Replace(q, @"\s+", " ");
        return q;
    }
}
