using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsCredentialProviderTest
{
    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string value) => String.IsNullOrEmpty(value);
    }
}
