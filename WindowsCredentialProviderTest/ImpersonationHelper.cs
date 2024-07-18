using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace WindowsCredentialProviderTest;
public static class ImpersonationHelper
{
    // see also https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-logonusera
    static IntPtr? GetUser(CredentialHelper.ApiClient.VerificationResult credentials)
    {
        if(PInvoke.LogonUser(credentials.Username, credentials.Domain, credentials.Password, (int)LogonType.LOGON32_LOGON_INTERACTIVE, (int)LogonProvider.LOGON32_PROVIDER_DEFAULT, out var userToken)){
            return userToken;
        }
        return null;
    }

    public static bool RunImpersonated(CredentialHelper.ApiClient.VerificationResult credentials, Action toRun) {
            if(GetUser(credentials) is { } userToken)
        {
            using var wi = new WindowsIdentity(userToken);
            using var imp = wi.Impersonate();
            toRun();
            //WindowsIdentity.RunImpersonated(userToken, toRun);
            return true;
        }

        return false;
    }
}
