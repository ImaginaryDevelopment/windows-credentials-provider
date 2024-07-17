using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CredentialHelper.UI
{
    public static class Extensions
    {
        public static void SmartInvoke<C>(this C Control, Action<C> Action) where C : Control
        {
            if (Control == null || (Control?.IsDisposed ?? true)) { return ; }
            switch (Control.InvokeRequired)
            {
                case true:
                    Control.Invoke(new Action(() => Action.Invoke(Control)));
                    return;
                case false:
                    Action.Invoke(Control);
                    return;
            }
        }
        public static bool IsValueString(this string  value) =>
            !string.IsNullOrWhiteSpace(value);
    }
}
