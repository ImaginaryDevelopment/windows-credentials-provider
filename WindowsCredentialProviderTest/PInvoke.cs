using System;
using System.Runtime.InteropServices;

namespace WindowsCredentialProviderTest
{
    public static class PInvoke
    {
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
    }
}
