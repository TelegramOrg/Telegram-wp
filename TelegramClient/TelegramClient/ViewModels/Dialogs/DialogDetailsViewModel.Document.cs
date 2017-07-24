﻿using System;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Threading;
using System.Windows;
using TelegramClient.Converters;
using TelegramClient.Resources;
using TelegramClient.Views;
#if WP81
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using System.Threading.Tasks;
using Windows.Storage.AccessCache;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.Storage;
#endif
using Telegram.Api.TL;
using TelegramClient.Services;

namespace TelegramClient.ViewModels.Dialogs
{
    public partial class DialogDetailsViewModel
    {
        public void AddToStickers(TLMessageBase messageBase)
        {
            if (messageBase == null) return;

            var message = messageBase as TLMessage;
            if (message == null) return;

            var mediaDocument = message.Media as TLMessageMediaDocument;
            if (mediaDocument == null) return;

            var document = mediaDocument.Document as TLDocument22;
            if (document != null)
            {
                var inputStickerSet = document.StickerSet;
                if (inputStickerSet != null)
                {
                    TelegramViewBase.NavigateToStickers(MTProtoService, StateService, inputStickerSet);
                }
            }
        }

        private void SendDocument(string fileName)
        {
            //create thumb
            byte[] bytes;

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bytes = new byte[fileStream.Length];
                    fileStream.Read(bytes, 0, bytes.Length);
                }
            }

            if (!CheckDocumentSize((ulong)bytes.Length))
            {
                MessageBox.Show(string.Format(AppResources.MaximumFileSizeExceeded, MediaSizeConverter.Convert((int)Telegram.Api.Constants.MaximumUploadedFileSize)), AppResources.Error, MessageBoxButton.OK);
                return;
            }

            //create document
            var document = new TLDocument22
            {
                Buffer = bytes,

                Id = new TLLong(0),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                FileName = new TLString(Path.GetFileName(fileName)),
                MimeType = new TLString("text/plain"),
                Size = new TLInt(bytes.Length),
                Thumb = new TLPhotoSizeEmpty { Type = TLString.Empty },
                DCId = new TLInt(0)
            };

            var media = new TLMessageMediaDocument { FileId = TLLong.Random(), Document = document };

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        m => SendDocumentInternal(message, null)));
            });
        }

