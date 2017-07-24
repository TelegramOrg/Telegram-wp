﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Caliburn.Micro;
using Microsoft.Phone.Tasks;
using Org.BouncyCastle.Security;
using Telegram.Api.Aggregator;
using Telegram.Api.Extensions;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.Services.FileManager;
using Telegram.Api.TL;
using TelegramClient.Converters;
using TelegramClient.Helpers;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Dialogs;
using TelegramClient.ViewModels.Media;
using Execute = Telegram.Api.Helpers.Execute;

namespace TelegramClient.ViewModels.Contacts
{
    public class ContactDetailsViewModel : ItemDetailsViewModelBase, Telegram.Api.Aggregator.IHandle<TLUpdateUserBlocked>, Telegram.Api.Aggregator.IHandle<TLUpdateNotifySettings>
    {
        public Visibility SecretChatVisibility
        {
            get
            {
                var user = CurrentContact as TLUser;
                if (user != null && user.IsBot)
                {
                    return Visibility.Collapsed;
                }

                return Visibility.Visible;
            }
        }

        private TimerSpan _selectedSpan;

        public TimerSpan SelectedSpan
        {
            get { return _selectedSpan; }
            set
            {
                _selectedSpan = value;

                if (_selectedSpan != null)
                {
                    if (_selectedSpan.Seconds == 0
                        || _selectedSpan.Seconds == int.MaxValue)
                    {
                        MuteUntil = _selectedSpan.Seconds;
                    }
                    else
                    {
                        var now = DateTime.Now;
                        var muteUntil = now.AddSeconds(_selectedSpan.Seconds);

                        MuteUntil = muteUntil < now ? 0 : TLUtils.DateToUniversalTimeTLInt(MTProtoService.ClientTicksDelta, muteUntil).Value;
                    }
                }
            }
        }

        public IList<TimerSpan> Spans { get; protected set; } 

        public TLUserBase CurrentContact { get { return CurrentItem as TLUserBase; } }

        public bool IsBot
        {
            get
            {
                var user = CurrentContact as TLUser;
                return user != null && user.IsBot;
            }
        }

        public TLString CurrentPhone { get; set; }

        public string Phone
        {
            get
            {
                if (CurrentContact != null && CurrentContact.HasPhone) return CurrentContact.Phone.ToString();

                if (CurrentPhone != null) return CurrentPhone.ToString();

                return null;
            }
        }

        public bool HasPhone { get { return CurrentContact != null && CurrentContact.HasPhone || CurrentPhone != null; } }

        public int PreviousUserId { get; set; }

        private bool _suppressUpdating = true;

        private int _muteUntil;
        
        public int MuteUntil
        {
            get { return _muteUntil; }
            set { SetField(ref _muteUntil, value, () => MuteUntil); }
        }

        private string _selectedSound;

        public string SelectedSound
        {
            get { return _selectedSound; }
            set
            {
                SetField(ref _selectedSound, value, () => SelectedSound);
            }
        }

        public void SetSelectedSound(string sound)
        {
            _selectedSound = sound;
        }

        public List<string> Sounds { get; protected set; }

        private string _subtitle;

        public string Subtitle
        {
            get { return _subtitle; }
            set { SetField(ref _subtitle, value, () => Subtitle); }
        }

        private readonly IFileManager _fileManager;

        public ProfilePhotoViewerViewModel ProfilePhotoViewer { get; set; }

        public ContactDetailsViewModel(IFileManager fileManager, ICacheService cacheService, ICommonErrorHandler errorHandler,
            IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService,
            ITelegramEventAggregator eventAggregator)
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            Spans = new List<TimerSpan>
            {
                new TimerSpan(AppResources.Enabled, string.Empty, 0, AppResources.Enabled),
                new TimerSpan(AppResources.HourNominativeSingular,  "1", (int)TimeSpan.FromHours(1.0).TotalSeconds, string.Format(AppResources.MuteFor, string.Format("{0} {1}", "1", AppResources.HourNominativeSingular).ToLowerInvariant())),
                new TimerSpan(AppResources.HourGenitivePlural, "8", (int)TimeSpan.FromHours(8.0).TotalSeconds, string.Format(AppResources.MuteFor, string.Format("{0} {1}", "8", AppResources.HourGenitivePlural).ToLowerInvariant())),
                new TimerSpan(AppResources.DayNominativePlural, "2", (int)TimeSpan.FromDays(2.0).TotalSeconds, string.Format(AppResources.MuteFor, string.Format("{0} {1}", "2", AppResources.DayNominativePlural).ToLowerInvariant())),
                new TimerSpan(AppResources.Disabled, string.Empty, int.MaxValue, AppResources.Disabled),
            };
            _selectedSpan = Spans[0];

