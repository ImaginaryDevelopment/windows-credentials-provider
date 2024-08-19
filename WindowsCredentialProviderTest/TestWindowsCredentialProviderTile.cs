namespace WindowsCredentialProviderTest;

using CredentialProvider.Interop;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

using WindowsCredentialProviderTest.Properties;

using CredProviderFieldStruct = CredentialProvider.Interop._CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR;
using CredProviderUsageEnum = CredentialProvider.Interop._CREDENTIAL_PROVIDER_USAGE_SCENARIO;


[ComVisible(true)]
[Guid(Constants.CredentialProviderTileUID)]
[ClassInterface(ClassInterfaceType.None)]
public sealed class TestWindowsCredentialProviderTile : ITestWindowsCredentialProviderTile
{

    ICredentialProviderCredentialEvents credentialProviderCredentialEvents;
    NetworkCredential _credential;

    // ui offers to make this a singleton lock
    readonly object _testUILock = new();
    CredentialHelper.UI.Form1 _form1;

    // more recent example has this:
    bool IsUnlock { get; set; }
    // more recent example has this:
    string CurrentConsoleUser { get; set; } = null;

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

    void InitUI()
    {
        _form1 ??= new CredentialHelper.UI.Form1((txt,ll) => Log.LogText(txt,ll));
        _form1.FormClosed += this._form1_FormClosed;
        //_form1.OnCredentialSubmit += this._form1_OnCredentialSubmit;
    }

