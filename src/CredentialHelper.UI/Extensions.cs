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

        public static void SetDoubleBuffered(this System.Windows.Forms.Control c)
        {
            //Taxes: Remote Desktop Connection and painting
            //http://blogs.msdn.com/oldnewthing/archive/2006/01/03/508694.aspx
            if (System.Windows.Forms.SystemInformation.TerminalServerSession)
                return;

            System.Reflection.PropertyInfo aProp =
                  typeof(System.Windows.Forms.Control).GetProperty(
                        "DoubleBuffered",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

            aProp.SetValue(c, true, null);
        }

        public static bool IsValueString(this string  value) =>
            !string.IsNullOrWhiteSpace(value);
    }
}
