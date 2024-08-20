using System;
using System.Collections.Generic;
using System.Text;

namespace CredentialHelper.SourceGen;
// putting things in here that may fail to load at assembly time
static internal class Helpers
{
    public static string GetLastCommitHash()
    {
        try
        {
            return CredentialHelper.Reusable.ProcessAdapters.Git.getLastCommit();
            //return "";
        } catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
