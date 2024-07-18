using System;
using System.Runtime.InteropServices;

namespace WindowsCredentialProviderTest
{
    // see also: https://stackoverflow.com/questions/125341/how-do-you-do-impersonation-in-net/7250145#7250145 for impersonation info
    public static class PInvoke

    {
        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        // From http://www.pinvoke.net/default.aspx/secur32/LsaLogonUser.html

        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public /*PCHAR*/ IntPtr Buffer;
        }

        public class LsaStringWrapper : IDisposable
        {
            public LSA_STRING _string;

            public LsaStringWrapper(string value)
            {
                _string = new LSA_STRING
                {
                    Length = (ushort)value.Length,
                    MaximumLength = (ushort)value.Length,
                    Buffer = Marshal.StringToHGlobalAnsi(value)
                };
            }

            ~LsaStringWrapper()
            {
                Dispose(false);
            }

            void Dispose(bool disposing)
            {
                if (_string.Buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_string.Buffer);
                    _string.Buffer = IntPtr.Zero;
                }
                if (disposing)
                    GC.SuppressFinalize(this);
            }

            #region IDisposable Members

            public void Dispose()
            {
                // disposals should never throw
                try
                {
                    Dispose(true);
                } catch { };
            }

            #endregion
        }

        // https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-logonusera
        // added for impersonation attempts to test credentials returned
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool LogonUser(string lpszUsername, string lpszDomain, string lpszPassword, int dwLogonType, int dwLogonProvider, out IntPtr phToken);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredPackAuthenticationBuffer(
            int dwFlags,
            string pszUserName,
            string pszPassword,
            IntPtr pPackedCredentials,
            ref int pcbPackedCredentials);

        [DllImport("secur32.dll", SetLastError = false)]
        public static extern uint LsaConnectUntrusted([Out] out IntPtr lsaHandle);

        [DllImport("secur32.dll", SetLastError = false)]
        public static extern uint LsaLookupAuthenticationPackage([In] IntPtr lsaHandle, [In] ref LSA_STRING packageName, [Out] out UInt32 authenticationPackage);

        [DllImport("secur32.dll", SetLastError = false)]
        public static extern uint LsaDeregisterLogonProcess([In] IntPtr lsaHandle);

        [System.Runtime.InteropServices.DllImportAttribute("advapi32.dll", EntryPoint = "CredProtectW")]
        [return: System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool CredProtectW(
    [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.Bool)] bool fAsSelf,
    [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder? pszCredentials,
    uint cchCredentials, [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)] System.Text.StringBuilder pszProtectedCredentials,
    ref uint pcchMaxChars,
    ref int protectionType);

    }
}

public enum LogonType
{
    /// <summary>
    /// This logon type is intended for users who will be interactively using the computer, such as a user being logged on  
    /// by a terminal server, remote shell, or similar process.
    /// This logon type has the additional expense of caching logon information for disconnected operations; 
    /// therefore, it is inappropriate for some client/server applications,
    /// such as a mail server.
    /// </summary>
    LOGON32_LOGON_INTERACTIVE = 2,

    /// <summary>
    /// This logon type is intended for high performance servers to authenticate plaintext passwords.

    /// The LogonUser function does not cache credentials for this logon type.
    /// </summary>
    LOGON32_LOGON_NETWORK = 3,

    /// <summary>
    /// This logon type is intended for batch servers, where processes may be executing on behalf of a user without 
    /// their direct intervention. This type is also for higher performance servers that process many plaintext
    /// authentication attempts at a time, such as mail or Web servers. 
    /// The LogonUser function does not cache credentials for this logon type.
    /// </summary>
    LOGON32_LOGON_BATCH = 4,

    /// <summary>
    /// Indicates a service-type logon. The account provided must have the service privilege enabled. 
    /// </summary>
    LOGON32_LOGON_SERVICE = 5,

    /// <summary>
    /// This logon type is for GINA DLLs that log on users who will be interactively using the computer. 
    /// This logon type can generate a unique audit record that shows when the workstation was unlocked. 
    /// </summary>
    LOGON32_LOGON_UNLOCK = 7,

