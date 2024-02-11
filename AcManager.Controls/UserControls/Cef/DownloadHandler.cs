using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Controls.UserControls.Web;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Controls.UserControls.Cef {
    internal class DownloadHandler : NotifyPropertyChanged, IDownloadHandler {
        [CanBeNull]
        public IWebDownloadListener Listener { get; set; }

        private class DownloadData {
            internal TaskCompletionSource<string> TaskCompletionSource;
            internal string Destination;
            internal IProgress<long> Progress;
            internal CancellationToken CancellationToken;
        }

        private class WebDownloader : IWebDownloader {
            internal DownloadData Data;

            public Task<string> DownloadAsync(string destination, IProgress<long> progress, CancellationToken cancellation) {
                var tcs = new TaskCompletionSource<string>();
                Data = new DownloadData {
                    TaskCompletionSource = tcs,
                    Destination = destination,
                    Progress = progress,
                    CancellationToken = cancellation
                };
                return tcs.Task;
            }
        }

        private readonly Dictionary<int, DownloadData> _downloads = new Dictionary<int, DownloadData>();

        bool IDownloadHandler.CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod) {
            return true;
        }

        void IDownloadHandler.OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback) {
            if (Listener == null) return;
            try {
                if (!callback.IsDisposed) {
                    using (callback) {
                        var loader = new WebDownloader();
                        Listener.OnDownload(downloadItem.Url, downloadItem.SuggestedFileName, downloadItem.TotalBytes, loader);
                        if (loader.Data != null) {
                            callback.Continue(loader.Data.Destination, false);
                            _downloads[downloadItem.Id] = loader.Data;
                            UpdateIsAnyDownloadActive();
                        }
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        void IDownloadHandler.OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback) {
            if (_downloads.TryGetValue(downloadItem.Id, out var data)) {
                try {
                    if (!downloadItem.IsValid) {
                        throw new Exception("Download item isn’t valid anymore");
                    }

                    if (downloadItem.IsCancelled) {
                        throw new OperationCanceledException();
                    }

                    if (data.CancellationToken.IsCancellationRequested) {
                        if (!callback.IsDisposed) {
                            using (callback) {
                                callback.Cancel();
                            }
                        }
                        throw new OperationCanceledException();
                    }

                    if (downloadItem.IsInProgress) {
                        data.Progress.Report(downloadItem.ReceivedBytes);
                    } else {
                        _downloads.Remove(downloadItem.Id);
                        UpdateIsAnyDownloadActive();
                        data.TaskCompletionSource.TrySetResult(downloadItem.FullPath);
                    }
                } catch (Exception e) when (e.IsCancelled()) {
                    _downloads.Remove(downloadItem.Id);
                    UpdateIsAnyDownloadActive();
                    data.TaskCompletionSource.TrySetCanceled();
                } catch (Exception e) {
                    _downloads.Remove(downloadItem.Id);
                    UpdateIsAnyDownloadActive();
                    data.TaskCompletionSource.TrySetException(e);
                }
            }
        }

        private void UpdateIsAnyDownloadActive() {
            ActionExtension.InvokeInMainThreadAsync(() => IsAnyDownloadActive = _downloads.Count > 0);
        }

        private bool _isAnyDownloadActive;

        public bool IsAnyDownloadActive {
            get => _isAnyDownloadActive;
            set => Apply(value, ref _isAnyDownloadActive);
        }
    }
}