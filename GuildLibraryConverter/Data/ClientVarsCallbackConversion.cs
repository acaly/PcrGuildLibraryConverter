using CefSharp;
using CefSharp.OffScreen;
using CefSharp.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GuildLibraryConverter.Data
{
    static class ClientVarsCallbackConversion
    {
        private class LoadCheckBrowser : ChromiumWebBrowser
        {
            private volatile int _loaded;

            public LoadCheckBrowser(string html) : base(new HtmlString(html, charSet: "UTF-8"))
            {
                FrameLoadEnd += (sender, e) => _loaded = 1;
            }

            public bool IsReady => _loaded == 1 && IsBrowserInitialized && CanExecuteJavascriptInMainFrame;
        }

        private const string _html = @"<html><body><script type=""text/javascript"">" +
            "function conv_clientVarsCallback(str) { " +
            "var elem = document.createElement('textarea');" +
            "elem.innerHTML = str;" +
            "return elem.value; }" +
            "</script></body></html>";

        private static readonly LoadCheckBrowser _browser = new LoadCheckBrowser(_html);
        private static readonly SemaphoreSlim _browserLock = new(1);

        public static async Task<string> ConvertAsync(string response)
        {
            await _browserLock.WaitAsync();
            while (!_browser.IsReady)
            {
                await Task.Delay(100);
            }
            try
            {
                var result = await _browser.EvaluateScriptAsync("eval", "conv_" + response);
                //var result = await _browser.EvaluateScriptAsync("conv_clientVarsCallback", "abc");
                return (string)result.Result;
            }
            finally
            {
                _browserLock.Release();
            }
        }
    }
}