            _notificationTimer = new DispatcherTimer();
            _notificationTimer.Interval = TimeSpan.FromSeconds(Constants.NotificationTimerInterval);
            _notificationTimer.Tick += OnNotificationTimerTick;

            _fileManager = fileManager;

            EventAggregator.Subscribe(this);
            DisplayName = LowercaseConverter.Convert(AppResources.Profile);

            Sounds = new List<string>();

            PropertyChanged += (sender, args) =>
            {
                if (Property.NameEquals(args.PropertyName, () => MuteUntil)
                    && !_suppressUpdating)
                {
                    UpdateNotifySettingsAsync();
                }

                if (Property.NameEquals(args.PropertyName, () => SelectedSound)
                    && !_suppressUpdating)
                {
                    NotificationsViewModel.PlaySound(SelectedSound);

                    UpdateNotifySettingsAsync();
                }
            };

            CalculateSecretChatParamsAsync();
        }

        private void UpdateNotifySettingsAsync()
        {
            if (CurrentContact == null) return;

            var notifySettings = new TLInputPeerNotifySettings
            {
                EventsMask = new TLInt(1),
                MuteUntil = new TLInt(MuteUntil),
                ShowPreviews = new TLBool(true),
                Sound = string.IsNullOrEmpty(SelectedSound) ? new TLString("default") : new TLString(SelectedSound)
            };

            IsWorking = true;
            MTProtoService.UpdateNotifySettingsAsync(
                CurrentContact.ToInputNotifyPeer(), notifySettings,
                result =>
                {
                    IsWorking = false;
                    CurrentContact.NotifySettings = new TLPeerNotifySettings
                    {
                        EventsMask = new TLInt(1),
                        MuteUntil = new TLInt(MuteUntil),
                        ShowPreviews = notifySettings.ShowPreviews,
                        Sound = notifySettings.Sound
                    };

                    var dialog = CacheService.GetDialog(new TLPeerUser { Id = CurrentContact.Id });
                    if (dialog != null)
                    {
                        dialog.NotifySettings = CurrentContact.NotifySettings;
                        dialog.NotifyOfPropertyChange(() => dialog.NotifySettings);
                        var settings = dialog.With as INotifySettings;
                        if (settings != null)
                        {
                            settings.NotifySettings = CurrentContact.NotifySettings;
                        }
                    }

                    CacheService.Commit();
                },
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("account.updateNotifySettings error: " + error);
                });
        }

        public event EventHandler<BlockedEventArgs> BlockedStatusChanged;

        protected virtual void RaiseBlockedStatusChanged(BlockedEventArgs e)
        {
            var handler = BlockedStatusChanged;
            if (handler != null) handler(this, e);
        }

        public event EventHandler<ImportEventArgs> ImportStatusChanged;

        protected virtual void RaiseImportStatusChanged(ImportEventArgs e)
        {
            var handler = ImportStatusChanged;
            if (handler != null) handler(this, e);
        }

        protected override void OnInitialize()
        {
            if (CurrentContact == null) return;

            Subtitle = DialogDetailsViewModel.GetUserStatus(CurrentContact);

            Execute.BeginOnThreadPool(TimeSpan.FromSeconds(0.5), () =>
            {
                UpdateNotificationSettings();

                IsWorking = true;
                MTProtoService.GetFullUserAsync(
                    CurrentContact.ToInputUser(),
                    userFull =>
                    {
                        IsWorking = false;
                        UpdateNotificationSettings();
                        Subtitle = DialogDetailsViewModel.GetUserStatus(CurrentContact);

                        RaiseBlockedStatusChanged(new BlockedEventArgs { Blocked = userFull.Blocked.Value });
                    },
                    error =>
                    {
                        IsWorking = false;
                        Execute.ShowDebugMessage("users.getFullUser error: " + error);
                    });
            });
            

            base.OnInitialize();
        }

        public void UpdateNotificationSettings()
        {
            if (CurrentContact != null && CurrentContact.NotifySettings != null)
            {
                _suppressUpdating = true;

                var notifySettings = CurrentContact.NotifySettings as TLPeerNotifySettings;
                if (notifySettings != null)
                {
                    MuteUntil = notifySettings.MuteUntil.Value;
                    var sound = StateService.Sounds.FirstOrDefault(x => string.Equals(x, notifySettings.Sound.Value, StringComparison.OrdinalIgnoreCase));
                    SelectedSound = sound ?? StateService.Sounds[0];

                    _suppressUpdating = false;
                }
            }
        }

        #region Notification timer
        private readonly DispatcherTimer _notificationTimer;

        public void StartTimer()
        {
            _notificationTimer.Start();
        }

        public void StopTimer()
        {
            _notificationTimer.Stop();
        }

        private void OnNotificationTimerTick(object sender, System.EventArgs e)
        {
            if (MuteUntil > 0 && MuteUntil < int.MaxValue)
            {
                NotifyOfPropertyChange(() => MuteUntil);
            }
        }
        #endregion

        #region Actions

        public void SelectSpan(TimerSpan selectedSpan)
        {
            SelectedSpan = selectedSpan;
        }

        public void SelectNotificationSpan()
        {
            //StateService.SelectedTimerSpan = SelectedSpan;
            NavigationService.UriFor<ChooseNotificationSpanViewModel>().Navigate();
        }

        public void AddToGroup()
        {
            if (CurrentContact == null) return;

            var user = CurrentContact as TLUser;
            if (user != null && user.IsBot && !user.IsBotGroupsBlocked)
            {
                StateService.Bot = CurrentContact;
                NavigationService.UriFor<ChooseDialogViewModel>().Navigate();
            }
        }

        public void BlockContact()
        {
            if (CurrentContact == null) return;

            var confirmation = IsBot
                ? MessageBoxResult.OK
                : MessageBox.Show(AppResources.BlockContactConfirmation, AppResources.AppName, MessageBoxButton.OKCancel);
                
            if (confirmation == MessageBoxResult.OK)
            {
                IsWorking = true;
                MTProtoService.BlockAsync(CurrentContact.ToInputUser(),
                    result => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        CurrentContact.Blocked = TLBool.True;
                        CacheService.Commit();
                        EventAggregator.Publish(new TLUpdateUserBlocked { UserId = CurrentContact.Id, Blocked = TLBool.True });
                    }),
                    error => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        Execute.ShowDebugMessage("contacts.block error " + error);
                    }));
            }
        }

        public void UnblockContact()
        {
            if (CurrentContact == null) return;

            var confirmation = IsBot
                ? MessageBoxResult.OK
                : MessageBox.Show(AppResources.UnblockContactConfirmation, AppResources.AppName, MessageBoxButton.OKCancel);

            if (confirmation == MessageBoxResult.OK)
            {
                IsWorking = true;
                MTProtoService.UnblockAsync(CurrentContact.ToInputUser(),
                    result => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        CurrentContact.Blocked = TLBool.False;
                        CacheService.Commit();
                        EventAggregator.Publish(new TLUpdateUserBlocked { UserId = CurrentContact.Id, Blocked = TLBool.False });

                        if (IsBot)
                        {
                            NavigationService.GoBack();
                        }
                    }),
                    error => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        Execute.ShowDebugMessage("contacts.unblock error " + error);
                    }));
            }
        }

        public void AddContact()
        {
            if (CurrentContact == null) return;
            if (!HasPhone) return;

            var userPhone = new TLString(Phone);
            if (TLString.IsNullOrEmpty(userPhone)) return;

            IsWorking = true;
            ImportContactAsync(CurrentContact, userPhone, MTProtoService,
                result =>
                {
                    IsWorking = false;

                    if (result.Users.Count > 0)
                    {
                        EventAggregator.Publish(result.Users[0]);
                        RaiseImportStatusChanged(new ImportEventArgs { Imported = true });
                        var contact = result.Users[0] as TLUserContact;
                        if (contact != null)
                        {
                            EventAggregator.Publish(new AddedToContactsEventArgs { Contact = contact });
                            ContactsHelper.CreateContactAsync(_fileManager, StateService, contact);
                        }
                    }
                },
                error =>
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("contacts.importContacts error " + error);
                });
        }

        public static void ImportContactAsync(TLUserBase contact, TLString phone, IMTProtoService mtProtoService, Action<TLImportedContacts> callback, Action<TLRPCError> faultCallback)
        {
            var importedContacts = new TLVector<TLInputContactBase>
            {
                new TLInputContact
                {
                    Phone = phone,
                    FirstName = contact.FirstName,
                    LastName = contact.LastName,
                    ClientId = new TLLong(contact.Id.Value)
                }
            };

            mtProtoService.ImportContactsAsync(
                importedContacts, new TLBool(false),
                callback.SafeInvoke,
                error =>
                {
                    faultCallback.SafeInvoke(error);
                    Execute.ShowDebugMessage("contacts.importContacts error: " + error);
                });
        }

        public void DeleteContact()
        {
            if (CurrentContact == null) return;

            var messageBoxResult = MessageBox.Show(AppResources.ConfirmDeleteContact, AppResources.Confirm, MessageBoxButton.OKCancel);
            if (messageBoxResult != MessageBoxResult.OK)
            {
                return;
            }
            
            IsWorking = true;
            MTProtoService.DeleteContactAsync(
                CurrentContact.ToInputUser(),
                result => 
                {
                    IsWorking = false;
                    EventAggregator.Publish(result.User);
                    RaiseImportStatusChanged(new ImportEventArgs{Imported = false});

                    ContactsHelper.DeleteContactAsync(StateService, CurrentContact.Id);
                },
                error => 
                {
                    IsWorking = false;
                    Execute.ShowDebugMessage("contacts.deleteContact error: " + error);
                });
        }

        public void SendMessage()
        {
            if (CurrentContact == null) return;

            StateService.FocusOnInputMessage = true;
            if (PreviousUserId != 0 && PreviousUserId == CurrentContact.Index)
            {
                NavigationService.GoBack();
            }
            else
            {
                StateService.RemoveBackEntries = true;
                StateService.With = CurrentContact;
                NavigationService.UriFor<DialogDetailsViewModel>().Navigate();
            }
        }

        public void OpenPhoto()
        {
            if (CurrentContact == null) return;

            var photo = CurrentContact.Photo as TLUserProfilePhoto;
            if (photo != null)
            {
                StateService.CurrentPhoto = photo;
                StateService.CurrentContact = CurrentContact;

                if (ProfilePhotoViewer == null)
                {
                    ProfilePhotoViewer = new ProfilePhotoViewerViewModel(StateService, MTProtoService, EventAggregator, NavigationService);
                    NotifyOfPropertyChange(() => ProfilePhotoViewer);
                }

                BeginOnUIThread(() => ProfilePhotoViewer.OpenViewer());
            }
        }

        public void OpenMedia()
        {
            if (CurrentContact == null) return;

            StateService.CurrentInputPeer = CurrentContact;
            NavigationService.UriFor<FullMediaViewModel>().Navigate();
        }

        public void Call()
        {
            if (CurrentContact == null) return;

            var phone = CurrentContact.Phone ?? CurrentPhone;
            if (phone == null) return;

            var task = new PhoneCallTask();
            task.DisplayName = CurrentContact.FullName;
            task.PhoneNumber = "+" + phone;
            task.Show();
        }
        #endregion

        #region Secret chats

        public Visibility ProgressVisibility { get { return IsWorking ? Visibility.Visible : Visibility.Collapsed; } }

        private TLString _a;
        private TLInt _g;
        private TLString _p;
        private TLString _ga;
        private volatile bool _invokeDelayedUserAction;

        public void CreateSecretChat()
        {
            var user = CurrentItem as TLUserBase;
            if (user == null) return;

            if (_a == null
                || _g == null
                || _p == null
                || _ga == null)
            {
                IsWorking = true;
                NotifyOfPropertyChange(() => ProgressVisibility);
                _invokeDelayedUserAction = true;
                return;
            }

            IsWorking = false;
            NotifyOfPropertyChange(() => ProgressVisibility);

            var random = new Random();
            var randomId = random.Next();

            MTProtoService.RequestEncryptionAsync(user.ToInputUser(), new TLInt(randomId), _ga,
                encryptedChat =>
                {
                    var chatWaiting = encryptedChat as TLEncryptedChatWaiting;
                    if (chatWaiting == null) return;

                    chatWaiting.A = _a;
                    chatWaiting.P = _p;
                    chatWaiting.G = _g;

                    StateService.With = chatWaiting;
                    StateService.Participant = user;

                    Execute.BeginOnUIThread(() =>
                    {
                        StateService.RemoveBackEntries = true;
                        NavigationService.UriFor<SecretDialogDetailsViewModel>().Navigate();
                    });

                    CacheService.SyncEncryptedChat(chatWaiting, result => EventAggregator.Publish(result));

                    var message = new TLDecryptedMessageService
                    {
                        RandomId = TLLong.Random(),
                        RandomBytes = new TLString(""),
                        ChatId = chatWaiting.Id,
                        Action = new TLDecryptedMessageActionEmpty(),
                        FromId = new TLInt(StateService.CurrentUserId),
                        Date = chatWaiting.Date,
                        Out = new TLBool(false),
                        Unread = new TLBool(false),
                        Status = MessageStatus.Read
                    };

                    CacheService.SyncDecryptedMessage(message, chatWaiting, result => { });
                },
                error =>
                {
                    Execute.ShowDebugMessage("messages.requestEncryption error: " + error);
                });
        }

        private void CalculateSecretChatParamsAsync()
        {
            MTProtoService.GetDHConfigAsync(new TLInt(0), new TLInt(0),
                result =>
                {
                    var dhConfig = (TLDHConfig)result;
                    if (!TLUtils.CheckPrime(dhConfig.P.Data, dhConfig.G.Value))
                    {
                        return;
                    }

                    var aBytes = new byte[256];
                    var random = new SecureRandom();
                    random.NextBytes(aBytes);
                    _a = TLString.FromBigEndianData(aBytes);
                    _p = dhConfig.P;
                    _g = dhConfig.G;
#if DEBUG
                    //Thread.Sleep(15000);
#endif
                    var gaBytes = Telegram.Api.Services.MTProtoService.GetGB(aBytes, dhConfig.G, dhConfig.P);
                    _ga = TLString.FromBigEndianData(gaBytes);
                    if (_invokeDelayedUserAction)
                    {
                        _invokeDelayedUserAction = false;
                        CreateSecretChat();
                    }

                },
                error =>
                {
                    Execute.ShowDebugMessage("messages.getDhConfig error: " + error);
                });
        }