#if WP81
        public static async Task<TLPhotoSizeBase> GetFileThumbAsync(StorageFile file)
        {
            try
            {
                const int imageSize = 60;
                IRandomAccessStream thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, imageSize, ThumbnailOptions.ResizeThumbnail);

                if (((StorageItemThumbnail)thumb).ContentType == "image/png")
                {
                    var tempThumb = new InMemoryRandomAccessStream();
                    var decoder = await BitmapDecoder.CreateAsync(thumb);
                    var pixels = await decoder.GetPixelDataAsync();

                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, tempThumb);

                    encoder.SetPixelData(decoder.BitmapPixelFormat, BitmapAlphaMode.Ignore, decoder.PixelWidth, decoder.PixelHeight, decoder.DpiX, decoder.DpiY, pixels.DetachPixelData());

                    await encoder.FlushAsync();

                    thumb = tempThumb;
                }

                var volumeId = TLLong.Random();
                var localId = TLInt.Random();
                var secret = TLLong.Random();

                var thumbLocation = new TLFileLocation
                {
                    DCId = new TLInt(0),
                    VolumeId = volumeId,
                    LocalId = localId,
                    Secret = secret,
                };

                var fileName = String.Format("{0}_{1}_{2}.jpg",
                    thumbLocation.VolumeId,
                    thumbLocation.LocalId,
                    thumbLocation.Secret);

                var thumbFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
                var thumbBuffer = new Windows.Storage.Streams.Buffer(Convert.ToUInt32(thumb.Size));
                var iBuf = await thumb.ReadAsync(thumbBuffer, thumbBuffer.Capacity, InputStreamOptions.None);
                using (var thumbStream = await thumbFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await thumbStream.WriteAsync(iBuf);
                }

                var thumbSize = new TLPhotoSize
                {
                    W = new TLInt(imageSize),
                    H = new TLInt(imageSize),
                    Size = new TLInt((int)thumb.Size),
                    Type = TLString.Empty,
                    Location = thumbLocation,

                    Bytes = TLString.FromBigEndianData(thumbBuffer.ToArray())
                };

                return thumbSize;
            }
            catch (Exception ex)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("GetFileThumbAsync exception " + ex);
            }

            return null;
        }

        public async void SendDocument(StorageFile file)
        {
            if (file == null) return;

            var properties = await file.GetBasicPropertiesAsync();
            var size = properties.Size;

            //file.Properties.GetImagePropertiesAsync()

            if (!CheckDocumentSize(size))
            {
                MessageBox.Show(string.Format(AppResources.MaximumFileSizeExceeded, MediaSizeConverter.Convert((int)Telegram.Api.Constants.MaximumUploadedFileSize)), AppResources.Error, MessageBoxButton.OK);
                return;
            }


            // to get access to the file with StorageFile.GetFileFromPathAsync in future
            AddFileToFutureAccessList(file);

            var thumb = await GetFileThumbAsync(file);

            //create document
            var document = new TLDocument22
            {
                Id = new TLLong(0),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                FileName = new TLString(Path.GetFileName(file.Name)),
                MimeType = new TLString(file.ContentType),
                Size = new TLInt((int)size),
                Thumb = thumb ?? new TLPhotoSizeEmpty{Type = TLString.Empty},
                DCId = new TLInt(0)
            };

            if (string.Equals(document.FileExt, "webp", StringComparison.OrdinalIgnoreCase)
                && document.DocumentSize < Telegram.Api.Constants.StickerMaxSize)
            {
                document.MimeType = new TLString("image/webp");
                document.Attributes.Add(new TLDocumentAttributeSticker());

                var fileName = document.GetFileName();
                await file.CopyAsync(ApplicationData.Current.LocalFolder, fileName, NameCollisionOption.ReplaceExisting);
            }

            var media = new TLMessageMediaDocument { FileId = TLLong.Random(), Document = document, IsoFileName = file.Path, File = file };

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        m => SendDocumentInternal(message, file)));
            });
        }

        public static void AddFileToFutureAccessList(StorageFile file)
        {
            try
            {
                StorageApplicationPermissions.MostRecentlyUsedList.Add(file);
            }
            catch (Exception ex)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("MostRecentlyUsedList.Add exception\n" + ex);
            }

            try
            {
                if (StorageApplicationPermissions.FutureAccessList.Entries.Count > 900)
                {
                    var item = StorageApplicationPermissions.FutureAccessList.Entries.Last();
                    StorageApplicationPermissions.FutureAccessList.Remove(item.Token);
                }

                StorageApplicationPermissions.FutureAccessList.Add(file);
            }
            catch (Exception ex)
            {
                Telegram.Api.Helpers.Execute.ShowDebugMessage("FutureAccessList.Add exception\n" + ex);
            }
        }
#endif

        private void SendDocument(Photo d)
        {
            //create thumb
            var bytes = d.PreviewBytes;

            if (!CheckDocumentSize((ulong)d.Bytes.Length))
            {
                MessageBox.Show(string.Format(AppResources.MaximumFileSizeExceeded, MediaSizeConverter.Convert((int)Telegram.Api.Constants.MaximumUploadedFileSize)), AppResources.Error, MessageBoxButton.OK);
                return;
            }

            var volumeId = TLLong.Random();
            var localId = TLInt.Random();
            var secret = TLLong.Random();

            var thumbLocation = new TLFileLocation //TODO: replace with TLFileLocationUnavailable
            {
                DCId = new TLInt(0),
                VolumeId = volumeId,
                LocalId = localId,
                Secret = secret,
                //Buffer = bytes
            };

            var fileName = String.Format("{0}_{1}_{2}.jpg",
                thumbLocation.VolumeId,
                thumbLocation.LocalId,
                thumbLocation.Secret);

            using (var store = IsolatedStorageFile.GetUserStoreForApplication())
            {
                using (var fileStream = store.CreateFile(fileName))
                {
                    fileStream.Write(bytes, 0, bytes.Length);
                }
            }

            var thumbSize = new TLPhotoSize
            {
                W = new TLInt(d.Width),
                H = new TLInt(d.Height),
                Size = new TLInt(bytes.Length),
                Type = new TLString(""),
                Location = thumbLocation,
            };

            //create document
            var document = new TLDocument22
            {
                Buffer = d.Bytes,

                Id = new TLLong(0),
                AccessHash = new TLLong(0),
                Date = TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, DateTime.Now),
                FileName = new TLString(Path.GetFileName(d.FileName)),
                MimeType = new TLString("image/jpeg"),
                Size = new TLInt(d.Bytes.Length),
                Thumb = thumbSize,
                DCId = new TLInt(0)
            };

            var media = new TLMessageMediaDocument { FileId = TLLong.Random(), Document = document };

            var message = GetMessage(TLString.Empty, media);

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        m => SendDocumentInternal(message, null)));
            });
        }

        public static bool CheckDocumentSize(ulong size)
        {
            return size < Telegram.Api.Constants.MaximumUploadedFileSize;
        }

