﻿using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Caliburn.Micro;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using TelegramClient.Resources;
using TelegramClient.Services;
using TelegramClient.ViewModels.Additional;
using TelegramClient.ViewModels.Search;

namespace TelegramClient.ViewModels.Chats
{
    public class AddChatParticipantViewModel : ItemsViewModelBase<TLUserBase>
    {
        private Visibility _inviteVisibility;

        public Visibility InviteVisibility
        {
            get { return _inviteVisibility; }
            set { SetField(ref _inviteVisibility, value, () => InviteVisibility); }
        }

        private AddChatParticipantConfirmationViewModel _confirmation;

        public AddChatParticipantConfirmationViewModel Confirmation
        {
            get { return _confirmation = _confirmation ?? new AddChatParticipantConfirmationViewModel(); }
        }

        private readonly bool _requestForwardingCount;

        private readonly Dictionary<int, TLUserBase> _removedUsers = new Dictionary<int, TLUserBase>();

        private readonly TLChatBase _currentChat;

        private readonly TLChannelParticipantRoleBase _currentRole;

        public AddChatParticipantViewModel(ICacheService cacheService, ICommonErrorHandler errorHandler, IStateService stateService, INavigationService navigationService, IMTProtoService mtProtoService, ITelegramEventAggregator eventAggregator) 
            : base(cacheService, errorHandler, stateService, navigationService, mtProtoService, eventAggregator)
        {
            if (StateService.CurrentRole != null)
            {
                _currentRole = StateService.CurrentRole;
                StateService.CurrentRole = null;
            }

            if (StateService.CurrentChat != null)
            {
                _currentChat = StateService.CurrentChat;
                StateService.CurrentChat = null;
            }

            if (StateService.RemovedUsers != null)
            {
                foreach (var user in StateService.RemovedUsers)
                {
                    _removedUsers[user.Index] = user;
                }
                StateService.RemovedUsers = null;
            }

            if (StateService.RequestForwardingCount)
            {
                _requestForwardingCount = true;
                StateService.RequestForwardingCount = false;
            }

            _inviteVisibility = StateService.IsInviteVisible ? Visibility.Visible : Visibility.Collapsed;
            StateService.IsInviteVisible = false;

            BeginOnThreadPool(() =>
            {
                var isAuthorized = SettingsHelper.GetValue<bool>(Constants.IsAuthorizedKey);
                if (isAuthorized)
                {
                    var contacts = CacheService.GetContacts();
                    Status = string.Empty;
                    Items.Clear();
                    foreach (var contact in contacts.OrderBy(x => x.FullName))
                    {
                        if (!(contact is TLUserEmpty) && contact.Index != StateService.CurrentUserId && !_removedUsers.ContainsKey(contact.Index))
                        {
                            Items.Add(contact);
                        }
                    }
                }

                BeginOnUIThread(() =>
                {
                    if (StateService.RemoveBackEntry)
                    {
                        NavigationService.RemoveBackEntry();
                        StateService.RemoveBackEntry = false;
                    }

                });
            });
        }

        public void InviteGroupViaLink()
        {
            if (_currentChat == null) return;

            StateService.CurrentChat = _currentChat;
            NavigationService.UriFor<InviteLinkViewModel>().Navigate();
        }

        public void UserAction(TLUserBase userBase)
        {
            if (userBase == null) return;

            var user = userBase as TLUser;
            if (user != null && user.IsBot)
            {
                if (user.IsBotGroupsBlocked)
                {
                    MessageBox.Show(AppResources.AddBotToGroupsError, AppResources.Error, MessageBoxButton.OK);
                    return;
                }

                var userName = user.FirstName;
                if (TLString.IsNullOrEmpty(userName))
                {
                    userName = user.LastName;
                }

                var confirmation = MessageBox.Show(string.Format(AppResources.AddUserToTheGroup, userName, _currentChat.FullName), AppResources.AppName, MessageBoxButton.OKCancel);
                if (confirmation == MessageBoxResult.OK)
                {
                    NavigateBackward(userBase);
                }

                return;
            }


            var channel = _currentChat as TLChannel;
            if (channel != null)
            {
                IsWorking = true;
                MTProtoService.GetParticipantAsync(channel.ToInputChannel(), userBase.ToInputUser(),
                    result => BeginOnUIThread(() =>
                    {
                        IsWorking = false;

                        var participantKicked = result.Participant as TLChannelParticipantKicked;
                        var participant = result.Participant as TLChannelParticipant;
                        if (participant != null || participantKicked != null)
                        {
                            if (_currentRole is TLChannelRoleEditor)
                            {
                                if (participantKicked != null)
                                {
                                    var confirmation = MessageBox.Show(string.Format(AppResources.InviteContactConfirmation, userBase.FullName), AppResources.AppName, MessageBoxButton.OKCancel);
                                    if (confirmation != MessageBoxResult.OK)
                                    {
                                        return;
                                    }
                                }

                                NavigateBackward(userBase);
                            }
                        }
                    }),
                    error => BeginOnUIThread(() =>
                    {
                        IsWorking = false;
                        Telegram.Api.Helpers.Execute.ShowDebugMessage("channels.getParticipant error " + error);

                        if (error.TypeEquals(ErrorType.USER_NOT_PARTICIPANT))
                        {
                            if (_currentRole is TLChannelRoleEditor)
                            {
                                var confirmation = MessageBox.Show(string.Format(AppResources.InviteContactConfirmation, userBase.FullName), AppResources.AppName, MessageBoxButton.OKCancel);
                                if (confirmation != MessageBoxResult.OK)
                                {
                                    return;
                                }
                            }

                            NavigateBackward(userBase);
                        }
                    }));

                return;
            }

            if (_requestForwardingCount)
            {
                _confirmation.Open(userBase, _currentChat,
                    result =>
                    {
                        if (result.Result == MessageBoxResult.OK)
                        {
                            NavigateBackward(userBase, result.ForwardingMessagesCount);
                        }
                    });
            }
            else
            {
                NavigateBackward(userBase);
            }
        }

        private void NavigateBackward(TLUserBase user, int forwardingMessagesCount = 0)
        {
            StateService.Participant = user;
            StateService.ForwardingMessagesCount = forwardingMessagesCount;
            NavigationService.GoBack(); 
        }

        public void Search()
        {
            StateService.CurrentChat = _currentChat;
            StateService.CurrentRole = _currentRole;
            StateService.RemoveBackEntry = true;
            StateService.RequestForwardingCount = _requestForwardingCount;
            NavigationService.UriFor<SearchContactsViewModel>().Navigate();
        }
    }
}
