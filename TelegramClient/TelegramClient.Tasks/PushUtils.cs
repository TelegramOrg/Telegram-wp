﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.Resources;
using Windows.Data.Xml.Dom;
using Windows.Networking.PushNotifications;
using Windows.UI.Notifications;

namespace TelegramClient.Tasks
{
    public static class PushUtils
    {
        public static void UpdateToastAndTiles(RawNotification rawNotification)
        {
            var payload = rawNotification != null ? rawNotification.Content : null;
            if (payload == null) return;

            var rootObject = GetRootObject(payload);
            if (rootObject == null) return;
            if (rootObject.data == null) return;

            if (rootObject.data.loc_key == null)
            {
                var groupname = GetGroup(rootObject.data);
                RemoveToastGroup(groupname);
                return;
            }

            var caption = GetCaption(rootObject.data);
            var message = GetMessage(rootObject.data);
            var sound = GetSound(rootObject.data);
            var launch = GetLaunch(rootObject.data);
            var tag = GetTag(rootObject.data);
            var group = GetGroup(rootObject.data);

            if (!IsMuted(rootObject.data))
            {
                AddToast(caption, message, sound, launch, tag, group);
            }
            if (!IsMuted(rootObject.data) && !IsServiceNotification(rootObject.data))
            {
                UpdateTile(caption, message);
            }
            if (!IsMuted(rootObject.data))
            {
                UpdateBadge(rootObject.data.badge);
            }
        }

        private static bool IsMuted(Data data)
        {
            return data.mute == "1";
        }

        private static bool IsServiceNotification(Data data)
        {
            return data.loc_key == "DC_UPDATE";
        }

        public static RootObject GetRootObject(string payload)
        {
            var serializer = new DataContractJsonSerializer(typeof(RootObject));
            RootObject rootObject;
            using (var stream = new MemoryStream(Encoding.Unicode.GetBytes(payload)))
            {
                rootObject = serializer.ReadObject(stream) as RootObject;
            }

            return rootObject;
        }

        private static string GetCaption(Data data)
        {
            var locKey = data.loc_key;
            if (locKey == null)
            {
                return "locKey=null";
            }

            if (locKey.StartsWith("CHAT") || locKey.StartsWith("GEOCHAT"))
            {
                return data.loc_args[1];
            }

            if (locKey.StartsWith("MESSAGE"))
            {
                return data.loc_args[0];
            }

            if (locKey.StartsWith("CHANNEL"))
            {
                return data.loc_args[0];
            }

            if (locKey.StartsWith("AUTH")
                || locKey.StartsWith("CONTACT")
                || locKey.StartsWith("ENCRYPTED")
                || locKey.StartsWith("ENCRYPTION"))
            {
                return "Telegram";
            }

#if DEBUG
            return locKey;
#else
            return "Telegram";
#endif
        }

        private static string GetSound(Data data)
        {
            return data.sound;
        }

        private static string GetGroup(Data data)
        {
            return data.group;
        }

        private static string GetTag(Data data)
        {
            return data.tag;
        }

        private static string GetLaunch(Data data)
        {
            var locKey = data.loc_key;
            if (locKey == null) return null;

            var path = "/Views/ShellView.xaml";
            if (locKey == "DC_UPDATE")
            {
                path = "/Views/Additional/SettingsView.xaml";
            }

            var customParams = new List<string> {"Action=" + locKey};
            customParams.AddRange(data.custom.GetParams());

            return string.Format("{0}?{1}", path, string.Join("&", customParams));
        }

        private static string GetMessage(Data data)
        {
            var locKey = data.loc_key;
            if (locKey == null)
            {
                Telegram.Logs.Log.Write("::PushNotificationsBackgroundTask locKey=null text=" + data.text);
                return string.Empty;
            }

            string locValue;
            var resourceLoader = new ResourceLoader("TelegramClient.Tasks/Resources");
            locValue = resourceLoader.GetString(locKey);
            //if (_locKeys.TryGetValue(locKey, out locValue))
            if (locValue != "")
            {
                return string.Format(locValue, data.loc_args);
            }
            var builder = new StringBuilder();
            if (data.loc_args != null)
            {
                builder.AppendLine("loc_args");
                foreach (var locArg in data.loc_args)
                {
                    builder.AppendLine(locArg);
                }
            }
            Telegram.Logs.Log.Write(string.Format("::PushNotificationsBackgroundTask missing locKey={0} locArgs={1}", locKey, builder.ToString()));

            //if (locKey.StartsWith("CHAT") || locKey.StartsWith("GEOCHAT"))
            //{
            //    return data.text;
            //}

            //if (locKey.StartsWith("MESSAGE"))
            //{
            //    if (locKey == "MESSAGE_TEXT")
            //    {
            //        return data.loc_args[1];
            //    }

            //    return data.text; //add localization string here 
            //}

#if DEBUG
            return data.text;
#else
            return string.Empty;
#endif
        }

