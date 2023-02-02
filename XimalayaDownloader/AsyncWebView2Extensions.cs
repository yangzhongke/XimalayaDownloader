﻿using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using Xyzzer.AsyncUI;

namespace XimalayaDownloader
{
    internal static class AsyncWebView2Extensions
    {
        public static Task WaitContentLoadedAsync(this Microsoft.Web.WebView2.WinForms.WebView2 webView)
        {

            return EventAsync.FromEvent<CoreWebView2DOMContentLoadedEventArgs>(
                eh => webView.CoreWebView2.DOMContentLoaded += eh,
                eh => webView.CoreWebView2.DOMContentLoaded -= eh);
        }

        public static async Task<int> WaitAndClickAsync(this Microsoft.Web.WebView2.WinForms.WebView2 webView, string querySelector)
        {
            TaskCompletionSource<int> tcs = new();
            while (true)
            {
                string lenJson = await webView.ExecuteScriptAsync("document.querySelectorAll('" + querySelector + "').length");
                int len = JsonSerializer.Deserialize<int>(lenJson);
                if (len > 0)
                {
                    await Task.Delay(1000);
                    await webView.ExecuteScriptAsync("document.querySelector('" + querySelector + "').click()");
                    tcs.SetResult(len);
                    break;
                }
                await Task.Delay(100);
            }
            return await tcs.Task;
        }

        public static async Task<int> WaitAsync(this Microsoft.Web.WebView2.WinForms.WebView2 webView, string querySelector)
        {
            TaskCompletionSource<int> tcs = new();
            while (true)
            {
                string lenJson = await webView.ExecuteScriptAsync("document.querySelectorAll('" + querySelector + "').length");
                int len = JsonSerializer.Deserialize<int>(lenJson);
                if (len > 0)
                {
                    tcs.SetResult(len);
                    break;
                }
                await Task.Delay(100);
            }
            return await tcs.Task;
        }
    }
}