#endregion

        public void Handle(TLUpdateUserBlocked update)
        {
            RaiseBlockedStatusChanged(new BlockedEventArgs { Blocked = update.Blocked.Value });
        }

        public void Handle(TLUpdateNotifySettings updateNotifySettings)
        {
            var notifyPeer = updateNotifySettings.Peer as TLNotifyPeer;
            if (notifyPeer != null)
            {
                var peer = notifyPeer.Peer;
                if (peer is TLPeerChat
                    && peer.Id.Value == CurrentContact.Index)
                {
                    Execute.BeginOnUIThread(() =>
                    {
                        CurrentContact.NotifySettings = updateNotifySettings.NotifySettings;
                        var notifySettings = updateNotifySettings.NotifySettings as TLPeerNotifySettings;
                        if (notifySettings != null)
                        {
                            _suppressUpdating = true;
                            MuteUntil = notifySettings.MuteUntil.Value;
                            _suppressUpdating = false;
                        }
                    });
                }
            }
        }
    }

    public class AddedToContactsEventArgs : System.EventArgs
    {
        public TLUserContact Contact { get; set; }
    }

    public class BlockedEventArgs : System.EventArgs
    {
        public bool Blocked { get; set; }
    }

    public class ImportEventArgs : System.EventArgs
    {
        public bool Imported { get; set; }
    }
}