        private static void UpdateBadge(int badgeNumber)
        {
            var badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
            if (badgeNumber == 0)
            {
                badgeUpdater.Clear();
                return;
            }

            var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);

            var badgeElement = (XmlElement)badgeXml.SelectSingleNode("/badge");
            badgeElement.SetAttribute("value", badgeNumber.ToString());

            try
            {
                badgeUpdater.Update(new BadgeNotification(badgeXml));
            }
            catch (Exception ex)
            {
                Telegram.Logs.Log.Write(ex.ToString());
            }
        }

        public static void AddToast(string caption, string message, string sound, string launch, string tag, string group)
        {
            var toastNotifier = ToastNotificationManager.CreateToastNotifier();

            var toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);
            SetText(toastXml, caption, message);
            SetLaunch(toastXml, launch);

            if (!string.IsNullOrEmpty(sound) 
                && !string.Equals(sound, "default", StringComparison.OrdinalIgnoreCase))
            {
                SetSound(toastXml, sound);
            }

            try
            {
                var toast = new ToastNotification(toastXml);
                if (tag != null) toast.Tag = tag;
                if (group != null) toast.Group = group;
                //RemoveToastGroup(group);
                toastNotifier.Show(toast);
            }
            catch (Exception ex)
            {
                Telegram.Logs.Log.Write(ex.ToString());
            }
        }

        private static void RemoveToastGroup(string groupname)
        {
            ToastNotificationManager.History.RemoveGroup(groupname);
        }

        private static void UpdateTile(string caption, string message)
        {
            var tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
            //tileUpdater.EnableNotificationQueue(false);
            tileUpdater.EnableNotificationQueue(true);
            tileUpdater.EnableNotificationQueueForSquare150x150(false);
            //tileUpdater.EnableNotificationQueueForWide310x150(true);

            var wideTileXml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWide310x150IconWithBadgeAndText);
            SetImage(wideTileXml, "IconicSmall110.png");
            SetText(wideTileXml, caption, message);

            var squareTile150Xml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150IconWithBadge);
            SetImage(squareTile150Xml, "IconicTileMedium202.png");
            AppendTile(wideTileXml, squareTile150Xml);

            var squareTile71Xml = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare71x71IconWithBadge);
            SetImage(squareTile71Xml, "IconicSmall110.png");
            AppendTile(wideTileXml, squareTile71Xml);

            try
            {
                tileUpdater.Update(new TileNotification(wideTileXml));
            }
            catch (Exception ex)
            {
                Telegram.Logs.Log.Write(ex.ToString());
            }
        }

        private static void AppendTile(XmlDocument toTile, XmlDocument fromTile)
        {
            var fromTileNode = toTile.ImportNode(fromTile.GetElementsByTagName("binding").Item(0), true);
            toTile.GetElementsByTagName("visual")[0].AppendChild(fromTileNode);
        }

        private static void SetText(XmlDocument document, string caption, string message)
        {
            var toastTextElements = document.GetElementsByTagName("text");
            toastTextElements[0].InnerText = caption ?? string.Empty;
            toastTextElements[1].InnerText = message ?? string.Empty;
        }

        private static void SetImage(XmlDocument document, string imageSource)
        {
            var imageElements = document.GetElementsByTagName("image");
            ((XmlElement)imageElements[0]).SetAttribute("src", imageSource);
        }

        private static void SetSound(XmlDocument document, string soundSource)
        {
            //return;

            if (!Regex.IsMatch(soundSource, @"^sound[1-6]$", RegexOptions.IgnoreCase))
            {
                return;
            }

            var toastNode = document.SelectSingleNode("/toast");
            ((XmlElement)toastNode).SetAttribute("duration", "long");
            var audioElement = document.CreateElement("audio");
            audioElement.SetAttribute("src", "ms-appx:///Sounds/" + soundSource + ".wav");
            audioElement.SetAttribute("loop", "false");

            toastNode.AppendChild(audioElement);
        }

        private static void SetLaunch(XmlDocument document, string launch)
        {
            if (string.IsNullOrEmpty(launch))
            {
                return;
            }
            if (PushNotificationsBackgroundTask.SystemVersion != null
                && PushNotificationsBackgroundTask.SystemVersion.StartsWith("10"))
            {
                return;
            }
            //launch = "/Views/ShellView.xaml";
            var toastNode = document.SelectSingleNode("/toast");
            ((XmlElement)toastNode).SetAttribute("launch", launch);
        }

        private static readonly Dictionary<string, string> _locKeys = new Dictionary<string, string>
        {
            {"MESSAGE_FWDS", "forwarded you {1} messages"},
            {"MESSAGE_TEXT", "{1}"},
            {"MESSAGE_NOTEXT", "sent you a message"},
            {"MESSAGE_PHOTO", "sent you a photo"},
            {"MESSAGE_VIDEO", "sent you a video"},
            {"MESSAGE_DOC", "sent you a document"},
            {"MESSAGE_AUDIO", "sent you a voice message"},
            {"MESSAGE_CONTACT", "shared a contact with you"},
            {"MESSAGE_GEO", "sent you a map"},
            {"MESSAGE_STICKER", "sent you a sticker"},

            {"CHAT_MESSAGE_FWDS", "{0} forwarded {2} messages to the group"},
            {"CHAT_MESSAGE_TEXT", "{0}: {2}"},
            {"CHAT_MESSAGE_NOTEXT", "{0} sent a message to the group"},
            {"CHAT_MESSAGE_PHOTO", "{0} sent a photo to the group"},
            {"CHAT_MESSAGE_VIDEO", "{0} sent a video to the group"},
            {"CHAT_MESSAGE_DOC", "{0} sent a document to the group"},
            {"CHAT_MESSAGE_AUDIO", "{0} sent a voice message to the group"},
            {"CHAT_MESSAGE_CONTACT", "{0} shared a contact in the group"},
            {"CHAT_MESSAGE_GEO", "{0} sent a map to the group"},
            {"CHAT_MESSAGE_STICKER", "{0} sent a sticker to the group"},
            
            {"CHANNEL_MESSAGE_FWDS", "posted {1} forwarded messages"},
            {"CHANNEL_MESSAGE_TEXT", "{1}"},
            {"CHANNEL_MESSAGE_NOTEXT", "posted a message"},
            {"CHANNEL_MESSAGE_PHOTO", "posted a photo"},
            {"CHANNEL_MESSAGE_VIDEO", "posted a video"},
            {"CHANNEL_MESSAGE_DOC", "posted a document"},
            {"CHANNEL_MESSAGE_AUDIO", "posted a voice message"},
            {"CHANNEL_MESSAGE_CONTACT", "posted a contact"},
            {"CHANNEL_MESSAGE_GEO", "posted a map"},
            {"CHANNEL_MESSAGE_STICKER", "posted a sticker"},

            {"CHAT_CREATED", "{0} invited you to the group"},
            {"CHAT_TITLE_EDITED", "{0} edited the group's name"},
            {"CHAT_PHOTO_EDITED", "{0} edited the group's photo"},
            {"CHAT_ADD_MEMBER", "{0} invited {2} to the group"},
            {"CHAT_ADD_YOU", "{0} invited you to the group"},
            {"CHAT_DELETE_MEMBER", "{0} kicked {2} from the group"},
            {"CHAT_DELETE_YOU", "{0} kicked you from the group"},
            {"CHAT_LEFT", "{0} has left the group"},
            {"CHAT_RETURNED", "{0} has returned to the group"},
            {"GEOCHAT_CHECKIN", "{0} has checked-in"},
            {"CHAT_JOINED", "{0} has joined the group"},

            {"CONTACT_JOINED", "{0} joined the App!"},
            {"AUTH_UNKNOWN", "New login from unrecognized device {0}"},
            {"AUTH_REGION", "New login from unrecognized device {0}, location: {1}"},

            {"CONTACT_PHOTO", "updated profile photo"},
            
            {"ENCRYPTION_REQUEST", "You have a new message"},
            {"ENCRYPTION_ACCEPT", "You have a new message"},
            {"ENCRYPTED_MESSAGE", "You have a new message"},
            
            {"DC_UPDATE", "Open this notification to update app settings"},
        };
    }

    public sealed class Custom
    {
        public string msg_id { get; set; }
        public string from_id { get; set; }
        public string chat_id { get; set; }

        public string group
        {
            get
            {
                if (chat_id != null) return "c" + chat_id;
                if (from_id != null) return "u" + from_id;
                return null;
            }
        }

        public string tag { get { return msg_id; } }

        public IEnumerable<string> GetParams()
        {
            if (msg_id != null) yield return "msg_id=" + msg_id;
            if (from_id != null) yield return "from_id=" + from_id;
            if (chat_id != null) yield return "chat_id=" + chat_id;
        } 
    }

    public sealed class Data
    {
        public Custom custom { get; set; }
        public string sound { get; set; }
        public string mute { get; set; }
        public int badge { get; set; }
        public string loc_key { get; set; }
        public string[] loc_args { get; set; }
        public int random_id { get; set; }
        public int user_id { get; set; }
        public string text { get; set; }
        public string system { get; set; }

        public string group { get { return custom != null ? custom.group : null; } }
        public string tag { get { return custom != null ? custom.tag : null; } }
    }

    public sealed class RootObject
    {
        public int date { get; set; }
        public Data data { get; set; }
    }
}