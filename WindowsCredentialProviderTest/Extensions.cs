using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CredentialHelper;

namespace WindowsCredentialProviderTest
{
    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string value) => String.IsNullOrEmpty(value);
        public static string Before(this string value, string delimiter) => CHelpers.before(value, delimiter);
        public static string After(this string value, string delimiter) => CHelpers.after(value, delimiter);
    }
}
