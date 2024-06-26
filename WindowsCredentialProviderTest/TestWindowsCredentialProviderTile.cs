﻿// Uncomment for autologin
// #define AUTOLOGIN

using CredentialProvider.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using WindowsCredentialProviderTest.Properties;

using CredProviderFieldStruct = CredentialProvider.Interop._CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR;
using CredProviderUsageEnum = CredentialProvider.Interop._CREDENTIAL_PROVIDER_USAGE_SCENARIO;

namespace WindowsCredentialProviderTest
{
    [ComVisible(true)]
    [Guid(Constants.CredentialProviderTileUID)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class TestWindowsCredentialProviderTile : ITestWindowsCredentialProviderTile
    {

        ICredentialProviderCredentialEvents credentialProviderCredentialEvents;

        // more recent example has this:
        bool IsUnlock { get; set; }
        // more recent example has this:
        string CurrentConsoleUser { get; set; } = null;


#if AUTOLOGIN
        TimerOnDemandLogon timerOnDemandLogon;
        bool shouldAutoLogin;
#endif

        public TestWindowsCredentialProviderTile(
            TestWindowsCredentialProvider testWindowsCredentialProvider,
            CredProviderUsageEnum usageScenario
        )
        {
            this.testWindowsCredentialProvider = testWindowsCredentialProvider;
            this.usageScenario = usageScenario;

            // more recent example has these:
            this.IsUnlock = usageScenario.HasFlag(_CREDENTIAL_PROVIDER_USAGE_SCENARIO.CPUS_UNLOCK_WORKSTATION);
            //this.CurrentConsoleUser = PInvoke.GetConsoleUser();
        }

        public string GetLabel()
        {
            string label = "QRCode Logon";
            if (this.IsUnlock && !string.IsNullOrWhiteSpace(this.CurrentConsoleUser))
            {
                label = "QRCode:" + this.CurrentConsoleUser;
            }
            return label;
        }

        // variables presumably here for debugging visibility
        // https://stackoverflow.com/questions/3820985/suppressing-is-never-used-and-is-never-assigned-to-warnings-in-c-sharp
#pragma warning disable IDE0052 // Remove unread private members
        readonly TestWindowsCredentialProvider testWindowsCredentialProvider;
        readonly CredProviderUsageEnum usageScenario;
#pragma warning restore IDE0052 // Remove unread private members

        public List<CredProviderFieldStruct> CredentialProviderFieldDescriptorList => new List<CredProviderFieldStruct>(){
            new CredProviderFieldStruct
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,
                dwFieldID = 0,
                pszLabel = this.GetLabel() // "Rebootify Awesomeness",
            },
            new CredProviderFieldStruct
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON,
                dwFieldID = 1,
                pszLabel = "Login",
            },
            // this was added to get a tile working
            new CredProviderFieldStruct
            {
                cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_TILE_IMAGE,
                guidFieldType = Guid.Parse("2d837775-f6cd-464e-a745-482fd0b47493"),

                dwFieldID = 2,
                pszLabel = "Icon",
            }
        };

        public int Advise(ICredentialProviderCredentialEvents pcpce)
        {
            Log.LogMethodCall();

            if (pcpce != null)
            {
                credentialProviderCredentialEvents = pcpce;
                var intPtr = Marshal.GetIUnknownForObject(pcpce);
                Marshal.AddRef(intPtr);
            }

            return HResultValues.S_OK;
        }

        public int UnAdvise()
        {
            Log.LogMethodCall();

            if (credentialProviderCredentialEvents != null)
            {
                var intPtr = Marshal.GetIUnknownForObject(credentialProviderCredentialEvents);
                Marshal.Release(intPtr);
                credentialProviderCredentialEvents = null;
            }

            return HResultValues.S_OK;
        }

        public int SetSelected(out int pbAutoLogon)
        {
            Log.LogMethodCall();

#if AUTOLOGIN
            if (!shouldAutoLogin)
            {
                timerOnDemandLogon = new TimerOnDemandLogon(
                    testWindowsCredentialProvider.CredentialProviderEvents,
                    credentialProviderCredentialEvents,
                    this,
                    CredentialProviderFieldDescriptorList[0].dwFieldID,
                    testWindowsCredentialProvider.CredentialProviderEventsAdviseContext);

                timerOnDemandLogon.TimerEnded += TimerOnDemandLogon_TimerEnded;

                pbAutoLogon = 0;
            }
            else
            {
                // We got the info from the async timer
                pbAutoLogon = 1;
            }
#else
            pbAutoLogon = 0; // Auto-logon when the tile is selected
#endif

            return HResultValues.S_OK;
        }

#if AUTOLOGIN
        void TimerOnDemandLogon_TimerEnded()
        {
            // Sync other data from your async service here
            shouldAutoLogin = true;
        }
#endif

        public int SetDeselected()
        {
            Log.LogMethodCall();

#if AUTOLOGIN
            timerOnDemandLogon?.Dispose();
            timerOnDemandLogon = null;
#endif

            return HResultValues.E_NOTIMPL;
        }

        public int GetFieldState(uint dwFieldID, out _CREDENTIAL_PROVIDER_FIELD_STATE pcpfs,
            out _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE pcpfis)
        {
            Log.LogMethodCall();
            pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
            return HResultValues.S_OK;
        }

