using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using Xilium.CefGlue.Avalonia;

namespace BrowserTool
{
    public class BrowserTab
    {
        public string Id { get; }
        public string Title { get; set; }
        public string Url { get; set; }
        public MyAvaloniaCefBrowser Browser { get; set; }
        public Button? TabButton { get; set; }
        public bool IsActive { get; set; }
        public bool IsLoading { get; set; }
        public DateTime CreatedAt { get; }
        public long? SessionId { get; set; }

        public BrowserTab()
        {
            Id = Guid.NewGuid().ToString();
            Title = "New Tab";
            Url = "";
            CreatedAt = DateTime.Now;
            IsActive = false;
            IsLoading = false;
            Browser = new MyAvaloniaCefBrowser();
        }

        public BrowserTab(string url) : this()
        {
            Url = url;
            if (!string.IsNullOrEmpty(url))
            {
                Title = GetDomainFromUrl(url);
            }
        }

        private string GetDomainFromUrl(string url)
        {
            try
            {
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    url = "https://" + url;
                }
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return "New Tab";
            }
        }

        public void UpdateTitle(string newTitle)
        {
            if (string.IsNullOrWhiteSpace(newTitle))
            {
                Title = string.IsNullOrEmpty(Url) ? "New Tab" : GetDomainFromUrl(Url);
            }
            else
            {
                Title = newTitle.Length > 30 ? newTitle.Substring(0, 27) + "..." : newTitle;
            }
        }

        public void UpdateUrl(string newUrl)
        {
            Url = newUrl;
            if (string.IsNullOrWhiteSpace(Title) || Title == "New Tab")
            {
                Title = GetDomainFromUrl(newUrl);
            }
        }
    }
}
