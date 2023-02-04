using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Core.DevToolsProtocolExtension;
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

        var currenUri = webView.Source.ToString();
        if(!currenUri.StartsWith("https://www.ximalaya.com/album/")&&!currenUri.StartsWith("https://www.ximalaya.com/sound/"))
        {
            MessageBox.Show(this, "请先打开要下载的专辑页面");
            return;
        }

        //如果是专辑页面，则先转到第一个音频页面地址，因为音频页面地址有“查看更多”，免得在专辑页面翻页
        if(currenUri.StartsWith("https://www.ximalaya.com/album/"))
        {
            await webView.WaitAndClickAsync("#anchor_sound_list > div.sound-list.H_g > ul > li:nth-child(1) > div.text._nO > a");
        }
        //等待音频列表加载完成
        await webView.WaitAsync("#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.track-list-wrap._sZ > ul > li:nth-child(1) > div.text._nO > a");

        var devToolsContext = await webView.CoreWebView2.CreateDevToolsContextAsync();

        //点击“查看更多”
        WebView2.DevTools.Dom.HtmlElement loadMore;
        while ((loadMore = await devToolsContext.QuerySelectorAsync("#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.track-list-wrap._sZ > ul > p")) != null)
        {
            try
            {
                await loadMore.ClickAsync();
            }
            catch
            {

            }
            await Task.Delay(100);
        }

        //遍历音频列表
        var liItems = await devToolsContext.QuerySelectorAllAsync("#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.track-list-wrap._sZ > ul > li");
        List<MediaItem> mediaItems = new();
        foreach (var item in liItems)
        {
            var link = await item.QuerySelectorAsync("a");
            string href = await link.GetAttributeAsync("href");
            string title = await link.GetAttributeAsync("title");
            mediaItems.Add(new MediaItem(title, "https://www.ximalaya.com" + href));
        }
        downloadProgress.Maximum = mediaItems.Count;
        webView.CoreWebView2.AddWebResourceRequestedFilter("*",
                                              CoreWebView2WebResourceContext.Media);
        //遍历下载音频
        int counter = 0;
        using HttpClient httpClient = new HttpClient();
        foreach (var mediaItem in mediaItems)
        {
            counter++;
            downloadProgress.Value = counter;
            string title = mediaItem.Title;
            //去除路径中的非法字符
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(invalidChar, '_');
            }
            string destFile = Path.Combine(destDir, title + ".m4a");
            if (System.IO.File.Exists(destFile) && new FileInfo(destFile).Length > 4 * 1024)
            {
                labelStatus.Text = title + "已经存在，跳过";
                continue;
            }
            webView.CoreWebView2.Navigate(mediaItem.url);
            await webView.WaitContentLoadedAsync();

            //嗅探音频地址
            var tcsAudioUrl = new TaskCompletionSource<string>();
            EventHandler<CoreWebView2WebResourceRequestedEventArgs> audioRequested = (se, ev) => {
                string audioUrl = ev.Request.Uri;
                tcsAudioUrl.SetResult(audioUrl);
            };
            webView.CoreWebView2.WebResourceRequested += audioRequested;
            //等待标题出现，并且点击
            await webView.WaitAndClickAsync("#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.sound-container.kn_ > div > div.sound-info.clearfix.kn_ > div > div.controls.kn_ > div.fl-wrapper.fl.price-btn-wrapper.kn_ > div > xm-player > div");
            string titleJson = await webView.ExecuteScriptAsync("document.querySelector('#award > main > div.sound-detail > div.clearfix > div.detail.layout-main > div.sound-container.kn_ > div > div.sound-info.clearfix.kn_ > div > h1').innerHTML");

            string audioUrl =  await tcsAudioUrl.Task;//等待音频加载，返回值为音频的路径
            webView.CoreWebView2.WebResourceRequested -= audioRequested;
            
            using Stream audioStream = await httpClient.GetStreamAsync(audioUrl);            
            using var outStream = System.IO.File.OpenWrite(destFile);
            await audioStream.CopyToAsync(outStream);
            labelStatus.Text = title + "下载完成";
        }
        await devToolsContext.DisposeAsync();
        MessageBox.Show(this, "全部下载完成");
    }

    private async void MainForm_Load(object sender, EventArgs e)
    {
        await webView.EnsureCoreWebView2Async();
        webView.CoreWebView2.IsMuted = true;
        webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
        await webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable", "{}");
        var helper = webView.CoreWebView2.GetDevToolsProtocolHelper();

        //https://stackoverflow.com/questions/66303305/webview2-devtoolsprotocolevent-not-raising
        await helper.Console.EnableAsync();//it's required for Console.MessageAdded += 
        helper.Console.MessageAdded += (sender, args) => {
            if(args.Message.Text.Contains("今天操作太频繁啦，可以明天再试试哦"))
            {
                //避免阻塞JS线程，造成执行超时
                this.BeginInvoke(() => {
                    MessageBox.Show(this, "今天操作太频繁啦，可以明天再试试哦");
                });
            }
        };
        txtURL.Focus();
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

file record MediaItem(string Title,string url);