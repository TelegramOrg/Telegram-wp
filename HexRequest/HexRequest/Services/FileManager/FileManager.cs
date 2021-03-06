﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.TL;


namespace Telegram.Api.Services.FileManager
{
    public class FileManager : IFileManager
    {
        private readonly object _randomRoot = new object();

        private readonly Random _random = new Random();

        private readonly List<Worker> _workers = new List<Worker>(Constants.WorkersNumber);

        private readonly object _itemsSyncRoot = new object();

        private readonly List<DownloadableItem> _items = new List<DownloadableItem>();

        private readonly ITelegramEventAggregator _eventAggregator;

        private readonly IMTProtoService _mtProtoService;

        public FileManager(ITelegramEventAggregator eventAggregator, IMTProtoService mtProtoService)
        {
            _eventAggregator = eventAggregator;
            _mtProtoService = mtProtoService;

            for (int i = 0; i < Constants.WorkersNumber; i++)
            {
                var worker = new Worker(OnDownloading, "downloader"+i);
                _workers.Add(worker);
            }
        }

        private void OnDownloading(object state)
        {
            DownloadablePart part = null;
            lock (_itemsSyncRoot)
            {
                for (var i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item.Canceled)
                    {
                        _items.RemoveAt(i--);
                        try
                        {
                            _eventAggregator.Publish(new DownloadingCanceledEventArgs(item));
                        }
                        catch (Exception e)
                        {
                            TLUtils.WriteException(e);
                        }
                    }
                }

                foreach (var item in _items)
                {
                    part = item.Parts.FirstOrDefault(x => x.Status == PartStatus.Ready);
                    if (part != null)
                    {
                        part.Status = PartStatus.Processing;
                        break;
                    }
                }
            }

            if (part == null)
            {
                var currentWorker = (Worker)state;
                currentWorker.Stop();
                return;
            }

            part.File = GetFile(part.ParentItem.Location, part.Offset, part.Limit);
            while (part.File == null)
            {
                part.File = GetFile(part.ParentItem.Location, part.Offset, part.Limit);
            }

            part.Status = PartStatus.Processed;

            var data = part.File.Bytes.Data;
            if (data.Length < part.Limit.Value && (part.Number + 1) != part.ParentItem.Parts.Count)
            {
                lock (_itemsSyncRoot)
                {
                    var complete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
                    if (!complete)
                    {
                        var emptyBufferSize = part.Limit.Value - data.Length;
                        var position = data.Length;

                        var missingPart = new DownloadablePart(part.ParentItem, new TLInt(position), new TLInt(emptyBufferSize), -part.Number);

                        var currentItemIndex = part.ParentItem.Parts.IndexOf(part);
                        part.ParentItem.Parts.Insert(currentItemIndex + 1, missingPart);
                    }
                }
            }

            // indicate progress
            // indicate complete
            bool isComplete;
            bool isCanceled;
            var progress = 0.0;
            lock (_itemsSyncRoot)
            {
                part.Status = PartStatus.Processed;
                isCanceled = part.ParentItem.Canceled;

                isComplete = part.ParentItem.Parts.All(x => x.Status == PartStatus.Processed);
                if (!isComplete)
                {
                    var downloadedCount = part.ParentItem.Parts.Count(x => x.Status == PartStatus.Processed);
                    var count = part.ParentItem.Parts.Count;
                    progress = downloadedCount / count;
                }
                else
                {
                    _items.Remove(part.ParentItem);
                }
            }

            if (!isCanceled)
            {
                if (isComplete)
                {
                    byte[] bytes = { };
                    foreach (var p in part.ParentItem.Parts)
                    {
                        bytes = TLUtils.Combine(bytes, p.File.Bytes.Data);
                    }
                    //part.ParentItem.Location.Buffer = bytes;
                    var fileName = String.Format("{0}_{1}_{2}.jpg",
                        part.ParentItem.Location.VolumeId,
                        part.ParentItem.Location.LocalId, 
                        part.ParentItem.Location.Secret);

                    FileUtils.WriteBytes(fileName, bytes);

                    _eventAggregator.Publish(part.ParentItem);
                }
                else
                {
                    _eventAggregator.Publish(new ProgressChangedEventArgs(part.ParentItem, progress));
                }
            }
        }

        private TLFile GetFile(TLFileLocation location, TLInt offset, TLInt limit)
        {
            var manualResetEvent = new ManualResetEvent(false);
            TLFile result = null;

            _mtProtoService.GetFileAsync(location.DCId, location.ToInputFileLocation(), offset, limit,
                file =>
                {
                    result = file;
                    manualResetEvent.Set();
                },
                error =>
                {
                    int delay;
                    lock (_randomRoot)
                    {
                        delay = _random.Next(1000, 3000);
                    }

                    Execute.BeginOnThreadPool(TimeSpan.FromMilliseconds(delay), () => manualResetEvent.Set());
                });

            manualResetEvent.WaitOne();
            return result;
        }

        public void DownloadFile(TLFileLocation file, TLObject owner, TLInt fileSize)
        {
            var downloadableItem = GetDownloadableItem(file, owner, fileSize);

            lock (_itemsSyncRoot)
            {
                bool addFile = true;
                foreach (var item in _items)
                {
                    if (item.Location.VolumeId.Value == file.VolumeId.Value
                        && item.Location.LocalId.Value == file.LocalId.Value
                        && item.Owner == owner)
                    {
                        addFile = false;
                        break;
                    }
                }

                if (addFile)
                {
                    _items.Add(downloadableItem);
                }
            }

            StartAwaitingWorkers();
        }

        private void StartAwaitingWorkers()
        {
            var awaitingWorkers = _workers.Where(x => x.IsWaiting);

            foreach (var awaitingWorker in awaitingWorkers)
            {
                awaitingWorker.Start();
            }
        }

        private DownloadableItem GetDownloadableItem(TLFileLocation location, TLObject owner, TLInt fileSize)
        {
            var item = new DownloadableItem
            {
                Owner = owner,
                Location = location
            };
            item.Parts = GetItemParts(fileSize, item);

            return item;
        }

        private List<DownloadablePart> GetItemParts(TLInt size, DownloadableItem item)
        {
            var chunkSize = Constants.DownloadedChunkSize;
            var parts = new List<DownloadablePart>();
            var partsCount = size.Value / chunkSize + 1;
            for (var i = 0; i < partsCount; i++)
            {
                var part = new DownloadablePart(item, new TLInt(i * chunkSize), size.Value == 0? new TLInt(0) : new TLInt(chunkSize));
                parts.Add(part);
            }

            return parts;
        }

        public void CancelDownloadFile(TLObject owner)
        {
            lock (_itemsSyncRoot)
            {
                var items = _items.Where(x => x.Owner == owner);

                foreach (var item in items)
                {
                    item.Canceled = true;
                }
            }
        }
    }
}
