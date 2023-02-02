using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using WebView2.DevTools.Dom;

namespace XimalayaDownloader;
public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();
    }

    private void btnGO_Click(object sender, EventArgs e)
    {
        webView.Source = new Uri(txtURL.Text);
    }

    private void btnSelectDir_Click(object sender, EventArgs e)
    {
        if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
        {
            txtDestDir.Text = folderBrowserDialog.SelectedPath;
        }
    }

    private async void btnStart_Click(object sender, EventArgs e)
    {
        string destDir = txtDestDir.Text;
        if(string.IsNullOrEmpty(destDir)) 
        {
            MessageBox.Show(this, "请先设定路径");
            return;
        }

        var devToolsContext = await webView.CoreWebView2.CreateDevToolsContextAsync();
        var liItems = await devToolsContext.QuerySelectorAllAsync("#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.track-list-wrap._sZ > ul > li");
        List<string> songUrls = new();
        foreach (var item in liItems)
        {
            var link = await item.QuerySelectorAsync("a");
            string href = await link.GetAttributeAsync("href");
            songUrls.Add("https://www.ximalaya.com" + href);
        }
        downloadProgress.Maximum = songUrls.Count;
        webView.CoreWebView2.AddWebResourceRequestedFilter("*",
                                              CoreWebView2WebResourceContext.Media);
        int counter = 0;
        foreach (var songUrl in songUrls)
        {
            webView.CoreWebView2.Navigate(songUrl);
            await webView.WaitContentLoaded();

            var tcsAudioUrl = new TaskCompletionSource<string>();
            EventHandler<CoreWebView2WebResourceRequestedEventArgs> audioRequested = (se, ev) => {
                string audioUrl = ev.Request.Uri;
                tcsAudioUrl.SetResult(audioUrl);
            };
            webView.CoreWebView2.WebResourceRequested += audioRequested;
            //等待标题出现，并且点击
            await webView.WaitAndClick("#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.sound-container.kn_ > div > div.sound-info.clearfix.kn_ > div > div.controls.kn_ > div.fl-wrapper.fl.price-btn-wrapper.kn_ > div > xm-player > div");
            string titleJson = await webView.ExecuteScriptAsync("document.querySelector('#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.sound-container.kn_ > div > div.sound-info.clearfix.kn_ > div > h1').innerHTML");

            string title = JsonSerializer.Deserialize<string>(titleJson);
            string audioUrl =  await tcsAudioUrl.Task;//等待音频加载，返回值为音频的路径
            webView.CoreWebView2.WebResourceRequested -= audioRequested;

            HttpClient httpClient = new HttpClient();
            using Stream audioStream = await httpClient.GetStreamAsync(audioUrl);
            using var outStream = System.IO.File.OpenWrite(Path.Combine(destDir, title + ".m4a"));
            await audioStream.CopyToAsync(outStream);
            labelStatus.Text = title+"下载完成";
            counter++;
            downloadProgress.Value = counter;
        }
        await devToolsContext.DisposeAsync();
        MessageBox.Show(this, "全部下载完成");
    }

    private async void MainForm_Load(object sender, EventArgs e)
    {
        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
    }

    private void webView_SourceChanged(object sender, Microsoft.Web.WebView2.Core.CoreWebView2SourceChangedEventArgs e)
    {
        txtURL.Text = webView.Source.ToString();
    }

    private void btnBack_Click(object sender, EventArgs e)
    {
        if (webView.CanGoBack)
        {
            webView.GoBack();
        }
    }

    private void btnForward_Click(object sender, EventArgs e)
    {
        if (webView.CanGoForward)
        {
            webView.GoForward();
        }
    }

    private void btnRefresh_Click(object sender, EventArgs e)
    {
        webView.Refresh();
    }
}