#if WP81
        private void SendDocumentInternal(TLMessage25 message, StorageFile file)
#else
        private void SendDocumentInternal(TLMessage25 message, object o)
#endif
        {
            var document = ((TLMessageMediaDocument)message.Media).Document as TLDocument;
            if (document == null) return;

            byte[] thumbBytes = null;
            var thumb = document.Thumb as TLPhotoSize;
            if (thumb != null)
            {
                var thumbLocation = thumb.Location as TLFileLocation;
                if (thumbLocation != null)
                {
                    var fileName = String.Format("{0}_{1}_{2}.jpg",
                        thumbLocation.VolumeId,
                        thumbLocation.LocalId,
                        thumbLocation.Secret);

                    using (var store = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        using (var fileStream = store.OpenFile(fileName, FileMode.Open, FileAccess.Read))
                        {
                            thumbBytes = new byte[fileStream.Length];
                            fileStream.Read(thumbBytes, 0, thumbBytes.Length);
                        }
                    }
                }
            }

            var bytes = document.Buffer;
            var md5Bytes = Telegram.Api.Helpers.Utils.ComputeMD5(bytes ?? new byte[0]);
            var md5Checksum = BitConverter.ToInt64(md5Bytes, 0);

            StateService.GetServerFilesAsync(
                results =>
                {
                    var serverFile = results.FirstOrDefault(result => result.MD5Checksum.Value == md5Checksum);

                    if (serverFile != null)
                    {
                        message.InputMedia = serverFile.Media;
                        ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService);
                    }
                    else
                    {
                        if (thumbBytes != null)
                        {
                            var thumbFileId = TLLong.Random();
                            UploadFileManager.UploadFile(thumbFileId, message.Media, thumbBytes);

                            Thread.Sleep(100); //NOTE: без этой строки не работает. Почему???
                        }

                        var fileId = TLLong.Random();
                        message.Media.FileId = fileId;
                        message.Media.UploadingProgress = 0.001;
#if WP81
                        if (file == null)
                        {
                            UploadDocumentFileManager.UploadFile(fileId, message, bytes);
                        }
                        else
                        {
                            UploadDocumentFileManager.UploadFile(fileId, message, file);
                        }
#else
                        UploadDocumentFileManager.UploadFile(fileId, message, bytes);
#endif
                    }
                });
        }

        public void SendSticker(TLDocument22 document)
        {
            if (document == null) return;

            var media = new TLMessageMediaDocument { FileId = TLLong.Random(), Document = document };

            var message = GetMessage(TLString.Empty, media);

            var inputMedia = new TLInputMediaDocument
            {
                MD5Hash = new byte[] { },
                Id = new TLInputDocument
                {
                    Id = document.Id,
                    AccessHash = document.AccessHash
                }
            };

            message.InputMedia = inputMedia;

            BeginOnUIThread(() =>
            {
                var previousMessage = InsertSendingMessage(message);
                IsEmptyDialog = Items.Count == 0 && LazyItems.Count == 0;
                Text = string.Empty;

                BeginOnThreadPool(() =>
                    CacheService.SyncSendingMessage(
                        message, previousMessage,
                        TLUtils.InputPeerToPeer(Peer, StateService.CurrentUserId),
                        m => ShellViewModel.SendMediaInternal(message, MTProtoService, StateService, CacheService)));
            });
        }
    }
}