    void _form1_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
    {
        Log.LogMethodCall();

        _form1?.RequestCancellation();

        if (_form1?.VerificationResult != null)
        {
            var vr = _form1.VerificationResult;
            var isValuePwd = !String.IsNullOrWhiteSpace(vr.Password);
            //var valuePwdTxt = isValuePwd ? 'v' : 'n';
            Log.LogText($"Got credential for user:{vr.Domain}\\{vr.Username}-{isValuePwd}");

            this._credential = new NetworkCredential(_form1.VerificationResult.Username, _form1.VerificationResult.Password, _form1.VerificationResult.Domain);
            this.testWindowsCredentialProvider.CredentialProviderEvents?.CredentialsChanged(this.testWindowsCredentialProvider.AdviseContext);

        }
        onDeselected("form closed");
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

    public List<CredProviderFieldStruct> CredentialProviderFieldDescriptorList => new(){
        new() {
            cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,
            dwFieldID = 0,
            pszLabel = this.GetLabel() // "Rebootify Awesomeness",
        },
        new() {
            cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON,
            dwFieldID = 1,
            pszLabel = "Login",
        },
        // this was added to get a tile working
        new() {
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

    int ICredentialProviderCredential.UnAdvise()
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

    int onSetSelectedRequest(string reason, out int pbAutoLogon)
    {
        Log.LogTextWithCaller($"{nameof(onSetSelectedRequest)}:{reason}");

        if (this._credential != null)
        {
            pbAutoLogon = 1;
            return HResultValues.S_OK;
        }
        lock (_testUILock)
        {
            InitUI();
            try
            {
                this._form1.Show();
                pbAutoLogon = 0;
                if (this._form1 == null)
                {
                    return HResultValues.E_INVALIDARG;
                }
                return HResultValues.S_OK;
            } catch (ObjectDisposedException)
            {
                this._credential = null;
                this._form1 = null;
                InitUI();
                this._form1.Show();
                pbAutoLogon = 0;
                return HResultValues.S_OK;
            }

        }
    }

    int ICredentialProviderCredential.SetSelected(out int pbAutoLogon)
    {
        Log.LogMethodCall();
        return onSetSelectedRequest("SetSelected", out pbAutoLogon);
    }

    public int onDeselected(string reason)
    {
        Log.LogText($"Deselected:{reason}");
        lock (_testUILock)
        {
            if (_form1 != null)
            {
                if (!_form1.IsDisposed)
                {

                    try
                    {
                        _form1.Hide();
                    } catch { }
                    try
                    {
                        _form1.Dispose();
                    } catch { }
                    _form1 = null;
                }
            }
            return HResultValues.S_OK;
        }

    }

    public int SetDeselected()
    {
        Log.LogMethodCall();
        return onDeselected("SetDeselected");

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

        var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[]
        {
            _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT,
            _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_LARGE_TEXT,
        });

        if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
        {
            ppsz = string.Empty;
            return HResultValues.E_NOTIMPL;
        }

        // HACK: this looks like bad code
        // TODOL investigate this not using first or default
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

        // HACK: this looks like bad code
        // TODOL investigate this not using first or default
        var descriptor = CredentialProviderFieldDescriptorList.First(searchFunction);
        pbChecked = 0; // TODO: selection state
        ppszLabel = descriptor.pszLabel;

        return HResultValues.E_NOTIMPL;
    }

    public int GetSubmitButtonValue(uint dwFieldID, out uint pdwAdjacentTo)
    {
        Log.LogMethodCall();

        var searchFunction = FieldSearchFunctionGenerator(dwFieldID, new[] { _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON });

        if (!CredentialProviderFieldDescriptorList.Any(searchFunction))
        {
            pdwAdjacentTo = 0;
            return HResultValues.E_NOTIMPL;
        }

        // HACK: this looks like bad code
        // TODOL investigate this not using first or default
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

        // HACK: this looks like bad code
        // TODOL investigate this not using first or default
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

    static unsafe void _UnicodeStringPackedUnicodeStringCopy(UNICODE_STRING rus, byte* pwzBuffer, UNICODE_STRING* pus)
    {
        pus->Length = rus.Length;
        pus->MaximumLength = rus.Length;
        pus->Buffer = new IntPtr(pwzBuffer);
        PInvoke.CopyMemory(pus->Buffer, rus.Buffer, pus->Length);
    }

    public unsafe int AttemptUnsafeLogin(UNICODE_STRING uniPwd,
        out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs, out string ppszOptionalStatusText,
        out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
    {
        Log.LogMethodCall();
        _KERB_INTERACTIVE_LOGON pkilIn;
        _KERB_INTERACTIVE_UNLOCK_LOGON kiul;
        pkilIn.LogonDomainName = new UNICODE_STRING(_credential.Domain);
        pkilIn.MessageType = _KERB_LOGON_SUBMIT_TYPE.KerbWorkstationUnlockLogon;
        pkilIn.Password = uniPwd;
        pkilIn.UserName = new UNICODE_STRING(_credential.UserName);
        kiul.Logon = pkilIn;
        kiul.LogonId = new LUID() { HighPart = 0, LowPart = 0 };

        int cb = sizeof(_KERB_INTERACTIVE_UNLOCK_LOGON) + pkilIn.LogonDomainName.Length + pkilIn.UserName.Length + pkilIn.Password.Length;

        var pkiulOut = (_KERB_INTERACTIVE_UNLOCK_LOGON*)Marshal.AllocCoTaskMem(cb);


        byte* pbBuffer = (byte*)pkiulOut + sizeof(_KERB_INTERACTIVE_UNLOCK_LOGON);



        _KERB_INTERACTIVE_LOGON* pkilOut = &pkiulOut->Logon;

        pkilOut->MessageType = pkilIn.MessageType;

        _UnicodeStringPackedUnicodeStringCopy(pkilIn.LogonDomainName, pbBuffer, &pkilOut->LogonDomainName);
        pkilOut->LogonDomainName.Buffer = new IntPtr((byte*)(pbBuffer - (byte*)pkiulOut));
        pbBuffer += pkilOut->LogonDomainName.Length;

        _UnicodeStringPackedUnicodeStringCopy(pkilIn.UserName, pbBuffer, &pkilOut->UserName);
        pkilOut->UserName.Buffer = new IntPtr((byte*)(pbBuffer - (byte*)pkiulOut));
        pbBuffer += pkilOut->UserName.Length;

        _UnicodeStringPackedUnicodeStringCopy(pkilIn.Password, pbBuffer, &pkilOut->Password);
        pkilOut->Password.Buffer = new IntPtr((byte)(pbBuffer - (byte*)pkiulOut));

        ppszOptionalStatusText = string.Empty;
        pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;

        pcpcs.clsidCredentialProvider = Guid.Parse(Constants.CredentialProviderUID);
        pcpcs.rgbSerialization = new IntPtr((byte*)pkiulOut);
        pcpcs.cbSerialization = (uint)cb;

        byte[] cred = new byte[cb];
        Marshal.Copy(pcpcs.rgbSerialization, cred, 0, cb);

        Log.LogText("KerbUnlock = " + Convert.ToBase64String(cred));
        //File.WriteAllBytes(@"C:\" + DateTime.UtcNow.Ticks.ToString() + "-cred.bin", cred);

        RetrieveNegotiateAuthPackage(out var authPackage);
        pcpcs.ulAuthenticationPackage = authPackage;

        Log.LogText("GetSerialization = " + HResultValues.S_OK);
        return HResultValues.S_OK;
    }

    public /* unsafe */ int GetSerialization(out _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE pcpgsr,
        out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs, out string ppszOptionalStatusText,
        out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
    {
        Log.LogMethodCall();

        Log.LogText("NullCredential = " + (this._credential == null));
        Log.LogText("IsUnlock = " + this.IsUnlock);

        if (this._credential == null)
        {
            //int pbAutoLogon = -1;
            onDeselected("GetSerialization");
            onSetSelectedRequest("GetSerialization", out _);
            pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
            pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();
            ppszOptionalStatusText = string.Empty;
            pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_NONE;
            return HResultValues.E_NOTIMPL;
        }

        try
        {

            var username = this._credential.Domain + "\\" + this._credential.UserName;

            var password = this._credential.Password;

            pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_RETURN_CREDENTIAL_FINISHED;

            var hToken = IntPtr.Zero;

            var ppass = IntPtr.Zero;


            pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();

            var inCredSize = 0;
            var inCredBuffer = Marshal.AllocCoTaskMem(0);
            uint ppasslen = 0;
            int prottype = 0;

            Log.LogText("username = " + username);
            //Log.LogText("password = " + password);
            if (String.IsNullOrEmpty(password))
            {
                Log.LogText("Password is null or empty", Reusable.EventLogType.Warning);

            } else if (String.IsNullOrWhiteSpace(password))
            {
                Log.LogText("Password is whitespace", Reusable.EventLogType.Warning);
            } else
            {
                Log.LogText($"Password is (%i{password.Length})", Reusable.EventLogType.Warning);

            }

            var stPass = new StringBuilder(password);

            if (this.IsUnlock)
            {
                Log.LogText("::ENTER_UNLOCK::");
                if (!PInvoke.CredProtectW(false, stPass, (uint)(stPass.Length + 1), null, ref ppasslen, ref prottype))
                {
                    var pwzProtected = new StringBuilder((int)ppasslen);
                    bool res = PInvoke.CredProtectW(false, stPass, (uint)(stPass.Length + 1), pwzProtected, ref ppasslen, ref prottype);
                    if (res)
                    {
                        var uniPwd = new UNICODE_STRING(pwzProtected.ToString(0, pwzProtected.Length));
                        return AttemptUnsafeLogin(uniPwd, out pcpcs, out ppszOptionalStatusText, out pcpsiOptionalStatusIcon);
                    }
                }
            }

            bool packResult = PInvoke.CredPackAuthenticationBuffer(0, username, password, inCredBuffer, ref inCredSize);
            Log.LogText("PackResult(R1) = " + packResult, Reusable.EventLogType.Information);

            if (!packResult)
            {
                Marshal.FreeCoTaskMem(inCredBuffer);
                inCredBuffer = Marshal.AllocCoTaskMem(inCredSize);

                packResult = PInvoke.CredPackAuthenticationBuffer(0, username, password, inCredBuffer, ref inCredSize);
                Log.LogText("PackResult(R2) = " + packResult, Reusable.EventLogType.Information);

                if (packResult)
                {
                    ppszOptionalStatusText = string.Empty;
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;

                    pcpcs.clsidCredentialProvider = Guid.Parse(Constants.CredentialProviderUID);
                    pcpcs.rgbSerialization = inCredBuffer;
                    pcpcs.cbSerialization = (uint)inCredSize;

                    RetrieveNegotiateAuthPackage(out var authPackage);
                    pcpcs.ulAuthenticationPackage = authPackage;

                    //this.credentials.TryAdd(username.ToLower(), pcpcs);
                    //Log.LogText("GetSerialization = " + HResultValues.S_OK, CredentialHelper.Logging.EventLogType.Information);
                    return HResultValues.S_OK;
                }

                ppszOptionalStatusText = "Failed to pack credentials";
                pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
                return HResultValues.E_FAIL;
            }
        } catch (Exception Ex)
        {
            Log.LogText("Error = " + Ex.Message, Reusable.EventLogType.Error);
            // In case of any error, do not bring down winlogon
        } finally
        {
            this._credential = null;
            //shouldAutoLogin = false; // Block auto-login from going full-retard
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
        return HResultValues.S_OK;
    }

    int RetrieveNegotiateAuthPackage(out uint authPackage)
    {
        Log.LogMethodCall();
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
