using System.Text.RegularExpressions;

namespace Atamatay.Utilities
{
    public class Validation
    {
        public static bool IsUrl(string url)
        {
            const string pattern = @"^(https?|ftp):\/\/[^\s/$.?#].[^\s]*$";
            return Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase);
        }
    }
}
