﻿using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Download
{
    public interface IFailedDownloadService
    {
        void MarkAsFailed(int historyId);
        void MarkAsFailed(string downloadId);
        void Check(TrackedDownload trackedDownload);
        void ProcessFailed(TrackedDownload trackedDownload);
    }

    public class FailedDownloadService : IFailedDownloadService
    {
        private readonly IHistoryService _historyService;
        private readonly IEventAggregator _eventAggregator;

        public FailedDownloadService(IHistoryService historyService,
                                     IEventAggregator eventAggregator)
        {
            _historyService = historyService;
            _eventAggregator = eventAggregator;
        }

        public void MarkAsFailed(int historyId)
        {
            var history = _historyService.Get(historyId);

            var downloadId = history.DownloadId;
            if (downloadId.IsNullOrWhiteSpace())
            {
                PublishDownloadFailedEvent(new List<History.History> { history }, "Manually marked as failed");
            }
            else
            {
                var grabbedHistory = _historyService.Find(downloadId, HistoryEventType.Grabbed).ToList();
                PublishDownloadFailedEvent(grabbedHistory, "Manually marked as failed");
            }
        }

        public void MarkAsFailed(string downloadId)
        {
            var history = _historyService.Find(downloadId, HistoryEventType.Grabbed);

            if (history.Any())
            {
                PublishDownloadFailedEvent(history, "Manually marked as failed");
            }
        }

        public void Check(TrackedDownload trackedDownload)
        {
            // Only process tracked downloads that are still downloading
            if (trackedDownload.State != TrackedDownloadState.Downloading)
            {
                return;
            }

            if (trackedDownload.DownloadItem.IsEncrypted ||
                trackedDownload.DownloadItem.Status == DownloadItemStatus.Failed)
            {
                var grabbedItems = _historyService
                                   .Find(trackedDownload.DownloadItem.DownloadId, HistoryEventType.Grabbed)
                                   .ToList();

                if (grabbedItems.Empty())
                {
                    trackedDownload.Warn("Download wasn't grabbed by sonarr, skipping");
                    return;
                }

                trackedDownload.State = TrackedDownloadState.FailedPending;
            }
        }

        public void ProcessFailed(TrackedDownload trackedDownload)
        {
            if (trackedDownload.State != TrackedDownloadState.FailedPending)
            {
                return;
            }

            var grabbedItems = _historyService
                               .Find(trackedDownload.DownloadItem.DownloadId, HistoryEventType.Grabbed)
                               .ToList();

            if (grabbedItems.Empty())
            {
                return;
            }

            var failure = "Failed download detected";

            if (trackedDownload.DownloadItem.IsEncrypted)
            {
                failure = "Encrypted download detected";
            }
            else if (trackedDownload.DownloadItem.Status == DownloadItemStatus.Failed && trackedDownload.DownloadItem.Message.IsNotNullOrWhiteSpace())
            {
                failure = trackedDownload.DownloadItem.Message;
            }

            trackedDownload.State = TrackedDownloadState.Failed;
            PublishDownloadFailedEvent(grabbedItems, failure, trackedDownload);
        }

        private void PublishDownloadFailedEvent(List<History.History> historyItems, string message, TrackedDownload trackedDownload = null)
        {
            var historyItem = historyItems.First();

            var downloadFailedEvent = new DownloadFailedEvent
            {
                SeriesId = historyItem.SeriesId,
                EpisodeIds = historyItems.Select(h => h.EpisodeId).ToList(),
                Quality = historyItem.Quality,
                SourceTitle = historyItem.SourceTitle,
                DownloadClient = historyItem.Data.GetValueOrDefault(History.History.DOWNLOAD_CLIENT),
                DownloadId = historyItem.DownloadId,
                Message = message,
                Data = historyItem.Data,
                TrackedDownload = trackedDownload,
                Language = historyItem.Language
            };

            _eventAggregator.PublishEvent(downloadFailedEvent);
        }
    }
}