    /// <summary>
    /// This logon type preserves the name and password in the authentication package, which allows the server to make 
    /// connections to other network servers while impersonating the client. A server can accept plaintext credentials 
    /// from a client, call LogonUser, verify that the user can access the system across the network, and still 
    /// communicate with other servers.
    /// NOTE: Windows NT:  This value is not supported. 
    /// </summary>
    LOGON32_LOGON_NETWORK_CLEARTEXT = 8,

    /// <summary>
    /// This logon type allows the caller to clone its current token and specify new credentials for outbound connections.
    /// The new logon session has the same local identifier but uses different credentials for other network connections. 
    /// NOTE: This logon type is supported only by the LOGON32_PROVIDER_WINNT50 logon provider.
    /// NOTE: Windows NT:  This value is not supported. 
    /// </summary>
    LOGON32_LOGON_NEW_CREDENTIALS = 9,
}

public enum LogonProvider
{
    /// <summary>
    /// Use the standard logon provider for the system. 
    /// The default security provider is negotiate, unless you pass NULL for the domain name and the user name 
    /// is not in UPN format. In this case, the default provider is NTLM. 
    /// NOTE: Windows 2000/NT:   The default security provider is NTLM.
    /// </summary>
    LOGON32_PROVIDER_DEFAULT = 0,
    LOGON32_PROVIDER_WINNT35 = 1,
    LOGON32_PROVIDER_WINNT40 = 2,
    LOGON32_PROVIDER_WINNT50 = 3
}
[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct UNICODE_STRING
{

    /// USHORT->unsigned short
    public ushort Length;

    /// USHORT->unsigned short
    public ushort MaximumLength;

    /// PWSTR->WCHAR*
    [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
    public IntPtr Buffer;

    public UNICODE_STRING(string s)
    {
        Length = (ushort)(s.Length * 2);
        MaximumLength = (ushort)(Length + 2);
        Buffer = Marshal.StringToHGlobalUni(s);
    }

    public void Dispose()
    {
        Marshal.FreeHGlobal(Buffer);
        Buffer = IntPtr.Zero;
    }

    public override string? ToString()
    {
        return Marshal.PtrToStringUni(Buffer);
    }
}


[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct LUID
{

    /// DWORD->unsigned int
    public uint LowPart;

    /// LONG->int
    public int HighPart;
}
//LUID, * PLUID;

public enum _CRED_PROTECTION_TYPE
{
    CredUnprotected,
    CredUserProtection,
    CredTrustedProtection
}

[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct _KERB_INTERACTIVE_UNLOCK_LOGON
{
    public _KERB_INTERACTIVE_LOGON Logon;
    public LUID LogonId;
}
//KERB_INTERACTIVE_UNLOCK_LOGON, * PKERB_INTERACTIVE_UNLOCK_LOGON;

[System.Runtime.InteropServices.StructLayoutAttribute(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct _KERB_INTERACTIVE_LOGON
{
    public _KERB_LOGON_SUBMIT_TYPE MessageType;
    public UNICODE_STRING LogonDomainName;
    public UNICODE_STRING UserName;
    public UNICODE_STRING Password;
}
//KERB_INTERACTIVE_LOGON, * PKERB_INTERACTIVE_LOGON;

public enum _KERB_LOGON_SUBMIT_TYPE
{
    KerbInteractiveLogon = 2,
    KerbSmartCardLogon = 6,
    KerbWorkstationUnlockLogon = 7,
    KerbSmartCardUnlockLogon = 8,
    KerbProxyLogon = 9,
    KerbTicketLogon = 10,
    KerbTicketUnlockLogon = 11,
    KerbS4ULogon = 12,
    KerbCertificateLogon = 13,
    KerbCertificateS4ULogon = 14,
    KerbCertificateUnlockLogon = 15
}
//KERB_LOGON_SUBMIT_TYPE, * PKERB_LOGON_SUBMIT_TYPE;
