using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace GuildLibraryConverter.Helpers
{
    class HyperlinkHelper
    {
        public static readonly RequestNavigateEventHandler OpenWithBrowser = OpenWithBrowser_;

        public static void OpenWithBrowser_(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
