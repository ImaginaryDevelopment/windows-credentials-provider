using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CredentialHelper.UI;
public static class PartialGen
{
    public static DateTime Built => CredentialHelper.Generated.PartialGen.Built;
    public static string LastCommitHash => CredentialHelper.Generated.PartialGen.LastCommitHash;
}