        public int GetStringValue(uint dwFieldID, out string ppsz)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new []
            {
                _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,
                _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_LARGE_TEXT,
            });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                ppsz = string.Empty;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);

            ppsz = descriptor.pszLabel;
            return HResultValues.S_OK;
        }

        public int GetBitmapValue(uint dwFieldID, IntPtr phbmp)
        {
            Log.LogMethodCall();

            //var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_TILE_IMAGE });

            //if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            //{
            //    phbmp = IntPtr.Zero;
            //    return HResultValues.E_NOTIMPL;
            //}

            //var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
            //phbmp = IntPtr.Zero; // TODO: show a bitmap
            Marshal.WriteIntPtr(phbmp, Resources.qr_code_148603_640.GetHbitmap());

            return HResultValues.S_OK;
        }

        public int GetCheckboxValue(uint dwFieldID, out int pbChecked, out string ppszLabel)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_CHECKBOX });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                pbChecked = 0;
                ppszLabel = string.Empty;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
            pbChecked = 0; // TODO: selection state
            ppszLabel = descriptor.pszLabel;

            return HResultValues.E_NOTIMPL;
        }

        public int GetSubmitButtonValue(uint dwFieldID, out uint pdwAdjacentTo)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new [] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                pdwAdjacentTo = 0;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);

            pdwAdjacentTo = descriptor.dwFieldID - 1; // TODO: selection state

            return HResultValues.S_OK;
        }

        public int GetComboBoxValueCount(uint dwFieldID, out uint pcItems, out uint pdwSelectedItem)
        {
            Log.LogMethodCall();

            var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_COMBOBOX });

            if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
            {
                pcItems = 0;
                pdwSelectedItem = 0;
                return HResultValues.E_NOTIMPL;
            }

            var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
            pcItems = 0; // TODO: selection state
            pdwSelectedItem = 0;

            return HResultValues.E_NOTIMPL;
        }

        public int GetComboBoxValueAt(uint dwFieldID, uint dwItem, out string ppszItem)
        {
            Log.LogMethodCall();
            ppszItem = string.Empty;
            return HResultValues.E_NOTIMPL;
        }

        public int SetStringValue(uint dwFieldID, string psz)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int SetCheckboxValue(uint dwFieldID, int bChecked)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int SetComboBoxSelectedValue(uint dwFieldID, uint dwSelectedItem)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int CommandLinkClicked(uint dwFieldID)
        {
            Log.LogMethodCall();

            // TODO: change state

            return HResultValues.E_NOTIMPL;
        }

        public int GetSerialization(out _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE pcpgsr,
            out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs, out string ppszOptionalStatusText,
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            Log.LogMethodCall();

            try
            {
                pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_RETURN_CREDENTIAL_FINISHED;
                pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();

                var username = "<domain>\\<username>";
                var password = "<password>";
                var inCredSize = 0;
                var inCredBuffer = Marshal.AllocCoTaskMem(0);

                if (!PInvoke.CredPackAuthenticationBuffer(0, username, password, inCredBuffer, ref inCredSize))
                {
                    Marshal.FreeCoTaskMem(inCredBuffer);
                    inCredBuffer = Marshal.AllocCoTaskMem(inCredSize);

                    if (PInvoke.CredPackAuthenticationBuffer(0, username, password, inCredBuffer, ref inCredSize))
                    {
                        ppszOptionalStatusText = string.Empty;
                        pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;

                        pcpcs.clsidCredentialProvider = Guid.Parse(Constants.CredentialProviderUID);
                        pcpcs.rgbSerialization = inCredBuffer;
                        pcpcs.cbSerialization = (uint)inCredSize;

                        RetrieveNegotiateAuthPackage(out var authPackage);
                        pcpcs.ulAuthenticationPackage = authPackage;

                        return HResultValues.S_OK;
                    }

                    ppszOptionalStatusText = "Failed to pack credentials";
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
                    return HResultValues.E_FAIL;
                }
            }
            catch (Exception)
            {
                // In case of any error, do not bring down winlogon
            }
            finally
            {
#if AUTOLOGIN
                shouldAutoLogin = false; // Block auto-login from going full-retard
#endif
            }

            pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
            pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();
            ppszOptionalStatusText = string.Empty;
            pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_NONE;
            return HResultValues.E_NOTIMPL;
        }

        public int ReportResult(int ntsStatus, int ntsSubstatus, out string ppszOptionalStatusText,
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            Log.LogMethodCall();
            ppszOptionalStatusText = string.Empty;
            pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_NONE;
            return HResultValues.E_NOTIMPL;
        }

        int RetrieveNegotiateAuthPackage(out uint authPackage)
        {
            // TODO: better checking on the return codes

            var status = PInvoke.LsaConnectUntrusted(out var lsaHandle);

            using (var name = new PInvoke.LsaStringWrapper("Negotiate"))
            {
                status = PInvoke.LsaLookupAuthenticationPackage(lsaHandle, ref name._string, out authPackage);
            }

            PInvoke.LsaDeregisterLogonProcess(lsaHandle);

            return (int)status;
        }

        Func<CredProviderFieldStruct, bool> FieldSearchFunctionGenerator(uint dwFieldID, _CREDENTIAL_PROVIDER_FIELD_TYPE[] allowedFieldTypes)
        {
            return x =>
                x.dwFieldID == dwFieldID
                && allowedFieldTypes.Contains(x.cpft);
        }
    }
}
